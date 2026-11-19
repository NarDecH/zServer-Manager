using System.Text.RegularExpressions;

namespace ZServer.Core;

public sealed record LatestVersionInfo(string Component, string? LatestVersion, string? Detail, bool Ok, string Error = "");

/// <summary>
/// ตรวจสอบเวอร์ชันล่าสุดจากเว็บทางการ (best-effort — ถ้าเน็ต/หน้าเว็บเปลี่ยนจะคืน Ok=false
/// และให้ผู้ใช้ใช้รายการใน VersionCatalog แทน)
/// </summary>
public static class UpdateChecker
{
    public static async Task<List<LatestVersionInfo>> CheckAllAsync(HttpClient http, CancellationToken ct = default)
    {
        var results = new List<LatestVersionInfo>
        {
            await CheckPhpAsync(http, ct).ConfigureAwait(false),
            await CheckApacheAsync(http, ct).ConfigureAwait(false),
            await CheckMariaDbAsync(http, ct).ConfigureAwait(false),
        };
        return results;
    }

    // PHP: releases.json ของ windows.php.net (โครงสร้าง: { "8.5": { "version": "8.5.13", "ts-vs17-x64": { "zip": {...} } }, ... })
    public static async Task<LatestVersionInfo> CheckPhpAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var json = await http.GetStringAsync("https://windows.php.net/downloads/releases/releases.json", ct).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string? best = null;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                if (!prop.Value.TryGetProperty("version", out var v)) continue;
                var ver = v.GetString();
                if (string.IsNullOrEmpty(ver)) continue;
                if (best == null || CompareVersions(ver, best) > 0) best = ver;
            }
            return best == null
                ? new LatestVersionInfo("php", null, null, false, "ไม่พบเวอร์ชันใน releases.json")
                : new LatestVersionInfo("php", best, null, true);
        }
        catch (Exception ex)
        {
            return new LatestVersionInfo("php", null, null, false, ex.Message);
        }
    }

    // Apache: parse หน้า download ของ ApacheLounge หาไฟล์ win64-VS18
    public static async Task<LatestVersionInfo> CheckApacheAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var html = await http.GetStringAsync("https://www.apachelounge.com/download/", ct).ConfigureAwait(false);
            var matches = Regex.Matches(html, @"httpd-(\d+\.\d+\.\d+)-\d+-Win64-VS18\.zip", RegexOptions.IgnoreCase);
            string? best = null;
            foreach (Match m in matches)
            {
                var v = m.Groups[1].Value;
                if (best == null || CompareVersions(v, best) > 0) best = v;
            }
            return best == null
                ? new LatestVersionInfo("apache", null, null, false, "หาเวอร์ชันในหน้า download ไม่เจอ (หน้าเว็บอาจเปลี่ยนรูปแบบ)")
                : new LatestVersionInfo("apache", best, null, true);
        }
        catch (Exception ex)
        {
            return new LatestVersionInfo("apache", null, null, false, ex.Message);
        }
    }

    // MariaDB: parse รายการเวอร์ชันจาก archive.mariadb.org แล้วเลือกเวอร์ชันสูงสุดของแต่ละสายที่ยังดูแล (10.11 / 11.4 / 11.8+)
    public static async Task<LatestVersionInfo> CheckMariaDbAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var html = await http.GetStringAsync("https://archive.mariadb.org/", ct).ConfigureAwait(false);
            var versions = new List<Version>();
            foreach (Match m in Regex.Matches(html, @"href=""mariadb-(\d+\.\d+\.\d+)/""", RegexOptions.IgnoreCase))
            {
                if (Version.TryParse(m.Groups[1].Value, out var v)) versions.Add(v);
            }
            if (versions.Count == 0)
                return new LatestVersionInfo("mariadb", null, null, false, "หาเวอร์ชันใน archive ไม่เจอ");

            // จับกลุ่มตามสาย (major.minor) แล้วเอา patch สูงสุดของแต่ละสาย จากนั้นเลือกสายใหม่สุด
            var latestPerLine = versions
                .GroupBy(v => $"{v.Major}.{v.Minor}")
                .Select(g => g.OrderByDescending(v => v.Build).First())
                .OrderByDescending(v => v.Major * 100 + v.Minor)
                .ToList();

            // เลือกสายแรกที่มี zip จริง (ลองเรียงจากใหม่ไปเก่า สูงสุด 3 สาย)
            foreach (var v in latestPerLine.Take(4))
            {
                var ver = $"{v.Major}.{v.Minor}.{v.Build}";
                var url = $"https://archive.mariadb.org/mariadb-{ver}/winx64-packages/mariadb-{ver}-winx64.zip";
                try
                {
                    using var resp = await http.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode && (resp.Content.Headers.ContentLength ?? 0) > 1_000_000)
                        return new LatestVersionInfo("mariadb", ver, null, true);
                }
                catch { }
            }
            return new LatestVersionInfo("mariadb", null, null, false, "ไม่พบไฟล์ zip ที่ใช้ได้ใน archive");
        }
        catch (Exception ex)
        {
            return new LatestVersionInfo("mariadb", null, null, false, ex.Message);
        }
    }

    /// <summary>เทียบเวอร์ชันแบบตัวเลข (1.2.10 > 1.2.9)</summary>
    public static int CompareVersions(string a, string b)
    {
        if (Version.TryParse(NormalizeVersion(a), out var va) && Version.TryParse(NormalizeVersion(b), out var vb))
            return va.CompareTo(vb);
        return string.Compare(a, b, StringComparison.Ordinal);
    }

    private static string NormalizeVersion(string v)
    {
        var parts = v.Split('.');
        if (parts.Length == 2) return v + ".0";
        return v;
    }
}
