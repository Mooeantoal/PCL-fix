using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using fNbt;

namespace PCLCS.Port.Saves;

/// <summary>
/// 存档管理器（只读核心）。移植自 PCL-CE-dev 的 SaveManager.cs，
/// 但移除对 PCL.Core（fNbt 之外的类型）的依赖，改为本地解析常用字段。
/// 提供：扫描存档目录、读取单个世界元数据（名称/版本/种子/出生点/模式/难度/时长等）。
/// 可作为 2.14.0.0 缺失的「存档管理」基础——在其之上可继续做导入/导出/编辑。
/// </summary>
public class SaveManager
{
    /// <summary>
    /// 扫描指定目录下的所有有效存档文件夹，返回按最后游玩时间降序排列的列表。
    /// 不含 level.dat 的文件夹会被静默跳过。
    /// </summary>
    /// <param name="savesPath">存档根目录（通常为 .minecraft/saves）。</param>
    public async Task<IReadOnlyList<SaveInfo>> ScanSaveFoldersAsync(
        string savesPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(savesPath))
            return Array.Empty<SaveInfo>();

        var folderPaths = Directory.GetDirectories(savesPath);
        var results = new List<SaveInfo>(folderPaths.Length);

        foreach (var folder in folderPaths)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = await LoadSaveAsync(folder, ct).ConfigureAwait(false);
                if (info is not null)
                    results.Add(info);
            }
            catch (SaveNotFoundException)
            {
                // 非存档文件夹，静默跳过
            }
            catch (Exception)
            {
                // 解析失败的存档跳过，不中断整体扫描
            }
        }

        return results.OrderByDescending(s => s.LastPlayedUtc).ToList();
    }

    /// <summary>加载指定文件夹中的单个存档；找不到 level.dat 抛 <see cref="SaveNotFoundException"/>。</summary>
    public Task<SaveInfo> LoadSaveAsync(string folderPath, CancellationToken ct = default)
    {
        var levelDatPath = ResolveLevelDatPath(folderPath)
            ?? throw new SaveNotFoundException(folderPath);
        return LoadFromPathAsync(folderPath, levelDatPath, ct);
    }

    private async Task<SaveInfo> LoadFromPathAsync(string folderPath, string levelDatPath, CancellationToken ct)
    {
        NbtFile nbtFile;
        try
        {
            nbtFile = await LoadNbtFileAsync(levelDatPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new SaveCorruptedException(folderPath, $"解析 level.dat 失败：'{levelDatPath}'", ex);
        }

        var data = nbtFile.RootTag.Get<NbtCompound>("Data")
            ?? throw new SaveCorruptedException(folderPath, $"level.dat 中缺少 Data 复合标签：{levelDatPath}");

        var createdAt = Directory.GetCreationTimeUtc(folderPath);
        var modifiedAt = File.GetLastWriteTimeUtc(levelDatPath);
        return Parse(folderPath, data, createdAt, modifiedAt);
    }

    /// <summary>从 Data 复合标签解析常用字段为 SaveInfo。</summary>
    private SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var levelName = data.TryGet<NbtString>("LevelName", out var name) && name != null
            ? name.Value : Path.GetFileName(folderPath);

        var info = new SaveInfo(levelName, folderPath, createdAt, modifiedAt);

        if (data.TryGet<NbtCompound>("Version", out var ver) && ver != null)
        {
            if (ver.TryGet<NbtString>("Name", out var vn) && vn != null) info.VersionName = vn.Value;
            if (ver.TryGet<NbtInt>("Id", out var vid) && vid != null) info.VersionId = vid.Value;
        }

        if (data.TryGet<NbtLong>("RandomSeed", out var seed) && seed != null) info.Seed = seed.Value;

        if (data.TryGet<NbtLong>("LastPlayed", out var lp) && lp != null)
            info.LastPlayedUtc = _FromUnixMilliseconds(lp.Value);

        Vector3? spawn = null;
        if (data.TryGet<NbtInt>("SpawnX", out var sx) && sx != null &&
            data.TryGet<NbtInt>("SpawnY", out var sy) && sy != null &&
            data.TryGet<NbtInt>("SpawnZ", out var sz) && sz != null)
            spawn = new Vector3(sx.Value, sy.Value, sz.Value);
        info.Spawn = spawn;

        if (data.TryGet<NbtInt>("GameType", out var gt) && gt != null)
            info.GameMode = (GameMode)gt.Value;

        if (data.TryGet<NbtByte>("hardcore", out var hc) && hc != null)
            info.IsHardcore = hc.Value != 0;

        if (data.TryGet<NbtByte>("allowCommands", out var ac) && ac != null)
            info.AllowCommands = ac.Value != 0;

        if (data.TryGet<NbtByte>("Difficulty", out var diff) && diff != null)
            info.Difficulty = (Difficulty)diff.Value;

        // 游戏内时间以 tick 计（20 tick/秒），用作累计时长估计
        if (data.TryGet<NbtLong>("Time", out var time) && time != null)
            info.PlayTime = TimeSpan.FromSeconds((double)time.Value / 20.0);

        return info;
    }

    private static DateTime _FromUnixMilliseconds(long value)
    {
        try
        {
            // 小于 1e12 视为秒级时间戳（个别旧版本），否则毫秒级
            return value < 1_000_000_000_000L
                ? DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime
                : DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static async Task<NbtFile> LoadNbtFileAsync(string path, CancellationToken ct)
    {
        var nbtFile = new NbtFile();
        await Task.Run(() =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
        }, ct).ConfigureAwait(false);
        return nbtFile;
    }

    private static string? ResolveLevelDatPath(string folderPath)
    {
        var primary = Path.Combine(folderPath, "level.dat");
        if (File.Exists(primary)) return primary;
        var backup = Path.Combine(folderPath, "level.dat_old");
        return File.Exists(backup) ? backup : null;
    }
}
