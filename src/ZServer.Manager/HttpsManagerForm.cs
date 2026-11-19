using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// จัดการ HTTPS ในที่เดียว: สร้าง/ต่ออายุใบรับรอง (mkcert ครอบคลุมทุกโดเมน)
/// → เปิด/ปิด HTTPS → รีสตาร์ท Apache → เปิด Firewall — แสดงสถานะจริงจาก openssl
/// </summary>
public sealed class HttpsManagerForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;
    private readonly StackController _stack;
    private readonly ConfigWriter _cw;

    private readonly Label _lblStep1 = new() { AutoSize = true };
    private readonly Label _lblStep2 = new() { AutoSize = true };
    private readonly Label _lblStep3 = new() { AutoSize = true };
    private readonly Label _lblCert = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _lblDomains = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _lblHttps = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly CheckBox _chkEnable = new() { Text = "เปิด HTTPS", AutoSize = true };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Width = 90 };
    private readonly Button _btnCreate = new() { Text = "① สร้าง / ต่ออายุใบรับรอง", Width = 250 };
    private readonly Button _btnApply = new() { Text = "② บันทึกและใช้ HTTPS", Width = 250 };
    private readonly Button _btnRestart = new() { Text = "③ รีสตาร์ท Apache เดี๋ยวนี้", Width = 250 };
    private readonly Button _btnFirewall = new() { Text = "เปิดพอร์ต 443 ใน Firewall", Width = 250 };
    private readonly Button _btnTest = new() { Text = "ทดสอบเปิด https://localhost", Width = 250 };
    private readonly Label _lblState = new() { AutoSize = true, ForeColor = Color.DimGray };
    private bool _needRestart;

    public HttpsManagerForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;
        _stack = new StackController(paths, cfg, log);
        _cw = new ConfigWriter(paths, cfg, log);

        Text = "จัดการ HTTPS — ใบรับรองสำหรับพัฒนางาน (mkcert)";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 600);
        MinimumSize = new Size(760, 600);

        BuildUi();
        Theme.Apply(this);
        Load += (s, e) => RefreshState();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // ① cert
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // ② enable
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));  // ③ restart/test/firewall
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // state
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // note

        // ---- ① ใบรับรอง ----
        var g1 = new GroupBox { Text = " ① ใบรับรอง (ครอบคลุม localhost + Virtual Host ทุกตัว) ", Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 4) };
        var l1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        l1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        l1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _lblStep1.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblStep1.Dock = DockStyle.Fill;
        _lblCert.Dock = DockStyle.Fill;
        _lblDomains.Dock = DockStyle.Fill;
        l1.Controls.Add(_lblStep1, 0, 0);
        l1.Controls.Add(_lblCert, 1, 0);
        l1.Controls.Add(_btnCreate, 0, 1);
        l1.Controls.Add(_lblDomains, 1, 1);
        g1.Controls.Add(l1);
        root.Controls.Add(g1);

        // ---- ② เปิด HTTPS ----
        var g2 = new GroupBox { Text = " ② เปิดใช้ HTTPS ", Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 4) };
        var l2 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        l2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        l2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _lblStep2.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblStep2.Dock = DockStyle.Fill;
        _lblHttps.Dock = DockStyle.Fill;
        var flowEnable = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        flowEnable.Controls.Add(_chkEnable);
        flowEnable.Controls.Add(new Label { Text = "พอร์ต:", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        flowEnable.Controls.Add(_numPort);
        l2.Controls.Add(_lblStep2, 0, 0);
        l2.Controls.Add(_lblHttps, 1, 0);
        l2.Controls.Add(_btnApply, 0, 1);
        l2.Controls.Add(flowEnable, 1, 1);
        g2.Controls.Add(l2);
        root.Controls.Add(g2);

        // ---- ③ ใช้งาน ----
        var g3 = new GroupBox { Text = " ③ ใช้งานจริง ", Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 4) };
        var l3 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        l3.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        l3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _lblStep3.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblStep3.Dock = DockStyle.Fill;
        l3.Controls.Add(_lblStep3, 0, 0);
        l3.Controls.Add(_btnRestart, 0, 1);
        l3.Controls.Add(_btnFirewall, 1, 0);
        l3.Controls.Add(_btnTest, 1, 1);
        g3.Controls.Add(l3);
        root.Controls.Add(g3);

        // ---- สถานะ ----
        var gState = new GroupBox { Text = " บันทึก ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        var stateFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        stateFlow.Controls.Add(_lblState);
        gState.Controls.Add(stateFlow);
        root.Controls.Add(gState);

        // ---- หมายเหตุ ----
        var note = new Label
        {
            Text = "ใบรับรองนี้สำหรับพัฒนางานบนเครื่องนี้ (browser เชื่อเพราะติดตั้ง CA ของ mkcert ไว้) — เว็บจริงบน server ใช้ Let's Encrypt",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 14, 0, 0),
        };
        root.Controls.Add(note);

        _btnCreate.Click += (s, e) => CreateCert();
        _btnApply.Click += (s, e) => ApplyHttps();
        _btnRestart.Click += (s, e) => RestartApache();
        _btnFirewall.Click += (s, e) => OpenFirewall();
        _btnTest.Click += (s, e) =>
        {
            try { StackController.OpenBrowser($"https://localhost:{_cfg.Apache.HttpsPort}"); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        };

        Controls.Add(root);
    }

    // ================= สถานะ =================
    private void RefreshState()
    {
        var cert = HttpsHelper.GetCertInfo(_paths);
        if (cert == null)
        {
            _lblStep1.Text = "① ยังไม่มีใบรับรอง";
            _lblStep1.ForeColor = Color.Firebrick;
            _lblCert.Text = "กดปุ่มด้านล่างเพื่อสร้าง (ดาวน์โหลด mkcert + ติดตั้ง CA อัตโนมัติ)";
            _lblDomains.Text = "";
        }
        else
        {
            _lblStep1.Text = "① มีใบรับรองแล้ว ✓";
            _lblStep1.ForeColor = Color.FromArgb(20, 130, 60);
            _lblCert.Text = $"{cert.Subject} — หมดอายุ {cert.NotAfter}";
            _lblDomains.Text = "ครอบคลุม: " + string.Join(", ", cert.Domains);
        }

        _chkEnable.Checked = _cfg.Apache.EnableHttps;
        _numPort.Value = Math.Clamp(_cfg.Apache.HttpsPort, 1, 65535);
        _lblStep2.Text = _cfg.Apache.EnableHttps ? "② เปิดใช้อยู่ ✓" : "② ยังปิดอยู่";
        _lblStep2.ForeColor = _cfg.Apache.EnableHttps ? Color.FromArgb(20, 130, 60) : Color.DimGray;
        _lblHttps.Text = "เว็บหลัก + Virtual Host ทุกตัวจะมีทั้ง http และ https";
        _btnApply.Text = _cfg.Apache.EnableHttps ? "② ปิด HTTPS" : "② บันทึกและเปิด HTTPS";

        _lblStep3.Text = _needRestart ? "③ ⚠ มีการเปลี่ยนแปลง — รีสตาร์ท Apache จึงจะมีผล" : "③ config ตรงกับที่ Apache ใช้อยู่";
        _lblStep3.ForeColor = _needRestart ? Color.FromArgb(190, 100, 0) : Color.DimGray;
    }

    // ================= ① สร้าง/ต่ออายุใบรับรอง =================
    private async void CreateCert()
    {
        if (!File.Exists(_paths.ApacheBin))
        {
            MessageBox.Show("ยังไม่ได้ติดตั้ง Apache", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var names = new List<string> { _cfg.Apache.ServerName };
        names.AddRange(_cfg.Apache.VirtualHosts.Where(v => v.Enabled).Select(v => v.Name));
        names = names.Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        _btnCreate.Enabled = false;
        UseWaitCursor = true;
        _lblState.Text = "กำลังสร้างใบรับรอง… (โหลด mkcert + ติดตั้ง CA ครั้งแรกใช้เวลาสักครู่)";
        try
        {
            await HttpsHelper.SetupAsync(_paths, _log, _dl, names);
            _needRestart = true;
            _lblState.Text = "สร้างใบรับรองเรียบร้อย ✓ — CA ของ mkcert ติดตั้งเข้า Windows แล้ว browser จะเชื่อ https://localhost ทันที";
            _log.Info("สร้างใบรับรอง HTTPS (mkcert) ครอบคลุม: " + string.Join(", ", names));
        }
        catch (Exception ex)
        {
            _lblState.Text = "สร้างใบรับรองไม่สำเร็จ";
            MessageBox.Show("สร้างใบรับรองไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnCreate.Enabled = true;
            UseWaitCursor = false;
            RefreshState();
        }
    }

    // ================= ② เปิด/ปิด HTTPS =================
    private void ApplyHttps()
    {
        var enable = _chkEnable.Checked;
        if (enable && !HttpsHelper.IsCertReady(_paths))
        {
            MessageBox.Show("ยังไม่มีใบรับรอง — กด \"① สร้าง / ต่ออายุใบรับรอง\" ก่อน",
                "ตรวจสอบ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _cfg.Apache.EnableHttps = enable;
        _cfg.Apache.HttpsPort = (int)_numPort.Value;
        _cfg.Save(_paths);
        _cw.WriteApacheConf(); // ครอบ vhosts conf ด้วย
        _needRestart = true;
        _log.Info((enable ? "เปิด" : "ปิด") + $" HTTPS (พอร์ต {_cfg.Apache.HttpsPort})");

        RefreshState();

        var stack = _stack;
        var running = stack.GetApacheStatus().State == ComponentState.Running;
        if (running && MessageBox.Show(
            $"บันทึกแล้ว ✓ — Apache กำลังทำงานอยู่ รีสตาร์ทเดี๋ยวนี้เพื่อให้ HTTPS มีผลไหม?",
            "รีสตาร์ท Apache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            RestartApache();
    }

    // ================= ③ รีสตาร์ท / Firewall / ทดสอบ =================
    private void RestartApache()
    {
        _btnRestart.Enabled = false;
        UseWaitCursor = true;
        try
        {
            _stack.RestartApache();
            _needRestart = false;
            _lblState.Text = "รีสตาร์ท Apache แล้ว ✓ — ทดสอบได้ที่ https://localhost";
        }
        catch (Exception ex)
        {
            MessageBox.Show("รีสตาร์ทไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnRestart.Enabled = true;
            UseWaitCursor = false;
            RefreshState();
        }
    }

    private void OpenFirewall()
    {
        try
        {
            FirewallHelper.AllowPort(_cfg.Apache.HttpsPort, "zServer Apache HTTPS");
            MessageBox.Show($"เปิดพอร์ต {_cfg.Apache.HttpsPort} (HTTPS) ใน Firewall เรียบร้อย",
                "Firewall", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("เปิดพอร์ตไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
