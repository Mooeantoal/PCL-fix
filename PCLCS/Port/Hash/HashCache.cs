using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PCLCS.Port.Hash;

/// <summary>
/// 文件哈希缓存。移植自 PCL-CE-dev 的 HashCache，但底层由 SQLite 改为 JSON 文件持久化，
/// 以避免在 .NET Framework 4.8 上引入 Microsoft.Data.Sqlite 依赖。
/// 通过「文件路径 + 文件大小 + 最后写入时间」判断缓存是否仍然有效，命中则直接返回，
/// 否则重新计算并写回，适合对大量文件反复做哈希（如下载校验、去重）。
/// </summary>
public class HashCache
{
    private readonly string _cachePath;
    private readonly object _lock = new();
    private Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public HashCache(string cachePath)
    {
        _cachePath = cachePath ?? throw new ArgumentNullException(nameof(cachePath));
        var dir = Path.GetDirectoryName(Path.GetFullPath(_cachePath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _Load();
    }

    private void _Load()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, CacheEntry>>(json);
                if (loaded != null) _entries = loaded;
            }
        }
        catch
        {
            _entries = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void _Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_entries);
            File.WriteAllText(_cachePath, json);
        }
        catch
        {
            // 缓存持久化失败不应影响主流程
        }
    }

    public Task<string> GetMD5Async(string filePath) =>
        GetHashAsync(filePath, MD5Provider.Instance);
    public Task<string> GetSHA1Async(string filePath) =>
        GetHashAsync(filePath, SHA1Provider.Instance);
    public Task<string> GetSHA256Async(string filePath) =>
        GetHashAsync(filePath, SHA256Provider.Instance);
    public Task<string> GetSHA512Async(string filePath) =>
        GetHashAsync(filePath, SHA512Provider.Instance);
    public Task<string> GetMurmurHash2Async(string filePath) =>
        GetHashAsync(filePath, MurmurHash2Provider.Instance);

    public async Task<string> GetHashAsync(string filePath, IHashProvider provider)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        var algoName = _AlgoName(provider);

        try
        {
            var fileInfo = new FileInfo(fullPath);
            var fileSize = fileInfo.Length;
            var lastWrite = fileInfo.LastWriteTimeUtc.ToString("O");

            CacheEntry? cached;
            lock (_lock)
                _entries.TryGetValue(fullPath, out cached);

            if (cached != null && cached.FileSize == fileSize && cached.LastWriteTime == lastWrite)
            {
                var hash = _GetHashFromEntry(cached, algoName);
                if (hash != null) return hash;
            }
            else if (cached != null)
            {
                lock (_lock) _entries.Remove(fullPath);
            }

            var computed = (await provider.ComputeHashAsync(
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)).ConfigureAwait(false))
                .ToHexString();

            lock (_lock)
            {
                if (!_entries.TryGetValue(fullPath, out var entry))
                    entry = new CacheEntry { FilePath = fullPath, FileSize = fileSize, LastWriteTime = lastWrite };
                _SetHash(entry, algoName, computed);
                _entries[fullPath] = entry;
            }
            _Save();
            return computed;
        }
        catch (FileNotFoundException)
        {
            lock (_lock) _entries.Remove(fullPath);
            _Save();
            throw;
        }
    }

    private static string _AlgoName(IHashProvider provider) => provider switch
    {
        MD5Provider => "MD5",
        SHA1Provider => "SHA1",
        SHA256Provider => "SHA256",
        SHA512Provider => "SHA512",
        MurmurHash2Provider => "MurmurHash2",
        _ => throw new ArgumentException($"不支持的哈希算法: {provider.GetType().Name}")
    };

    private static string? _GetHashFromEntry(CacheEntry entry, string algoName) => algoName switch
    {
        "MD5" => entry.MD5,
        "SHA1" => entry.SHA1,
        "SHA256" => entry.SHA256,
        "SHA512" => entry.SHA512,
        "MurmurHash2" => entry.MurmurHash2,
        _ => null
    };

    private static void _SetHash(CacheEntry entry, string algoName, string hash)
    {
        switch (algoName)
        {
            case "MD5": entry.MD5 = hash; break;
            case "SHA1": entry.SHA1 = hash; break;
            case "SHA256": entry.SHA256 = hash; break;
            case "SHA512": entry.SHA512 = hash; break;
            case "MurmurHash2": entry.MurmurHash2 = hash; break;
        }
    }

    private sealed class CacheEntry
    {
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string LastWriteTime { get; set; } = "";
        public string? MD5 { get; set; }
        public string? SHA1 { get; set; }
        public string? SHA256 { get; set; }
        public string? SHA512 { get; set; }
        public string? MurmurHash2 { get; set; }
    }
}
