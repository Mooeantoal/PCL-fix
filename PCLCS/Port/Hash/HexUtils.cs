using System;

namespace PCLCS.Port.Hash;

/// <summary>字节数组转十六进制字符串的小工具（替代 PCL.Core 的 ToHexString 扩展）。</summary>
internal static class HexUtils
{
    private const string HexChars = "0123456789abcdef";

    public static string ToHexString(this byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return string.Empty;

        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = HexChars[b >> 4];
            chars[i * 2 + 1] = HexChars[b & 0xF];
        }
        return new string(chars);
    }
}
