using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCLCS.Port.Hash;

/// <summary>
/// 哈希算法提供器接口。移植自 PCL-CE-dev 的 PCL.Core/Utils/Hash/IHashProvider.cs。
/// </summary>
public interface IHashProvider
{
    Task<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default);
    byte[] ComputeHash(Stream input);
    byte[] ComputeHash(ReadOnlySpan<byte> input);
    byte[] ComputeHash(string input, Encoding? en = null);
    int Length { get; }
}
