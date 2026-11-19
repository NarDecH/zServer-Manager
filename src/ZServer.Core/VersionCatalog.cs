namespace ZServer.Core;

/// <summary>รายการคอมโพเนนต์ 1 ตัวใน catalog สำหรับดาวน์โหลด</summary>
public sealed record CatalogItem(
    string Component,
    string Version,
    string Url,
    string? Sha256,
    long? SizeBytes,
    string? Notes = null)
{
    public string DisplayName => Component switch
    {
        "apache" => $"Apache {Version}",
        "php" => $"PHP {Version}",
        "mariadb" => $"MariaDB {Version}",
        _ => $"{Component} {Version}",
    };
}

/// <summary>
/// แคตตาล็อกเวอร์ชันที่โปรแกรมรู้จัก (URL จากเว็บทางการเท่านั้น)
/// URL และ SHA256 ตรวจสอบจริงแล้วเมื่อวันที่ 2026-11-19
/// </summary>
public static class VersionCatalog
{
    public const string VcRedistVs18Url = "https://aka.ms/vs/18/release/vc_redist.x64.exe";
    public const string VcRedistVs17Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

    public static List<CatalogItem> Apache =>
    [
        new("apache", "2.4.71",
            "https://www.apachelounge.com/download/VS18/binaries/httpd-2.4.71-261112-Win64-VS18.zip",
            "44961A929DFF16B767B3ADBCEA55760A942A00239B2891DC55806BE34029E1FC",
            14_518_924,
            "build ล่าสุด (VS18) — ต้องใช้ VC++ Redistributable เวอร์ชันใหม่ โปรแกรมช่วยติดตั้งให้ได้"),
        new("apache", "2.4.66",
            "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.66-251206-Win64-VS17.zip",
            null, null,
            "รุ่น VS17 — ใช้ VC++ Redistributable 2015-2022"),
    ];

    public static List<CatalogItem> Php =>
    [
        new("php", "8.6.0",
            "https://windows.php.net/downloads/releases/php-8.6.0-Win32-vs18-x64.zip",
            "DAF3639ADDC12197BFEA7AFF8797EDC78939F5D8809E0CCED49D899347C337B0",
            39_726_247, "สายใหม่ล่าสุด (VS18)"),
        new("php", "8.5.13",
            "https://windows.php.net/downloads/releases/php-8.5.13-Win32-vs17-x64.zip",
            "792FE1553895EE98F9BC3C9D50C539910F7D0066676A6523207CA91429D4D475",
            36_543_488, "Thread Safe x64 (เป็นโมดูลของ Apache ได้)"),
        new("php", "8.4.28",
            "https://windows.php.net/downloads/releases/php-8.4.28-Win32-vs17-x64.zip",
            "59CCAA095D0F0C3FBB9F7E2BFD86713EBAC032D5783CC22BC8934E2285629999",
            35_553_000, "Thread Safe x64"),
        new("php", "8.3.37",
            "https://windows.php.net/downloads/releases/php-8.3.37-Win32-vs16-x64.zip",
            "07984BDCFD4391888954C2E73B1CF2C0D403CFA5FD8CB32AD60DDCD854C6B7DE",
            34_344_000, "Thread Safe x64 (สายรองรับยาว 8.3)"),
    ];

    public static List<CatalogItem> MariaDb =>
    [
        new("mariadb", "11.8.6",
            "https://archive.mariadb.org/mariadb-11.8.6/winx64-packages/mariadb-11.8.6-winx64.zip",
            null, 96_628_849, "สายล่าสุด (rolling)"),
        new("mariadb", "11.4.10",
            "https://archive.mariadb.org/mariadb-11.4.10/winx64-packages/mariadb-11.4.10-winx64.zip",
            null, 94_970_049, "สาย LTS รองรับยาว"),
        new("mariadb", "10.11.16",
            "https://archive.mariadb.org/mariadb-10.11.16/winx64-packages/mariadb-10.11.16-winx64.zip",
            null, 93_370_590, "สายเสถียรเดิม (คล้าย XAMPP)"),
    ];

    public static List<CatalogItem> All => Apache.Concat(Php).Concat(MariaDb).ToList();

    public static CatalogItem? Find(string component, string version) =>
        All.FirstOrDefault(i => i.Component == component && i.Version == version);

    /// <summary>ไฟล์ zip ที่จะถูกเก็บใน downloads\</summary>
    public static string LocalZipName(CatalogItem item) =>
        item.Url.Split('/').Last().Split('?').First();

    /// <summary>URL ของตัวติดตั้ง VC++ Redistributable ที่เหมาะกับ build ปัจจุบัน</summary>
    public static string VcRedistUrl => VcRedistVs18Url;
}
