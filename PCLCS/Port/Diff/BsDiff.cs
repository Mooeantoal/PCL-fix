/*
部分内容参考了 https://github.com/LogosBible/bsdiff.net 的实现

Copyright 2010-2024 Logos Bible Software
...

Copyright 2003-2005 Colin Percival
All rights reserved
（BSD 许可，此处省略原文以节省篇幅，完整许可见 PCL-CE-dev 源文件）

注意：本移植仅实现 ApplyAsync（应用服务端预先生成的 BSDIFF40 差量包）。
MakeAsync 在 PCL-CE-dev 中同样未实现——差量包由更新服务器生成，客户端只负责打补丁。
依赖 ICSharpCode.SharpZipLib（BZip2 解压），已在 PCLCS.csproj 中添加 PackageReference。
*/

using System;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;

namespace PCLCS.Port.Diff;

/// <summary>
/// BSDIFF40 二进制差量（应用侧）。移植自 PCL-CE-dev 的 PCL.Core/Utils/Diff/BsDiff.cs。
/// 文件格式：8 字节 "BSDIFF40" + 三个 8 字节长度（控制块/差异块/新文件大小）+ 三段 BZip2 压缩数据。
/// </summary>
public class BsDiff : IBinaryDiff
{
    private const int HeaderSize = 32;
    private const int HeaderVersionIndex = 0;
    private const long HeaderVersion = 0x3034464649445342; // "BSDIFF40" little-endian
    private const int HeaderCtrlIndex = 8;
    private const int HeaderDiffIndex = 16;
    private const int HeaderNewSizeIndex = 24;

    public async Task<byte[]> ApplyAsync(byte[] originData, byte[] diffData)
    {
        return await Task.Run(() =>
        {
            if (diffData.Length < HeaderSize)
                throw new Exception("Diff file size is less than the header size");
            if (BitConverter.ToInt64(diffData, HeaderVersionIndex) != HeaderVersion)
                throw new Exception("Diff file version is wrong");

            var ctrlLen = BitConverter.ToInt64(diffData, HeaderCtrlIndex);
            var diffLen = BitConverter.ToInt64(diffData, HeaderDiffIndex);
            var newLen = BitConverter.ToInt64(diffData, HeaderNewSizeIndex);
            var extraLen = diffData.Length - HeaderSize - ctrlLen - diffLen;

            if (ctrlLen < 0 || diffLen < 0 || extraLen < 0)
                throw new Exception("Block size is negative");
            if (newLen < 0)
                throw new Exception("Final file size info is negative");
            if (HeaderSize + ctrlLen + diffLen + extraLen > diffData.Length)
                throw new Exception("Diff file size info is not correct");

            var ctrlContent = new byte[ctrlLen];
            long curOffset = HeaderSize;
            Array.Copy(diffData, curOffset, ctrlContent, 0, ctrlLen);
            using var ctrlStream = new BZip2InputStream(new MemoryStream(ctrlContent));
            using var ctrlReader = new BinaryReader(ctrlStream);

            curOffset += ctrlLen;
            var diffContent = new byte[diffLen];
            Array.Copy(diffData, curOffset, diffContent, 0, diffLen);
            using var diffStream = new BZip2InputStream(new MemoryStream(diffContent));
            using var diffReader = new BinaryReader(diffStream);

            curOffset += diffLen;
            var extraContent = new byte[extraLen];
            Array.Copy(diffData, curOffset, extraContent, 0, extraLen);
            using var extraStream = new BZip2InputStream(new MemoryStream(extraContent));
            using var extraReader = new BinaryReader(extraStream);

            var ret = new byte[newLen];

            long newDataPos = 0;
            long oldDataPos = 0;
            while (newDataPos < newLen)
            {
                var addRange = ReadInt64(ctrlReader.ReadBytes(8));
                var copyRange = ReadInt64(ctrlReader.ReadBytes(8));
                var seekPos = ReadInt64(ctrlReader.ReadBytes(8));

                if (newDataPos + addRange > newLen)
                    throw new Exception(
                        $"Add range overflows, want add {addRange}, but only have {newLen - newDataPos} left");

                for (long i = 0; i < addRange; i++)
                {
                    var readedByte = diffReader.ReadByte();
                    if (oldDataPos + i < originData.Length)
                        ret[newDataPos + i] = (byte)(readedByte + originData[oldDataPos + i]);
                    else
                        ret[newDataPos + i] = readedByte;
                }

                newDataPos += addRange;
                oldDataPos += addRange;

                if (newDataPos + copyRange > newLen)
                    throw new Exception(
                        $"Copy range overflows, want copy {copyRange}, but only have {newLen - newDataPos} left");

                for (var i = 0; i < copyRange; i++)
                    ret[newDataPos + i] = extraReader.ReadByte();

                newDataPos += copyRange;
                oldDataPos += seekPos;
                if (oldDataPos > originData.Length)
                    throw new Exception(
                        $"Old data pos overflows, current old data length = {originData.Length}, but want {oldDataPos}");
            }

            return ret;
        });
    }

    public Task<byte[]> MakeAsync(byte[] originData, byte[] newData)
        => throw new NotImplementedException("差量包由更新服务器生成，客户端不实现 Make。");

    internal static long ReadInt64(byte[] buffer, int offset = 0)
    {
        var value = ((long)buffer[offset] << 0) | ((long)buffer[offset + 1] << 8) |
                   ((long)buffer[offset + 2] << 16) | ((long)buffer[offset + 3] << 24) |
                   ((long)buffer[offset + 4] << 32) | ((long)buffer[offset + 5] << 40) |
                   ((long)buffer[offset + 6] << 48) | ((long)buffer[offset + 7] << 56);

        var mask = value >> 63;
        return (~mask & value) |
               (((value & unchecked((long)0x8000000000000000)) - value) & mask);
    }
}
