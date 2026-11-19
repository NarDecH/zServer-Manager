using ZServer.Core;

// ============================================================
// ZServer.Installer — ติดตั้ง Apache / PHP / MariaDB จาก command line
// ใช้โค้ดชุดเดียวกับ GUI (ComponentManager) เหมาะกับ scripting/automation
//
// ใช้งาน (ต้องรันด้วยสิทธิ์ Administrator เพื่อติดตั้ง Windows Service):
//   ZServer.Installer apache php mariadb --start
//     apache|php|mariadb   คอมโพเนนต์ที่จะติดตั้ง (เลือกได้หลายตัว)
//     --version=<ver>      เลือกเวอร์ชันเฉพาะจากแคตตาล็อก เช่น --version=8.4.28 (ใช้ได้ 1 คอมโพเนนต์)
//     --no-service         ไม่ติดตั้งเป็น Windows Service
//     --start              เริ่มเซอร์วิสทันทีหลังติดตั้งเสร็จ
// ============================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;

var paths = ServerPaths.Discover();
paths.EnsureDirectories();
var cfg = AppConfig.Load(paths);
var log = new Logger(paths.ManagerLog);
using var dl = new Downloader(log);
var cw = new ConfigWriter(paths, cfg, log);
var catalog = new CatalogService(log);
try { await catalog.RefreshAsync(dl.GetHttpClient()); } catch { } // ดึงเวอร์ชันใหม่จากเว็บ (ถ้าออนไลน์)

var components = args.Where(a => a is "apache" or "php" or "mariadb").ToList();
bool noService = args.Contains("--no-service");
bool start = args.Contains("--start");
string? versionArg = args.FirstOrDefault(a => a.StartsWith("--version="))?["--version=".Length..];

if (components.Count == 0)
{
    Console.WriteLine("ใช้งาน: ZServer.Installer <apache|php|mariadb>... [--version=<ver>] [--no-service] [--start]");
    Console.WriteLine("ตัวอย่าง: ZServer.Installer apache php mariadb --start");
    Console.WriteLine($"\nโฟลเดอร์หลักที่ตรวจพบ: {paths.Root}");
    Console.WriteLine("เวอร์ชันในแคตตาล็อก:");
    foreach (var item in catalog.All.OrderBy(i => i.Component).ThenByDescending(i => i.Version, StringComparer.Ordinal))
        Console.WriteLine($"  {item.DisplayName,-22} {item.Url}");
    return 0;
}

if (versionArg != null && components.Count > 1)
{
    Console.Error.WriteLine("--version ใช้ได้กับการติดตั้งคอมโพเนนต์เดียวเท่านั้น");
    return 1;
}

Console.WriteLine($"zServer Installer — โฟลเดอร์หลัก: {paths.Root}");
Console.WriteLine($"คอมโพเนนต์: {string.Join(", ", components)} | service={(noService ? "ไม่ติดตั้ง" : "ติดตั้ง")} | start={(start ? "ใช่" : "ไม่")}");
Console.WriteLine(new string('=', 60));

int exitCode = 0;

foreach (var comp in components)
{
    CatalogItem? item;
    if (versionArg != null)
    {
        item = catalog.Find(comp, versionArg);
        if (item == null)
        {
            Console.Error.WriteLine($"ไม่พบ {comp} เวอร์ชัน {versionArg} ในแคตตาล็อก (ดูรายการด้วยการรันไม่ใส่ argument)");
            return 1;
        }
    }
    else
    {
        var options = catalog.For(comp);
        // เวอร์ชันแนะนำ: ล่าสุดของสายที่โปรแกรมรองรับ
        // ข้อยกเว้น: MariaDB เอาสาย LTS 11.4 เป็นค่าแนะนำเพื่อความเสถียร (ถ้ามี)
        item = comp == "mariadb"
            ? options.FirstOrDefault(i => i.Version.StartsWith("11.4")) ?? options.FirstOrDefault()
            : options.FirstOrDefault();
        if (item == null)
        {
            Console.Error.WriteLine($"ไม่พบคอมโพเนนต์ {comp} ในแคตตาล็อก");
            return 1;
        }
    }

    Console.WriteLine($"\n>>> กำลังติดตั้ง {item.DisplayName} ...");
    Console.WriteLine($"    แหล่งดาวน์โหลด: {item.Url}");

    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        ComponentManager mgr = comp switch
        {
            "apache" => new ApacheManager(paths, cfg, log, dl),
            "php" => new PhpManager(paths, cfg, log, dl),
            _ => new MariaDbManager(paths, cfg, log, dl),
        };

        var progress = new Progress<InstallProgress>(p =>
            Console.WriteLine($"    {p.Percent,3}%  {p.Phase}: {p.Message}"));

        await mgr.InstallAsync(item, progress, installService: !noService);
        Console.WriteLine($"    ✓ {item.DisplayName} เสร็จใน {sw.Elapsed.TotalSeconds:F0} วินาที");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"    ✗ ติดตั้ง {item.DisplayName} ไม่สำเร็จ: {ex.Message}");
        log.Error($"[installer] {item.DisplayName} ติดตั้งไม่สำเร็จ: {ex.Message}");
        exitCode = 1;
        continue;
    }
}

// ---- รายงานผลหลังติดตั้ง ----
Console.WriteLine("\n" + new string('=', 60));
var stack = new StackController(paths, cfg, log);
foreach (var st in new[] { stack.GetApacheStatus(), stack.GetPhpStatus(), stack.GetMariaDbStatus() })
    Console.WriteLine($"  {st.Component,-8}: {st.StateText} {st.Version} {st.Detail}");

if (start && exitCode == 0)
{
    Console.WriteLine("\n>>> เริ่มเซอร์วิสทั้งหมด...");
    try
    {
        stack.StartAll();
        Console.WriteLine($"    ✓ เริ่มแล้ว — เว็บอยู่ที่ {stack.LocalUrl}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("    ✗ เริ่มเซอร์วิสไม่สำเร็จ: " + ex.Message);
        exitCode = 1;
    }
}

Console.WriteLine($"\nผลรวม: {(exitCode == 0 ? "สำเร็จทั้งหมด ✓" : "มีข้อผิดพลาด ✗ (ดู log: " + paths.ManagerLog + ")")}");
return exitCode;
