using System.Diagnostics;

namespace ZServer.Core;

/// <summary>
/// รันโปรแกรมภายนอก พร้อมจับ stdout/stderr และรองรับ timeout
/// </summary>
public static class ProcessRunner
{
    public static (int ExitCode, string StdOut, string StdErr) Run(
        string exe, string args, string? workDir = null, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workDir ?? "",
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("รันโปรแกรมไม่สำเร็จ: " + exe);
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"โปรแกรม {Path.GetFileName(exe)} ทำงานเกิน {timeoutMs / 1000} วินาที จึงถูกหยุด");
        }

        return (proc.ExitCode, outTask.GetAwaiter().GetResult(), errTask.GetAwaiter().GetResult());
    }

    /// <summary>
    /// รันแบบพื้นหลัง (ไม่รอ) — ใช้สำหรับโหมด "process" ของ Apache/MariaDB
    /// คืน Process ที่กำลังทำงาน หรือ throw ถ้าเริ่มไม่สำเร็จ
    /// </summary>
    public static Process StartDetached(string exe, string args, string? workDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir ?? "",
        };
        return Process.Start(psi) ?? throw new InvalidOperationException("รันโปรแกรมไม่สำเร็จ: " + exe);
    }

    /// <summary>จบโปรเซสและโปรเซสลูกทั้งหมด (taskkill /T /F)</summary>
    public static void KillTree(int pid)
    {
        try
        {
            Run("taskkill", $"/T /F /PID {pid}", null, 15000);
        }
        catch
        {
            try { System.Diagnostics.Process.GetProcessById(pid).Kill(entireProcessTree: true); } catch { }
        }
    }

    /// <summary>โปรเซสนี้ยังทำงานอยู่หรือไม่</summary>
    public static bool IsAlive(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch { return false; }
    }
}
