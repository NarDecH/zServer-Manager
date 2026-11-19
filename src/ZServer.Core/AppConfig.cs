using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZServer.Core;

public sealed class GeneralSettings
{
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoStartServices { get; set; } = false;
    /// <summary>เปิดโปรแกรมอัตโนมัติเมื่อเข้าสู่ระบบ Windows (ผ่าน Task Scheduler)</summary>
    public bool StartWithWindows { get; set; } = false;
    /// <summary>ใช้ธีมมืด (ค่าเริ่มต้น)</summary>
    public bool DarkTheme { get; set; } = true;
    /// <summary>ถ้าเซอร์วิสล้มเอง ให้เริ่มใหม่อัตโนมัติ + แจ้งเตือน</summary>
    public bool WatchdogAutoRestart { get; set; } = false;
    /// <summary>เก็บไฟล์ log ไว้กี่วันก่อนจัดเก็บ/ลบ (0 = ไม่จำกัด)</summary>
    public int LogRetentionDays { get; set; } = 14;
    /// <summary>เวลาที่ตรวจเวอร์ชันใหม่อัตโนมัติครั้งล่าสุด (ใช้กันตรวจถี่เกิน)</summary>
    public DateTime? LastUpdateCheck { get; set; }
}

public sealed class ApacheSettings
{
    public int Port { get; set; } = 80;
    public string ServerName { get; set; } = "localhost";
    public string ServerAdmin { get; set; } = "admin@localhost";
    public string DocumentRoot { get; set; } = "";
    public bool KeepAlive { get; set; } = true;
    public int KeepAliveTimeout { get; set; } = 5;
    public int ThreadsPerChild { get; set; } = 250;
    public bool EnableRewrite { get; set; } = true;
    public bool EnableDeflate { get; set; } = true;
    public bool EnableExpires { get; set; } = true;
    public bool EnableAutoIndex { get; set; } = true;
    /// <summary>เปิด HTTPS (ต้องมีใบรับรองจาก mkcert ก่อน)</summary>
    public bool EnableHttps { get; set; } = false;
    public int HttpsPort { get; set; } = 443;
    /// <summary>รายการเว็บเพิ่มเติม (Virtual Hosts)</summary>
    public List<VhostInfo> VirtualHosts { get; set; } = new();
}

public sealed class VhostInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class PhpSettings
{
    public string MemoryLimit { get; set; } = "256M";
    public string UploadMaxFilesize { get; set; } = "64M";
    public string PostMaxSize { get; set; } = "64M";
    public int MaxExecutionTime { get; set; } = 120;
    public int MaxInputTime { get; set; } = 60;
    public int MaxInputVars { get; set; } = 3000;
    public bool DisplayErrors { get; set; } = false;
    public bool LogErrors { get; set; } = true;
    public string Timezone { get; set; } = "Asia/Bangkok";
    public bool OpCache { get; set; } = true;
    /// <summary>เปิด Xdebug (ต้องมี php_xdebug.dll ใน ext\ ด้วยจึงจะมีผล)</summary>
    public bool XdebugEnabled { get; set; } = false;
    public int XdebugPort { get; set; } = 9003;
    /// <summary>ชื่อ extension (ไม่รวมนำหน้า php_) → เปิด/ปิด</summary>
    public Dictionary<string, bool> Extensions { get; set; } = new()
    {
        ["mysqli"] = true,
        ["pdo_mysql"] = true,
        ["mbstring"] = true,
        ["curl"] = true,
        ["gd"] = true,
        ["openssl"] = true,
        ["zip"] = true,
        ["intl"] = true,
        ["fileinfo"] = true,
        ["exif"] = false,
        ["soap"] = false,
        ["xsl"] = false,
    };
}

