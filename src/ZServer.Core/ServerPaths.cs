namespace ZServer.Core;

/// <summary>
/// รวมตำแหน่งไฟล์และโฟลเดอร์ทั้งหมดที่โปรแกรมใช้ อ้างอิงจากโฟลเดอร์ราก (root) ของ zServer
/// </summary>
public sealed class ServerPaths
{
    public string Root { get; }

    public ServerPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    // ===== โครงสร้างหลัก =====
    public string ConfigFile => Path.Combine(Root, "zserver.json");
    public string ApacheDir => Path.Combine(Root, "apache");
    public string PhpDir => Path.Combine(Root, "php");
    public string MariaDbDir => Path.Combine(Root, "mariadb");
    public string WwwDir => Path.Combine(Root, "www");
    public string DataDir => Path.Combine(Root, "data");
    public string LogsDir => Path.Combine(Root, "logs");
    public string DownloadsDir => Path.Combine(Root, "downloads");
    public string BackupsDir => Path.Combine(Root, "backups");
    public string StagingDir => Path.Combine(DownloadsDir, "staging");

    // ===== ไฟล์ปฏิบัติการ =====
    public string ApacheBin => Path.Combine(ApacheDir, "bin", "httpd.exe");
    public string PhpBin => Path.Combine(PhpDir, "php.exe");
    public string PhpModuleDll => Path.Combine(PhpDir, "php8apache2_4.dll");
    public string MariaDbBin => Path.Combine(MariaDbDir, "bin", "mariadbd.exe");
    public string MariaDbInstallDb => Path.Combine(MariaDbDir, "bin", "mariadb-install-db.exe");
    public string MariaDbClient => Path.Combine(MariaDbDir, "bin", "mariadb.exe");
    public string MariaDbAdmin => Path.Combine(MariaDbDir, "bin", "mariadb-admin.exe");

    // ===== ไฟล์ config =====
    public string ApacheConf => Path.Combine(ApacheDir, "conf", "httpd.conf");
    public string PhpIni => Path.Combine(PhpDir, "php.ini");
    public string MariaDbMyIni => Path.Combine(MariaDbDir, "my.ini");

    // ===== ไฟล์ log =====
    public string ManagerLog => Path.Combine(LogsDir, "zserver-manager.log");
    public string ApacheErrorLog => Path.Combine(LogsDir, "apache-error.log");
    public string ApacheAccessLog => Path.Combine(LogsDir, "apache-access.log");
    public string PhpErrorLog => Path.Combine(LogsDir, "php-error.log");
    public string MariaDbErrorLog => Path.Combine(LogsDir, "mariadb-error.log");

    // ===== ไฟล์ PID สำหรับโหมด process =====
    public string ApachePidFile => Path.Combine(LogsDir, "apache.pid");
    public string MariaDbPidFile => Path.Combine(LogsDir, "mariadb.pid");

    public const string PhpIniBeginMarker = "; ===== zServer managed settings — BEGIN =====";
    public const string PhpIniEndMarker = "; ===== zServer managed settings — END =====";

    /// <summary>ค้นหาโฟลเดอร์รากของ zServer โดยอัตโนมัติ</summary>
    public static ServerPaths Discover()
    {
        var candidates = new List<string>();
        var env = Environment.GetEnvironmentVariable("ZSERVER_HOME");
        if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env);

        string exeDir = AppContext.BaseDirectory;
        candidates.Add(exeDir);
        var parent = Path.GetDirectoryName(exeDir.TrimEnd(Path.DirectorySeparatorChar));
        if (parent != null)
        {
            candidates.Add(parent);
            var grand = Path.GetDirectoryName(parent.TrimEnd(Path.DirectorySeparatorChar));
            if (grand != null) candidates.Add(grand);
        }

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(Path.Combine(c, "zserver.json")))
                    return new ServerPaths(c);
            }
            catch { /* ข้าม path ที่ใช้ไม่ได้ */ }
        }

        return new ServerPaths(env ?? @"C:\zServer");
    }

    /// <summary>สร้างโฟลเดอร์ที่จำเป็นทั้งหมด</summary>
    public void EnsureDirectories()
    {
        foreach (var dir in new[] { Root, ApacheDir, PhpDir, MariaDbDir, WwwDir, DataDir, LogsDir, DownloadsDir, StagingDir, BackupsDir, Path.Combine(ApacheDir, "logs") })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
