using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// หน้าอัพเดท: ตรวจเวอร์ชันล่าสุดจากเว็บทางการ → อัพเดทแบบปลอดภัย
/// (หยุดเซอร์วิส → สำรองโฟลเดอร์เดิมลง backups\ → ติดตั้งใหม่ → คง config และข้อมูลเดิม)
/// </summary>
public sealed class UpdateForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;
    private readonly CatalogService _catalog;

    private readonly Label _lblCurrent = new() { AutoSize = true };
    private readonly Button _btnCheck = new() { Text = "ตรวจสอบเวอร์ชันล่าสุดจากเว็บทางการ", AutoSize = true };
    private readonly Label _lblLatest = new() { AutoSize = false, Dock = DockStyle.Top, Height = 90, ForeColor = Color.DimGray };
    private readonly Button _btnUpdateApache = new() { Text = "อัพเดท Apache", Width = 170 };
    private readonly Button _btnUpdatePhp = new() { Text = "อัพเดท PHP", Width = 170 };
    private readonly Button _btnUpdateDb = new() { Text = "อัพเดท MariaDB", Width = 170 };
    private readonly ProgressBar _bar = new() { Dock = DockStyle.Top, Height = 22 };
    private readonly Label _lblPhase = new() { AutoSize = true, Text = "—" };
    private readonly TextBox _txtLog = new();
    private readonly Button _btnClose = new() { Text = "ปิด", AutoSize = true };

    private List<LatestVersionInfo> _latest = [];

    public UpdateForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;
        _catalog = new CatalogService(log);

        Text = "อัพเดทคอมโพเนนต์";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 580);
        MinimumSize = new Size(760, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        BuildUi();
        Load += (s, e) => RefreshCurrent();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // current
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // check btn
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // latest labels
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // update buttons
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // bar + phase
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // log

        root.Controls.Add(_lblCurrent);
        root.Controls.Add(_btnCheck);
        root.Controls.Add(_lblLatest);
        var updRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        updRow.Controls.Add(_btnUpdateApache);
        updRow.Controls.Add(_btnUpdatePhp);
        updRow.Controls.Add(_btnUpdateDb);
        updRow.Controls.Add(new Label
        {
            Text = " การอัพเดทจะสำรองโฟลเดอร์เดิมลง backups\\ ก่อนเสมอ — ข้อมูลใน data\\ ไม่ถูกลบ",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 8, 0, 0),
        });
        root.Controls.Add(updRow);

        var barWrap = new Panel { Dock = DockStyle.Fill };
        barWrap.Controls.Add(_bar);
        barWrap.Resize += (s, e) => _bar.Width = barWrap.ClientSize.Width - 4;
        var barCol = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        barCol.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        barCol.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        barCol.Controls.Add(barWrap, 0, 0);
        barCol.Controls.Add(_lblPhase, 0, 1);
        root.Controls.Add(barCol);

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.ReadOnly = true;
        _txtLog.Multiline = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.Font = new Font("Consolas", 9f);
        _txtLog.BackColor = Color.FromArgb(24, 26, 32);
        _txtLog.ForeColor = Color.FromArgb(180, 220, 190);
        _txtLog.WordWrap = false;
        var logGroup = new GroupBox { Text = " รายละเอียด ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 6) };
        logGroup.Controls.Add(_txtLog);
        root.Controls.Add(logGroup);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        bottom.Controls.Add(_btnClose);
        _btnClose.Click += (s, e) => Close();
        root.Controls.Add(bottom); // bottom flow อยู่ใน row สุดท้ายด้วย (dock bottom ภายใน cell)

        Controls.Add(root);
        Theme.Apply(this);

        _btnCheck.Click += (s, e) => CheckLatest();
        _btnUpdateApache.Click += (s, e) => RunUpdate("apache", _btnUpdateApache);
        _btnUpdatePhp.Click += (s, e) => RunUpdate("php", _btnUpdatePhp);
        _btnUpdateDb.Click += (s, e) => RunUpdate("mariadb", _btnUpdateDb);
    }

    private void RefreshCurrent()
    {
        var stack = new StackController(_paths, _cfg, _log);
        var ap = stack.GetApacheStatus();
        var php = stack.GetPhpStatus();
        var db = stack.GetMariaDbStatus();
        _lblCurrent.Text = $"ติดตั้งอยู่ตอนนี้:  Apache {ap.Version ?? "—"}   |   PHP {php.Version ?? "—"}   |   MariaDB {db.Version ?? "—"}";
        _btnUpdateApache.Enabled = File.Exists(_paths.ApacheBin);
        _btnUpdatePhp.Enabled = File.Exists(_paths.PhpBin);
        _btnUpdateDb.Enabled = File.Exists(_paths.MariaDbBin);
    }

    private async void CheckLatest()
    {
        _btnCheck.Enabled = false;
        _lblPhase.Text = "กำลังตรวจสอบจากเว็บทางการ… (อาจใช้เวลาสักครู่)";
        try
        {
            _latest = await UpdateChecker.CheckAllAsync(_dl.GetHttpClient());
            await _catalog.RefreshAsync(_dl.GetHttpClient(), _cfg.RemoteCatalogUrl); // รวมเวอร์ชันใหม่เข้ารายการติดตั้งด้วย
            var lines = _latest.Select(r => r.Ok
                ? $"• {CompName(r.Component)}: เวอร์ชันล่าสุดจากเว็บ = {r.LatestVersion}" +
                  (r.LatestVersion != null && _cfg.InstalledVersion(r.Component) != null &&
                   UpdateChecker.CompareVersions(r.LatestVersion, _cfg.InstalledVersion(r.Component)!) > 0
                    ? "   ← มีเวอร์ชันใหม่กว่าที่ติดตั้ง กดปุ่มอัพเดทได้"
                    : "   (ตรงกับที่ติดตั้งหรือเก่ากว่า)")
                : $"• {CompName(r.Component)}: ตรวจไม่สำเร็จ ({r.Error}) — ใช้รายการเวอร์ชันในแคตตาล็อกของโปรแกรมแทนได้ (หน้า \"ติดตั้ง\")");
            _lblLatest.Text = string.Join("\n", lines);
            _log.Info("ตรวจเวอร์ชันล่าสุด: " + string.Join("; ", _latest.Select(r => $"{r.Component}={r.LatestVersion ?? "?"}")));
        }
        catch (Exception ex)
        {
            _lblLatest.Text = "ตรวจสอบไม่สำเร็จ: " + ex.Message;
        }
        finally
        {
            _btnCheck.Enabled = true;
            _lblPhase.Text = "—";
        }
    }

    private static string CompName(string c) => c switch
    {
        "apache" => "Apache", "php" => "PHP", "mariadb" => "MariaDB", _ => c,
    };

    private async void RunUpdate(string component, Button btn)
    {
        // เลือกเวอร์ชันเป้าหมาย: จากผลตรวจล่าสุดถ้ามี ไม่งั้นใช้ตัวแรกในแคตตาล็อก (ล่าสุดที่โปรแกรมรองรับ)
        var checkedVersion = _latest.FirstOrDefault(r => r.Component == component && r.Ok)?.LatestVersion;
        var target = _catalog.Find(component, checkedVersion ?? "") ?? _catalog.For(component).FirstOrDefault();

        if (target == null)
        {
            MessageBox.Show($"ยังไม่รองรับเวอร์ชัน {checkedVersion} ในแคตตาล็อกของโปรแกรม — เปิดหน้า \"ติดตั้ง\" เพื่อเลือกเวอร์ชันที่รองรับแทน",
                "ไม่พบเวอร์ชัน", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var current = _cfg.InstalledVersion(component);
        if (current != null && UpdateChecker.CompareVersions(target.Version, current) <= 0 &&
            MessageBox.Show($"เวอร์ชันที่จะติดตั้ง ({target.Version}) ไม่ใหม่กว่าที่ติดตั้งอยู่ ({current})\nจะติดตั้งซ้ำ (reinstall) เพื่อซ่อมไฟล์ได้ — ทำต่อหรือไม่?",
                "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        if (component == "mariadb" && MessageBox.Show(
                "ก่อนอัพเดท MariaDB: โปรแกรมจะหยุดเซอร์วิสและสำรองโฟลเดอร์โปรแกรมเดิม\nข้อมูลทั้งหมดใน data\\ จะไม่ถูกแตะ\nหลังอัพเดทข้ามสายเวอร์ชันใหญ่ แนะนำรัน mariadb-upgrade\n\nอัพเดทต่อหรือไม่?",
                "ยืนยันการอัพเดท MariaDB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true, btn);
        _bar.Value = 0;
        var plog = new ProgressLogFilter(target.DisplayName, s =>
        {
            _txtLog.AppendText(s + Environment.NewLine);
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        });
        var progress = new Progress<InstallProgress>(p => plog.Handle(p, _bar, _lblPhase));

        try
        {
            ComponentManager mgr = component switch
            {
                "apache" => new ApacheManager(_paths, _cfg, _log, _dl),
                "php" => new PhpManager(_paths, _cfg, _log, _dl),
                _ => new MariaDbManager(_paths, _cfg, _log, _dl),
            };
            await mgr.InstallAsync(target, progress, installService: _cfg.Services.Mode == "service");
            _txtLog.AppendText($"✓ อัพเดท {target.DisplayName} เป็นเวอร์ชัน {target.Version} เสร็จ" + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _txtLog.AppendText($"✗ อัพเดทไม่สำเร็จ: {ex.Message}" + Environment.NewLine);
            MessageBox.Show("อัพเดทไม่สำเร็จ:\n\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, btn);
            RefreshCurrent();
        }
    }

    private void SetBusy(bool busy, Button except)
    {
        foreach (var b in new[] { _btnCheck, _btnUpdateApache, _btnUpdatePhp, _btnUpdateDb, _btnClose })
            b.Enabled = !busy || b == except;
        UseWaitCursor = busy;
        if (!busy) _bar.Value = 0;
    }
}
