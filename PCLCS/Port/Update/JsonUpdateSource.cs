using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PCLCS.Port.Update;

/// <summary>
/// 基于 HTTP + JSON 清单的更新源（可直接使用的示例实现）。
/// 期望清单格式（任一即可，字段缺失时忽略）：
/// {
///   "version": "2.99.0.0",
///   "sha256": "....",
///   "changelog": "....",
///   "url": "https://example.com/PCL.exe",
///   "mirrors": [ "https://mirror-a/PCL.exe", "https://mirror-b/PCL.exe" ]
/// }
/// 不同通道/架构可通过不同的清单 URL 区分（由调用方在构造时传入）。
/// </summary>
public class JsonUpdateSource : IUpdateSource
{
    private static readonly HttpClient Client = new();
    private readonly string _manifestUrl;

    public JsonUpdateSource(string manifestUrl) => _manifestUrl = manifestUrl;

    public async Task<UpdateVersionInfo?> GetLatestAsync(
        UpdateChannel channel, UpdateArch arch, CancellationToken ct = default)
    {
        using var resp = await Client.GetAsync(_manifestUrl, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var obj = JObject.Parse(json);

        var version = obj.Value<string>("version");
        if (string.IsNullOrWhiteSpace(version))
            return null;

        return new UpdateVersionInfo
        {
            VersionName = version!,
            Sha256 = obj.Value<string>("sha256"),
            Changelog = obj.Value<string>("changelog"),
            DownloadUrl = obj.Value<string>("url") ?? ""
        };
    }

    public IReadOnlyList<string> GetDownloadUrls(UpdateVersionInfo version)
    {
        var list = new List<string> { version.DownloadUrl };
        // 若清单中存在 mirrors 数组，可在此追加；此处预留扩展点。
        return list;
    }
}
