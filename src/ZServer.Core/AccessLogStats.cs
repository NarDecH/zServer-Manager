using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// วิเซต access.log (combined format ของ Apache) → Top IP / Top หน้าเว็บ / สัดส่วน status code
/// </summary>
public static class AccessLogStats
{
    private static readonly Regex LinePattern = new(
        @"^(?<ip>\S+) \S+ \S+ \[[^\]]+\] ""(?<method>\S+) (?<path>\S+)[^""]*"" (?<status>\d{3}) ",
        RegexOptions.Compiled);

    public sealed record Stats(
        int Lines,
        int SkippedLines,
        List<(string Key, int Count)> TopIps,
        List<(string Key, int Count)> TopPaths,
        List<(string Key, int Count)> StatusCounts);

    public static Stats Parse(string accessLogPath, int maxLines = 300_000)
    {
        var ips = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);
        int lines = 0, skipped = 0;

        foreach (var line in File.ReadLines(accessLogPath))
        {
            if (lines >= maxLines) { skipped++; continue; }
            lines++;

            var m = LinePattern.Match(line);
            if (!m.Success) { skipped++; continue; }

            var ip = m.Groups["ip"].Value;
            var status = m.Groups["status"].Value;
            var path = m.Groups["path"].Value;

            ips[ip] = ips.TryGetValue(ip, out var n) ? n + 1 : 1;
            statuses[status] = statuses.TryGetValue(status, out var s) ? s + 1 : 1;
            if (path.StartsWith('/') && !path.StartsWith("/icons/"))
                paths[path] = paths.TryGetValue(path, out var p) ? p + 1 : 1;
        }

        return new Stats(
            lines, skipped,
            Top(ips), Top(paths), Top(statuses));
    }

    private static List<(string Key, int Count)> Top(Dictionary<string, int> dict, int count = 10)
        => dict.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
               .Take(count)
               .Select(kv => (kv.Key, kv.Value))
               .ToList();
}
