using System.Security.Cryptography;
using System.Text;

namespace ZServer.Core;

/// <summary>
/// เข้ารหัส/ถอดรหัสข้อความลับ (เช่น รหัสผ่าน root ของ MariaDB)
/// ด้วย Windows DPAPI — ถอดได้เฉพาะ user เดียวกันบนเครื่องเดียวกันเท่านั้น
/// </summary>
public static class SecretProtector
{
    public static string Protect(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain);
        return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
    }

    public static string Unprotect(string encrypted)
    {
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser));
        }
        catch { return ""; }
    }
}
