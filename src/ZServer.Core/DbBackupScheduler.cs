namespace ZServer.Core;

/// <summary>
/// สำรองฐานข้อมูลอัตโนมัติทุกวันตามเวลาที่ตั้ง (AutoBackupTime)
/// — เรียกจาก timer ของหน้าหลักทุก 1 นาที; วิ่งสำเร็จวันละครั้ง (จำวันล่าสุดใน config)
/// ไฟล์เก่าเกิน AutoBackupRetentionDays วันจะถูกลบ
/// </summary>
public static class DbBackupScheduler
{
    private static readonly object Gate = new();
    private static bool _running;

    public static (bool Ran, string Message) CheckAndRun(ServerPaths p, AppConfig c, Logger log)
    {
        lock (Gate)
        {
            if (_running) return (false, "กำลังทำสำรองอยู่แล้ว");
            _running = true;
            try
            {
                return RunOnce(p, c, log);
            }
            finally
            {
                _running = false;
            }
        }
    }

    private static (bool Ran, string Message) RunOnce(ServerPaths p, AppConfig c, Logger log)
    {
        var m = c.MariaDb;
        if (!m.AutoBackupEnabled) return (false, "");

        if (!TimeSpan.TryParse(m.AutoBackupTime, out var target))
        {
            log.Warn("เวลาสำรองอัตโนมัติรูปแบบผิด: " + m.AutoBackupTime);
            return (false, "");
        }

        // วิ่งเมื่อถึง/เลยเวลาที่ตั้งของ "วันนี้" และยังไม่ได้ทำวันนี้
        var now = DateTime.Now;
        if (now.TimeOfDay < target) return (false, "");
        if (m.LastDbBackupDate?.Date == now.Date) return (false, "");

        if (!File.Exists(p.MariaDbBin))
        {
            log.Warn("สำรองอัตโนมัติ: ยังไม่ได้ติดตั้ง MariaDB");
            return (false, "");
        }

        var db = new MariaDbManager(p, c, log, new Downloader(log));
        var dumpDir = Path.Combine(p.BackupsDir, "databases");
        Directory.CreateDirectory(dumpDir);
        var file = Path.Combine(dumpDir, "all-" + now.ToString("yyyyMMdd-HHmmss") + ".sql");

        try
        {
            db.DumpAll(file);
        }
        catch (Exception ex)
        {
            // มักเกิดจาก MariaDB ยังไม่ทำงาน — ลองใหม่ติ๊กถัดไป
            log.Warn("สำรองอัตโนมัติยังไม่สำเร็จ: " + ex.Message);
            return (false, ex.Message);
        }

        // เก็บไฟล์เก่าเกิน retention
        var retention = m.AutoBackupRetentionDays;
        int removed = 0;
        if (retention > 0)
        {
            var cutoff = now.AddDays(-retention);
            foreach (var f in Directory.GetFiles(dumpDir, "*.sql"))
            {
                try
                {
                    if (new FileInfo(f).LastWriteTime < cutoff)
                    {
                        File.Delete(f);
                        removed++;
                    }
                }
                catch { }
            }
        }

        m.LastDbBackupDate = now;
        c.Save(p);
        var sizeText = new FileInfo(file).Length / 1024.0 / 1024.0;
        log.Info($"สำรองฐานข้อมูลอัตโนมัติสำเร็จ: {file} ({sizeText:F1} MB)" +
                 (removed > 0 ? $" — ลบไฟล์เก่า {removed} ไฟล์" : ""));
        return (true, file);
    }
}
