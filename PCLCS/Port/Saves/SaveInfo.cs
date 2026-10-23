using System;

namespace PCLCS.Port.Saves;

/// <summary>
/// 存档核心数据模型（不可变）。由 <see cref="SaveManager"/> 从 level.dat 解析得到。
/// 移植自 PCL-CE-dev 的 SaveInfo.cs，但 GameMode/Difficulty/Vector3 改用本命名空间内的本地定义，
/// 以避免依赖 PCL.Core。
/// </summary>
public sealed class SaveInfo
{
    public SaveInfo(
        string levelName,
        string folderPath,
        DateTime createdAt,
        DateTime modifiedAt)
    {
        LevelName = levelName ?? throw new ArgumentNullException(nameof(levelName));
        FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
    }

    /// <summary>世界名称。</summary>
    public string LevelName { get; }
    /// <summary>最后保存此存档的游戏版本名（如 "1.20.4"）。</summary>
    public string? VersionName { get; set; }
    /// <summary>最后保存此存档的游戏数据版本号（对应 Data.Version.Id）。</summary>
    public int? VersionId { get; set; }
    /// <summary>世界种子（旧版本 level.dat 中可见）。</summary>
    public long? Seed { get; set; }
    /// <summary>最后游玩时间（UTC）。</summary>
    public DateTime LastPlayedUtc { get; set; }
    /// <summary>出生点坐标。</summary>
    public Vector3? Spawn { get; set; }
    /// <summary>游戏模式。</summary>
    public GameMode GameMode { get; set; }
    /// <summary>游戏难度（旧版本）。</summary>
    public Difficulty? Difficulty { get; set; }
    /// <summary>是否为极限模式。</summary>
    public bool IsHardcore { get; set; }
    /// <summary>是否允许作弊命令。</summary>
    public bool AllowCommands { get; set; }
    /// <summary>累计游戏时间。</summary>
    public TimeSpan PlayTime { get; set; }
    /// <summary>存档文件夹的绝对路径。</summary>
    public string FolderPath { get; }
    /// <summary>存档文件夹的创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; }
    /// <summary>level.dat 的最后修改时间（UTC）。</summary>
    public DateTime ModifiedAt { get; }
}
