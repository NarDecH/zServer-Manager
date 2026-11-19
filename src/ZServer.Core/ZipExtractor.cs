using System.IO.Compression;

namespace ZServer.Core;

/// <summary>
/// แตกไฟล์ zip อย่างปลอดภัย (ป้องกัน Zip Slip) พร้อมช่วยตรวจจับโฟลเดอร์รากของ zip
/// </summary>
public static class ZipExtractor
{
    /// <summary>แตกไฟล์ลงโฟลเดอร์ปลายทาง (ทับของเดิมได้)</summary>
    public static void Extract(string zipPath, string destDir, Action<int>? progress = null)
    {
        Directory.CreateDirectory(destDir);

        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        var totalEntries = Math.Max(1, entries.Count);

        int done = 0;
        foreach (var entry in entries)
        {
            var dest = SafeResolve(destDir, entry.FullName);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
            done++;
            progress?.Invoke(done * 100 / totalEntries);
        }
        progress?.Invoke(100);
    }

    /// <summary>
    /// แตกไฟล์ลงโฟลเดอร์ชั่วคราว (staging) แล้วคืน path ของ "โฟลเดอร์เนื้อหาจริง"
    /// — ถ้า zip มีโฟลเดอร์รากเดียว (เช่น Apache24/, php-8.5.13-Win32-vs17-x64/)
    /// จะคืนโฟลเดอร์นั้นเลย เพื่อให้ย้ายเนื้อหาไปใช้งานได้ง่าย
    /// </summary>
    public static string ExtractToStaging(string zipPath, string stagingRoot)
    {
        Directory.CreateDirectory(stagingRoot);
        var temp = Path.Combine(stagingRoot, "extract-" + Guid.NewGuid().ToString("N")[..12]);
        Extract(zipPath, temp);
        return FindSingleRoot(temp) ?? temp;
    }

    /// <summary>ถ้าภายในโฟลเดอร์มีโฟลเดอร์รากเดียว คืนมัน ไม่งั้นคืน null</summary>
    public static string? FindSingleRoot(string dir)
    {
        var dirs = Directory.GetDirectories(dir);
        var files = Directory.GetFiles(dir);
        if (files.Length == 0 && dirs.Length == 1)
            return dirs[0];
        return null;
    }

    /// <summary>
    /// หา "โฟลเดอร์เนื้อหาหลัก" ในโฟลเดอร์ที่แตกแล้ว — ใช้กับ zip ที่มีไฟล์แถมอื่นปนอยู่
    /// เช่น zip ของ Apache Lounge ที่มี Apache24/ + ReadMe.txt + ไฟล์ marker
    /// </summary>
    public static string FindContentRoot(string extracted, params string[] preferredNames)
    {
        foreach (var name in preferredNames)
        {
            var p = Path.Combine(extracted, name);
            if (Directory.Exists(p)) return p;
        }
        return FindSingleRoot(extracted) ?? extracted;
    }

    /// <summary>ย้ายเนื้อหาทั้งหมดของ src ไปใส่ dest (รวมไฟล์ซ่อน)</summary>
    public static void MoveContents(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(dir);
            var target = Path.Combine(dest, name);
            if (Directory.Exists(target))
            {
                // รวมเนื้อหาเข้าด้วยกัน (โฟลเดอร์ชื่อซ้ำ)
                MergeDirectory(dir, target);
            }
            else
            {
                Directory.Move(dir, target);
            }
        }
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            if (File.Exists(target)) File.Delete(target);
            File.Move(file, target);
        }
    }

    private static void MergeDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.TopDirectoryOnly))
        {
            MergeDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            if (File.Exists(target)) File.Delete(target);
            File.Move(file, target);
        }
    }

    /// <summary>ป้องกัน Zip Slip: ห้าม path ของ entry หลุดออกนอก destDir</summary>
    private static string SafeResolve(string destDir, string entryName)
    {
        var fullDest = Path.GetFullPath(destDir + Path.DirectorySeparatorChar);
        // แทน / เป็น \ ก่อน แล้วตัด drive letter / .. ที่หลุม ๆ
        var normalized = entryName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(fullDest, normalized));
        if (!candidate.StartsWith(fullDest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("พบ entry ใน zip ที่ไม่ปลอดภัย (Zip Slip): " + entryName);
        return candidate;
    }
}
