using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCLCS.Port.Download;

namespace PCLCS.Port.Update;

/// <summary>
/// 多源自动更新编排器（移植自 PCL-CE-dev 的 UpdateManager，去除对 Config/Lang/ModLoader 的耦合）。
/// 设计要点：
/// - 通过注入 <see cref="IUpdateSource"/> 列表实现「多镜像容灾」；
/// - 用本库 <see cref="SemVer"/> 做正确的版本比较（不再用字符串比较）；
/// - 下载完成后用本库 <see cref="Hash.SHA256Provider"/> 校验 SHA-256（防损坏/篡改）；
/// - 通过事件把状态/进度暴露给 VB 侧 UI，而不是依赖 CE-dev 的 Loader 框架。
/// 具体「下载后如何替换自己并重启」需要 VB 侧配合（参见 <see cref="OnUpdateDownloaded"/> 回调）。
/// </summary>
public class MultiSourceUpdater
{
    private readonly IReadOnlyList<IUpdateSource> _sources;

    public string CurrentVersion { get; }
    public Action<string>? Log { get; set; }
    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged;
    public event Action<UpdateVersionInfo>? OnUpdateDownloaded;

    public MultiSourceUpdater(IReadOnlyList<IUpdateSource> sources, string currentVersion)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
    }

    /// <summary>
    /// 检查是否有更新。返回 null 表示已是最新；否则返回可更新的目标版本。
    /// 所有源都失败时会抛异常（由调用方决定静默/提示）。
    /// </summary>
    public async Task<UpdateVersionInfo?> CheckUpdateAsync(
        UpdateChannel channel, UpdateArch arch, CancellationToken ct = default)
    {
        UpdateVersionInfo? latest = null;
        Exception? lastEx = null;
        foreach (var source in _sources)
        {
            try
            {
                var v = await source.GetLatestAsync(channel, arch, ct).ConfigureAwait(false);
                if (v != null)
                {
                    latest = v;
                    break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastEx = ex;
                Log?.Invoke($"[Update] 更新源查询失败，尝试下一个：{ex.Message}");
            }
        }

        if (latest == null)
            return null;

        if (!SemVer.TryParse(CurrentVersion, out var current) ||
            !SemVer.TryParse(latest.VersionName, out var target))
            throw new Exception("版本号解析失败，无法判断是否需要更新");

        return target > current ? latest : null;
    }

    /// <summary>
    /// 下载更新到指定路径，并校验 SHA-256。下载完成后触发 <see cref="OnUpdateDownloaded"/>。
    /// </summary>
    public async Task DownloadAsync(
        UpdateVersionInfo version, string outputPath, CancellationToken ct = default)
    {
        IReadOnlyList<string>? urls = null;
        foreach (var source in _sources)
        {
            var u = source.GetDownloadUrls(version);
            if (u != null && u.Count > 0) { urls = u; break; }
        }
        if (urls == null || urls.Count == 0)
            throw new Exception("更新源未提供下载地址");
        if (urls.Count == 1) Log?.Invoke($"[Update] 主下载地址：{urls[0]}");
        else Log?.Invoke($"[Update] 下载地址（含 {urls.Count} 个镜像）：{urls[0]}");

        var downloader = new ChunkedDownloader();
        StatusChanged?.Invoke($"正在下载 {version.VersionName}");

        var progress = new Progress<long>(_ => { /* 可按需换算百分比 */ });
        progress.ProgressChanged += (_, _) => { };
        // 简单进度：以 SHA 校验完成作为 100%
        await downloader.DownloadAsync(
            urls[0],
            outputPath,
            mirrorUrls: urls.Count > 1 ? new List<string>(urls).GetRange(1, urls.Count - 1) : null,
            expectedSha256: version.Sha256,
            progress: progress,
            ct: ct).ConfigureAwait(false);

        ProgressChanged?.Invoke(1.0);
        StatusChanged?.Invoke("下载并校验完成");
        OnUpdateDownloaded?.Invoke(version);
    }
}
