using System.Text.RegularExpressions;

namespace ZServer.Core;

public sealed record InstallProgress(string Phase, int Percent, string Message);

/// <summary>ฐานร่วมของตัวจัดการคอมโพเนนต์แต่ละตัว (ติดตั้ง/อัพเดท/detect เวอร์ชัน)</summary>
public abstract class ComponentManager
{
    protected readonly ServerPaths P;
    protected readonly AppConfig C;
    protected readonly Logger L;
    protected readonly Downloader Dl;

    protected ComponentManager(ServerPaths p, AppConfig c, Logger l, Downloader dl)
    {
        P = p; C = c; L = l; Dl = dl;
    }

    public abstract string Component { get; }
    public abstract string DisplayName { get; }
    public abstract bool IsInstalled { get; }
    public abstract string? DetectInstalledVersion();

    /// <summary>ดาวน์โหลด + แตกไฟล์ + เขียน config + ติดตั้ง service (ตามเลือก)</summary>
    public abstract Task InstallAsync(CatalogItem item, IProgress<InstallProgress>? progress,
        bool installService, CancellationToken ct = default);

    private string _lastReportedPhase = "";
    private int _lastReportedPercent = -1;

    protected void Report(IProgress<InstallProgress>? progress, int percent, string phase, string message)
    {
        progress?.Report(new InstallProgress(phase, percent, message));

        // log ไฟล์/ช่อง log สด: เฉพาะเมื่อเปลี่ยนขั้นงานหรือเสร็จ 100%
        // (ความคืบหน้ารายวินาทีให้ UI แสดงบน progress bar เอง — กัน log ล้นด้วยบรรทัดซ้ำ)
        if (phase != _lastReportedPhase)
        {
            _lastReportedPhase = phase;
            _lastReportedPercent = percent;
            L.Info($"[{DisplayName}] {phase}: {message}");
        }
        else if (percent >= 100 && _lastReportedPercent < 100)
        {
            _lastReportedPercent = 100;
            L.Info($"[{DisplayName}] {phase}: {message}");
        }
    }

    protected void BackupExistingFolder(string dir, string label)
    {
        if (!Directory.Exists(dir) || !Directory.EnumerateFileSystemEntries(dir).Any()) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dest = Path.Combine(P.BackupsDir, $"{label}-{stamp}");
        Directory.Move(dir, dest);
        L.Info($"สำรองโฟลเดอร์เดิม {dir} → {dest}");
    }

    protected void MarkInstalled(string version, string url)
    {
        var info = new InstalledInfo
        {
            Version = version,
            InstalledAt = DateTime.Now.ToString("s"),
            SourceUrl = url,
        };
        switch (Component)
        {
            case "apache": C.ApacheInstalled = info; break;
            case "php": C.PhpInstalled = info; break;
            case "mariadb": C.MariaDbInstalled = info; break;
        }
        C.Save(P);
    }

    protected void CleanStaging(string staging)
    {
        try
        {
            var parent = Path.GetDirectoryName(staging);
            if (parent != null && parent.Contains("staging", StringComparison.OrdinalIgnoreCase))
                Directory.Delete(parent, recursive: true);
        }
        catch { }
    }
}

// =====================================================================
// Apache
// =====================================================================
public sealed class ApacheManager : ComponentManager
{
    public ApacheManager(ServerPaths p, AppConfig c, Logger l, Downloader dl) : base(p, c, l, dl) { }

    public override string Component => "apache";
    public override string DisplayName => "Apache";
    public override bool IsInstalled => File.Exists(P.ApacheBin);

