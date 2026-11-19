namespace ZServer.Core;

/// <summary>
/// ตั้งให้โปรแกรมเปิดอัตโนมัติเมื่อเข้าสู่ระบบ Windows ผ่าน Task Scheduler
/// — โปรแกรมนี้มี manifest แบบ requireAdministrator จึงใช้ Startup folder / Run key ไม่ได้
///   (Windows ไม่ยอมยกระดับสิทธิ์แบบเงียบ ๆ ตอน logon) ต้องใช้ Task Scheduler /rl highest
///   ซึ่งให้สิทธิ์ admin ทันทีโดยไม่เด้ง UAC
/// </summary>
public static class StartupHelper
{
    public const string TaskName = "ZServerManager";

    /// <summary>มี task autostart ตั้งไว้แล้วหรือไม่</summary>
    public static bool IsEnabled()
    {
        try
        {
            var (code, _, _) = ProcessRunner.Run("schtasks", $"/query /tn \"{TaskName}\"", null, 15_000);
            return code == 0;
        }
        catch { return false; }
    }

    /// <summary>สร้าง task ให้เปิดโปรแกรมเมื่อ user เข้าสู่ระบบ (สิทธิ์สูงสุด, เฉพาะเมื่อล็อกออน)</summary>
    public static void Enable(string exePath)
    {
        var args = $"/create /f /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /it";
        var (code, stdout, stderr) = ProcessRunner.Run("schtasks", args, null, 30_000);
        if (code != 0)
            throw new InvalidOperationException(
                "สร้าง task ใน Task Scheduler ไม่สำเร็จ:\n" + (stdout + " " + stderr).Trim());
    }

    /// <summary>ยกเลิก autostart (ลบ task ถ้ามี)</summary>
    public static void Disable()
    {
        if (!IsEnabled()) return;
        var (code, stdout, stderr) = ProcessRunner.Run("schtasks", $"/delete /f /tn \"{TaskName}\"", null, 30_000);
        if (code != 0)
            throw new InvalidOperationException(
                "ลบ task ใน Task Scheduler ไม่สำเร็จ:\n" + (stdout + " " + stderr).Trim());
    }
}
