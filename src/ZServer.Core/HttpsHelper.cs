using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// สร้างใบรับรอง HTTPS สำหรับพัฒนางานผ่าน mkcert (เครื่องมือทางการของ FiloSottile)
/// — ติดตั้ง root CA เข้า Windows trust store ให้ browser เชื่อใบรับรองโดยไม่เตือน
/// </summary>
public static class HttpsHelper
{
    public static bool IsCertReady(ServerPaths p) =>
        File.Exists(Path.Combine(p.ApacheDir, "conf", "ssl", "cert.pem"));

    public sealed record CertInfo(string Subject, string NotAfter, string[] Domains);

    /// <summary>อ่านข้อมูลใบรับรองจริง (subject/วันหมดอายุ/โดเมน) ผ่าน openssl ที่มากับ Apache — null ถ้ายังไม่มี</summary>
    public static CertInfo? GetCertInfo(ServerPaths p)
    {
        var cert = Path.Combine(p.ApacheDir, "conf", "ssl", "cert.pem");
        if (!File.Exists(cert)) return null;
        var openssl = Path.Combine(p.ApacheDir, "bin", "openssl.exe");
        if (!File.Exists(openssl)) return null;

        var (code, stdout, stderr) = ProcessRunner.Run(openssl,
            $"x509 -in \"{cert}\" -noout -subject -enddate -ext subjectAltName", null, 30_000);
        if (code != 0) return null;

        var text = stdout + stderr;
        var subject = Regex.Match(text, @"subject=.*?CN\s*=\s*([^,\r\n]+)").Groups[1].Value.Trim();
        var notAfter = Regex.Match(text, @"notAfter=(.+)").Groups[1].Value.Trim();
        var domains = Regex.Matches(text, @"DNS:([^,\s\r\n]+)")
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CertInfo(subject, notAfter, domains);
    }

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

        var sslDir = Path.Combine(p.ApacheDir, "conf", "ssl");
        Directory.CreateDirectory(sslDir);

        var names = commonNames.Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            throw new InvalidOperationException("ไม่มีชื่อโดเมนให้สร้างใบรับรอง");

        // mkcert จะสร้าง CA อัตโนมัติที่ %LOCALAPPDATA%\mkcert ระหว่าง generate
        // (เราไม่ใช้ mkcert -install เพราะบางเครื่องค้าง — ติดตั้ง trust เองผ่าน certutil แทน)
        var args = $"-cert-file \"{sslDir}\\cert.pem\" -key-file \"{sslDir}\\key.pem\" " +
                   string.Join(" ", names.Select(n => $"\"{n}\""));
        log.Info("สร้างใบรับรองสำหรับ: " + string.Join(", ", names));
        var (c2, o2, e2) = await Task.Run(() => ProcessRunner.Run(mkcert, args, null, 300_000), ct).ConfigureAwait(false);
        if (c2 != 0)
            throw new InvalidOperationException("mkcert สร้างใบรับรองไม่สำเร็จ:\n" + (o2 + e2).Trim());

        // ติดตั้ง rootCA เข้า Windows trust store ผ่าน certutil (LocalMachine Root — Chrome/Edge เชื่อทันที)
        var caroot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mkcert", "rootCA.pem");
        if (File.Exists(caroot))
        {
            log.Info("ติดตั้ง root CA เข้า Windows trust store (certutil) ...");
            var (c3, o3, e3) = await Task.Run(() =>
                ProcessRunner.Run("certutil", $"-addstore -f Root \"{caroot}\"", null, 120_000), ct).ConfigureAwait(false);
            if (c3 != 0)
                throw new InvalidOperationException("ติดตั้ง root CA ไม่สำเร็จ (certutil):\n" + (o3 + e3).Trim());
        }
        else
        {
            log.Warn("ไม่พบ rootCA.pem — browser อาจยังไม่เชื่อใบรับรอง (ลองรีรันอีกครั้ง)");
        }

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
