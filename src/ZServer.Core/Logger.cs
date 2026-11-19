using System.Text;

namespace ZServer.Core;

/// <summary>
/// Logger แบบ thread-safe เขียนลงไฟล์ พร้อม event ให้ UI โชว์แบบสดได้
/// </summary>
public sealed class Logger
{
    private readonly string _file;
    private readonly object _lock = new();

    /// <summary>เกิดขึ้นเมื่อมีบรรทัด log ใหม่ (ให้ UI ผูกตามแบบสด)</summary>
    public event Action<string>? OnLine;

    public Logger(string file)
    {
        _file = file;
        try { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); } catch { }
    }

    public string FilePath => _file;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level,-5}] {message}";
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_file, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch { /* log ล้มเหลวไม่ควรทำโปรแกรมล้ม */ }

        try { OnLine?.Invoke(line); } catch { }
    }

    /// <summary>อ่านบรรทัดล่าสุด N บรรทัด</summary>
    public string Tail(int lines = 200)
    {
        try
        {
            if (!File.Exists(_file)) return "(ยังไม่มี log)";
            var all = File.ReadAllLines(_file);
            if (all.Length <= lines) return string.Join(Environment.NewLine, all);
            return string.Join(Environment.NewLine, all[^lines..]);
        }
        catch (Exception ex) { return "(อ่าน log ไม่สำเร็จ: " + ex.Message + ")"; }
    }
}
