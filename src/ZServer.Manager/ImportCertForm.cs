using System.Text.RegularExpressions;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// นำเข้าใบรับรองจริงที่ซื้อมา (.crt/.key/.pem/.pfx)
/// — โปรแกรมแปลงเป็น PEM + ต่อ CA chain + ตรวจว่า key ตรงกับ cert ให้อัตโนมัติ
/// </summary>
public sealed class ImportCertForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;
    private readonly StackController _stack;
    private readonly ConfigWriter _cw;

    private readonly TextBox _txtCertPath = new();
    private readonly TextBox _txtKeyPath = new();
    private readonly TextBox _txtCaBundle = new();
    private readonly TextBox _txtKeyPass = new() { UseSystemPasswordChar = true };
    private readonly Button _btnImport = new() { Text = "นำเข้าและตรวจสอบ", Width = 220 };
    private readonly Label _lblState = new() { AutoSize = true, ForeColor = Color.DimGray };

    public ImportCertForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;
        _stack = new StackController(paths, cfg, log);
        _cw = new ConfigWriter(paths, cfg, log);

        Text = "นำเข้าใบรับรองจริง (ซื้อมา) — .crt / .key / .pem / .pfx";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 480);
        MinimumSize = new Size(760, 480);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // cert
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // key
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // ca bundle
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // key pass
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // state + buttons

        root.Controls.Add(FileRow("ไฟล์ใบรับรอง (.crt/.pem/.cer) หรือ .pfx/.p12 ไฟล์เดียว", _txtCertPath, PickCert));
        root.Controls.Add(FileRow("ไฟล์ private key (.key/.pem) — ถ้าเป็น .pfx เว้นว่างได้", _txtKeyPath, PickKey));
        root.Controls.Add(FileRow("ไฟล์ CA bundle / intermediate (ถ้ามี — ทำให้ chain ครบ)", _txtCaBundle, PickBundle));

        var passRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        passRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        passRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _txtKeyPass.Dock = DockStyle.Fill;
        _txtKeyPass.Margin = new Padding(0, 6, 0, 0);
        var lblPass = new Label
        {
            Text = "รหัสผ่านของ key/pfx (ถ้ามี)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 8, 0),
        };
        passRow.Controls.Add(lblPass, 0, 0);
        passRow.Controls.Add(_txtKeyPass, 1, 0);
        root.Controls.Add(passRow);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        _btnImport.Click += (s, e) => Import();
        bottom.Controls.Add(_btnImport);
        bottom.Controls.Add(_lblState);
        root.Controls.Add(bottom);

        Controls.Add(root);
        Theme.Apply(this);
    }

    private GroupBox FileRow(string title, TextBox txt, Action pick)
    {
        var g = new GroupBox { Text = " " + title + " ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        txt.Dock = DockStyle.Fill;
        txt.Margin = new Padding(0, 6, 0, 0);
        g.Controls.Add(txt);
        txt.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) pick(); };
        g.DoubleClick += (s, e) => pick();
        return g;
    }

    private void PickCert()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "เลือกไฟล์ใบรับรอง (.crt / .pem / .cer / .pfx / .p12)",
            Filter = "ใบรับรอง (*.crt;*.pem;*.cer;*.pfx;*.p12)|*.crt;*.pem;*.cer;*.pfx;*.p12|ทุกไฟล์|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) _txtCertPath.Text = dlg.FileName;
    }

    private void PickKey()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "เลือกไฟล์ private key (.key / .pem)",
            Filter = "Private key (*.key;*.pem)|*.key;*.pem|ทุกไฟล์|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) _txtKeyPath.Text = dlg.FileName;
    }

    private void PickBundle()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "เลือกไฟล์ CA bundle / intermediate (ถ้ามี)",
            Filter = "CA bundle (*.crt;*.pem)|*.crt;*.pem|ทุกไฟล์|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) _txtCaBundle.Text = dlg.FileName;
    }

    private async void Import()
    {
        var certPath = _txtCertPath.Text.Trim();
        if (!File.Exists(certPath))
        {
            MessageBox.Show("เลือกไฟล์ใบรับรองก่อน (กดดับเบิลคลิกที่ช่องเพื่อเลือกไฟล์)", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _btnImport.Enabled = false;
        _lblState.Text = "กำลังนำเข้าและตรวจสอบ…";
        UseWaitCursor = true;
        try
        {
            var keyPath = _txtKeyPath.Text.Trim();
            if (string.IsNullOrEmpty(keyPath) && !Regex.IsMatch(certPath, @"\.(pfx|p12)$", RegexOptions.IgnoreCase))
            {
                MessageBox.Show("ยังไม่ได้เลือกไฟล์ private key (.key)", "ตรวจสอบ",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = await Task.Run(() => HttpsHelper.ImportCommercialCert(
                _paths, _log, certPath,
                keyPath.Length > 0 ? keyPath : null,
                _txtCaBundle.Text.Trim().Length > 0 ? _txtCaBundle.Text.Trim() : null,
                _txtKeyPass.Text.Length > 0 ? _txtKeyPass.Text : null));

            if (result.Info == null)
                throw new InvalidOperationException("นำเข้าแล้วแต่อ่านข้อมูลใบรับรองไม่ได้");

            _lblState.Text = $"นำเข้าสำเร็จ ✓ {result.Info.Subject} — หมดอายุ {result.Info.NotAfter}";
            _log.Info($"นำเข้าใบรับรองจริงสำเร็จ: {result.Info.Subject} ครอบคลุม {string.Join(", ", result.Info.Domains)}");

            var chainNote = string.IsNullOrWhiteSpace(_txtCaBundle.Text) && !Regex.IsMatch(certPath, @"\.(pfx|p12)$", RegexOptions.IgnoreCase)
                ? "\n\n⚠ ไม่ได้ใส่ CA bundle — บาง browser/มือถืออาจเตือน chain ไม่ครบ (ขอไฟล์ ca-bundle จากผู้ขายแล้วนำเข้าอีกครั้งเพื่อแก้)"
                : "";

            var msg = $"นำเข้าใบรับรองจริงสำเร็จ ✓\n\n" +
                      $"Subject: {result.Info.Subject}\n" +
                      $"หมดอายุ: {result.Info.NotAfter}\n" +
                      $"โดเมน: {string.Join(", ", result.Info.Domains)}" + chainNote +
                      $"\n\nเปิด HTTPS และรีสตาร์ท Apache เดี๋ยวนี้ไหม?";

            if (MessageBox.Show(msg, "นำเข้าสำเร็จ", MessageBoxButtons.YesNo,
                MessageBoxIcon.Information) == DialogResult.Yes)
            {
                EnableHttpsAndRestart();
            }
        }
        catch (Exception ex)
        {
            _lblState.Text = "นำเข้าไม่สำเร็จ";
            MessageBox.Show("นำเข้าไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnImport.Enabled = true;
            UseWaitCursor = false;
        }
    }

    /// <summary>เปิด HTTPS ด้วยใบรับรองที่เพิ่งนำเข้า (config + รีสตาร์ท Apache ถ้ากำลังรัน)</summary>
    private void EnableHttpsAndRestart()
    {
        try
        {
            _cfg.Apache.EnableHttps = true;
            _cfg.Save(_paths);
            _cw.WriteApacheConf();

            var running = _stack.GetApacheStatus().State == ComponentState.Running;
            if (running)
            {
                _stack.RestartApache();
                _log.Info("เปิด HTTPS ด้วยใบรับรองจริงแล้ว (รีสตาร์ท Apache สำเร็จ)");
            }
            MessageBox.Show("เปิด HTTPS ด้วยใบรับรองจริงเรียบร้อย ✓" +
                (running ? "\nทดสอบได้ที่ https://" + _cfg.Apache.ServerName : "\nเริ่ม Apache แล้วเข้าได้ที่ https://"),
                "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เปิด HTTPS ไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