    public override string? DetectInstalledVersion()
    {
        if (!IsInstalled) return null;
        try
        {
            var (code, stdout, _) = ProcessRunner.Run(P.ApacheBin, "-v", null, 20_000);
            if (code != 0) return null;
            var m = Regex.Match(stdout, @"Apache/(\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    /// <summary>ทดสอบ config (httpd -t) — คืนข้อความ error ถ้าไม่ผ่าน หรือ null ถ้าผ่าน</summary>
    public string? TestConfig()
    {
        if (!IsInstalled) return "ยังไม่ได้ติดตั้ง Apache";
        var (code, stdout, stderr) = ProcessRunner.Run(P.ApacheBin,
            $"-t -d \"{P.ApacheDir}\" -f \"{P.ApacheConf}\"", P.ApacheDir, 30_000);
        return code == 0 ? null : (stdout + " " + stderr).Trim();
    }

    public void InstallService(bool autoStart = true)
    {
        var name = C.Services.ApacheName;
        if (ServiceHelper.Exists(name))
        {
            L.Info($"เซอร์วิส {name} มีอยู่แล้ว — ข้ามการติดตั้ง");
            ServiceHelper.SetStartType(name, autoStart ? "auto" : "demand");
            return;
        }
        var (code, stdout, stderr) = ProcessRunner.Run(P.ApacheBin,
            $"-k install -n \"{name}\" -d \"{P.ApacheDir}\" -f \"{P.ApacheConf}\"",
            P.ApacheDir, 60_000);
        if (code != 0)
            throw new InvalidOperationException($"ติดตั้งเซอร์วิส Apache ไม่สำเร็จ:\n{stdout}\n{stderr}");
        if (!autoStart) ServiceHelper.SetStartType(name, "demand");
        L.Info($"ติดตั้งเซอร์วิส {name} เรียบร้อย (start={ (autoStart ? "auto" : "demand") })");
    }

    public void UninstallService()
    {
        var name = C.Services.ApacheName;
        if (!ServiceHelper.Exists(name)) return;
        var (code, stdout, stderr) = ProcessRunner.Run(P.ApacheBin, $"-k uninstall -n \"{name}\"", P.ApacheDir, 60_000);
        if (code != 0)
            throw new InvalidOperationException($"ถอนเซอร์วิส Apache ไม่สำเร็จ:\n{stdout}\n{stderr}");
        L.Info($"ถอนเซอร์วิส {name} เรียบร้อย");
    }

    /// <summary>ก่อนอัพเดท: หยุดเซอร์วิส/โปรเซสที่กำลังใช้ไฟล์อยู่</summary>
    public void EnsureStoppedForUpgrade()
    {
        var name = C.Services.ApacheName;
        if (ServiceHelper.Exists(name))
        {
            try { ServiceHelper.Stop(name); } catch { }
        }
        try
        {
            if (File.Exists(P.ApachePidFile) && int.TryParse(File.ReadAllText(P.ApachePidFile), out var pid))
            {
                ProcessRunner.KillTree(pid);
                File.Delete(P.ApachePidFile);
            }
        }
        catch { }
        // เผื่อ httpd ตัวอื่นค้าง
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("httpd"))
        {
            try
            {
                if (proc.MainModule?.FileName?.StartsWith(P.ApacheDir, StringComparison.OrdinalIgnoreCase) == true)
                {
                    proc.Kill(entireProcessTree: true);
                    L.Warn($"จบโปรเซส httpd ค้าง (PID {proc.Id})");
                }
            }
            catch { }
        }
    }

    public override async Task InstallAsync(CatalogItem item, IProgress<InstallProgress>? progress,
        bool installService, CancellationToken ct = default)
    {
        P.EnsureDirectories();
        var zipPath = Path.Combine(P.DownloadsDir, VersionCatalog.LocalZipName(item));

        Report(progress, 5, "เตรียมพื้นที่", "สร้างโฟลเดอร์ที่จำเป็น");
        var dlProgress = new Progress<DownloadProgress>(dp => Report(progress, 10 + dp.Percent * 40 / 100, "ดาวน์โหลด", $"{dp.Bytes / 1024 / 1024:F1} MB — {dp.SpeedText}"));
        await Dl.DownloadAsync(item.Url, zipPath, dlProgress, ct, item.Sha256).ConfigureAwait(false);

        Report(progress, 52, "แตกไฟล์", "แตก zip ลงโฟลเดอร์ชั่วคราว");
        var staging = await Task.Run(() => ZipExtractor.ExtractToStaging(zipPath, P.StagingDir), ct).ConfigureAwait(false);

        Report(progress, 62, "เตรียมโฟลเดอร์", "หยุดเซอร์วิสเดิม (ถ้ามี) และสำรองโฟลเดอร์เก่า");
        await Task.Run(() =>
        {
            EnsureStoppedForUpgrade();
            BackupExistingFolder(P.ApacheDir, "apache-old");
            // zip ของ ApacheLounge มี Apache24/ + ไฟล์แถม ReadMe/marker → ใช้เฉพาะโฟลเดอร์ Apache24
            var contentRoot = ZipExtractor.FindContentRoot(staging, "Apache24", "Apache2");
            ZipExtractor.MoveContents(contentRoot, P.ApacheDir);
        }, ct).ConfigureAwait(false);
        CleanStaging(staging);

        Report(progress, 78, "เขียน config", "สร้าง httpd.conf และหน้าเว็บตัวอย่าง");
        await Task.Run(() =>
        {
            var cw = new ConfigWriter(P, C, L);
            cw.WriteApacheConf();
            cw.WriteIndexPage();
        }, ct).ConfigureAwait(false);

        Report(progress, 88, "ตรวจสอบ config", "รัน httpd -t เพื่อทดสอบความถูกต้อง");
        var err = await Task.Run(TestConfig, ct).ConfigureAwait(false);
        if (err != null)
            throw new InvalidOperationException("config ของ Apache ไม่ผ่านการทดสอบ (httpd -t):\n" + err);

        if (installService)
        {
            Report(progress, 93, "ติดตั้งเซอร์วิส", "ลงทะเบียน Windows Service: " + C.Services.ApacheName);
            await Task.Run(() => InstallService(C.Services.StartupType == "auto"), ct).ConfigureAwait(false);
        }

        MarkInstalled(item.Version, item.Url);
        Report(progress, 100, "สำเร็จ", $"ติดตั้ง Apache {item.Version} เรียบร้อย");
    }
}

// =====================================================================
// PHP
// =====================================================================
public sealed class PhpManager : ComponentManager
{
    public PhpManager(ServerPaths p, AppConfig c, Logger l, Downloader dl) : base(p, c, l, dl) { }

    public override string Component => "php";
    public override string DisplayName => "PHP";
    public override bool IsInstalled => File.Exists(P.PhpBin);

    public override string? DetectInstalledVersion()
    {
        if (!IsInstalled) return null;
        try
        {
            var (code, stdout, _) = ProcessRunner.Run(P.PhpBin, "-v", null, 20_000);
            if (code != 0) return null;
            var m = Regex.Match(stdout, @"PHP (\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    /// <summary>PHP ต้องเป็น Thread Safe ถึงจะมี php8apache2_4.dll สำหรับเป็นโมดูลของ Apache</summary>
    public bool HasApacheModule => File.Exists(P.PhpModuleDll);

    public override async Task InstallAsync(CatalogItem item, IProgress<InstallProgress>? progress,
        bool installService, CancellationToken ct = default)
    {
        P.EnsureDirectories();
        var zipPath = Path.Combine(P.DownloadsDir, VersionCatalog.LocalZipName(item));

        Report(progress, 5, "เตรียมพื้นที่", "สร้างโฟลเดอร์ที่จำเป็น");
        var dlProgress = new Progress<DownloadProgress>(dp => Report(progress, 10 + dp.Percent * 45 / 100, "ดาวน์โหลด", $"{dp.Bytes / 1024 / 1024:F1} MB — {dp.SpeedText}"));
        await Dl.DownloadAsync(item.Url, zipPath, dlProgress, ct, item.Sha256).ConfigureAwait(false);

        Report(progress, 58, "แตกไฟล์", "แตก zip ลงโฟลเดอร์ชั่วคราว");
        var staging = await Task.Run(() => ZipExtractor.ExtractToStaging(zipPath, P.StagingDir), ct).ConfigureAwait(false);

        Report(progress, 66, "เตรียมโฟลเดอร์", "หยุด Apache ชั่วคราว (ถ้ากำลังใช้ PHP อยู่) และสำรองโฟลเดอร์เก่า");
        await Task.Run(() =>
        {
            // Apache โหลด dll ของ PHP ไว้ ต้องหยุดก่อนถึงเปลี่ยนไฟล์ได้
            try
            {
                var apache = new ApacheManager(P, C, L, Dl);
                apache.EnsureStoppedForUpgrade();
            }
            catch { }
            BackupExistingFolder(P.PhpDir, "php-old");
            ZipExtractor.MoveContents(staging, P.PhpDir);
        }, ct).ConfigureAwait(false);
        CleanStaging(staging);

        Report(progress, 82, "เขียน config", "สร้าง php.ini + เชื่อมกับ Apache (httpd.conf)");
        await Task.Run(() =>
        {
            var cw = new ConfigWriter(P, C, L);
            cw.WritePhpIni();
            cw.WriteApacheConf(); // ใส่ LoadModule php_module ให้อัตโนมัติ
            cw.WriteIndexPage();
            if (!HasApacheModule)
                L.Warn("ไฟล์ php8apache2_4.dll ไม่พบ — PHP ตัวนี้อาจเป็นรุ่น NTS ซึ่งใช้เป็นโมดูลของ Apache ไม่ได้ (ควรใช้รุ่น Thread Safe)");
        }, ct).ConfigureAwait(false);

        var ver = DetectInstalledVersion();
        MarkInstalled(ver ?? item.Version, item.Url);
        Report(progress, 100, "สำเร็จ", $"ติดตั้ง PHP {ver ?? item.Version} เรียบร้อย");
    }
}

// =====================================================================
// MariaDB
// =====================================================================
public sealed class MariaDbManager : ComponentManager
{
    public MariaDbManager(ServerPaths p, AppConfig c, Logger l, Downloader dl) : base(p, c, l, dl) { }

    public override string Component => "mariadb";
    public override string DisplayName => "MariaDB";
    public override bool IsInstalled => File.Exists(P.MariaDbBin);

    public override string? DetectInstalledVersion()
    {
        if (!IsInstalled) return null;
        try
        {
            var (code, stdout, _) = ProcessRunner.Run(P.MariaDbBin, "--version", null, 20_000);
            if (code != 0) return null;
            var m = Regex.Match(stdout, @"(\d+\.\d+\.\d+)-MariaDB");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    public bool IsDataDirInitialized =>
        Directory.Exists(C.MariaDb.DataDir) &&
        (File.Exists(Path.Combine(C.MariaDb.DataDir, "ibdata1")) ||
         Directory.Exists(Path.Combine(C.MariaDb.DataDir, "mysql")));

    public void InitializeDataDir()
    {
        Directory.CreateDirectory(C.MariaDb.DataDir);
        var installDb = File.Exists(P.MariaDbInstallDb)
            ? P.MariaDbInstallDb
            : Path.Combine(P.MariaDbDir, "bin", "mysql-install-db.exe");

        if (!File.Exists(installDb))
            throw new InvalidOperationException("ไม่พบ mariadb-install-db.exe ในโฟลเดอร์ bin");

        var (code, stdout, stderr) = ProcessRunner.Run(installDb,
            $"--datadir=\"{C.MariaDb.DataDir}\"", P.MariaDbDir, 180_000);
        if (code != 0)
            throw new InvalidOperationException($"สร้างโฟลเดอร์ข้อมูล (datadir) ไม่สำเร็จ:\n{stdout}\n{stderr}");
        L.Info("สร้าง datadir เรียบร้อย: " + C.MariaDb.DataDir);
    }

    public void InstallService(bool autoStart = true)
    {
        var name = C.Services.MariaDbName;
        UninstallService(tolerant: true); // ลบของเดิมก่อน (ถ้ามี) เพราะ ImagePath ชี้ไฟล์ใหม่
        var verb = autoStart ? "--install" : "--install-manual";
        var (code, stdout, stderr) = ProcessRunner.Run(P.MariaDbBin,
            $"{verb} {name} --defaults-file=\"{P.MariaDbMyIni}\"", P.MariaDbDir, 60_000);
        if (code != 0)
            throw new InvalidOperationException($"ติดตั้งเซอร์วิส MariaDB ไม่สำเร็จ:\n{stdout}\n{stderr}");
        L.Info($"ติดตั้งเซอร์วิส {name} เรียบร้อย (start={ (autoStart ? "auto" : "demand") })");
    }

    public void UninstallService(bool tolerant = false)
    {
        var name = C.Services.MariaDbName;
        if (!ServiceHelper.Exists(name)) return;
        var (code, stdout, stderr) = ProcessRunner.Run(P.MariaDbBin, $"--remove {name}", P.MariaDbDir, 60_000);
        if (code != 0 && !tolerant)
            throw new InvalidOperationException($"ถอนเซอร์วิส MariaDB ไม่สำเร็จ:\n{stdout}\n{stderr}");
        L.Info($"ถอนเซอร์วิส {name} (ถ้ามี)");
    }

    public void EnsureStoppedForUpgrade()
    {
        var name = C.Services.MariaDbName;
        if (ServiceHelper.Exists(name))
        {
            try { ServiceHelper.Stop(name); } catch { }
            try { UninstallService(tolerant: true); } catch { }
        }
        try
        {
            if (File.Exists(P.MariaDbPidFile) && int.TryParse(File.ReadAllText(P.MariaDbPidFile), out var pid))
            {
                ProcessRunner.KillTree(pid);
                File.Delete(P.MariaDbPidFile);
            }
        }
        catch { }
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("mariadbd"))
        {
            try
            {
                if (proc.MainModule?.FileName?.StartsWith(P.MariaDbDir, StringComparison.OrdinalIgnoreCase) == true)
                {
                    proc.Kill(entireProcessTree: true);
                    L.Warn($"จบโปรเซส mariadbd ค้าง (PID {proc.Id})");
                }
            }
            catch { }
        }
    }

    /// <summary>รัน SQL ด้วย root (ใช้ตอนตั้งรหัสผ่าน/ตรวจสุขภาพ)</summary>
    public (int Code, string Output) RunSql(string sql, string? rootPassword = null)
    {
        if (!File.Exists(P.MariaDbClient))
            return (-1, "ไม่พบ mariadb.exe");
        var args = $"--port={C.MariaDb.Port} --protocol=TCP -u root";
        if (!string.IsNullOrEmpty(rootPassword))
            args += $" --password=\"{rootPassword.Replace("\"", "\\\"")}\"";
        args += $" --execute=\"{sql.Replace("\"", "\\\"")}\"";
        var (code, stdout, stderr) = ProcessRunner.Run(P.MariaDbClient, args, P.MariaDbDir, 30_000);
        return (code, stdout + stderr);
    }

    public void SetRootPassword(string newPassword, string? oldPassword = null)
    {
        var sql = string.IsNullOrEmpty(newPassword)
            ? "ALTER USER 'root'@'localhost' IDENTIFIED BY ''; FLUSH PRIVILEGES;"
            : $"ALTER USER 'root'@'localhost' IDENTIFIED BY '{newPassword.Replace("'", "''")}'; FLUSH PRIVILEGES;";
        var (code, output) = RunSql(sql, oldPassword);
        if (code != 0)
            throw new InvalidOperationException("ตั้งรหัสผ่าน root ไม่สำเร็จ (ต้องเริ่ม MariaDB ก่อน):\n" + output);
        L.Info("ตั้งรหัสผ่าน root เรียบร้อย");
    }

    public override async Task InstallAsync(CatalogItem item, IProgress<InstallProgress>? progress,
        bool installService, CancellationToken ct = default)
    {
        P.EnsureDirectories();
        var zipPath = Path.Combine(P.DownloadsDir, VersionCatalog.LocalZipName(item));

        Report(progress, 5, "เตรียมพื้นที่", "สร้างโฟลเดอร์ที่จำเป็น");
        var dlProgress = new Progress<DownloadProgress>(dp => Report(progress, 8 + dp.Percent * 42 / 100, "ดาวน์โหลด", $"{dp.Bytes / 1024 / 1024:F1} MB — {dp.SpeedText}"));
        await Dl.DownloadAsync(item.Url, zipPath, dlProgress, ct, item.Sha256).ConfigureAwait(false);

        Report(progress, 52, "แตกไฟล์", "แตก zip ลงโฟลเดอร์ชั่วคราว");
        var staging = await Task.Run(() => ZipExtractor.ExtractToStaging(zipPath, P.StagingDir), ct).ConfigureAwait(false);

        Report(progress, 60, "เตรียมโฟลเดอร์", "หยุดเซอร์วิสเดิม (ถ้ามี) และสำรองโฟลเดอร์เก่า — ข้อมูลใน data\\ ไม่ถูกแตะ");
        await Task.Run(() =>
        {
            EnsureStoppedForUpgrade();
            BackupExistingFolder(P.MariaDbDir, "mariadb-old");
            ZipExtractor.MoveContents(staging, P.MariaDbDir);
        }, ct).ConfigureAwait(false);
        CleanStaging(staging);

        Report(progress, 72, "เขียน config", "สร้าง my.ini");
        await Task.Run(() => new ConfigWriter(P, C, L).WriteMariaDbIni(), ct).ConfigureAwait(false);

        if (!IsDataDirInitialized)
        {
            Report(progress, 80, "สร้างโฟลเดอร์ข้อมูล", "รัน mariadb-install-db (ครั้งแรกเท่านั้น)");
            await Task.Run(InitializeDataDir, ct).ConfigureAwait(false);
        }
        else
        {
            Report(progress, 80, "ข้อมูลเดิม", "datadir มีข้อมูลอยู่แล้ว — คงข้อมูลเดิมไว้ (แนะนำรัน mariadb-upgrade หลังเริ่มใช้งาน)");
        }

        if (installService)
        {
            Report(progress, 93, "ติดตั้งเซอร์วิส", "ลงทะเบียน Windows Service: " + C.Services.MariaDbName);
            await Task.Run(() => InstallService(C.Services.StartupType == "auto"), ct).ConfigureAwait(false);
        }

        MarkInstalled(item.Version, item.Url);
        Report(progress, 100, "สำเร็จ", $"ติดตั้ง MariaDB {item.Version} เรียบร้อย");
    }
}
