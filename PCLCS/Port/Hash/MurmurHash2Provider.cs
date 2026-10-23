using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCLCS.Port.Hash;

/// <summary>
/// MurmurHash2（32 位）提供器。移植自 PCL-CE-dev，去掉了 CollectionsMarshal 依赖以兼容 net48。
/// 注意：计算前会过滤掉制表符(9)/换行(10)/回车(13)/空格(32)。
/// </summary>
public class MurmurHash2Provider : IHashProvider
{
    public static MurmurHash2Provider Instance { get; } = new();
    public int Length => 8;

    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        var filtered = new List<byte>(data.Length);
        foreach (var b in data)
            if (b != 9 && b != 10 && b != 13 && b != 32)
                filtered.Add(b);
        return _ComputeHash(filtered.ToArray());
    }

    public byte[] ComputeHash(string input, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return ComputeHash(encoding.GetBytes(input));
    }

    public byte[] ComputeHash(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return ComputeHash(ms.ToArray().AsSpan());
    }

    public async Task<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
        return ComputeHash(ms.ToArray().AsSpan());
    }

    private static byte[] _ComputeHash(byte[] data)
    {
        var h = (uint)(1 ^ data.Length);
        var i = 0;
        var loopTo = data.Length - 4;

        for (; i <= loopTo; i += 4)
        {
            var k = (uint)(data[i]
                 | (data[i + 1] << 8)
                 | (data[i + 2] << 16)
                 | (data[i + 3] << 24));
            k *= 0x5BD1E995;
            k ^= k >> 24;
            k *= 0x5BD1E995;
            h *= 0x5BD1E995;
            h ^= k;
        }

        switch (data.Length - i)
        {
            case 3:
                h ^= (uint)(data[i] | (data[i + 1] << 8));
                h ^= (uint)data[i + 2] << 16;
                h *= 0x5BD1E995;
                break;
            case 2:
                h ^= (uint)(data[i] | (data[i + 1] << 8));
                h *= 0x5BD1E995;
                break;
            case 1:
                h ^= data[i];
                h *= 0x5BD1E995;
                break;
        }

        h ^= h >> 13;
        h *= 0x5BD1E995;
        h ^= h >> 15;

        return BitConverter.GetBytes(h);
    }
}
