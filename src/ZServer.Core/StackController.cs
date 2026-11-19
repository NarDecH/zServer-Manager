namespace ZServer.Core;

public enum ComponentState
{
    NotInstalled,
    Stopped,
    Running,
    PortConflict,
    Error,
}

public sealed record ComponentStatus(
    string Component,
    ComponentState State,
    string? Version,
    int Port,
    string Detail)
{
    public string StateText => State switch
    {
        ComponentState.Running => "กำลังทำงาน",
        ComponentState.Stopped => "หยุดอยู่",
        ComponentState.NotInstalled => "ยังไม่ได้ติดตั้ง",
        ComponentState.PortConflict => "พอร์ตถูกใช้โดยโปรแกรมอื่น",
        _ => "มีปัญหา",
    };
}

/// <summary>
/// ควบคุมเริ่ม/หยุด/สถานะของ Apache และ MariaDB ทั้งโหมด Windows Service และโหมด Process
/// (PHP ไม่มีเซอร์วิสของตัวเอง — ทำงานผ่าน Apache เป็นโมดูล)
/// </summary>
public sealed class StackController
{
    private readonly ServerPaths _p;
    private readonly AppConfig _c;
    private readonly Logger _l;

    public StackController(ServerPaths paths, AppConfig config, Logger log)
    {
        _p = paths; _c = config; _l = log;
    }

    private bool ServiceMode => _c.Services.Mode == "service";

    // ================= สถานะ =================
    public ComponentStatus GetApacheStatus()
    {
        var port = _c.Apache.Port;
        if (!File.Exists(_p.ApacheBin))
            return new ComponentStatus("apache", ComponentState.NotInstalled, null, port, "ไม่พบ apache\\bin\\httpd.exe");

        var ver = CachedVersion(() => new VersionDetector(ApacheVersionRegex).From(_p.ApacheBin, "-v"),
            ref _apacheVersionCache, ref _apacheVerCacheAt);

        if (ServiceMode)
        {
            var name = _c.Services.ApacheName;
            if (!ServiceHelper.Exists(name))
                return new ComponentStatus("apache", ComponentState.Stopped, ver, port, $"ยังไม่ได้ติดตั้งเซอร์วิส {name}");
            try
            {
                var st = ServiceHelper.GetStatus(name);
                return st == System.ServiceProcess.ServiceControllerStatus.Running
                    ? new ComponentStatus("apache", ComponentState.Running, ver, port, $"เซอร์วิส {name}")
                    : new ComponentStatus("apache", ComponentState.Stopped, ver, port, $"เซอร์วิส {name} ({st})");
            }
            catch (Exception ex)
            {
                return new ComponentStatus("apache", ComponentState.Error, ver, port, ex.Message);
            }
        }
        else
        {
            var pid = ReadPid(_p.ApachePidFile);
            var alive = pid > 0 && ProcessRunner.IsAlive(pid);
            return alive
                ? new ComponentStatus("apache", ComponentState.Running, ver, port, $"โหมด process (PID {pid})")
                : new ComponentStatus("apache", ComponentState.Stopped, ver, port, "โหมด process");
        }
    }

    public ComponentStatus GetMariaDbStatus()
    {
        var port = _c.MariaDb.Port;
        if (!File.Exists(_p.MariaDbBin))
            return new ComponentStatus("mariadb", ComponentState.NotInstalled, null, port, "ไม่พบ mariadb\\bin\\mariadbd.exe");

        var ver = CachedVersion(() => new VersionDetector(MariaDbVersionRegex).From(_p.MariaDbBin, "--version"),
            ref _dbVersionCache, ref _dbVerCacheAt);

        if (ServiceMode)
        {
            var name = _c.Services.MariaDbName;
            if (!ServiceHelper.Exists(name))
                return new ComponentStatus("mariadb", ComponentState.Stopped, ver, port, $"ยังไม่ได้ติดตั้งเซอร์วิส {name}");
            try
            {
                var st = ServiceHelper.GetStatus(name);
                return st == System.ServiceProcess.ServiceControllerStatus.Running
                    ? new ComponentStatus("mariadb", ComponentState.Running, ver, port, $"เซอร์วิส {name}")
                    : new ComponentStatus("mariadb", ComponentState.Stopped, ver, port, $"เซอร์วิส {name} ({st})");
            }
            catch (Exception ex)
            {
                return new ComponentStatus("mariadb", ComponentState.Error, ver, port, ex.Message);
            }
        }
        else
        {
            var pid = ReadPid(_p.MariaDbPidFile);
            var alive = pid > 0 && ProcessRunner.IsAlive(pid);
            return alive
                ? new ComponentStatus("mariadb", ComponentState.Running, ver, port, $"โหมด process (PID {pid})")
                : new ComponentStatus("mariadb", ComponentState.Stopped, ver, port, "โหมด process");
        }
    }

