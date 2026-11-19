using System.IO.Compression;
using ZServer.Core;

// ============================================================
// SelfTest — ทดสอบ logic ของ ZServer.Core โดยไม่ต้องใช้สิทธิ์ admin
// ใช้:  ZServer.SelfTest [--net] [--keep]
//   --net   ทดสอบต่อเน็ตด้วย (เช็ค URL + ดาวน์โหลด PHP จริง)
//   --keep  ไม่ลบโฟลเดอร์ sandbox ทิ้ง (เอาไว้ตรวจไฟล์ที่สร้าง)
// ============================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;

bool doNet = args.Contains("--net");
bool keep = args.Contains("--keep");

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  [ผ่าน] {name}"); }
    else { fail++; Console.WriteLine($"  [ล้มเหลว] {name} {(detail == "" ? "" : "— " + detail)}"); }
}

void Section(string title) => Console.WriteLine($"\n=== {title} ===");

var sandbox = Path.Combine(Path.GetTempPath(), "zserver-selftest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(sandbox);
Console.WriteLine($"sandbox: {sandbox}");

string ProgressLine(DownloadProgress p) =>
    $"\r    {p.Bytes / 1024.0 / 1024.0:F1} / {(p.Total ?? 0) / 1024.0 / 1024.0:F1} MB ({p.SpeedText})   ";

var paths = new ServerPaths(sandbox);
paths.EnsureDirectories();
var cfg = AppConfig.Load(paths);
var log = new Logger(paths.ManagerLog);
var dl = new Downloader(log);

try
{
    // ============================================================
    Section("1. AppConfig: บันทึก/โหลดรอบสองทิศ");
    cfg.Apache.Port = 80;
    cfg.Php.MemoryLimit = "512M";
    cfg.MariaDb.Port = 3306;
    cfg.Services.Mode = "service";
    cfg.Save(paths);

    var cfg2 = AppConfig.Load(paths);
    Check("โหลดกลับมาค่าตรง (port=80)", cfg2.Apache.Port == 80);
    Check("โหลดกลับมาค่าตรง (memoryLimit=512M)", cfg2.Php.MemoryLimit == "512M");
    Check("โหลดกลับมาค่าตรง (mariadb port=3306)", cfg2.MariaDb.Port == 3306);
    Check("Normalize ใส่ DocumentRoot ให้เอง", cfg2.Apache.DocumentRoot == paths.WwwDir);
    Check("Normalize ใส่ DataDir ให้เอง", cfg2.MariaDb.DataDir == paths.DataDir);

    // ============================================================
    Section("2. ConfigWriter: สร้างไฟล์ config ทั้งสามตัว");
    var cw = new ConfigWriter(paths, cfg2, log);
    cw.WriteApacheConf();
    cw.WriteMariaDbIni();

    var httpdConf = File.ReadAllText(paths.ApacheConf);
    Check("httpd.conf: Listen 80", httpdConf.Contains("Listen 80"), "ไม่พบ Listen 80");
    Check("httpd.conf: ServerRoot ชี้ sandbox", httpdConf.Contains("ServerRoot \"${SRVROOT}\""));
    Check("httpd.conf: DocumentRoot = www", httpdConf.Contains("DocumentRoot \"" + paths.WwwDir.Replace('\\', '/') + "\""));
    Check("httpd.conf: mpm_winnt โหลดแล้ว", httpdConf.Contains("LoadModule mpm_winnt_module"));
    Check("httpd.conf: ยังไม่มี PHP (ยังไม่ติดตั้ง)", !httpdConf.Contains("LoadModule php_module"));
    Check("httpd.conf: Log ชี้ logs/", httpdConf.Contains(paths.ApacheErrorLog.Replace('\\', '/')));
    Check("httpd.conf: LogFormat มี Referer", httpdConf.Contains("%{Referer}i"));

    var myIni = File.ReadAllText(paths.MariaDbMyIni);
    Check("my.ini: datadir ถูกต้อง", myIni.Contains("datadir = \"" + paths.DataDir.Replace('\\', '/') + "\""));
    Check("my.ini: port 3306", myIni.Contains("port = 3306"));
    Check("my.ini: utf8mb4", myIni.Contains("character-set-server = utf8mb4"));

    // php.ini ยังสร้างไม่ได้เพราะไม่มีไฟล์ php.exe — ทดสอบเมื่อดาวน์โหลดจริง (ข้อ 5)
    Check("WriteAll ยังไม่เขียน php.ini (ไม่มี php.exe)", !File.Exists(paths.PhpIni));

    cw.WriteIndexPage();
    Check("www\\index.php ถูกสร้าง", File.Exists(Path.Combine(paths.WwwDir, "index.php")));
    Check("www\\phpinfo.php ถูกสร้าง", File.Exists(Path.Combine(paths.WwwDir, "phpinfo.php")));

    // ============================================================
    Section("3. ZipExtractor: แตกไฟล์ + ป้องกัน Zip Slip");
    // zip แบบมีโฟลเดอร์รากเดียว (เหมือน Apache24/, php-8.5.13-.../ ของจริง)
    var testZip = Path.Combine(paths.DownloadsDir, "test.zip");
    using (var fs = File.Create(testZip))
    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
    {
        var e1 = zip.CreateEntry("rootfolder/file1.txt");
        using (var w = new StreamWriter(e1.Open())) w.Write("hello1");
        var e2 = zip.CreateEntry("rootfolder/sub/file2.txt");
        using (var w = new StreamWriter(e2.Open())) w.Write("hello2");
    }
    var staging = ZipExtractor.ExtractToStaging(testZip, paths.StagingDir);
    Check("ExtractToStaging จับโฟลเดอร์รากเดียว (rootfolder)", Path.GetFileName(staging) == "rootfolder",
        "ได้: " + staging);
    Check("ไฟล์ในโฟลเดอร์รากถูกแตกครบ", File.Exists(Path.Combine(staging, "file1.txt")) &&
                                       File.Exists(Path.Combine(staging, "sub", "file2.txt")));

    // zip แบบไฟล์กระจายที่ราก (ไม่มีโฟลเดอร์รากเดียว) — ต้องคืนโฟลเดอร์แตกตรง ๆ
    var flatZip = Path.Combine(paths.DownloadsDir, "flat.zip");
    using (var fs = File.Create(flatZip))
    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
    {
        var e = zip.CreateEntry("loose.txt");
        using (var w = new StreamWriter(e.Open())) w.Write("loose");
    }
    var staging2 = ZipExtractor.ExtractToStaging(flatZip, paths.StagingDir);
    Check("zip ไฟล์กระจาย: คืนโฟลเดอร์แตกเลย", File.Exists(Path.Combine(staging2, "loose.txt")));

    var evilZip = Path.Combine(paths.DownloadsDir, "evil.zip");
    using (var fs = File.Create(evilZip))
    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
    {
        zip.CreateEntry("../escape.txt");
    }
    bool slipBlocked = false;
    try { ZipExtractor.Extract(evilZip, Path.Combine(paths.DownloadsDir, "evildest")); }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Zip Slip")) { slipBlocked = true; }
    catch (IOException) { slipBlocked = true; } // บาง runtime จะโยน IOException
    Check("Zip Slip ถูกป้องกัน", slipBlocked);
    Check("ไม่มีไฟล์หลุดออกนอก sandbox",
        !File.Exists(Path.Combine(sandbox, "escape.txt")));

    ZipExtractor.MoveContents(staging, Path.Combine(paths.DownloadsDir, "moved"));
    Check("MoveContents ย้ายครบ", File.Exists(Path.Combine(paths.DownloadsDir, "moved", "file1.txt")));

    // ============================================================
    Section("4. PortChecker + VersionCatalog + UpdateChecker (เทียบเวอร์ชัน)");
    var freePort = 45678;
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, freePort);
    listener.Start();
    Check("พอร์ตที่กำลัง bind = ไม่ว่าง", !PortChecker.IsAvailable(freePort));
    listener.Stop();
    Check("พอร์ตหลังปล่อย = ว่าง", PortChecker.IsAvailable(freePort));

    Check("เทียบเวอร์ชัน: 2.4.71 > 2.4.7", UpdateChecker.CompareVersions("2.4.71", "2.4.7") > 0);
    Check("เทียบเวอร์ชัน: 8.5.13 > 8.4.28", UpdateChecker.CompareVersions("8.5.13", "8.4.28") > 0);
    Check("เทียบเวอร์ชัน: 11.4.10 > 11.4.9", UpdateChecker.CompareVersions("11.4.10", "11.4.9") > 0);
    Check("เทียบเวอร์ชัน: 10.11.16 < 11.4.9", UpdateChecker.CompareVersions("10.11.16", "11.4.9") < 0);

    Check("catalog มี Apache 2.4.71", VersionCatalog.Find("apache", "2.4.71") != null);
    Check("catalog มี PHP 8.5.13", VersionCatalog.Find("php", "8.5.13") != null);
    Check("catalog มี MariaDB 11.8.6", VersionCatalog.Find("mariadb", "11.8.6") != null);
    Check("LocalZipName ตัดชื่อไฟล์ถูกต้อง",
        VersionCatalog.LocalZipName(VersionCatalog.Find("php", "8.5.13")!) == "php-8.5.13-Win32-vs17-x64.zip");

    // ============================================================
    Section("5. Logger");
    log.Info("ทดสอบบรรทัดแรก");
    log.Warn("ทดสอบบรรทัดสอง");
    var tail = log.Tail();
    Check("Tail อ่านกลับมาเจอข้อความ", tail.Contains("ทดสอบบรรทัดสอง"));

    // ============================================================
    if (doNet)
    {
        Section("6. เน็ต: ตรวจ URL ทั้งหมดใน catalog (HEAD)");
        foreach (var item in VersionCatalog.All)
        {
            try
            {
                using var resp = await dl.GetAsyncHead(item.Url);
                var size = resp.Content.Headers.ContentLength ?? 0;
                Check($"{item.DisplayName} → HTTP {(int)resp.StatusCode} ({size / 1024 / 1024} MB)",
                    (int)resp.StatusCode == 200 && size > 1_000_000);
            }
            catch (Exception ex)
            {
                Check($"{item.DisplayName} → ล้มเหลว", false, ex.Message);
            }
        }

        Section("7. เน็ต: UpdateChecker ตรวจเวอร์ชันล่าสุดจริง");
        var latest = await UpdateChecker.CheckAllAsync(dl.GetHttpClient());
        foreach (var r in latest)
            Check($"{r.Component}: เวอร์ชันล่าสุด = {r.LatestVersion ?? "(ไม่ทราบ)"}", r.Ok, r.Error);

        Section("8. เน็ต: ดาวน์โหลด PHP จริง + SHA256 + แตกไฟล์ + php -v");
        var phpItem = VersionCatalog.Find("php", "8.3.37")!; // ไฟล์เล็กสุด
        var phpZip = await dl.DownloadAsync(phpItem.Url, Path.Combine(paths.DownloadsDir, phpItem.Url.Split('/').Last()),
            new Progress<DownloadProgress>(p => Console.Write(ProgressLine(p))),
            CancellationToken.None, phpItem.Sha256);
        Console.WriteLine();
        Check("ดาวน์โหลด + SHA256 ผ่าน", File.Exists(phpZip));

        var phpStaging = ZipExtractor.ExtractToStaging(phpZip, paths.StagingDir);
        ZipExtractor.MoveContents(phpStaging, paths.PhpDir);
        CleanDir(paths.StagingDir);
        Check("php.exe มีอยู่จริง", File.Exists(paths.PhpBin));
        Check("php8apache2_4.dll มีอยู่จริง (Thread Safe)", File.Exists(paths.PhpModuleDll));

        var phpMgr = new PhpManager(paths, cfg2, log, dl);
        var phpVer = phpMgr.DetectInstalledVersion();
        Check("php -v ตรวจเวอร์ชันได้ = 8.3.37", phpVer == "8.3.37", "ได้: " + phpVer);

        cw.WritePhpIni();
        var phpIni = File.ReadAllText(paths.PhpIni);
        Check("php.ini มี managed block", phpIni.Contains(ServerPaths.PhpIniBeginMarker));
        Check("php.ini ตั้ง memory_limit = 512M", phpIni.Contains("memory_limit = \"512M\""));
        Check("php.ini extension_dir ถูกต้อง", phpIni.Contains("extension_dir"));
        Check("php.ini เปิด mysqli", phpIni.Contains("extension=mysqli"));
        Check("php.ini timezone Asia/Bangkok", phpIni.Contains("date.timezone = \"Asia/Bangkok\""));

        // เขียนซ้ำต้องตัด block เดิมออกก่อน
        cfg2.Php.MemoryLimit = "1024M";
        cw.WritePhpIni();
        phpIni = File.ReadAllText(paths.PhpIni);
        Check("เขียนซ้ำค่าใหม่ (1024M)", phpIni.Contains("memory_limit = \"1024M\""));
        Check("เขียนซ้ำมี block เดียว",
            phpIni.Split(ServerPaths.PhpIniBeginMarker).Length - 1 == 1, "มีหลาย block");

        // apache conf ต้องเพิ่ม LoadModule php อัตโนมัติแล้ว
        cw.WriteApacheConf();
        httpdConf = File.ReadAllText(paths.ApacheConf);
        Check("httpd.conf เพิ่ม PHP module อัตโนมัติ", httpdConf.Contains("LoadModule php_module"));
        Check("httpd.conf มี PHPIniDir", httpdConf.Contains("PHPIniDir"));

        // ============================================================
        Section("9. สรุปประกอบ: mariadb-install-db ครบไฟล์? (โหลด MariaDB 11.4.10 จริง)");
        var dbItem = VersionCatalog.Find("mariadb", "11.4.10")!;
        var dbZip = await dl.DownloadAsync(dbItem.Url, Path.Combine(paths.DownloadsDir, VersionCatalog.LocalZipName(dbItem)),
            new Progress<DownloadProgress>(p => Console.Write(ProgressLine(p))),
            CancellationToken.None, dbItem.Sha256);
        Console.WriteLine();
        var dbStaging = ZipExtractor.ExtractToStaging(dbZip, paths.StagingDir);
        ZipExtractor.MoveContents(dbStaging, paths.MariaDbDir);
        CleanDir(paths.StagingDir);
        Check("mariadbd.exe มีจริง", File.Exists(paths.MariaDbBin));
        Check("mariadb-install-db.exe มีจริง", File.Exists(paths.MariaDbInstallDb));
        var dbMgr = new MariaDbManager(paths, cfg2, log, dl);
        Check("MariaDB detect เวอร์ชันได้ = 11.4.10", dbMgr.DetectInstalledVersion() == "11.4.10", "ได้: " + dbMgr.DetectInstalledVersion());

        // สร้าง datadir จริง (ไม่ต้องใช้สิทธิ์พิเศษ)
        dbMgr.InitializeDataDir();
        Check("datadir ถูกสร้างจริง", dbMgr.IsDataDirInitialized);

        cw.WriteMariaDbIni();
        Check("my.ini ชี้ sandbox datadir", File.ReadAllText(paths.MariaDbMyIni).Contains(paths.DataDir.Replace('\\', '/')));

        // apache install จริงใน sandbox (ไฟล์เล็กสุด? apache 14MB) + httpd -t
        Section("10. Apache 2.4.66: แตกไฟล์ + httpd -t ตรวจ config จริง");
        var apItem = VersionCatalog.Find("apache", "2.4.66")!;
        var apZip = await dl.DownloadAsync(apItem.Url, Path.Combine(paths.DownloadsDir, VersionCatalog.LocalZipName(apItem)),
            new Progress<DownloadProgress>(p => Console.Write(ProgressLine(p))),
            CancellationToken.None, apItem.Sha256);
        Console.WriteLine();
        var apStaging = ZipExtractor.ExtractToStaging(apZip, paths.StagingDir);
        // zip ของ ApacheLounge มี Apache24/ + ไฟล์แถม → ใช้เฉพาะโฟลเดอร์ Apache24 (เหมือนที่ manager ทำ)
        var apContent = ZipExtractor.FindContentRoot(apStaging, "Apache24", "Apache2");
        Check("จับโฟลเดอร์ Apache24 จาก zip แถมไฟล์ได้", Path.GetFileName(apContent) == "Apache24", "ได้: " + apContent);
        ZipExtractor.MoveContents(apContent, paths.ApacheDir);
        CleanDir(paths.StagingDir);
        Check("httpd.exe มีจริง", File.Exists(paths.ApacheBin));
        var apMgr = new ApacheManager(paths, cfg2, log, dl);
        Check("Apache detect เวอร์ชันได้ = 2.4.66", apMgr.DetectInstalledVersion() == "2.4.66", "ได้: " + apMgr.DetectInstalledVersion());
        var confErr = apMgr.TestConfig();
        Check("httpd -t ผ่าน (config ถูกต้อง + php module โหลดได้)", confErr == null, confErr ?? "");
    }

    Console.WriteLine($"\n==============================");
    Console.WriteLine($"ผลรวม: ผ่าน {pass} | ล้มเหลว {fail}");
    Console.WriteLine(fail == 0 ? "✓ ทั้งหมดผ่านหมด" : "✗ มีรายการล้มเหลว");
    Environment.ExitCode = fail == 0 ? 0 : 1;
}
catch (Exception ex)
{
    Console.WriteLine($"\n[ข้อผิดพลาดไม่คาดคิด] {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.ExitCode = 2;
}
finally
{
    dl.Dispose();
    if (!keep)
    {
        try { Directory.Delete(sandbox, recursive: true); Console.WriteLine("ลบ sandbox แล้ว"); }
        catch { Console.WriteLine("ลบ sandbox ไม่สำเร็จ (อาจมีไฟล์ล็อค): " + sandbox); }
    }
    else Console.WriteLine("คง sandbox ไว้ที่: " + sandbox);
}

static void CleanDir(string dir)
{
    try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); Directory.CreateDirectory(dir); }
    catch { }
}
