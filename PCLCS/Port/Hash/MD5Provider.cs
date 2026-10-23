using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCLCS.Port.Hash;

/// <summary>MD5 提供器（兼容 .NET Framework 4.8）。</summary>
public class MD5Provider : IHashProvider
{
    public static MD5Provider Instance { get; } = new();
    public int Length => 32;

    public byte[] ComputeHash(ReadOnlySpan<byte> input) => MD5.Create().ComputeHash(input.ToArray());

    public byte[] ComputeHash(string input, Encoding? en = null)
        => ComputeHash(en is null ? Encoding.UTF8.GetBytes(input) : en.GetBytes(input));

    public byte[] ComputeHash(Stream input) => MD5.Create().ComputeHash(_ReadAll(input));

    public async Task<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default)
        => MD5.Create().ComputeHash(await _ReadAllAsync(input, cancellationToken).ConfigureAwait(false));

    private static byte[] _ReadAll(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return ms.ToArray();
    }

    private static async Task<byte[]> _ReadAllAsync(Stream input, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
