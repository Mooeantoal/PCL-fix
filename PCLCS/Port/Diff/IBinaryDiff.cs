using System.Threading.Tasks;

namespace PCLCS.Port.Diff;

/// <summary>二进制差量接口。移植自 PCL-CE-dev 的 PCL.Core/Utils/Diff/IBinaryDiff.cs。</summary>
public interface IBinaryDiff
{
    Task<byte[]> MakeAsync(byte[] originData, byte[] newData);
    Task<byte[]> ApplyAsync(byte[] originData, byte[] diffData);
}
