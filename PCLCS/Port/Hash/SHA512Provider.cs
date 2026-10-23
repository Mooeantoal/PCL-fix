using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCLCS.Port.Hash;

/// <summary>SHA-512 提供器（兼容 .NET Framework 4.8）。</summary>
public class SHA512Provider : IHashProvider
{
    public static SHA512Provider Instance { get; } = new();
    public int Length => 128;

    public byte[] ComputeHash(ReadOnlySpan<byte> input) => SHA512.Create().ComputeHash(input.ToArray());

    public byte[] ComputeHash(string input, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return ComputeHash(encoding.GetBytes(input));
    }

    public byte[] ComputeHash(Stream input) => SHA512.Create().ComputeHash(_ReadAll(input));

    public async Task<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default)
        => SHA512.Create().ComputeHash(await _ReadAllAsync(input, cancellationToken).ConfigureAwait(false));

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
