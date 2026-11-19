using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// ดูไฟล์ log ของ Apache / PHP / MariaDB / ตัวโปรแกรมเอง พร้อมรีเฟรชอัตโนมัติ
/// </summary>
public sealed class LogsForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;

    private readonly ComboBox _cboLog = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly TextBox _txtLog = new();
    private readonly Button _btnRefresh = new() { Text = "รีเฟรช" };
    private readonly Button _btnStats = new() { Text = "สรุปสถิติ access.log" };
    private readonly Button _btnOpenFolder = new() { Text = "เปิดโฟลเดอร์ logs" };
    private readonly Button _btnClear = new() { Text = "ล้างไฟล์นี้" };
    private readonly CheckBox _chkAuto = new() { Text = "รีเฟรชอัตโนมัติ (ทุก 3 วิ)", Checked = true };
    private readonly Label _lblInfo = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };

    public LogsForm(ServerPaths paths, AppConfig cfg)
    {
        _paths = paths;
        _cfg = cfg;

        Text = "ดู Logs";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(920, 640);

        _cboLog.Items.AddRange(new object[]
        {
            new LogChoice("Apache — error.log", paths.ApacheErrorLog),
            new LogChoice("Apache — access.log", paths.ApacheAccessLog),
            new LogChoice("PHP — error log", paths.PhpErrorLog),
            new LogChoice("MariaDB — error log", paths.MariaDbErrorLog),
            new LogChoice("zServer Manager — บันทึกเหตุการณ์โปรแกรม", paths.ManagerLog),
        });
        _cboLog.SelectedIndex = 0;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 0) };
        top.Controls.Add(_cboLog);
        top.Controls.Add(_btnRefresh);
        top.Controls.Add(_chkAuto);
        top.Controls.Add(_btnStats);
        top.Controls.Add(_btnOpenFolder);
        top.Controls.Add(_btnClear);

        _lblInfo.Dock = DockStyle.Top;
        _lblInfo.Height = 24;
        _lblInfo.Padding = new Padding(12, 2, 0, 0);

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.ReadOnly = true;
        _txtLog.Multiline = true;
        _txtLog.ScrollBars = ScrollBars.Both;
        _txtLog.WordWrap = false;
        _txtLog.Font = new Font("Consolas", 9.5f);
        _txtLog.BackColor = Color.FromArgb(24, 26, 32);
        _txtLog.ForeColor = Color.FromArgb(210, 220, 230);

        Controls.Add(_txtLog);
        Controls.Add(_lblInfo);
        Controls.Add(top);
        Theme.Apply(this);

        _cboLog.SelectedIndexChanged += (s, e) => LoadLog();
        _btnRefresh.Click += (s, e) => LoadLog();
        _btnStats.Click += (s, e) =>
        {
            var f = new AccessSummaryForm(_paths, _cfg) { StartPosition = FormStartPosition.CenterParent };
            f.Show(this);
        };
        _btnOpenFolder.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + _paths.LogsDir + "\""); } catch { }
        };
        _btnClear.Click += (s, e) =>
        {
            var choice = Current();
            if (!File.Exists(choice.Path)) return;
            var ans = MessageBox.Show("ล้างเนื้อหาไฟล์ " + Path.GetFileName(choice.Path) + "?",
                "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans == DialogResult.Yes)
            {
                File.WriteAllText(choice.Path, "");
                LoadLog();
            }
        };
        _chkAuto.CheckedChanged += (s, e) => _timer.Enabled = _chkAuto.Checked;
        _timer.Tick += (s, e) => LoadLog();

        Load += (s, e) => { LoadLog(); _timer.Start(); };
        FormClosed += (s, e) => _timer.Stop();
    }

    private LogChoice Current() => (LogChoice)_cboLog.SelectedItem!;

    private sealed record LogChoice(string Name, string Path)
    {
        public override string ToString() => Name;
    }

    private void LoadLog()
    {
        var choice = Current();
        try
        {
            if (!File.Exists(choice.Path))
            {
                _txtLog.Text = "(ยังไม่มีไฟล์ log นี้ — มักจะเกิดขึ้นหลังจากเริ่มใช้งานครั้งแรก)";
                _lblInfo.Text = choice.Path;
                return;
            }

            // อ่านท้ายไฟล์ 500 บรรทัดสุดท้ายพอ
            var lines = File.ReadAllLines(choice.Path);
            if (lines.Length > 500)
                _txtLog.Text = $"… (เหลือ {lines.Length - 500} บรรทัดเก่ากว่านี้ — เปิดไฟล์จริงเพื่อดูทั้งหมด)\n" +
                               string.Join(Environment.NewLine, lines[^500..]);
            else
                _txtLog.Text = string.Join(Environment.NewLine, lines);
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
            _lblInfo.Text = choice.Path + "  (" + new FileInfo(choice.Path).Length / 1024 + " KB)";
        }
        catch (Exception ex)
        {
            _txtLog.Text = "(อ่านไฟล์ไม่สำเร็จ: " + ex.Message + ")";
        }
    }
}