public sealed class MariaDbSettings
{
    public int Port { get; set; } = 3306;
    public string DataDir { get; set; } = "";
    /// <summary>ที่อยู่ที่อนุญาตให้ฟัง — 127.0.0.1 = เฉพาะเครื่องนี้, ว่าง = ทุก interface (เสี่ยง)</summary>
    public string BindAddress { get; set; } = "127.0.0.1";
    /// <summary>รหัสผ่าน root เข้ารหัสด้วย DPAPI (ใช้กับงานอัตโนมัติ เช่น backup — ถอดได้เฉพาะเครื่องนี้)</summary>
    public string RootPasswordEnc { get; set; } = "";
    /// <summary>สำรองฐานข้อมูลอัตโนมัติทุกวัน</summary>
    public bool AutoBackupEnabled { get; set; } = false;
    /// <summary>เวลาทำสำรอง (รูปแบบ HH:mm)</summary>
    public string AutoBackupTime { get; set; } = "03:00";
    /// <summary>เก็บไฟล์สำรองไว้กี่วัน (0 = ไม่จำกัด)</summary>
    public int AutoBackupRetentionDays { get; set; } = 7;
    /// <summary>สำรองสำเร็จล่าสุดเมื่อไร</summary>
    public DateTime? LastDbBackupDate { get; set; }
    public string CharacterSet { get; set; } = "utf8mb4";
    public string Collation { get; set; } = "utf8mb4_unicode_ci";
    public string InnoDBBufferPoolSize { get; set; } = "256M";
    public string KeyBufferSize { get; set; } = "64M";
    public int MaxConnections { get; set; } = 100;
    public int LowerCaseTableNames { get; set; } = 1;
}

public sealed class ServiceSettings
{
    /// <summary>"service" หรือ "process"</summary>
    public string Mode { get; set; } = "service";
    public string ApacheName { get; set; } = "ZServerApache";
    public string MariaDbName { get; set; } = "ZServerMariaDB";
    /// <summary>"auto" หรือ "demand" (manual)</summary>
    public string StartupType { get; set; } = "auto";
}

public sealed class InstalledInfo
{
    public string Version { get; set; } = "";
    public string InstalledAt { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}

public sealed class AppConfig
{
    public GeneralSettings General { get; set; } = new();
    public ApacheSettings Apache { get; set; } = new();
    public PhpSettings Php { get; set; } = new();
    public MariaDbSettings MariaDb { get; set; } = new();
    public ServiceSettings Services { get; set; } = new();
    public InstalledInfo? ApacheInstalled { get; set; }
    public InstalledInfo? PhpInstalled { get; set; }
    public InstalledInfo? MariaDbInstalled { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>URL ของ catalog.json เสริม (รูปแบบเดียวกับ CatalogItem) — เว้นว่าง = ปิด</summary>
    public string RemoteCatalogUrl { get; set; } = "";
    /// <summary>URL ไฟล์ JSON แจ้งเวอร์ชันโปรแกรมใหม่ {version, url} — เว้นว่าง = ปิด</summary>
    public string ProgramUpdateUrl { get; set; } = "";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>โหลด config ถ้ายังไม่มีไฟล์จะสร้างค่าเริ่มต้นและบันทึกให้</summary>
    public static AppConfig Load(ServerPaths paths)
    {
        try
        {
            if (File.Exists(paths.ConfigFile))
            {
                var json = File.ReadAllText(paths.ConfigFile);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null)
                {
                    cfg.Normalize(paths);
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("โหลด zserver.json ไม่สำเร็จ ใช้ค่าเริ่มต้นแทน: " + ex.Message);
        }

        var def = new AppConfig();
        def.Normalize(paths);
        def.Save(paths);
        return def;
    }

    /// <summary>ปรับค่า path ให้เป็นค่าปกติถ้ายังว่างอยู่</summary>
    public void Normalize(ServerPaths p)
    {
        if (string.IsNullOrWhiteSpace(Apache.DocumentRoot))
            Apache.DocumentRoot = p.WwwDir;
        if (string.IsNullOrWhiteSpace(MariaDb.DataDir))
            MariaDb.DataDir = p.DataDir;
    }

    public void Save(ServerPaths paths)
    {
        UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(paths.Root);
        var json = JsonSerializer.Serialize(this, JsonOpts)
            .Replace("\r\n", "\n").Replace("\n", "\r\n"); // CRLF สำหรับ Windows
        File.WriteAllText(paths.ConfigFile, json, new UTF8Encoding(false));
    }

    /// <summary>เวอร์ชันที่ติดตั้งอยู่ของคอมโพเนนต์ (apache / php / mariadb) หรือ null</summary>
    public string? InstalledVersion(string component) => component switch
    {
        "apache" => ApacheInstalled?.Version,
        "php" => PhpInstalled?.Version,
        "mariadb" => MariaDbInstalled?.Version,
        _ => null,
    };
}
