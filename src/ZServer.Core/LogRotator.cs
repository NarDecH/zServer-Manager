using System.IO.Compression;

namespace ZServer.Core;

/// <summary>
/// จัดการ log เก่า: ไฟล์ใน logs\ ที่ไม่ถูกแตะมานานกว่า N วัน
/// จะถูกบีบอัดเก็บไว้ใน backups\log-archives\ แล้วลบต้นฉบับ
/// (ไฟล์ที่ Apache/MariaDB กำลังล็อคอยู่จะถูกข้ามเงียบ ๆ)
/// </summary>
public static class LogRotator
{
    public static (int Archived, int Failed) Run(ServerPaths p, int retentionDays, Logger log)
    {
        if (retentionDays <= 0) return (0, 0);
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        var archiveDir = Path.Combine(p.BackupsDir, "log-archives");
        Directory.CreateDirectory(archiveDir);

        int archived = 0, failed = 0;
        foreach (var file in Directory.GetFiles(p.LogsDir, "*.*"))
        {
            var name = Path.GetFileName(file);
            // ไฟล์ .pid และ log ของโปรแกรมเองไม่จัดการ (ผู้ใช้ล้างเองผ่านปุ่ม)
            if (name.EndsWith(".pid", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.StartsWith("zserver-manager", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime >= cutoff) continue;

                var zipPath = Path.Combine(archiveDir,
                    Path.GetFileNameWithoutExtension(file) + "-" + info.LastWriteTime.ToString("yyyyMMdd") + ".zip");
                if (!File.Exists(zipPath))
                {
                    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal);
                }
                info.Delete();
                archived++;
            }
            catch
            {
                failed++; // ไฟล์ถูกล็อคโดยโปรเซสที่รันอยู่ — ข้ามไปก่อน ครั้งหน้าจะลองใหม่
            }
        }

        if (archived > 0 || failed > 0)
            log.Info($"Log rotation: จัดเก็บ {archived} ไฟล์ เก่ากว่า {retentionDays} วัน (ข้าม {failed} ไฟล์ที่กำลังถูกใช้)");
        return (archived, failed);
    }
}
