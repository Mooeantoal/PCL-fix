using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PCLCS.Port.Java;

/// <summary>
/// 检测到的 Java 信息。
/// </summary>
public class JavaInfo
{
    /// <summary>java.exe 所在目录（以 \ 结尾）。</summary>
    public string Folder { get; set; } = "";
    /// <summary>java.exe 完整路径。</summary>
    public string JavaExePath => Path.Combine(Folder, "java.exe");
    /// <summary>版本（Major 为特性版本，如 8/17/21）。</summary>
    public Version? Version { get; set; }
    /// <summary>架构。</summary>
    public JavaArchitecture Architecture { get; set; } = JavaArchitecture.Unknown;
    /// <summary>是否为 JDK（含 javac）。</summary>
    public bool IsJdk { get; set; }
}

public enum JavaArchitecture { Unknown = 0, x86 = 1, x64 = 2, arm64 = 3 }

/// <summary>
/// Java 扫描器（移植/增强自 PCL-CE-dev 的 Minecraft/Java 思路，重写为框架无关实现）。
/// 相比 2.14.0.0 原有 ModJava.vb，增加了：注册表扫描（JavaSoft 的 JRE/JDK）、常见安装目录、
/// 架构识别，以及通过 `java -version` 或同级 `release` 文件解析版本。
/// 返回去重后的 Java 列表，可由 VB 侧直接替换旧逻辑。
/// </summary>
public class JavaScanner
{
    // 常见搜索根目录
    private static readonly string[] CommonRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),          // C:\Program Files
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),        // C:\Program Files (x86)
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Common\\Microsoft\\Java"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks"),
    };

    /// <summary>扫描系统上所有可发现的 Java，去重后返回。</summary>
    public async Task<List<JavaInfo>> ScanAsync(CancellationToken ct = default)
    {
        var found = new Dictionary<string, JavaInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in CommonRoots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            foreach (var dir in SafeEnumerateDirectories(root, 3))
            {
                ct.ThrowIfCancellationRequested();
                var exe = Path.Combine(dir, "bin", "java.exe");
                if (File.Exists(exe)) TryAdd(found, exe);
            }
        }

        ScanRegistry(found, Registry.LocalMachine, RegistryView.Registry64);
        ScanRegistry(found, Registry.LocalMachine, RegistryView.Registry32);
        ScanRegistry(found, Registry.CurrentUser, RegistryView.Registry64);
        ScanRegistry(found, Registry.CurrentUser, RegistryView.Registry32);

        // 解析版本（可能需要执行 java -version，放到后台）
        foreach (var info in found.Values)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Run(() => ResolveVersion(info), ct).ConfigureAwait(false);
        }

        return new List<JavaInfo>(found.Values);
    }

    private static void ScanRegistry(Dictionary<string, JavaInfo> found, RegistryKey baseKey, RegistryView view)
    {
        try
        {
            using var hive = RegistryKey.OpenBaseKey(baseKey == Registry.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
            foreach (var sub in new[] { "SOFTWARE\\JavaSoft\\Java Runtime Environment", "SOFTWARE\\JavaSoft\\Java Development Kit" })
            {
                using var key = hive.OpenSubKey(sub);
                if (key == null) continue;
                foreach (var ver in key.GetSubKeyNames())
                {
                    using var vk = key.OpenSubKey(ver);
                    var home = vk?.GetValue("JavaHome") as string;
                    if (!string.IsNullOrEmpty(home))
                    {
                        var exe = Path.Combine(home, "bin", "java.exe");
                        if (File.Exists(exe)) TryAdd(found, exe);
                    }
                }
            }
        }
        catch
        {
            // 注册表不可访问时忽略
        }
    }

    private static void TryAdd(Dictionary<string, JavaInfo> found, string exe)
    {
        var folder = Path.GetDirectoryName(exe) + "\\";
        if (found.ContainsKey(folder)) return;
        found[folder] = new JavaInfo
        {
            Folder = folder,
            IsJdk = File.Exists(Path.Combine(folder, "javac.exe"))
        };
    }

    private static void ResolveVersion(JavaInfo info)
    {
        // 优先读取同级 release 文件
        var releaseFile = Path.Combine(info.Folder, "..", "release");
        if (File.Exists(releaseFile))
        {
            foreach (var line in File.ReadAllLines(releaseFile))
            {
                if (line.StartsWith("JAVA_VERSION="))
                {
                    var v = line.Substring("JAVA_VERSION=".Length).Trim('"');
                    info.Version = ParseJavaVersion(v);
                    break;
                }
            }
        }

        // 回退：执行 java -version（输出在 stderr）
        if (info.Version == null)
        {
            try
            {
                var psi = new ProcessStartInfo(info.JavaExePath, "-version")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                var output = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                var idx = output.IndexOf('"');
                if (idx >= 0)
                {
                    var end = output.IndexOf('"', idx + 1);
                    if (end > idx)
                        info.Version = ParseJavaVersion(output.Substring(idx + 1, end - idx - 1));
                }
            }
            catch
            {
                // 解析失败保留 null
            }
        }

        info.Architecture = DetectArchitecture(info);
    }

    /// <summary>
    /// 解析 Java 版本字符串为 Version，并把 1.8 这类旧式号映射为 Major=8。
    /// 例："1.8.0_301" → 8.0.0；"17.0.2" → 17.0.2；"21.0.1+12" → 21.0.1。
    /// </summary>
    public static Version? ParseJavaVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s.Substring(0, plus);

        // 旧式 1.x.y → x.y
        if (s.StartsWith("1.") && s.Length > 2)
        {
            var rest = s.Substring(2); // "8.0_301"
            var clean = rest.Replace("_", ".");
            if (Version.TryParse(SafeForParse(clean), out var v))
                return new Version(Math.Max(v.Major, 0), v.Minor, v.Build >= 0 ? v.Build : 0);
            return new Version(int.Parse(clean.Split('.')[0]), 0, 0);
        }

        var clean2 = s.Replace("_", ".");
        if (Version.TryParse(SafeForParse(clean2), out var v2))
            return v2;
        return null;
    }

    private static string SafeForParse(string s)
    {
        var parts = s.Split('.');
        var nums = new List<int>();
        foreach (var p in parts)
            if (int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n))
                nums.Add(n);
        while (nums.Count < 2) nums.Add(0);
        return $"{nums[0]}.{nums[1]}.{(nums.Count > 2 ? nums[2] : 0)}";
    }

    private static JavaArchitecture DetectArchitecture(JavaInfo info)
    {
        // 简单判定：x86 路径通常含 (x86)；否则 64 位常见。更严谨可用 IsWow64 / PE 头读取。
        if (info.Folder.Contains("(x86)", StringComparison.OrdinalIgnoreCase))
            return JavaArchitecture.x86;
        return JavaArchitecture.x64;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root, int maxDepth)
    {
        if (maxDepth <= 0) yield break;
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(root); }
        catch { yield break; }

        foreach (var d in dirs)
        {
            yield return d;
            foreach (var sub in SafeEnumerateDirectories(d, maxDepth - 1))
                yield return sub;
        }
    }
}
