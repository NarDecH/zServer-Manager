using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ZServer.Core;

/// <summary>
/// ตรวจสอบพอร์ตว่างและหาว่าโปรเซสใดใช้พอร์ตอยู่ (ผ่าน netstat)
/// </summary>
public static class PortChecker
{
    /// <summary>พอร์ตว่างหรือไม่ (ลอง bind จริง)</summary>
    public static bool IsAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>หาพอร์ตว่างถัดจาก preferred (ลองถัดไปสูงสุด tries พอร์ต) — 0 = ไม่พบ</summary>
    public static int SuggestFreePort(int preferred, int tries = 60)
    {
        for (var port = Math.Max(1, preferred + 1); port <= preferred + tries; port++)
        {
            if (IsAvailable(port)) return port;
        }
        return 0;
    }

    /// <summary>หาโปรเซสที่กำลัง listen บนพอร์ตนี้ ถ้ามี (ผ่าน netstat -ano)</summary>
    public static (int Pid, string Name)? FindListener(int port)
    {
        try
        {
            var (code, stdout, _) = ProcessRunner.Run("netstat", "-ano -p tcp", null, 15000);
            if (code != 0) return null;

            foreach (var raw in stdout.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = Regex.Split(line, @"\s+");
                if (parts.Length < 5) continue;

                // รูปแบบ: TCP  0.0.0.0:80  0.0.0.0:0  LISTENING  1234
                if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;

                var local = parts[1];
                var portPart = local.Split(':').Last();
                if (!int.TryParse(portPart, out var lp) || lp != port) continue;
                if (!int.TryParse(parts[4], out var pid) || pid <= 0) continue;

                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    return (pid, proc.ProcessName);
                }
                catch
                {
                    return (pid, "?");
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>คำอธิบายภาษาไทยว่าโปรเซสชื่อนี้น่าจะเป็นอะไร</summary>
    public static string GuessServiceName(string processName)
    {
        var p = processName.ToLowerInvariant();
        if (p.Contains("httpd")) return "Apache (น่าจะมาจาก XAMPP หรือ zServer รอบก่อน)";
        if (p.Contains("mysqld") || p.Contains("mariadbd")) return "MySQL/MariaDB (น่าจะมาจาก XAMPP)";
        if (p.Contains("php")) return "PHP";
        if (p.Contains("nginx")) return "Nginx";
        if (p.Contains("iis") || p.Contains("w3wp") || p.Contains("inetinfo")) return "IIS ของ Windows";
        if (p.Contains("node")) return "Node.js";
        if (p.Contains("python")) return "Python";
        if (p.Contains("system")) return "โปรเซสของระบบ";
        return "โปรเซส " + processName;
    }

    /// <summary>ข้อความเตือนภาษาไทยเมื่อพอร์ตถูกใช้งาน</summary>
    public static string ConflictMessage(int port, string forWhat)
    {
        var found = FindListener(port);
        var detail = found.HasValue
            ? $"พอร์ต {port} ถูกใช้โดย {GuessServiceName(found.Value.Name)} (โปรเซส {found.Value.Name}.exe, PID {found.Value.Pid})"
            : $"พอร์ต {port} ถูกโปรแกรมอื่นใช้อยู่";
        return $"{detail}\n\nวิธีแก้: เลือกอย่างใดอย่างหนึ่ง\n" +
               $"1. หยุดโปรแกรมเดิมที่ใช้พอร์ตนี้ (เช่น XAMPP)\n" +
               $"2. เปลี่ยนพอร์ตของ {forWhat} ในหน้า \"ตั้งค่า\" แล้วกดบันทึก";
    }
}
