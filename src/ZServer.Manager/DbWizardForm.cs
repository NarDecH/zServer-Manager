using System.Text;
using System.Text.RegularExpressions;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// จัดการฐานข้อมูล: สร้าง database + user + grant สิทธิ์ในคลิกเดียว
/// พร้อมแสดง connection string (mysqli / PDO) ที่คัดลอกไปวางในโค้ดได้ทันที
/// </summary>
public sealed class DbWizardForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;

    private readonly TextBox _txtRootPass = new() { UseSystemPasswordChar = true };
    private readonly TextBox _txtDbName = new();
    private readonly TextBox _txtUserName = new();
    private readonly TextBox _txtUserPass = new() { UseSystemPasswordChar = true };
    private readonly TextBox _txtUserPass2 = new() { UseSystemPasswordChar = true };
    private readonly Button _btnGenerate = new() { Text = "สุ่มรหัสผ่าน", Width = 120 };
    private readonly Button _btnCreate = new() { Text = "สร้าง Database + User", Width = 220 };
    private readonly ListBox _lstDatabases = new();
    private readonly Button _btnRefreshDb = new() { Text = "รีเฟรชรายการ", Width = 150 };
    private readonly TextBox _txtResult = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 9.5f),
        BackColor = Theme.LogBg,
        ForeColor = Color.FromArgb(180, 220, 190),
    };
    private readonly Button _btnCopy = new() { Text = "คัดลอก connection string", Width = 220 };
    private readonly Label _lblState = new() { AutoSize = true, ForeColor = Color.DimGray };
    private string _lastConnection = "";

    public DbWizardForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;

        Text = "จัดการฐานข้อมูล — สร้าง Database + User";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 640);
        MinimumSize = new Size(860, 640);

        BuildUi();
        Theme.Apply(this);
        Load += (s, e) => RefreshDatabases();
    }

    private MariaDbManager Db => new(_paths, _cfg, _log, _dl);

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // root pass
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));  // สร้างใหม่
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // ผลลัพธ์ + รายการ
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // สถานะ

        // ---- รหัสผ่าน root ----
        var gRoot = new GroupBox { Text = " รหัสผ่าน root (เว้นว่างถ้าไม่เคยตั้ง) ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        _txtRootPass.Dock = DockStyle.Fill;
        _txtRootPass.Margin = new Padding(0, 6, 0, 0);
        gRoot.Controls.Add(_txtRootPass);
        root.Controls.Add(gRoot);

        // ---- สร้างใหม่ ----
        var gCreate = new GroupBox { Text = " สร้างฐานข้อมูลใหม่ ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        var lay = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        void Row(int i, string lbl, Control input)
        {
            lay.Controls.Add(new Label { Text = lbl, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 8, 0) }, 0, i);
            lay.Controls.Add(input, 1, i);
        }

        _txtDbName.Margin = new Padding(0, 4, 0, 0);
        _txtUserName.Margin = new Padding(0, 4, 0, 0);
        Row(0, "ชื่อ database", _txtDbName);
        Row(1, "ชื่อ user", _txtUserName);
        Row(2, "รหัสผ่าน user", WrapInput(_txtUserPass, _btnGenerate));
        Row(3, "ยืนยันรหัสผ่าน", _txtUserPass2);
        _txtUserPass2.Margin = new Padding(0, 4, 0, 0);

        gCreate.Controls.Add(lay);
        root.Controls.Add(gCreate);

        // ---- ผลลัพธ์ + รายการที่มี ----
        var gResult = new GroupBox { Text = " Connection string + ฐานข้อมูลที่มีอยู่ ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _txtResult.Dock = DockStyle.Fill;
        var flowCopy = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        _btnCreate.Click += (s, e) => Create();
        flowCopy.Controls.Add(_btnCreate);
        flowCopy.Controls.Add(_btnCopy);
        left.Controls.Add(_txtResult, 0, 0);
        left.Controls.Add(flowCopy, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _lstDatabases.Dock = DockStyle.Fill;
        var flowDb = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        _btnRefreshDb.Click += (s, e) => RefreshDatabases();
        flowDb.Controls.Add(_btnRefreshDb);
        right.Controls.Add(_lstDatabases, 0, 0);
        right.Controls.Add(flowDb, 0, 1);

        split.Controls.Add(left, 0, 0);
        split.Controls.Add(right, 1, 0);
        gResult.Controls.Add(split);
        root.Controls.Add(gResult);

        root.Controls.Add(_lblState);

        _btnGenerate.Click += (s, e) =>
        {
            var pw = GeneratePassword();
            _txtUserPass.Text = pw;
            _txtUserPass2.Text = pw;
        };
        _btnCopy.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_lastConnection)) Clipboard.SetText(_lastConnection);
            _lblState.Text = "คัดลอก connection string แล้ว ✓";
        };
        _txtDbName.TextChanged += (s, e) =>
        {
            // ชื่อ user ตามชื่อ database อัตโนมัติ (จนกว่าจะพิมพ์เอง)
            _txtUserName.Text = _txtDbName.Text.Trim();
        };

        Controls.Add(root);
    }

    private static Control WrapInput(Control input, Button btn)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        input.Width = 220;
        flow.Controls.Add(input);
        flow.Controls.Add(btn);
        return flow;
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%_-";
        var sb = new StringBuilder();
        var rng = Random.Shared;
        for (int i = 0; i < 16; i++)
            sb.Append(chars[rng.Next(chars.Length)]);
        return sb.ToString();
    }

    private void RefreshDatabases()
    {
        try
        {
            _lblState.Text = "กำลังโหลดรายการฐานข้อมูล…";
            var dbs = Db.ListDatabases(_txtRootPass.Text.Length > 0 ? _txtRootPass.Text : null);
            _lstDatabases.Items.Clear();
            if (dbs.Count == 0)
            {
                _lstDatabases.Items.Add("(เชื่อมต่อไม่ได้ หรือยังไม่มีฐานข้อมูล)");
                _lblState.Text = dbs.Count == 0 && !_lstDatabases.Items.Contains("(เชื่อมต่อไม่ได้ หรือยังไม่มีฐานข้อมูล)")
                    ? "" : "";
                return;
            }
            foreach (var d in dbs) _lstDatabases.Items.Add(d);
            _lblState.Text = $"มีฐานข้อมูลของผู้ใช้ {dbs.Count} ตัว";
        }
        catch (Exception ex)
        {
            _lblState.Text = "โหลดรายการไม่สำเร็จ: " + ex.Message;
        }
    }

    private void Create()
    {
        var dbName = _txtDbName.Text.Trim();
        var userName = _txtUserName.Text.Trim();
        var pass = _txtUserPass.Text;

        if (!Regex.IsMatch(dbName, @"^[a-zA-Z0-9_]+$"))
        {
            MessageBox.Show("ชื่อ database ใช้ได้เฉพาะ a-z, A-Z, 0-9 และ _", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Regex.IsMatch(userName, @"^[a-zA-Z0-9_]+$"))
        {
            MessageBox.Show("ชื่อ user ใช้ได้เฉพาะ a-z, A-Z, 0-9 และ _", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (pass.Length < 4 || pass.Contains('\'') || pass.Contains('"') || pass.Contains('\\'))
        {
            MessageBox.Show("รหัสผ่านต้องยาวอย่างน้อย 4 ตัว และห้ามมี ' \" \\ (กัน SQL พัง)", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (pass != _txtUserPass2.Text)
        {
            MessageBox.Show("รหัสผ่านสองช่องไม่ตรงกัน", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnCreate.Enabled = false;
        UseWaitCursor = true;
        try
        {
            var rootPass = _txtRootPass.Text.Length > 0 ? _txtRootPass.Text : null;
            var (ok, output) = Db.CreateDatabaseAndUser(dbName, userName, pass, rootPass);
            if (!ok)
                throw new InvalidOperationException(output);

            var port = _cfg.MariaDb.Port;
            _lastConnection = $"""
                // mysqli
                $conn = mysqli_connect("127.0.0.1", "{userName}", "{pass}", "{dbName}", {port});

                // PDO
                $pdo = new PDO("mysql:host=127.0.0.1;port={port};dbname={dbName}", "{userName}", "{pass}");
                """;
            _txtResult.Text = _lastConnection;
            _log.Info($"สร้างฐานข้อมูล {dbName} + user {userName} สำเร็จ");
            _lblState.Text = $"สร้าง {dbName} + {userName} สำเร็จ ✓ — connection string พร้อมคัดลอก";

            RefreshDatabases();
            MessageBox.Show($"สร้างฐานข้อมูล `{dbName}` + user `{userName}` เรียบร้อย!\n" +
                "connection string แสดงอยู่ในช่องผลลัพธ์ (กดคัดลอกได้)",
                "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("สร้างไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCreate.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
