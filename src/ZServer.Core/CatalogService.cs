using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// รวมแคตตาล็อกที่ pin ไว้ในโปรแกรม (VersionCatalog) กับเวอร์ชันใหม่ล่าสุดที่ดึงสดจากเว็บทางการ
/// — ทำให้หน้าติดตั้งมีเวอร์ชันใหม่ให้เลือกเสมอ แม้ตัวโปรแกรมยังไม่ได้อัพเดตแคตตาล็อก
/// รายการจากเว็บ: PHP ได้ SHA256 จาก releases.json ด้วย / Apache-MariaDB ตรวจ URL ตอนดาวน์โหลด
/// </summary>
public sealed class CatalogService
{
    private List<CatalogItem> _merged = VersionCatalog.All.ToList();
    private DateTime _fetchedAt = DateTime.MinValue;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly Logger _log;

    public CatalogService(Logger log) { _log = log; }

    public IReadOnlyList<CatalogItem> All => _merged;

    public List<CatalogItem> For(string component) =>
        _merged.Where(i => i.Component == component)
               .OrderByDescending(i => i.Version, new VersionComparer())
               .ToList();

    public CatalogItem? Find(string component, string version) =>
        _merged.FirstOrDefault(i => i.Component == component && i.Version == version);

    /// <summary>ดึงเวอร์ชันล่าสุดจากเว็บทางการและรวมเข้าแคตตาล็อก (cache 10 นาที) — คืน false ถ้าเน็ตมีปัญหา</summary>
    /// <param name="remoteCatalogUrl">URL catalog.json เสริม (ถ้าผู้ใช้ตั้งไว้ใน zserver.json)</param>
    public async Task<bool> RefreshAsync(HttpClient http, string? remoteCatalogUrl = null, CancellationToken ct = default)
    {
        if ((DateTime.Now - _fetchedAt).TotalMinutes < 10) return true;
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if ((DateTime.Now - _fetchedAt).TotalMinutes < 10) return true;

            var dynamic = new List<CatalogItem>();
            dynamic.AddRange(await FetchPhpAsync(http, ct).ConfigureAwait(false));
            dynamic.AddRange(await FetchApacheAsync(http, ct).ConfigureAwait(false));
            dynamic.AddRange(await FetchMariaDbAsync(http, ct).ConfigureAwait(false));

            // catalog เสริมจาก URL ที่ผู้ใช้กำหนด (ถ้ามี) — รูปแบบเดียวกับ CatalogItem
            if (!string.IsNullOrWhiteSpace(remoteCatalogUrl))
            {
                try
                {
                    var json = await http.GetStringAsync(remoteCatalogUrl, ct).ConfigureAwait(false);
                    var remote = JsonSerializer.Deserialize<List<CatalogItem>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (remote != null)
                        dynamic.AddRange(remote.Where(i => !string.IsNullOrWhiteSpace(i.Url)
                            && !string.IsNullOrWhiteSpace(i.Version)
                            && !string.IsNullOrWhiteSpace(i.Component)));
                }
                catch (Exception ex)
                {
                    _log.Warn("โหลด remote catalog ไม่สำเร็จ (ข้ามไป): " + ex.Message);
                }
            }

            if (dynamic.Count == 0)
            {
                _log.Warn("ดึงรายการเวอร์ชันจากเว็บไม่ได้เลย — ใช้แคตตาล็อกที่ pin ไว้ในโปรแกรม");
                return false;
            }

            // รวม: รายการ pin ในโปรแกรมชนะเมื่อเวอร์ชันซ้ำกัน (เพราะมี SHA256 ที่ตรวจแล้ว)
            var byKey = _merged.ToDictionary(i => (i.Component, i.Version), i => i);
            foreach (var item in dynamic)
            {
                var key = (item.Component, item.Version);
                if (!byKey.ContainsKey(key))
                    byKey[key] = item with { Notes = "ดึงจากเว็บอัตโนมัติ — " + (item.Notes ?? "ตรวจสอบแล้วตอนดาวน์โหลด") };
            }
            _merged = byKey.Values.ToList();
            _fetchedAt = DateTime.Now;
            _log.Info($"รวมรายการเวอร์ชันจากเว็บแล้ว — ทั้งหมด {_merged.Count} รายการ (PHP {For("php").Count}, Apache {For("apache").Count}, MariaDB {For("mariadb").Count})");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warn("ดึงรายการเวอร์ชันจากเว็บไม่สำเร็จ ใช้แคตตาล็อกที่ pin ไว้ในโปรแกรมแทน: " + ex.Message);
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    // ================= PHP: releases.json =================
    private static async Task<List<CatalogItem>> FetchPhpAsync(HttpClient http, CancellationToken ct)
    {
        var list = new List<CatalogItem>();
        try
        {
            var json = await http.GetStringAsync("https://windows.php.net/downloads/releases/releases.json", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var entries = new List<(string Ver, JsonElement Build)>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                if (!prop.Value.TryGetProperty("version", out var v) || v.GetString() is not string ver) continue;

                foreach (var buildKey in prop.Value.EnumerateObject())
                {
                    if (buildKey.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!buildKey.Name.StartsWith("ts-", StringComparison.OrdinalIgnoreCase)) continue; // เอาเฉพาะ Thread Safe
                    if (!buildKey.Name.Contains("x64")) continue;
                    if (!buildKey.Value.TryGetProperty("zip", out var zip)) continue;
                    entries.Add((ver, buildKey.Value));
                    break;
                }
            }

            foreach (var (ver, build) in entries.OrderByDescending(e => e.Ver, new VersionComparer()).Take(4))
            {
                var path = build.TryGetProperty("path", out var pth) ? pth.GetString() : null;
                if (string.IsNullOrEmpty(path)) continue;
                var sha = build.TryGetProperty("sha256", out var sh) ? sh.GetString() : null;
                var sizeText = build.TryGetProperty("size", out var sz) ? sz.GetString() : null;
                list.Add(new CatalogItem("php", ver,
                    "https://windows.php.net/downloads/releases/" + path,
                    sha, ParseSizeMb(sizeText),
                    "Thread Safe x64 — SHA256 จาก php.net"));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PHP releases.json: " + ex.Message, ex);
        }
        return list;
    }

    // ================= Apache: หน้า download ของ ApacheLounge =================
    private static async Task<List<CatalogItem>> FetchApacheAsync(HttpClient http, CancellationToken ct)
    {
        var list = new List<CatalogItem>();
        try
        {
            var html = await http.GetStringAsync("https://www.apachelounge.com/download/", ct).ConfigureAwait(false);
            // href="/download/VS18/binaries/httpd-2.4.71-261112-Win64-VS18.zip" (บางทีเป็นตัวเล็ก)
            var matches = Regex.Matches(html,
                @"href=""(?<href>/download/VS1[78]/binaries/httpd-(?<ver>\d+\.\d+\.\d+)-\d+-(?<plat>Win64)-VS1[78]\.zip)""",
                RegexOptions.IgnoreCase);

            var byVersion = new Dictionary<string, (int VsLevel, string Url, string Ver)>();
            foreach (Match m in matches)
            {
                var href = m.Groups["href"].Value;
                var ver = m.Groups["ver"].Value;
                var vs = href.Contains("VS18", StringComparison.OrdinalIgnoreCase) ? 18 : 17;
                var url = "https://www.apachelounge.com" + href;
                if (byVersion.TryGetValue(ver, out var existing))
                {
                    if (vs > existing.VsLevel) byVersion[ver] = (vs, url, ver);
                }
                else byVersion[ver] = (vs, url, ver);
            }

            foreach (var (ver, (_, url, _)) in byVersion.OrderByDescending(kv => kv.Key, new VersionComparer()).Take(3))
                list.Add(new CatalogItem("apache", ver, url, null, null,
                    "ดึงจาก ApacheLounge (ต้องใช้ VC++ Redistributable ตามรุ่น build)"));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ApacheLounge page: " + ex.Message, ex);
        }
        return list;
    }

    // ================= MariaDB: หน้า archive =================
    private static async Task<List<CatalogItem>> FetchMariaDbAsync(HttpClient http, CancellationToken ct)
    {
        var list = new List<CatalogItem>();
        try
        {
            var html = await http.GetStringAsync("https://archive.mariadb.org/", ct).ConfigureAwait(false);
            var versions = new List<Version>();
            foreach (Match m in Regex.Matches(html, @"href=""mariadb-(\d+\.\d+\.\d+)/""", RegexOptions.IgnoreCase))
                if (Version.TryParse(m.Groups[1].Value, out var v)) versions.Add(v);

            // เอา patch สูงสุดของแต่ละสาย (major.minor) เรียงจากสายใหม่ไปเก่า 3 สาย
            var latest = versions
                .GroupBy(v => $"{v.Major}.{v.Minor}")
                .Select(g => g.OrderByDescending(v => v.Build).First())
                .OrderByDescending(v => v.Major * 100 + v.Minor)
                .Take(3);

            foreach (var v in latest)
            {
                var ver = $"{v.Major}.{v.Minor}.{v.Build}";
                list.Add(new CatalogItem("mariadb", ver,
                    $"https://archive.mariadb.org/mariadb-{ver}/winx64-packages/mariadb-{ver}-winx64.zip",
                    null, null, $"สาย {v.Major}.{v.Minor} (ดึงจาก archive.mariadb.org)"));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("archive.mariadb.org: " + ex.Message, ex);
        }
        return list;
    }

    private static long? ParseSizeMb(string? text)
    {
        var m = Regex.Match(text ?? "", @"([\d.]+)\s*MB", RegexOptions.IgnoreCase);
        return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var mb)
            ? (long)(mb * 1024 * 1024)
            : null;
    }

    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => UpdateChecker.CompareVersions(x ?? "", y ?? "");
    }
}