    private string? _phpVersionCache;
    private DateTime _phpCacheAt = DateTime.MinValue;
    private string? _apacheVersionCache;
    private DateTime _apacheVerCacheAt = DateTime.MinValue;
    private string? _dbVersionCache;
    private DateTime _dbVerCacheAt = DateTime.MinValue;

    /// <summary>cache เวอร์ชันไว้ 60 วิ กันเรียกโปรแกรมภายนอกถี่เกิน</summary>
    private string? CachedVersion(Func<string?> detect, ref string? cache, ref DateTime cachedAt)
    {
        if (cache != null && (DateTime.Now - cachedAt).TotalSeconds < 60) return cache;
        var v = TryDetect(detect);
        if (v != null)
        {
            cache = v;
            cachedAt = DateTime.Now;
        }
        return cache ?? v;
    }

    public ComponentStatus GetPhpStatus()
    {
        if (!File.Exists(_p.PhpBin))
            return new ComponentStatus("php", ComponentState.NotInstalled, null, 0, "ไม่พบ php\\php.exe");
        if ((DateTime.Now - _phpCacheAt).TotalSeconds < 60 && _phpVersionCache != null)
            return new ComponentStatus("php", ComponentState.Running, _phpVersionCache, 0,
                File.Exists(_p.PhpModuleDll) ? "โหลดผ่าน Apache (mod_php)" : "⚠ php8apache2_4.dll ไม่พบ (ต้องใช้รุ่น Thread Safe)");
        var ver = CachedVersion(() => new VersionDetector(PhpVersionRegex).From(_p.PhpBin, "-v"),
            ref _phpVersionCache, ref _phpCacheAt);
        _phpVersionCache = ver;
        _phpCacheAt = DateTime.Now;
        return new ComponentStatus("php", ComponentState.Running, ver, 0,
            File.Exists(_p.PhpModuleDll) ? "โหลดผ่าน Apache (mod_php)" : "⚠ php8apache2_4.dll ไม่พบ (ต้องใช้รุ่น Thread Safe)");
    }

    private static string? TryDetect(Func<string?> f)
    {
        try { return f(); } catch { return null; }
    }

    private const string ApacheVersionRegex = @"Apache/(\d+\.\d+\.\d+)";
    private const string PhpVersionRegex = @"PHP (\d+\.\d+\.\d+)";
    private const string MariaDbVersionRegex = @"(\d+\.\d+\.\d+)-MariaDB";

    private sealed class VersionDetector
    {
        private readonly string _regex;
        public VersionDetector(string regex) { _regex = regex; }
        public string? From(string exe, string args)
        {
            if (!File.Exists(exe)) return null;
            var (code, stdout, _) = ProcessRunner.Run(exe, args, null, 20_000);
            if (code != 0) return null;
            var m = RegexMatch(stdout);
            return m.Success ? m.Groups[1].Value : null;
        }
        private System.Text.RegularExpressions.Match RegexMatch(string s)
            => new System.Text.RegularExpressions.Regex(_regex).Match(s);
    }

    private int ReadPid(string pidFile)
    {
        try
        {
            return int.TryParse(File.ReadAllText(pidFile), out var pid) ? pid : 0;
        }
        catch { return 0; }
    }

    // ================= เริ่ม / หยุด =================
    /// <summary>ตรวจว่าพอร์ตที่ต้องใช้ว่างหรือไม่ — คืนรายการข้อความเตือนภาษาไทย</summary>
    public List<string> CheckPortConflicts()
    {
        var warnings = new List<string>();
        if (File.Exists(_p.ApacheBin) && !PortChecker.IsAvailable(_c.Apache.Port))
            warnings.Add("Apache: " + PortChecker.ConflictMessage(_c.Apache.Port, "Apache"));
        if (File.Exists(_p.MariaDbBin) && !PortChecker.IsAvailable(_c.MariaDb.Port))
            warnings.Add("MariaDB: " + PortChecker.ConflictMessage(_c.MariaDb.Port, "MariaDB"));
        return warnings;
    }

