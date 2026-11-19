using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// กรองข้อความ progress ไม่ให้ช่อง log ล้น
/// — บันทึกเฉพาะเมื่อเปลี่ยนขั้นงาน (เช่น ดาวน์โหลด → แตกไฟล์)
///   หรือความคืบหน้าขยับถึงหมุดหมาย (ทุก ~10%) — ความเดินรายวินาทีแสดงบน progress bar + บรรทัดสถานะแทน
/// </summary>
internal sealed class ProgressLogFilter
{
    private string _lastPhase = "";
    private int _lastLoggedPercent = -1;
    private readonly string _prefix;
    private readonly Action<string> _append;

    public ProgressLogFilter(string prefix, Action<string> append)
    {
        _prefix = prefix;
        _append = append;
    }

    public void Handle(InstallProgress p, ProgressBar bar, Label phaseLabel)
    {
        if (p.Percent >= 0)
            bar.Value = Math.Min(100, Math.Max(0, p.Percent));
        phaseLabel.Text = $"[{_prefix}] {p.Phase}: {p.Message}";

        var phaseChanged = p.Phase != _lastPhase;
        _lastPhase = p.Phase;

        var shouldLog =
            phaseChanged ||
            (p.Percent >= 100 && _lastLoggedPercent != 100) ||
            (p.Percent > _lastLoggedPercent && p.Percent - _lastLoggedPercent >= 10);

        if (!shouldLog) return;
        _lastLoggedPercent = p.Percent;
        _append($"  {p.Percent,3}%  {p.Phase}: {p.Message}");
    }
}
