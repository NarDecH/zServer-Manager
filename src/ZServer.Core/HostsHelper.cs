using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// ซิงก์รายชื่อ Virtual Host เข้าไฟล์ hosts ของ Windows
/// แก้เฉพาะ section ที่มี marker ของ zServer เท่านั้น — ส่วนอื่นของ hosts ไม่ถูกแตะ
/// </summary>
public static class HostsHelper
{
    private const string Begin = "# ---- zServer vhosts BEGIN ----";
    private const string End = "# ---- zServer vhosts END ----";

    public static string HostsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public static void Sync(IEnumerable<string> names)
    {
        var path = HostsPath;
        var content = File.Exists(path) ? File.ReadAllText(path) : "";

        // ตัด section เดิมออก (ถ้ามี)
        var pattern = Regex.Escape(Begin) + @"[\s\S]*?" + Regex.Escape(End) + @"\s*";
        content = Regex.Replace(content, pattern, "", RegexOptions.CultureInvariant);

        var lines = new List<string> { Begin };
        foreach (var n in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
            lines.Add($"127.0.0.1\t{n}");
        lines.Add(End);

        content = content.TrimEnd() + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
        File.WriteAllText(path, content);
    }

    /// <summary>ชื่อโดเมนที่ zServer จัดการอยู่ตอนนี้ (อ่านจาก hosts section)</summary>
    public static List<string> CurrentNames()
    {
        var list = new List<string>();
        try
        {
            if (!File.Exists(HostsPath)) return list;
            var inside = false;
            foreach (var raw in File.ReadAllLines(HostsPath))
            {
                if (raw.Contains(Begin)) { inside = true; continue; }
                if (raw.Contains(End)) { inside = false; continue; }
                if (!inside) continue;
                var parts = raw.Split('\t', ' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == "127.0.0.1") list.Add(parts[1]);
            }
        }
        catch { }
        return list;
    }
}
