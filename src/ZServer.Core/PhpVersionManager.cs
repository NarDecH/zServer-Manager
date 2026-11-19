using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// ระบบ PHP หลายเวอร์ชัน:
///   - แต่ละเวอร์ชันเก็บอยู่ที่ php-versions\{เวอร์ชัน}\ (โฟลเดอร์จริง)
///   - ตัวที่ใช้งานคือ junction "php" ที่ชี้ไปยังเวอร์ชันนั้น
///     (httpd.conf / php.ini / extension_dir อ้างอิง C:\zServer\php เสมอ จึงไม่ต้องแก้ config เวลาสลับ)
/// </summary>
public static class PhpVersionManager
{
    public static string VersionsRoot(ServerPaths p) => Path.Combine(p.Root, "php-versions");

    public static List<string> ListVersions(ServerPaths p)
    {
        var root = VersionsRoot(p);
        if (!Directory.Exists(root)) return new List<string>();
        return Directory.GetDirectories(root)
            .Select(Path.GetFileName)!
            .Where(n => File.Exists(Path.Combine(root, n, "php.exe")))
            .OrderByDescending(n => n, new VersionComparer())
            .ToList();
    }

    /// <summary>เวอร์ชันที่ junction php\ ชี้อยู่ (null = ยังเป็นโฟลเดอร์จริง/ยังไม่ได้ย้าย)</summary>
    public static string? ActiveVersion(ServerPaths p)
    {
        try
        {
            var di = new DirectoryInfo(p.PhpDir);
            if (!Directory.Exists(p.PhpDir) || (di.Attributes & FileAttributes.ReparsePoint) == 0)
                return null;
            var target = di.LinkTarget;
            return string.IsNullOrEmpty(target) ? null : Path.GetFileName(target.TrimEnd('\\'));
        }
        catch { return null; }
    }

    /// <summary>
    /// เตรียมโครงสร้างระบบหลายเวอร์ชัน: ถ้า php\ เป็น "โฟลเดอร์จริง"
    /// จะย้ายทั้งโฟลเดอร์เข้า php-versions\{เวอร์ชันที่ตรวจพบ}\
    /// </summary>
    public static void EnsureVersionsLayout(ServerPaths p, Logger log)
    {
        Directory.CreateDirectory(VersionsRoot(p));
        if (!Directory.Exists(p.PhpDir)) return;

        var di = new DirectoryInfo(p.PhpDir);
        if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
            return; // เป็น junction อยู่แล้ว — ระบบพร้อมใช้

        if (!File.Exists(Path.Combine(p.PhpDir, "php.exe")))
        {
            // php\ ว่างหรือไม่สมบูรณ์ — ลบทิ้งเพื่อสร้าง junction ใหม่
            try { Directory.Delete(p.PhpDir, recursive: true); log.Warn("ลบ php\\ ที่ว่างเปล่าเพื่อสร้าง junction ใหม่"); }
            catch { }
            return;
        }

        var ver = DetectVersionFromDir(p) ?? "unknown-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dest = Path.Combine(VersionsRoot(p), ver);
        if (Directory.Exists(dest))
            dest += "-" + DateTime.Now.ToString("HHmmss");
        Directory.Move(p.PhpDir, dest);
        log.Info($"ย้าย PHP เดิมเข้าระบบหลายเวอร์ชัน: {dest}");
    }

    /// <summary>สลับ junction php\ ไปชี้เวอร์ชันที่กำหนด (เรียกตอน Apache หยุดอยู่)</summary>
    public static void SwitchTo(ServerPaths p, string version, Logger log)
    {
        var target = Path.Combine(VersionsRoot(p), version);
        if (!File.Exists(Path.Combine(target, "php.exe")))
            throw new InvalidOperationException("ไม่พบเวอร์ชัน PHP " + version + " ใน php-versions\\");

        EnsureVersionsLayout(p, log);

        // ลบ junction เดิม (ลบเฉพาะลิงก์ ไม่ลบข้อมูลเวอร์ชัน)
        if (Directory.Exists(p.PhpDir))
        {
            var di = new DirectoryInfo(p.PhpDir);
            if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                di.Delete();
            else
                throw new InvalidOperationException(
                    "php\\ ยังเป็นโฟลเดอร์จริงอยู่ (ย้ายเข้าระบบหลายเวอร์ชันไม่สำเร็จ) — ยกเลิกการสลับเพื่อความปลอดภัยของข้อมูล");
        }

        var (code, _, err) = ProcessRunner.Run("cmd.exe",
            $"/c mklink /J \"{p.PhpDir}\" \"{target}\"", null, 30_000);
        if (code != 0 || !Directory.Exists(p.PhpDir))
            throw new InvalidOperationException("สร้าง junction php\\ ไม่สำเร็จ: " + err);

        log.Info($"สลับ PHP เป็นเวอร์ชัน {version} (junction → {target})");
    }

    private static string? DetectVersionFromDir(ServerPaths p)
    {
        var phpExe = Path.Combine(p.PhpDir, "php.exe");
        if (!File.Exists(phpExe)) return null;
        try
        {
            var (code, stdout, _) = ProcessRunner.Run(phpExe, "-v", null, 20_000);
            if (code != 0) return null;
            var m = Regex.Match(stdout, @"PHP (\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => UpdateChecker.CompareVersions(x ?? "", y ?? "");
    }
}
