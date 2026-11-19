namespace ZServer.Core;

/// <summary>
/// เปิด/ปิดพอร์ตใน Windows Firewall ผ่าน netsh (ต้องรันด้วยสิทธิ์ผู้ดูแลระบบ)
/// </summary>
public static class FirewallHelper
{
    public static string RuleName(int port) => $"zServer port {port}";

    public static void AllowPort(int port, string description)
    {
        var rule = RuleName(port);
        // ลบของเดิมก่อนกันซ้ำ
        try { RemoveRule(rule); } catch { }

        var (code, _, stderr) = ProcessRunner.Run("netsh",
            $"advfirewall firewall add rule name=\"{rule}\" dir=in action=allow protocol=TCP localport={port} description=\"{description}\"",
            null, 20_000);
        if (code != 0) throw new InvalidOperationException("เปิดพอร์ตใน Firewall ไม่สำเร็จ: " + stderr);
    }

    public static void RemoveRule(int port) => RemoveRule(RuleName(port));

    public static void RemoveRule(string ruleName)
    {
        ProcessRunner.Run("netsh",
            $"advfirewall firewall delete rule name=\"{ruleName}\"",
            null, 20_000);
    }

    public static bool RuleExists(int port)
    {
        var (code, stdout, _) = ProcessRunner.Run("netsh",
            $"advfirewall firewall show rule name=\"{RuleName(port)}\"",
            null, 20_000);
        return code == 0 && stdout.Contains("Rule Name", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Firewall บล็อกพอร์ตนี้อยู่หรือไม่ (ประมาณการ: ถ้ายังไม่มี rule = อาจถูกบล็อก)</summary>
    public static bool PortProbablyBlocked(int port) => !RuleExists(port);
}
