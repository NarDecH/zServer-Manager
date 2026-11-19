using Microsoft.Win32;
using System.Text.RegularExpressions;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// หน้าติดตั้ง: เลือกเวอร์ชัน → ดาวน์โหลดจากเว็บทางการ → แตกไฟล์ → เขียน config → ติดตั้งเซอร์วิส
/// </summary>
public sealed class InstallForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;
    private readonly Label _lblAdminer = new();
    private readonly Button _btnInstallAdminer = new();
    private readonly CatalogService _catalog;

    private readonly ComboBox _cboApache = new();
    private readonly ComboBox _cboPhp = new();
    private readonly ComboBox _cboDb = new();
    private readonly Button _btnInstallApache = new();
    private readonly Button _btnInstallPhp = new();
    private readonly Button _btnInstallDb = new();
    private readonly Label _lblApacheState = new();
    private readonly Label _lblPhpState = new();
    private readonly Label _lblDbState = new();
    private readonly Label _lblVcRedist = new();
    private readonly Button _btnVcRedist = new();
    private readonly CheckBox _chkService = new();
    private readonly ProgressBar _bar = new();
    private readonly Label _lblPhase = new();
    private readonly TextBox _txtLog = new();
    private readonly Button _btnClose = new();

    public InstallForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;
        _catalog = new CatalogService(log);

        Text = "ติดตั้งคอมโพเนนต์ — ดาวน์โหลดจากเว็บทางการ";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 640);
        MinimumSize = new Size(760, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        BuildUi();
        Theme.Apply(this);
        Load += async (s, e) =>
        {
            RefreshStates();
            // ดึงเวอร์ชันใหม่ล่าสุดจากเว็บทางการ (ถ้าออนไลน์) เพื่อให้เลือกได้เสมอ
            try
            {
                await _catalog.RefreshAsync(_dl.GetHttpClient(), _cfg.RemoteCatalogUrl);
                RefreshStates();
            }
            catch { /* ออฟไลน์ — ใช้แคตตาล็อกที่ pin ไว้ */ }
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));   // vc redist
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // apache
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // php
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // mariadb
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // adminer
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // options
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // log
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // bottom

        // ---- VC++ Redistributable ----
        var vcRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _lblVcRedist.AutoSize = true;
        _lblVcRedist.Padding = new Padding(0, 8, 8, 0);
        _btnVcRedist.Text = "ดาวน์โหลดและติดตั้ง VC++ Redistributable";
        _btnVcRedist.AutoSize = true;
        _btnVcRedist.Click += (s, e) => InstallVcRedist();
        vcRow.Controls.Add(_lblVcRedist);
        vcRow.Controls.Add(_btnVcRedist);
        root.Controls.Add(vcRow);

        // ---- 3 คอมโพเนนต์ ----
        root.Controls.Add(BuildComponentRow(
            "Apache (เว็บเซิร์ฟเวอร์)", _cboApache, _btnInstallApache, _lblApacheState, () => _btnInstallApache_Click()));
        root.Controls.Add(BuildComponentRow(
            "PHP (ภาษาสคริปต์ — ต้องเป็นรุ่น Thread Safe จึงโหลดผ่าน Apache ได้)", _cboPhp, _btnInstallPhp, _lblPhpState, () => _btnInstallPhp_Click()));
        root.Controls.Add(BuildComponentRow(
            "MariaDB (ฐานข้อมูล)", _cboDb, _btnInstallDb, _lblDbState, () => _btnInstallDb_Click()));

        // ---- Adminer (ไฟล์เดียว จัดการฐานข้อมูลผ่านเว็บ) ----
        var adminerRow = new GroupBox
        {
            Text = " Adminer — จัดการฐานข้อมูลผ่านเว็บ (ไฟล์เดียว ~500KB)",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 4, 10, 4),
        };
        var layA = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        layA.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layA.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layA.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        var lblAdminerDesc = new Label
        {
            Text = "ดาวน์โหลดเวอร์ชันล่าสุดจาก adminer.org — ใช้ได้เมื่อ PHP ติดตั้งแล้ว",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
        };
        _lblAdminer.Dock = DockStyle.Fill;
        _lblAdminer.TextAlign = ContentAlignment.MiddleLeft;
        _lblAdminer.ForeColor = Color.DimGray;
        _btnInstallAdminer.Text = "ดาวน์โหลด + ติดตั้ง";
        _btnInstallAdminer.Width = 180;
        _btnInstallAdminer.Margin = new Padding(0, 6, 0, 0);
        _btnInstallAdminer.Click += (s, e) => InstallAdminer();
        layA.Controls.Add(lblAdminerDesc, 0, 0);
        layA.Controls.Add(_lblAdminer, 1, 0);
        layA.Controls.Add(_btnInstallAdminer, 2, 0);
        adminerRow.Controls.Add(layA);
        root.Controls.Add(adminerRow);

        // ---- ตัวเลือก ----
        var optRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _chkService.Text = "ติดตั้งเป็น Windows Service (แนะนำ — เปิดเครื่องมาทำงานเอง)";
        _chkService.AutoSize = true;
        _chkService.Checked = true;
        optRow.Controls.Add(_chkService);
        root.Controls.Add(optRow);

        // ---- progress + log ----
        var progRow = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        progRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        progRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _bar.Dock = DockStyle.Fill;
        _lblPhase.AutoSize = true;
        _lblPhase.Text = "พร้อมติดตั้ง";
        var top = new Panel { Dock = DockStyle.Fill };
        top.Controls.Add(_bar);
        _bar.Location = new Point(0, 2);
        _bar.Width = top.Width - 4;
        top.Resize += (s, e) => _bar.Width = top.ClientSize.Width;
        progRow.Controls.Add(top, 0, 0);
        progRow.Controls.Add(_lblPhase, 0, 1);
        _lblPhase.Dock = DockStyle.Top;

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.ReadOnly = true;
        _txtLog.Multiline = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.BackColor = Color.FromArgb(24, 26, 32);
        _txtLog.ForeColor = Color.FromArgb(180, 220, 190);
        _txtLog.Font = new Font("Consolas", 9f);
        _txtLog.WordWrap = false;
        _txtLog.Text = _log.Tail(60);

        var logGroup = new GroupBox { Text = " ความคืบหน้า ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 6) };
        logGroup.Controls.Add(_txtLog);
        var logAndBar = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        logAndBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        logAndBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logAndBar.Controls.Add(top, 0, 0);
        logAndBar.Controls.Add(logGroup, 0, 1);
        root.Controls.Add(logAndBar);

        // ---- bottom ----
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _btnClose.Text = "ปิดหน้านี้";
        _btnClose.AutoSize = true;
        _btnClose.Click += (s, e) => Close();
        bottom.Controls.Add(_btnClose);
        root.Controls.Add(bottom);

        Controls.Add(root);
        Theme.Apply(this);
    }

    private GroupBox BuildComponentRow(string title, ComboBox cbo, Button btn, Label lbl, Action onClick)
    {
        var g = new GroupBox { Text = " " + title + " ", Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));

        cbo.Dock = DockStyle.Fill;
        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
        cbo.DropDownWidth = 380;
        cbo.Margin = new Padding(0, 8, 8, 0);

        lbl.AutoSize = false;
        lbl.Dock = DockStyle.Fill;
        lbl.TextAlign = ContentAlignment.MiddleLeft;
        lbl.ForeColor = Color.DimGray;
        lbl.Padding = new Padding(2, 2, 0, 0);

        btn.Text = "ดาวน์โหลด + ติดตั้ง";
        btn.Width = 180;
        btn.Margin = new Padding(0, 6, 0, 0);
        btn.Click += (s, e) => onClick();

        layout.Controls.Add(cbo, 0, 0);
        layout.Controls.Add(lbl, 1, 0);
        layout.Controls.Add(btn, 2, 0);
        g.Controls.Add(layout);
        return g;
    }

    private void FillCombo(ComboBox cbo, List<CatalogItem> items, string? installedVersion)
    {
        cbo.Items.Clear();
        foreach (var item in items)
        {
            var isInstalled = installedVersion == item.Version;
            cbo.Items.Add(new ComboCatalogItem(item, isInstalled));
        }
        // เลือกตัวที่ติดตั้งแล้ว ถ้าไม่มีเลือกตัวแรก
        int idx = 0;
        if (installedVersion != null)
        {
            for (int i = 0; i < cbo.Items.Count; i++)
                if (cbo.Items[i] is ComboCatalogItem c && c.Item.Version == installedVersion) { idx = i; break; }
        }
        cbo.SelectedIndex = idx;
    }

    private sealed record ComboCatalogItem(CatalogItem Item, bool IsInstalled)
    {
        public override string ToString() =>
            Item.DisplayName + (IsInstalled ? "  ✓ (ติดตั้งอยู่)" : "") +
            (Item.Notes != null ? "  — " + Item.Notes : "");
    }

    private CatalogItem? SelectedItem(ComboBox cbo) =>
        cbo.SelectedItem is ComboCatalogItem c ? c.Item : null;

    // ================= สถานะ =================
    private void RefreshStates()
    {
        FillCombo(_cboApache, _catalog.For("apache"), _cfg.ApacheInstalled?.Version);
        FillCombo(_cboPhp, _catalog.For("php"), _cfg.PhpInstalled?.Version);
        FillCombo(_cboDb, _catalog.For("mariadb"), _cfg.MariaDbInstalled?.Version);

        var apVer = File.Exists(_paths.ApacheBin) ? (_cfg.ApacheInstalled?.Version ?? "?") : null;
        var phpVer = File.Exists(_paths.PhpBin) ? (_cfg.PhpInstalled?.Version ?? "?") : null;
        var dbVer = File.Exists(_paths.MariaDbBin) ? (_cfg.MariaDbInstalled?.Version ?? "?") : null;

        _lblApacheState.Text = apVer == null ? "ยังไม่ได้ติดตั้ง" : $"ติดตั้งแล้ว: เวอร์ชัน {apVer}";
        _lblPhpState.Text = phpVer == null ? "ยังไม่ได้ติดตั้ง" : $"ติดตั้งแล้ว: เวอร์ชัน {phpVer}";
        _lblDbState.Text = dbVer == null ? "ยังไม่ได้ติดตั้ง" : $"ติดตั้งแล้ว: เวอร์ชัน {dbVer}";

        var adminerFile = Path.Combine(_paths.WwwDir, "adminer.php");
        _lblAdminer.Text = File.Exists(adminerFile)
            ? $"ติดตั้งแล้ว ✓ เปิดที่ http://localhost:{_cfg.Apache.Port}/adminer.php"
            : "ยังไม่ได้ติดตั้ง";

        var vcOk = VcRedistInstalled();
        _lblVcRedist.Text = vcOk
            ? "✓ VC++ Redistributable ติดตั้งอยู่แล้ว (Apache/PHP ใช้ได้ปกติ)"
            : "⚠ ยังไม่พบ VC++ Redistributable — Apache และ PHP จะเปิดไม่ได้ถ้าไม่ติดตั้งตัวนี้ก่อน";
        _lblVcRedist.ForeColor = vcOk ? Color.FromArgb(20, 130, 60) : Color.FromArgb(190, 100, 0);
        _btnVcRedist.Enabled = !vcOk;
    }

    private static bool VcRedistInstalled()
    {
        // วิธีหลัก: เช็คไฟล์ runtime DLL ที่ Apache/PHP ใช้จริงใน System32
        // (บางเครื่องไม่มี registry key แต่ runtime ใช้งานได้ปกติ)
        try
        {
            var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(sys, "vcruntime140.dll")) &&
                File.Exists(Path.Combine(sys, "vcruntime140_1.dll")) &&
                File.Exists(Path.Combine(sys, "msvcp140.dll")))
                return true;
        }
        catch { }

        // วิธีเสริม: ตรวจ registry ของ VC++ 2015-2022 runtime
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC.Runtimes\x64");
            if (key?.GetValue("Installed") is 1) return true;
            using var keyW = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var key2 = keyW.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC.Runtimes\x64");
            return key2?.GetValue("Installed") is 1;
        }
        catch { return false; }
    }

    private async void InstallVcRedist()
    {
        SetBusy(true);
        try
        {
            _lblPhase.Text = "ดาวน์โหลด VC++ Redistributable…";
            var dest = Path.Combine(_paths.DownloadsDir, "vc_redist.x64.exe");
            var dlProgress = new Progress<DownloadProgress>(p =>
            {
                _bar.Value = Math.Min(100, p.Percent);
                _lblPhase.Text = $"ดาวน์โหลด VC++ Redistributable… {p.Bytes / 1024.0 / 1024.0:F1} MB ({p.SpeedText})";
            });
            await _dl.DownloadAsync(VersionCatalog.VcRedistUrl, dest, dlProgress);

            _lblPhase.Text = "กำลังติดตั้ง VC++ Redistributable (หน้าต่างติดตั้งของ Microsoft จะแสดงขึ้นมาเอง)…";
            AppendLog("รันตัวติดตั้ง vc_redist.x64.exe (/passive)");
            var (code, stdout, stderr) = await Task.Run(() =>
                ProcessRunner.Run(dest, "/install /passive /norestart", null, 600_000));

            if (code == 0 || code == 3010) // 3010 = สำเร็จ (ต้อง restart เครื่อง)
            {
                _log.Info("ติดตั้ง VC++ Redistributable เรียบร้อย" + (code == 3010 ? " (แนะนำรีสตาร์ทเครื่อง)" : ""));
                MessageBox.Show(
                    "ติดตั้ง VC++ Redistributable เรียบร้อย" +
                    (code == 3010 ? "\n\nแนะนำให้รีสตาร์ทเครื่องก่อนเริ่มใช้ Apache" : ""),
                    "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("ติดตั้งไม่สำเร็จ (exit code " + code + ")\n" + (stdout + stderr).Trim(),
                    "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("เกิดข้อผิดพลาด:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
            RefreshStates();
        }
    }

    private void SetBusy(bool busy)
    {
        _btnInstallApache.Enabled = _btnInstallPhp.Enabled = _btnInstallDb.Enabled = !busy;
        _btnInstallAdminer.Enabled = !busy;
        _btnVcRedist.Enabled = !busy && !VcRedistInstalled();
        _btnClose.Enabled = !busy;
        _chkService.Enabled = !busy;
        UseWaitCursor = busy;
        if (!busy) _bar.Value = 0;
    }

    private void AppendLog(string line)
    {
        _txtLog.AppendText(line + Environment.NewLine);
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    // ================= ติดตั้งแต่ละตัว =================
    private void _btnInstallApache_Click() => RunInstall("apache", _cboApache);
    private void _btnInstallPhp_Click() => RunInstall("php", _cboPhp);
    private void _btnInstallDb_Click() => RunInstall("mariadb", _cboDb);

    private async void RunInstall(string component, ComboBox cbo)
    {
        var item = SelectedItem(cbo);
        if (item == null) return;

        // เตือนพอร์ตก่อนเริ่ม (Apache/MariaDB) — พร้อมเสนอพอร์ตว่างอัตโนมัติ
        var port = component == "apache" ? _cfg.Apache.Port : component == "mariadb" ? _cfg.MariaDb.Port : 0;
        if (port > 0 && !PortChecker.IsAvailable(port))
        {
            var r = MessageBox.Show(
                PortChecker.ConflictMessage(port, component == "apache" ? "Apache" : "MariaDB") +
                "\n\n\"ใช่\" = ติดตั้งต่อไป (เซอร์วิสจะเริ่มไม่ได้จนกว่าจะแก้พอร์ต)\n" +
                "\"ไม่ใช่\" = เปลี่ยนเป็นพอร์ตว่างที่แนะนำอัตโนมัติ แล้วติดตั้งต่อ\n" +
                "\"ยกเลิก\" = ยกเลิกการติดตั้ง",
                "พอร์ตถูกใช้งานอยู่", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) return;

            if (r == DialogResult.No)
            {
                var suggested = PortChecker.SuggestFreePort(port);
                if (suggested == 0)
                {
                    MessageBox.Show("ไม่พบพอร์ตว่างใกล้ ๆ พอร์ต " + port + " — ยกเลิกการติดตั้ง",
                        "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (component == "apache") _cfg.Apache.Port = suggested;
                else _cfg.MariaDb.Port = suggested;
                _cfg.Save(_paths);
                var cwFix = new ConfigWriter(_paths, _cfg, _log);
                if (component == "apache") cwFix.WriteApacheConf();
                else cwFix.WriteMariaDbIni();
                MessageBox.Show($"เปลี่ยนพอร์ตเป็น {suggested} แล้ว (config เขียนใหม่ให้เรียบร้อย) — ติดตั้งต่อเลย",
                    "พอร์ตใหม่", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        SetBusy(true);
        _bar.Value = 0;
        var plog = new ProgressLogFilter(item.DisplayName, AppendLog);
        var progress = new Progress<InstallProgress>(p => plog.Handle(p, _bar, _lblPhase));

        bool ok = false;
        try
        {
            ComponentManager mgr = component switch
            {
                "apache" => new ApacheManager(_paths, _cfg, _log, _dl),
                "php" => new PhpManager(_paths, _cfg, _log, _dl),
                _ => new MariaDbManager(_paths, _cfg, _log, _dl),
            };

            await mgr.InstallAsync(item, progress, _chkService.Checked);
            AppendLog($"✓ ติดตั้ง {item.DisplayName} เสร็จสมบูรณ์");
            ok = true;
        }
        catch (Exception ex)
        {
            AppendLog($"✗ ติดตั้งไม่สำเร็จ: {ex.Message}");
            MessageBox.Show("ติดตั้งไม่สำเร็จ:\n\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshStates();
        }

        if (ok && _chkService.Checked && MessageBox.Show(
                $"ติดตั้ง {item.DisplayName} เรียบร้อย!\n\nต้องการเริ่มใช้งานเลยหรือไม่?",
                "ติดตั้งสำเร็จ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var stack = new StackController(_paths, _cfg, _log);
            try
            {
                if (component == "apache") stack.StartApache();
                if (component == "mariadb") stack.StartMariaDb();
                if (component == "php")
                {
                    stack.StartApache();
                }
                MessageBox.Show("เริ่มใช้งานแล้ว! เปิดเว็บได้ที่ " + stack.LocalUrl, "พร้อมใช้งาน",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("เริ่มเซอร์วิสไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- MariaDB: เตือนตั้งรหัสผ่าน root ถ้ายังว่าง ----
        if (ok && component == "mariadb")
        {
            try
            {
                var db = new MariaDbManager(_paths, _cfg, _log, _dl);
                var (code, _) = db.RunSql("SELECT 1");
                if (code == 0 && MessageBox.Show(
                    "root ของ MariaDB ยังไม่มีรหัสผ่าน (ถ้าเครื่องอื่นเข้าถึงพอร์ตได้ ก็เข้าฐานข้อมูลได้เลย)\n\n" +
                    "ตั้งรหัสผ่านเดี๋ยวนี้ไหม?",
                    "ความปลอดภัย", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    using var dlg = new PasswordDialog();
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        db.SetRootPassword(dlg.NewPassword, dlg.OldPassword);
                        MessageBox.Show("ตั้งรหัสผ่าน root เรียบร้อย", "สำเร็จ",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch { /* MariaDB ยังไม่เริ่ม — ข้าม */ }
        }
    }

    /// <summary>ติดตั้ง Adminer (ไฟล์เดียว adminer.php ลง www\)</summary>
    private async void InstallAdminer()
    {
        SetBusy(true);
        try
        {
            _bar.Value = 0;
            _lblPhase.Text = "ดาวน์โหลด Adminer…";
            var dest = Path.Combine(_paths.WwwDir, "adminer.php");
            if (File.Exists(dest))
                File.Copy(dest, dest + ".bak", overwrite: true); // สำรองตัวเก่า

            var dlProgress = new Progress<DownloadProgress>(p =>
            {
                _bar.Value = Math.Min(100, p.Percent);
                _lblPhase.Text = $"ดาวน์โหลด Adminer… {p.Bytes / 1024.0 / 1024.0:F2} MB ({p.SpeedText})";
            });
            await _dl.DownloadAsync("https://www.adminer.org/latest.php", dest, dlProgress);

            var header = File.ReadLines(dest).Take(3).FirstOrDefault(l => l.Contains("Adminer")) ?? "";
            var m = Regex.Match(header, @"(\d+\.\d+\.\d+)");
            var ver = m.Success ? "v" + m.Groups[1].Value : "เวอร์ชันล่าสุด";
            AppendLog($"✓ ติดตั้ง Adminer {ver} เสร็จ → www\\adminer.php");
            RefreshStates();

            if (MessageBox.Show(
                $"ติดตั้ง Adminer {ver} เรียบร้อย!\n\nเปิดหน้าจัดการฐานข้อมูลเลยไหม?\n" +
                $"http://localhost:{_cfg.Apache.Port}/adminer.php",
                "สำเร็จ", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                StackController.OpenBrowser($"http://localhost:{_cfg.Apache.Port}/adminer.php");
            }
        }
        catch (Exception ex)
        {
            AppendLog("✗ ติดตั้ง Adminer ไม่สำเร็จ: " + ex.Message);
            MessageBox.Show("ติดตั้ง Adminer ไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshStates();
        }
    }
}
