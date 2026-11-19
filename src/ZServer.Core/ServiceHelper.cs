using System.ServiceProcess;

namespace ZServer.Core;

/// <summary>
/// ควบคุม Windows Service (ตรวจสถานะ เริ่ม หยุด ตรวจว่ามีอยู่ไหม)
/// ส่วนการติดตั้ง/ถอนจะใช้ตัวติดตั้งของ Apache/MariaDB เอง (ดูใน manager แต่ละตัว)
/// </summary>
public static class ServiceHelper
{
    public static bool Exists(string name)
    {
        try
        {
            return ServiceController.GetServices().Any(s =>
                s.ServiceName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public static ServiceControllerStatus GetStatus(string name)
    {
        using var sc = new ServiceController(name);
        return sc.Status;
    }

    public static bool IsRunning(string name)
    {
        try
        {
            return Exists(name) && GetStatus(name) == ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    public static bool IsInstalledAndStopped(string name)
    {
        try
        {
            return Exists(name) && GetStatus(name) == ServiceControllerStatus.Stopped;
        }
        catch { return false; }
    }

    /// <summary>เริ่มเซอร์วิสและรอจนกว่าจะ Running</summary>
    public static void Start(string name, int timeoutMs = 40_000)
    {
        using var sc = new ServiceController(name);
        if (sc.Status == ServiceControllerStatus.Running) return;
        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.Paused)
        {
            // กำลังเริ่ม/หยุดอยู่ รอสถานะนิ่งก่อน
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>หยุดเซอร์วิสและรอจนกว่าจะ Stopped</summary>
    public static void Stop(string name, int timeoutMs = 40_000)
    {
        using var sc = new ServiceController(name);
        if (sc.Status == ServiceControllerStatus.Stopped) return;
        try
        {
            sc.Stop();
        }
        catch (InvalidOperationException)
        {
            // เซอร์วิสอาจหยุดไปแล้ว
        }
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>รีสตาร์ท = หยุดแล้วเริ่ม</summary>
    public static void Restart(string name, int timeoutMs = 60_000)
    {
        Stop(name, timeoutMs);
        Start(name, timeoutMs);
    }

    public static string GetStartMode(string name)
    {
        // ใช้ sc.exe query qc เพื่อดู start type (ชื่อเดียวกับใน ServiceController)
        try
        {
            var (code, stdout, _) = ProcessRunner.Run("sc.exe", $"qc \"{name}\"", null, 15000);
            if (code == 0)
            {
                foreach (var line in stdout.Split('\n'))
                {
                    if (line.Contains("START_TYPE"))
                    {
                        if (line.Contains("AUTO_START")) return "auto";
                        if (line.Contains("DEMAND_START")) return "demand";
                        if (line.Contains("DISABLED")) return "disabled";
                    }
                }
            }
        }
        catch { }
        return "?";
    }

    /// <summary>ตั้ง start type ผ่าน sc config (auto / demand)</summary>
    public static void SetStartType(string name, string startType)
    {
        var arg = startType == "auto" ? "auto" : "demand";
        var (code, _, stderr) = ProcessRunner.Run("sc.exe", $"config \"{name}\" start= {arg}", null, 15000);
        if (code != 0) throw new InvalidOperationException("ตั้ง start type ไม่สำเร็จ: " + stderr);
    }

    /// <summary>ลบเซอร์วิสแบบบังคับ (ใช้เมื่อถอนด้วยตัวติดตั้งเดิมไม่สำเร็จ)</summary>
    public static void ForceDelete(string name)
    {
        if (!Exists(name)) return;
        try { Stop(name); } catch { }
        var (code, _, stderr) = ProcessRunner.Run("sc.exe", $"delete \"{name}\"", null, 15000);
        if (code != 0) throw new InvalidOperationException("ลบเซอร์วิสไม่สำเร็จ: " + stderr);
    }

    /// <summary>รอให้เซอร์วิสหายไปจากระบบ (หลัง delete ต้องรอเล็กน้อย)</summary>
    public static void WaitUntilGone(string name, int timeoutMs = 20_000)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            if (!Exists(name)) return;
            Thread.Sleep(300);
        }
    }
}
