using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// สร้างใบรับรอง HTTPS สำหรับพัฒนางานผ่าน mkcert (เครื่องมือทางการของ FiloSottile)
/// — ติดตั้ง root CA เข้า Windows trust store ให้ browser เชื่อใบรับรองโดยไม่เตือน
/// และรองรับ "นำเข้าใบรับรองจริง" ที่ซื้อมา (.crt/.key/.pem/.pfx) ผ่าน openssl
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
        var domains = Regex.Matches(text, @"DNS:([^,\s\r\n]+)")
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var subject = Regex.Match(text, @"subject=.*?CN\s*=\s*([^,\r\n]+)").Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(subject))
            subject = domains.FirstOrDefault() ?? "(ใบรับรอง mkcert — ดูโดเมนด้านล่าง)"; // ใบรับรอง mkcert ไม่มี CN ใน subject
        var notAfter = Regex.Match(text, @"notAfter=(.+)").Groups[1].Value.Trim();
        return new CertInfo(subject, notAfter, domains);
    }

    // ================= นำเข้าใบรับรองจริง (ซื้อมา) =================
    public sealed record ImportResult(bool Ok, string Message, CertInfo? Info);

    /// <summary>
    /// นำเข้าใบรับรองจริงที่ซื้อมา: แปลงเป็น PEM + ต่อ CA chain + ตรวจว่า key ตรงกับ cert
    /// รองรับ: .crt/.pem/.cer + .key/.pem (แยกไฟล์) หรือ .pfx/.p12 (ไฟล์เดียว ใส่รหัสผ่าน)
    /// </summary>
    public static ImportResult ImportCommercialCert(ServerPaths p, Logger log,
        string certPath, string? keyPath, string? caBundlePath, string? keyPassword)
    {
        var sslDir = Path.Combine(p.ApacheDir, "conf", "ssl");
        Directory.CreateDirectory(sslDir);
        var openssl = Path.Combine(p.ApacheDir, "bin", "openssl.exe");
        if (!File.Exists(openssl))
            throw new InvalidOperationException("ไม่พบ openssl.exe (ต้องติดตั้ง Apache ก่อน)");

        var isPfx = Regex.IsMatch(certPath, @"\.(pfx|p12)$", RegexOptions.IgnoreCase);
        var passArg = string.IsNullOrEmpty(keyPassword) ? "" : $" -passin pass:\"{keyPassword}\"";
        var tmpCert = Path.Combine(sslDir, "_tmp-cert.pem");
        var tmpKey = Path.Combine(sslDir, "_tmp-key.pem");
        var tmpChain = Path.Combine(sslDir, "_tmp-chain.pem");

        try
        {
            if (isPfx)
            {
                // ---- PFX/P12: ไฟล์เดียวมีทั้ง cert + key + chain ----
                log.Info("นำเข้าจากไฟล์ PFX/P12 ...");
                var (c1, o1, e1) = ProcessRunner.Run(openssl,
                    $"pkcs12 -in \"{certPath}\" -clcerts -nokeys -out \"{tmpCert}\"{passArg}", null, 60_000);
                if (c1 != 0)
                    throw new InvalidOperationException("อ่านใบรับรองจาก PFX ไม่สำเร็จ (เช็ครหัสผ่าน):\n" + (o1 + e1).Trim());

                var (c2, o2, e2) = ProcessRunner.Run(openssl,
                    $"pkcs12 -in \"{certPath}\" -nocerts -nodes -out \"{tmpKey}\"{passArg}", null, 60_000);
                if (c2 != 0)
                    throw new InvalidOperationException("ดึง private key จาก PFX ไม่สำเร็จ:\n" + (o2 + e2).Trim());

                // intermediate/chain ใน pfx (ถ้ามี)
                var (c3, _, e3) = ProcessRunner.Run(openssl,
                    $"pkcs12 -in \"{certPath}\" -cacerts -nokeys -out \"{tmpChain}\"{passArg}", null, 60_000);
                if (c3 != 0 || !File.Exists(tmpChain) || new FileInfo(tmpChain).Length < 100)
                    tmpChain = ""; // ไม่มี chain ในไฟล์ — ใช้แค่ leaf
            }
            else
            {
                // ---- แยกไฟล์: .crt + .key (+ ca-bundle) ----
                if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
                    throw new InvalidOperationException("ยังไม่ได้เลือกไฟล์ private key (.key)");

                log.Info("แปลงใบรับรองเป็น PEM ...");
                var (c1, o1, e1) = ProcessRunner.Run(openssl,
                    $"x509 -in \"{certPath}\" -out \"{tmpCert}\"", null, 60_000);
                if (c1 != 0)
                {
                    // ลองแบบ DER (ไฟล์ .cer บางตัวเป็น binary)
                    var (c1b, o1b, e1b) = ProcessRunner.Run(openssl,
                        $"x509 -in \"{certPath}\" -inform DER -out \"{tmpCert}\"", null, 60_000);
                    if (c1b != 0)
                        throw new InvalidOperationException("อ่านไฟล์ใบรับรองไม่สำเร็จ (ลองทั้ง PEM และ DER):\n" + (o1 + e1 + o1b + e1b).Trim());
                }

                log.Info("แปลง private key เป็น PEM (รองรับ RSA/EC/encrypted) ...");
                var (c2, o2, e2) = ProcessRunner.Run(openssl,
                    $"pkey -in \"{keyPath}\" -out \"{tmpKey}\"{passArg}", null, 60_000);
                if (c2 != 0)
                    throw new InvalidOperationException("อ่าน private key ไม่สำเร็จ (เช็ครหัสผ่านของ key):\n" + (o2 + e2).Trim());

                if (!string.IsNullOrWhiteSpace(caBundlePath) && File.Exists(caBundlePath))
                {
                    File.Copy(caBundlePath, tmpChain, overwrite: true);
                }
                else
                {
                    tmpChain = "";
                    log.Warn("ไม่มีไฟล์ CA bundle — browser อาจเตือนว่า chain ไม่ครบ (ขอไฟล์ ca-bundle จากผู้ขายใบรับรอง)");
                }
            }

            // ---- ตรวจว่า key ตรงกับ cert (เทียบ public key) ----
            var (_, pubCert, _) = ProcessRunner.Run(openssl, $"x509 -in \"{tmpCert}\" -pubkey -noout", null, 60_000);
            var (_, pubKey, _) = ProcessRunner.Run(openssl, $"pkey -in \"{tmpKey}\" -pubout", null, 60_000);
            static string Squash(string s) => Regex.Replace(s, @"\s+", "");
            if (Squash(pubCert) != Squash(pubKey))
                throw new InvalidOperationException(
                    "private key ไม่ตรงกับใบรับรอง (public key ต่างกัน) — เช็คว่าหยิบไฟล์มาถูกคู่");

            // ---- ประกอบไฟล์ที่ Apache ใช้: cert.pem = leaf + chain, key.pem ----
            var certPem = Path.Combine(sslDir, "cert.pem");
            var keyPem = Path.Combine(sslDir, "key.pem");
            var sb = new StringBuilder();
            sb.Append(File.ReadAllText(tmpCert).TrimEnd() + "\n");
            if (File.Exists(tmpChain) && new FileInfo(tmpChain).Length > 100)
                sb.Append(File.ReadAllText(tmpChain).TrimEnd() + "\n");
            File.WriteAllText(certPem, sb.ToString(), new UTF8Encoding(false));
            File.WriteAllText(keyPem, File.ReadAllText(tmpKey), new UTF8Encoding(false));

            var info = GetCertInfo(p);
            log.Info($"นำเข้าใบรับรองจริงสำเร็จ: {info?.Subject} (หมดอายุ {info?.NotAfter})");
            return new ImportResult(true, "นำเข้าสำเร็จ", info);
        }
        finally
        {
            try { File.Delete(tmpCert); } catch { }
            try { File.Delete(tmpKey); } catch { }
            try { File.Delete(tmpChain); } catch { }
        }
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
