using System;

namespace PCLCS.Port.Saves;

/// <summary>存档文件夹中找不到 level.dat（或 level.dat_old）。</summary>
public class SaveNotFoundException : Exception
{
    public SaveNotFoundException(string folderPath)
        : base($"存档文件夹中找不到 level.dat：{folderPath}") { }
}

/// <summary>level.dat 存在但无法解析或写入。</summary>
public class SaveCorruptedException : Exception
{
    public SaveCorruptedException(string folderPath, string message, Exception? inner = null)
        : base($"{message}（{folderPath}）", inner) { }
}
