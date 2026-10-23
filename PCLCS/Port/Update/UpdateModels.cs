using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCLCS.Port.Update;

/// <summary>更新通道。</summary>
public enum UpdateChannel { Stable = 0, Beta = 1 }

/// <summary>目标架构。</summary>
public enum UpdateArch { x64 = 0, arm64 = 1 }

/// <summary>单个可下载版本的信息（由更新源提供）。</summary>
public class UpdateVersionInfo
{
    /// <summary>版本名，如 "2.99.0.0" 或 "3.0.0-beta.1"。</summary>
    public string VersionName { get; set; } = "";
    /// <summary>SHA-256（小写十六进制），用于下载后校验。</summary>
    public string? Sha256 { get; set; }
    /// <summary>更新日志（Markdown 等）。</summary>
    public string? Changelog { get; set; }
    /// <summary>主下载地址。</summary>
    public string DownloadUrl { get; set; } = "";
}

/// <summary>更新源接口。多个源组合成「多源」，任一可用即可。</summary>
public interface IUpdateSource
{
    /// <summary>获取指定通道/架构下的最新版本；无可用版本时返回 null。</summary>
    Task<UpdateVersionInfo?> GetLatestAsync(UpdateChannel channel, UpdateArch arch, CancellationToken ct = default);

    /// <summary>返回某版本的下载地址列表（第一个为主地址，其余为镜像）。</summary>
    IReadOnlyList<string> GetDownloadUrls(UpdateVersionInfo version);
}