    public void StartApache()
    {
        if (!File.Exists(_p.ApacheBin))
            throw new InvalidOperationException("ยังไม่ได้ติดตั้ง Apache — ไปที่หน้า \"ติดตั้ง\" ก่อน");

        if (!PortChecker.IsAvailable(_c.Apache.Port))
            throw new InvalidOperationException(PortChecker.ConflictMessage(_c.Apache.Port, "Apache"));

        _l.Info("เริ่ม Apache...");
        if (ServiceMode)
        {
            var name = _c.Services.ApacheName;
            if (!ServiceHelper.Exists(name))
            {
                _l.Info("ยังไม่มีเซอร์วิส — ติดตั้งให้ก่อน");
                var apache = new ApacheManager(_p, _c, _l, new Downloader(_l));
                apache.InstallService(_c.Services.StartupType == "auto");
            }
            ServiceHelper.Start(name);
        }
        else
        {
            var proc = ProcessRunner.StartDetached(_p.ApacheBin,
                $"-d \"{_p.ApacheDir}\" -f \"{_p.ApacheConf}\"", _p.ApacheDir);
            File.WriteAllText(_p.ApachePidFile, proc.Id.ToString());
        }
        _l.Info("Apache เริ่มแล้ว ✓");
    }

    public void StopApache()
    {
        _l.Info("หยุด Apache...");
        if (ServiceMode)
        {
            var name = _c.Services.ApacheName;
            if (ServiceHelper.Exists(name)) ServiceHelper.Stop(name);
        }
        else
        {
            var pid = ReadPid(_p.ApachePidFile);
            if (pid > 0)
            {
                ProcessRunner.KillTree(pid);
                try { File.Delete(_p.ApachePidFile); } catch { }
            }
        }
        _l.Info("Apache หยุดแล้ว");
    }

    public void RestartApache()
    {
        StopApache();
        Thread.Sleep(800);
        StartApache();
    }

    public void StartMariaDb()
    {
        if (!File.Exists(_p.MariaDbBin))
            throw new InvalidOperationException("ยังไม่ได้ติดตั้ง MariaDB — ไปที่หน้า \"ติดตั้ง\" ก่อน");

        if (!PortChecker.IsAvailable(_c.MariaDb.Port))
            throw new InvalidOperationException(PortChecker.ConflictMessage(_c.MariaDb.Port, "MariaDB"));

        _l.Info("เริ่ม MariaDB...");
        if (ServiceMode)
        {
            var name = _c.Services.MariaDbName;
            if (!ServiceHelper.Exists(name))
            {
                _l.Info("ยังไม่มีเซอร์วิส — ติดตั้งให้ก่อน");
                var db = new MariaDbManager(_p, _c, _l, new Downloader(_l));
                db.InstallService(_c.Services.StartupType == "auto");
            }
            ServiceHelper.Start(name);
        }
        else
        {
            var proc = ProcessRunner.StartDetached(_p.MariaDbBin,
                $"--defaults-file=\"{_p.MariaDbMyIni}\" --console", _p.MariaDbDir);
            File.WriteAllText(_p.MariaDbPidFile, proc.Id.ToString());
        }
        _l.Info("MariaDB เริ่มแล้ว ✓");
    }

    public void StopMariaDb()
    {
        _l.Info("หยุด MariaDB...");
        if (ServiceMode)
        {
            var name = _c.Services.MariaDbName;
            if (ServiceHelper.Exists(name)) ServiceHelper.Stop(name);
        }
        else
        {
            var pid = ReadPid(_p.MariaDbPidFile);
            if (pid > 0)
            {
                ProcessRunner.KillTree(pid);
                try { File.Delete(_p.MariaDbPidFile); } catch { }
            }
        }
        _l.Info("MariaDB หยุดแล้ว");
    }

    public void RestartMariaDb()
    {
        StopMariaDb();
        Thread.Sleep(800);
        StartMariaDb();
    }

    public void StartAll()
    {
        if (File.Exists(_p.MariaDbBin)) StartMariaDb();
        if (File.Exists(_p.ApacheBin)) StartApache();
    }

    public void StopAll()
    {
        if (File.Exists(_p.ApacheBin)) StopApache();
        if (File.Exists(_p.MariaDbBin)) StopMariaDb();
    }

    /// <summary>เปิด URL ใน browser ด้านนอก (ปลอดภัยกับ path)</summary>
    public static void OpenBrowser(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
    }

    /// <summary>URL ของเว็บหลักตาม port ที่ตั้งไว้</summary>
    public string LocalUrl => $"http://localhost:{_c.Apache.Port}";
}
