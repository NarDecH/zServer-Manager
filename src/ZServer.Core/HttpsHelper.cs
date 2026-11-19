using System.Text.Json;

namespace ZServer.Core;

/// <summary>
/// สร้างใบรับรอง HTTPS สำหรับพัฒนางานผ่าน mkcert (เครื่องมือทางการของ FiloSottile)
/// — ติดตั้ง root CA เข้า Windows trust store ให้ browser เชื่อใบรับรองโดยไม่เตือน
/// </summary>
public static class HttpsHelper
{
    public static bool IsCertReady(ServerPaths p) =>
        File.Exists(Path.Combine(p.ApacheDir, "conf", "ssl", "cert.pem"));

    public static async Task SetupAsync(ServerPaths p, Logger log, Downloader dl,
        IEnumerable<string> commonNames, CancellationToken ct = default)
    {
        var mkcert = Path.Combine(p.DownloadsDir, "mkcert.exe");
        if (!File.Exists(mkcert))
        {
            var url = await LatestMkcertUrlAsync(dl.GetHttpClient(), ct).ConfigureAwait(false);
            log.Info("ดาวน์โหลด mkcert เวอร์ชันล่าสุดจาก GitHub ...");
            await dl.DownloadAsync(url, mkcert, ct: ct).ConfigureAwait(false);
        }

        log.Info("ติดตั้ง root CA เข้า Windows trust store (mkcert -install) ...");
        var (c1, o1, e1) = await Task.Run(() => ProcessRunner.Run(mkcert, "-install", null, 120_000), ct).ConfigureAwait(false);
        if (c1 != 0)
            throw new InvalidOperationException("mkcert -install ไม่สำเร็จ:\n" + (o1 + e1).Trim());

        var sslDir = Path.Combine(p.ApacheDir, "conf", "ssl");
        Directory.CreateDirectory(sslDir);

        var names = commonNames.Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            throw new InvalidOperationException("ไม่มีชื่อโดเมนให้สร้างใบรับรอง");

        var args = $"-cert-file \"{sslDir}\\cert.pem\" -key-file \"{sslDir}\\key.pem\" " +
                   string.Join(" ", names.Select(n => $"\"{n}\""));
        log.Info("สร้างใบรับรองสำหรับ: " + string.Join(", ", names));
        var (c2, o2, e2) = await Task.Run(() => ProcessRunner.Run(mkcert, args, null, 120_000), ct).ConfigureAwait(false);
        if (c2 != 0)
            throw new InvalidOperationException("mkcert สร้างใบรับรองไม่สำเร็จ:\n" + (o2 + e2).Trim());

        log.Info("สร้างใบรับรอง HTTPS เรียบร้อย → apache\\conf\\ssl\\cert.pem");
    }

    private static async Task<string> LatestMkcertUrlAsync(HttpClient http, CancellationToken ct)
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("zServerManager/1.0");
        var json = await http.GetStringAsync(
            "https://api.github.com/repos/FiloSottile/mkcert/releases/latest", ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            if (name.Contains("windows-amd64", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return a.GetProperty("browser_download_url").GetString()!;
        }
        throw new InvalidOperationException("ไม่พบไฟล์ mkcert สำหรับ Windows ใน release ล่าสุด");
    }
}
