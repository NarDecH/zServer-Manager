using System.Security.Cryptography;

namespace ZServer.Core;

public sealed record DownloadProgress(long Bytes, long? Total, string SpeedText)
{
    public int Percent => Total is > 0 ? (int)Math.Min(100, Bytes * 100 / Total.Value) : 0;
}

/// <summary>
/// ดาวน์โหลดไฟล์พร้อมรายงานความคืบหน้าและตรวจ SHA256 (ถ้ามีค่าอ้างอิง)
/// </summary>
public sealed class Downloader : IDisposable
{
    private readonly HttpClient _http;
    private readonly Logger _log;

    public Downloader(Logger log)
    {
        _log = log;
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        _http.Timeout = TimeSpan.FromMinutes(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("zServerManager/1.0 (+Windows)");
    }

    /// <summary>ดาวน์โหลดไฟล์ลง destPath — คืน path ที่โหลดเสร็จ</summary>
    public async Task<string> DownloadAsync(string url, string destPath,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default,
        string? expectedSha256 = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var partPath = destPath + ".part";

        _log.Info($"เริ่มดาวน์โหลด: {url}");
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long received = 0;
        var lastReport = DateTime.MinValue;

        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;

                if ((DateTime.Now - lastReport).TotalMilliseconds > 200)
                {
                    lastReport = DateTime.Now;
                    var secs = Math.Max(0.5, sw.Elapsed.TotalSeconds);
                    var speed = $"{received / 1024 / 1024 / secs:F1} MB/s";
                    progress?.Report(new DownloadProgress(received, total, speed));
                }
            }
        } // ปิดไฟล์ก่อนตรวจ SHA256

        progress?.Report(new DownloadProgress(received, total, "เสร็จ"));
        _log.Info($"ดาวน์โหลดเสร็จ: {destPath} ({received / 1024.0 / 1024:F1} MB)");

        // ตรวจ SHA256 ถ้ามีค่าอ้างอิง (ต้องทำหลังปิด stream แล้วเท่านั้น)
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            progress?.Report(new DownloadProgress(received, total, "กำลังตรวจ SHA256..."));
            var actual = HashUtil.Sha256File(partPath);
            if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partPath);
                throw new InvalidOperationException($"ตรวจ SHA256 ไม่ผ่าน! ไฟล์อาจเสียหายหรือถูกแก้ไข (คาดว่า {expectedSha256[..12]}… ได้มา {actual[..12]}…)");
            }
            _log.Info("SHA256 ถูกต้อง ✓");
        }

        // ถ้าปลายทางมีอยู่แล้ว ลบก่อนย้าย
        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(partPath, destPath);
        return destPath;
    }

    /// <summary>ดาวน์โหลดแบบเงียบ ๆ (ใช้กับไฟล์เล็ก เช่น releases.json)</summary>
    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        return await _http.GetStringAsync(url, ct);
    }

    /// <summary>HEAD request (ใช้ตรวจว่า URL มีไฟล์จริงและขนาดเท่าไร)</summary>
    public async Task<HttpResponseMessage> GetAsyncHead(string url, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        return await _http.SendAsync(req, ct);
    }

    /// <summary>HttpClient ภายใน (ให้ UpdateChecker ใช้ต่อ)</summary>
    public HttpClient GetHttpClient() => _http;

    public void Dispose() => _http.Dispose();
}

public static class HashUtil
{
    public static string Sha256File(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
