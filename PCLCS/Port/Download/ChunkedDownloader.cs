using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCLCS.Port.Hash;

namespace PCLCS.Port.Download;

/// <summary>
/// 分块下载引擎（移植自 PCL-CE-dev 的 Network/Downloader 思路，重写为框架无关的独立实现）。
/// 特点：
/// - 用 HTTP Range 请求把文件切成多个分块并行下载，提升大文件下载速度；
/// - 支持多镜像源：某个分块失败时自动换下一个 URL 重试；
/// - 可选择性用 SHA-256 校验最终结果（依赖本库 PCLCS.Port.Hash）。
/// 与 CE-dev 不同，这里不依赖 PCL.Core 的 NetworkService/Loader 框架，可直接在 VB 侧调用。
/// </summary>
public class ChunkedDownloader
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

    /// <summary>
    /// 分块并行下载。
    /// </summary>
    /// <param name="primaryUrl">主下载地址。</param>
    /// <param name="outputPath">本地输出文件路径。</param>
    /// <param name="mirrorUrls">可选镜像地址列表（分块失败时在它们之间轮换）。</param>
    /// <param name="expectedSha256">可选的预期 SHA-256（小写十六进制）；为空则不校验。</param>
    /// <param name="chunkCount">分块数（默认 8，小于 2 时退化为整文件下载）。</param>
    /// <param name="progress">进度回调，参数为已下载字节数。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task DownloadAsync(
        string primaryUrl,
        string outputPath,
        IEnumerable<string>? mirrorUrls = null,
        string? expectedSha256 = null,
        int chunkCount = 8,
        IProgress<long>? progress = null,
        CancellationToken ct = default)
    {
        var sources = new List<string> { primaryUrl };
        if (mirrorUrls != null) sources.AddRange(mirrorUrls);

        var total = await _GetContentLengthAsync(sources, ct).ConfigureAwait(false);
        if (!total.HasValue || total.Value <= 0 || chunkCount < 2)
        {
            await _DownloadWholeAsync(sources, outputPath, progress, ct).ConfigureAwait(false);
        }
        else
        {
            await _DownloadChunkedAsync(sources, outputPath, total.Value, chunkCount, progress, ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actual = (await Hash.SHA256Provider.Instance.ComputeHashAsync(
                new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)).ConfigureAwait(false))
                .ToHexString();
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"SHA-256 校验失败：期望 {expectedSha256}，实际 {actual}");
        }
    }

    private async Task<long?> _GetContentLengthAsync(List<string> sources, CancellationToken ct)
    {
        foreach (var url in sources)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var resp = await Client.SendAsync(req, ct).ConfigureAwait(false);
                if (resp.Content.Headers.ContentLength.HasValue)
                    return resp.Content.Headers.ContentLength;
            }
            catch
            {
                // 尝试下一个源
            }
        }
        return null;
    }

    private async Task _DownloadWholeAsync(
        List<string> sources, string outputPath, IProgress<long>? progress, CancellationToken ct)
    {
        var lastEx = default(Exception);
        foreach (var url in sources)
        {
            try
            {
                using var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var dst = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                int read;
                while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                    progress?.Report(read);
                }
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastEx = ex;
            }
        }
        throw new Exception("所有下载源均失败", lastEx);
    }

    private async Task _DownloadChunkedAsync(
        List<string> sources, string outputPath, long total, int chunkCount,
        IProgress<long>? progress, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var partPaths = new string[chunkCount];
        var chunkSize = total / chunkCount;
        var completed = 0L;
        var lockObj = new object();

        var tasks = new List<Task>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            var start = i * chunkSize;
            var end = (i == chunkCount - 1) ? total - 1 : start + chunkSize - 1;
            var partPath = Path.Combine(dir, $"{Path.GetFileName(outputPath)}.{i}.part");
            partPaths[i] = partPath;
            var localIdx = i;
            var localStart = start;
            var localEnd = end;
            tasks.Add(Task.Run(async () =>
            {
                await _DownloadRangeAsync(sources, localStart, localEnd, partPath, bytes =>
                {
                    lock (lockObj) { completed += bytes; progress?.Report(completed); }
                }, ct).ConfigureAwait(false);
            }, ct));
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // 合并分块
        using (var dst = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            foreach (var part in partPaths)
            {
                using var src = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await src.CopyToAsync(dst, 81920, ct).ConfigureAwait(false);
            }
        }
        foreach (var part in partPaths)
        {
            try { File.Delete(part); } catch { /* best-effort */ }
        }
    }

    private async Task _DownloadRangeAsync(
        List<string> sources, long start, long end, string partPath, Action<long> onBytes, CancellationToken ct)
    {
        var lastEx = default(Exception);
        foreach (var url in sources)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);
                using var resp = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.PartialContent)
                    throw new Exception($"分块下载返回 {resp.StatusCode}");
                using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var dst = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                int read;
                while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                    onBytes(read);
                }
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastEx = ex;
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            }
        }
        throw new Exception($"分块 [{start}-{end}] 所有源均失败", lastEx);
    }
}
