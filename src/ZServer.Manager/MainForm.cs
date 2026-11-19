using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// หน้าต่างหลัก: แดชบอร์ดสถานะ Apache / PHP / MariaDB + ปุ่มควบคุม + tray icon
/// </summary>
public sealed class MainForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly StackController _stack;
    private readonly Downloader _dl;
    private readonly ApacheManager _apacheMgr;
    private readonly PhpManager _phpMgr;
    private readonly MariaDbManager _dbMgr;
    private readonly ConfigWriter _cw;

    private readonly Label _lblApacheState = new();
    private readonly Label _lblApacheDetail = new();
    private readonly Label _lblPhpState = new();
    private readonly Label _lblPhpDetail = new();
    private readonly Label _lblDbState = new();
    private readonly Label _lblDbDetail = new();
    private readonly Button _btnApacheStart = new();
    private readonly Button _btnApacheStop = new();
    private readonly Button _btnApacheRestart = new();
    private readonly Button _btnDbStart = new();
    private readonly Button _btnDbStop = new();
    private readonly Button _btnDbRestart = new();
    private readonly Button _btnPhpInfo = new();
    private readonly TextBox _txtLog = new();
    private readonly Button _btnClearLog = new();
    private readonly Button _btnClearLogFile = new();
    private readonly Button _btnNewVersion = new();
    private readonly System.Windows.Forms.Timer _updateCheckTimer = new() { Interval = 6 * 60 * 60 * 1000 }; // ตรวจทุก 6 ชม.
    private readonly System.Windows.Forms.Timer _backupTimer = new() { Interval = 60_000 }; // เช็คเวลาสำรองอัตโนมัติทุก 1 นาที
    private string? _lastNotifiedSet;
    private string? _lastProgramNotified;
    private readonly ToolStripStatusLabel _statusLabel = new();
    private NotifyIcon _tray = null!;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private bool _busy;
    private bool _trayNoticeShown;
    private ComponentStatus _lastApache = new("apache", ComponentState.NotInstalled, null, 0, "");
    private ComponentStatus _lastDb = new("mariadb", ComponentState.NotInstalled, null, 0, "");
    private ComponentStatus _lastPhp = new("php", ComponentState.NotInstalled, null, 0, "");

    public MainForm()
    {
        _paths = ServerPaths.Discover();
        _paths.EnsureDirectories();
        _cfg = AppConfig.Load(_paths);
        _log = new Logger(_paths.ManagerLog);
        _stack = new StackController(_paths, _cfg, _log);
        _dl = new Downloader(_log);
        _apacheMgr = new ApacheManager(_paths, _cfg, _log, _dl);
        _phpMgr = new PhpManager(_paths, _cfg, _log, _dl);
        _dbMgr = new MariaDbManager(_paths, _cfg, _log, _dl);
        _cw = new ConfigWriter(_paths, _cfg, _log);

        Text = "zServer Manager — ควบคุม Apache / PHP / MariaDB";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(860, 620);
        MinimumSize = new Size(820, 560);
        Icon = LoadAppIcon(256);
        KeyPreview = true;
        WindowState = FormWindowState.Maximized; // เปิดโปรแกรมมาเต็มจอเสมอ

        Theme.Dark = _cfg.General.DarkTheme;
        BuildUi();
        BuildTray();
        Theme.Apply(this);

        _log.OnLine += OnLogLine;
        _timer.Tick += (s, e) => RefreshStatuses();
        _timer.Start();
        _updateCheckTimer.Tick += (s, e) => RunUpdateCheckAsync();
        _backupTimer.Tick += (s, e) => _ = Task.Run(() =>
        {
            try { DbBackupScheduler.CheckAndRun(_paths, _cfg, _log); } catch { }
        });

        Shown += OnShownFirstTime;
        Resize += OnResizeForTray;
        FormClosing += OnFormClosingGuard;
        FormClosed += (s, e) => { _tray.Visible = false; _tray.Dispose(); _timer.Stop(); _updateCheckTimer.Stop(); _backupTimer.Stop(); _dl.Dispose(); };
    }

    // ================= ไอคอนโปรแกรม =================
    /// <summary>โหลดไอคอนจาก resource ที่ฝังในตัว exe (assets\zserver.ico)</summary>
    private static Icon LoadAppIcon(int size)
    {
        try
        {
            var asm = typeof(MainForm).Assembly;
            using var s = asm.GetManifestResourceStream("zserver.ico");
            if (s != null) return new Icon(s, size, size);
        }
        catch { }
        return SystemIcons.Application;
    }

    // ================= สร้าง UI =================
    private void BuildUi()
    {
        // ---- ส่วนหัว ----
        var top = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(14, 8, 14, 0) };
        var title = new Label
        {
            Text = "⚙ zServer Manager",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 90, 60),
            AutoSize = true,
            Location = new Point(14, 6),
        };
        var subtitle = new Label
        {
            Text = $"โฟลเดอร์หลัก: {_paths.Root}   |   โหมดเซอร์วิส: {(_cfg.Services.Mode == "service" ? "Windows Service" : "Process")}   |   เวอร์ชันโปรแกรม 1.0.0",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(14, 34),
        };
        top.Controls.Add(title);
        top.Controls.Add(subtitle);

        // ---- การ์ดสถานะ 3 ใบ ----
        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 190,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10, 6, 10, 0),
        };
        for (int i = 0; i < 3; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

        cards.Controls.Add(BuildApacheCard(), 0, 0);
        cards.Controls.Add(BuildPhpCard(), 1, 0);
        cards.Controls.Add(BuildDbCard(), 2, 0);

        // ---- แถวปุ่มฟังก์ชันหลัก ----
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10, 6, 10, 0),
            WrapContents = false,
        };
        actions.Controls.Add(MakeButton("ติดตั้ง / จัดการคอมโพเนนต์", () => ShowDialogForm(new InstallForm(_paths, _cfg, _log, _dl))));
        actions.Controls.Add(MakeButton("อัพเดท", () => ShowDialogForm(new UpdateForm(_paths, _cfg, _log, _dl))));

        // ป้ายแจ้งเตือนเวอร์ชันใหม่ (ซ่อนไว้จนกว่าจะตรวจเจอเวอร์ชันใหม่จริง)
        _btnNewVersion.Text = "มีเวอร์ชันใหม่ ★";
        _btnNewVersion.AutoSize = true;
        _btnNewVersion.FlatStyle = FlatStyle.Flat;
        _btnNewVersion.FlatAppearance.BorderColor = Color.FromArgb(230, 140, 20);
        _btnNewVersion.BackColor = Color.FromArgb(196, 106, 0);
        _btnNewVersion.ForeColor = Color.White;
        _btnNewVersion.Visible = false;
        _btnNewVersion.Click += (s, e) => ShowDialogForm(new UpdateForm(_paths, _cfg, _log, _dl));
        actions.Controls.Add(_btnNewVersion);
        actions.Controls.Add(MakeButton("ตั้งค่า", () => ShowDialogForm(new SettingsForm(_paths, _cfg, _log, _dl))));
        actions.Controls.Add(MakeButton("แก้ไขไฟล์ Config", () => ShowDialogForm(new ConfigEditorForm(_paths, _cfg, _log, _cw))));
        actions.Controls.Add(MakeButton("ฐานข้อมูล", () => ShowDialogForm(new DbWizardForm(_paths, _cfg, _log, _dl))));
        actions.Controls.Add(MakeButton("ดู Logs", () => ShowDialogForm(new LogsForm(_paths, _cfg))));
        actions.Controls.Add(MakeButton("เริ่มทั้งหมด", StartAll));
        actions.Controls.Add(MakeButton("หยุดทั้งหมด", StopAll));
        actions.Controls.Add(MakeButton("เปิดพอร์ต Firewall", OpenFirewall));
        actions.Controls.Add(MakeButton("เปิด Terminal", OpenTerminal));
        actions.Controls.Add(MakeButton("คู่มือการใช้งาน", OpenHelp));
        actions.Controls.Add(MakeButton("เปิดเว็บ localhost", () =>
        {
            try { StackController.OpenBrowser(_stack.LocalUrl); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }));

        // ---- กล่อง log สด ----
        var logGroup = new GroupBox
        {
            Text = " บันทึกเหตุการณ์ (สด) ",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 4, 10, 8),
        };
        _txtLog.Dock = DockStyle.Fill;
        _txtLog.ReadOnly = true;
        _txtLog.Multiline = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.BackColor = Color.FromArgb(24, 26, 32);
        _txtLog.ForeColor = Color.FromArgb(180, 220, 190);
        _txtLog.Font = new Font("Consolas", 9f);
        _txtLog.WordWrap = false;

        var logBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(2, 2, 2, 2),
        };
        _btnClearLog.Text = "ล้าง log";
        _btnClearLog.AutoSize = true;
        _btnClearLog.Click += (s, e) => _txtLog.Clear();
        logBar.Controls.Add(_btnClearLog);

        _btnClearLogFile.Text = "ล้างไฟล์ log";
        _btnClearLogFile.AutoSize = true;
        _btnClearLogFile.Click += (s, e) => ClearManagerLogFile();
        logBar.Controls.Add(_btnClearLogFile);

        logGroup.Controls.Add(_txtLog);
        logGroup.Controls.Add(logBar);

        // ---- status bar ----
        var status = new StatusStrip();
        status.Items.Add(_statusLabel);
        _statusLabel.Text = "พร้อมทำงาน";

        Controls.Add(logGroup);
        Controls.Add(actions);
        Controls.Add(cards);
        Controls.Add(top);
        Controls.Add(status);

        _txtLog.Text = _log.Tail(120);
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    private GroupBox BuildApacheCard()
    {
        var g = new GroupBox { Text = " Apache ", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblApacheState.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblApacheState.AutoSize = true;
        _lblApacheDetail.ForeColor = Color.DimGray;
        _lblApacheDetail.AutoSize = false;
        _lblApacheDetail.Dock = DockStyle.Fill;
        _lblApacheDetail.TextAlign = ContentAlignment.TopLeft;

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _btnApacheStart.Text = "เริ่ม";
        _btnApacheStop.Text = "หยุด";
        _btnApacheRestart.Text = "รีสตาร์ท";
        _btnApacheStart.Width = _btnApacheStop.Width = _btnApacheRestart.Width = 92;
        _btnApacheStart.Click += (s, e) => RunAsync("เริ่ม Apache", _stack.StartApache);
        _btnApacheStop.Click += (s, e) => RunAsync("หยุด Apache", _stack.StopApache);
        _btnApacheRestart.Click += (s, e) => RunAsync("รีสตาร์ท Apache", _stack.RestartApache);
        row.Controls.Add(_btnApacheStart);
        row.Controls.Add(_btnApacheStop);
        row.Controls.Add(_btnApacheRestart);

        layout.Controls.Add(_lblApacheState, 0, 0);
        layout.Controls.Add(_lblApacheDetail, 0, 1);
        layout.Controls.Add(row, 0, 2);
        g.Controls.Add(layout);
        return g;
    }

    private GroupBox BuildPhpCard()
    {
        var g = new GroupBox { Text = " PHP ", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblPhpState.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblPhpState.AutoSize = true;
        _lblPhpDetail.ForeColor = Color.DimGray;
        _lblPhpDetail.AutoSize = false;
        _lblPhpDetail.Dock = DockStyle.Fill;

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _btnPhpInfo.Text = "เปิด phpinfo";
        _btnPhpInfo.Width = 120;
        _btnPhpInfo.Click += (s, e) =>
        {
            try { StackController.OpenBrowser(_stack.LocalUrl + "/phpinfo.php"); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        };
        row.Controls.Add(_btnPhpInfo);

        layout.Controls.Add(_lblPhpState, 0, 0);
        layout.Controls.Add(_lblPhpDetail, 0, 1);
        layout.Controls.Add(row, 0, 2);
        g.Controls.Add(layout);
        return g;
    }

    private GroupBox BuildDbCard()
    {
        var g = new GroupBox { Text = " MariaDB ", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblDbState.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblDbState.AutoSize = true;
        _lblDbDetail.ForeColor = Color.DimGray;
        _lblDbDetail.AutoSize = false;
        _lblDbDetail.Dock = DockStyle.Fill;

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _btnDbStart.Text = "เริ่ม";
        _btnDbStop.Text = "หยุด";
        _btnDbRestart.Text = "รีสตาร์ท";
        _btnDbStart.Width = _btnDbStop.Width = _btnDbRestart.Width = 92;
        _btnDbStart.Click += (s, e) => RunAsync("เริ่ม MariaDB", _stack.StartMariaDb);
        _btnDbStop.Click += (s, e) => RunAsync("หยุด MariaDB", _stack.StopMariaDb);
        _btnDbRestart.Click += (s, e) => RunAsync("รีสตาร์ท MariaDB", _stack.RestartMariaDb);
        row.Controls.Add(_btnDbStart);
        row.Controls.Add(_btnDbStop);
        row.Controls.Add(_btnDbRestart);

        layout.Controls.Add(_lblDbState, 0, 0);
        layout.Controls.Add(_lblDbDetail, 0, 1);
        layout.Controls.Add(row, 0, 2);
        g.Controls.Add(layout);
        return g;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(2, 3, 6, 3),
        };
        b.Click += (s, e) => onClick();
        return b;
    }

    // ================= Tray =================
    private void BuildTray()
    {
        _tray = new NotifyIcon
        {
            Icon = LoadAppIcon(32),
            Text = "zServer Manager",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("แสดงหน้าต่างหลัก", null, (s, e) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("เริ่มทั้งหมด", null, (s, e) => StartAll());
        menu.Items.Add("หยุดทั้งหมด", null, (s, e) => StopAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("เปิดเว็บ localhost", null, (s, e) =>
        {
            try { StackController.OpenBrowser(_stack.LocalUrl); } catch { }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("สลับธีมมืด/สว่าง", null, (s, e) => ToggleTheme());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("ออกจากโปรแกรม", null, (s, e) => { _tray.Visible = false; Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (s, e) => ShowMainWindow();
        // กด balloon แจ้งเตือนเวอร์ชันใหม่ → เปิดหน้าอัพเดททันที
        _tray.BalloonTipClicked += (s, e) => ShowDialogForm(new UpdateForm(_paths, _cfg, _log, _dl));
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnResizeForTray(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _cfg.General.MinimizeToTray)
        {
            Hide();
            if (!_trayNoticeShown)
            {
                _tray.ShowBalloonTip(2500, "zServer Manager",
                    "โปรแกรมยังทำงานอยู่ในถาด (tray) — ดับเบิลคลิกไอคอนเพื่อเปิดหน้าต่าง", ToolTipIcon.Info);
                _trayNoticeShown = true;
            }
        }
    }

    // ================= เหตุการณ์หลัก =================
    private void OnShownFirstTime(object? sender, EventArgs e)
    {
        RefreshStatuses();
        RunUpdateCheckAsync(); // ตรวจเวอร์ชันใหม่ตอนเปิดโปรแกรม (เบื้องหลัง)
        _backupTimer.Start();  // เริ่มจับเวลาสำรองฐานข้อมูลอัตโนมัติ

        // จัดเก็บ/บีบอัด log เก่ากว่า LogRetentionDays วัน (ตามค่าตั้ง)
        _ = Task.Run(() =>
        {
            try { LogRotator.Run(_paths, _cfg.General.LogRetentionDays, _log); } catch { }
        });

        // เปิดอัตโนมัติตามค่าตั้ง
        if (_cfg.General.AutoStartServices)
        {
            var anyInstalled = File.Exists(_paths.ApacheBin) || File.Exists(_paths.MariaDbBin);
            if (anyInstalled) StartAll();
        }

        // ยังไม่ติดตั้งอะไรเลย → ชวนเข้าหน้าติดตั้ง
        if (!File.Exists(_paths.ApacheBin) && !File.Exists(_paths.PhpBin) && !File.Exists(_paths.MariaDbBin))
        {
            var ans = MessageBox.Show(
                "ยังไม่ได้ติดตั้ง Apache / PHP / MariaDB เลย\n\n" +
                "ต้องการเปิดหน้า \"ติดตั้ง\" เพื่อดาวน์โหลดและติดตั้งจากเว็บทางการตอนนี้เลยหรือไม่?",
                "เริ่มต้นใช้งาน zServer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans == DialogResult.Yes)
                ShowDialogForm(new InstallForm(_paths, _cfg, _log, _dl));
        }
    }

    private void OnLogLine(string line)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            BeginInvoke(() =>
            {
                // เลื่อนลงล่างตามข้อความใหม่เสมอ — ยกเว้นผู้ใช้เลื่อนขึ้นไปอ่านของเก่า
                bool atBottom = IsLogAtBottom();
                _txtLog.AppendText(line + Environment.NewLine);
                if (_txtLog.Lines.Length > 400)
                    _txtLog.Text = string.Join(Environment.NewLine, _txtLog.Lines[^300..]);
                if (atBottom)
                {
                    _txtLog.SelectionStart = _txtLog.TextLength;
                    _txtLog.ScrollToCaret();
                }
            });
        }
        catch { }
    }

    /// <summary>ช่อง log กำลังแสดงท้ายสุดอยู่หรือไม่ (ใช้ตัดสินว่าจะเลื่อนอัตโนมัติไหม)</summary>
    private bool IsLogAtBottom()
    {
        try
        {
            if (_txtLog.TextLength == 0) return true;
            var pos = _txtLog.GetPositionFromCharIndex(_txtLog.TextLength - 1);
            // บรรทัดสุดท้ายอยู่ในมุมมอง = อยู่ท้าย
            return pos.Y <= _txtLog.ClientSize.Height - _txtLog.Font.Height;
        }
        catch { return true; }
    }

    private void RefreshStatuses()
    {
        try
        {
            _lastApache = _stack.GetApacheStatus();
            _lastDb = _stack.GetMariaDbStatus();
            _lastPhp = _stack.GetPhpStatus();
        }
        catch { }

        ApplyCard(_lblApacheState, _lblApacheDetail, _lastApache);
        ApplyCard(_lblPhpState, _lblPhpDetail, _lastPhp);
        ApplyCard(_lblDbState, _lblDbDetail, _lastDb);

        _btnApacheStart.Enabled = !_busy && _lastApache.State is ComponentState.Stopped or ComponentState.PortConflict or ComponentState.Error;
        _btnApacheStop.Enabled = !_busy && _lastApache.State == ComponentState.Running;
        _btnApacheRestart.Enabled = !_busy && _lastApache.State == ComponentState.Running;
        _btnDbStart.Enabled = !_busy && _lastDb.State is ComponentState.Stopped or ComponentState.PortConflict or ComponentState.Error;
        _btnDbStop.Enabled = !_busy && _lastDb.State == ComponentState.Running;
        _btnDbRestart.Enabled = !_busy && _lastDb.State == ComponentState.Running;
        _btnPhpInfo.Enabled = !_busy && _lastPhp.State == ComponentState.Running && _lastApache.State == ComponentState.Running;

        _statusLabel.Text = $"Apache: {_lastApache.StateText}   |   MariaDB: {_lastDb.StateText}   |   PHP: {_lastPhp.StateText}";
        _tray.Text = $"zServer — Apache: {_lastApache.StateText} | MariaDB: {_lastDb.StateText}";
    }

    private static void ApplyCard(Label stateLabel, Label detailLabel, ComponentStatus st)
    {
        var (icon, color) = st.State switch
        {
            ComponentState.Running => ("●", Color.FromArgb(20, 130, 60)),
            ComponentState.Stopped => ("○", Color.DimGray),
            ComponentState.NotInstalled => ("◌", Color.FromArgb(140, 90, 20)),
            ComponentState.PortConflict => ("⚠", Color.FromArgb(190, 120, 0)),
            _ => ("✖", Color.Firebrick),
        };
        stateLabel.Text = $"{icon} {st.StateText}";
        stateLabel.ForeColor = color;
        var versionPart = st.Version == null ? "" : $"เวอร์ชัน {st.Version}   ";
        var portPart = st.Port > 0 ? $"พอร์ต {st.Port}   " : "";
        detailLabel.Text = versionPart + portPart + st.Detail;
    }

    // ================= การทำงานเบื้องหลัง =================
    private async void RunAsync(string doing, Action work)
    {
        if (_busy) return;
        _busy = true;
        UseWaitCursor = true;
        _statusLabel.Text = doing + "… กำลังทำงาน";
        RefreshStatuses();
        try
        {
            await Task.Run(work);
            _log.Info($"{doing} — เสร็จสิ้น");
            _statusLabel.Text = doing + " เสร็จ ✓";
        }
        catch (Exception ex)
        {
            _log.Error($"{doing} ล้มเหลว: {ex.Message}");
            _statusLabel.Text = doing + " ล้มเหลว ✗";
            MessageBox.Show(ex.Message, doing + " — ไม่สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
            RefreshStatuses();
        }
    }

    private void StartAll() => RunAsync("เริ่มทั้งหมด (MariaDB → Apache)", _stack.StartAll);
    private void StopAll() => RunAsync("หยุดทั้งหมด (Apache → MariaDB)", _stack.StopAll);

    /// <summary>เปิด terminal ที่ www\ พร้อม PATH ชั่วคราวมี php + mariadb\bin</summary>
    private void OpenTerminal()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                WorkingDirectory = _paths.WwwDir,
                UseShellExecute = false,
                Arguments = "/k echo === zServer terminal — php และ mariadb\\bin พร้อมใช้ใน PATH นี้ ===",
            };
            var extra = _paths.PhpDir + ";" + Path.Combine(_paths.MariaDbDir, "bin") + ";";
            psi.EnvironmentVariables["PATH"] = extra + Environment.GetEnvironmentVariable("PATH");
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("เปิด Terminal ไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>เปิดโฟลเดอร์เอกสารคู่มือ</summary>
    private void OpenHelp()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Path.Combine(_paths.Root, "docs") + "\""); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    /// <summary>ล้างเนื้อหาไฟล์ logs\zserver-manager.log (ถามยืนยันพร้อมแสดงขนาดก่อน)</summary>
    private void ClearManagerLogFile()
    {
        try
        {
            var sizeText = File.Exists(_paths.ManagerLog)
                ? $"ขนาดปัจจุบัน {new FileInfo(_paths.ManagerLog).Length / 1024.0:F1} KB"
                : "ไฟล์ยังไม่มีข้อมูล";
            var ans = MessageBox.Show(
                $"ล้างเนื้อหาไฟล์ logs\\zserver-manager.log?\n({sizeText})\n\n" +
                "ไฟล์ log ของ Apache / PHP / MariaDB ไม่ถูกแตะ — ล้างไฟล์เหล่านั้นที่หน้า \"ดู Logs\"",
                "ยืนยันการล้าง", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            File.WriteAllText(_paths.ManagerLog,
                $"— ล้างไฟล์ log เมื่อ {DateTime.Now:yyyy-MM-dd HH:mm:ss} —{Environment.NewLine}");
            _log.Info("ผู้ใช้ล้างไฟล์ log");
            _statusLabel.Text = "ล้างไฟล์ zserver-manager.log เรียบร้อย ✓";
            _log.Info("ล้างไฟล์ zserver-manager.log แล้ว (จากปุ่มบนหน้าหลัก)");
        }
        catch (Exception ex)
        {
            MessageBox.Show("ล้างไฟล์ log ไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ================= แจ้งเตือนเวอร์ชันใหม่อัตโนมัติ =================
    /// <summary>
    /// ตรวจเวอร์ชันล่าสุดจากเว็บทางการ (ตอนเปิดโปรแกรม + ทุก 6 ชม.)
    /// เจอเวอร์ชันใหม่กว่าที่ติดตั้ง → ขึ้นป้ายบนหน้าหลัก + balloon จาก tray
    /// </summary>
    private async void RunUpdateCheckAsync()
    {
        try
        {
            // กันตรวจถี่เกิน — เว้นระยะ 6 ชม. จากครั้งล่าสุด
            var last = _cfg.General.LastUpdateCheck;
            if (last.HasValue && (DateTime.Now - last.Value).TotalHours < 6) return;

            var latest = await UpdateChecker.CheckAllAsync(_dl.GetHttpClient());
            _cfg.General.LastUpdateCheck = DateTime.Now;
            _cfg.Save(_paths);

            var newer = new List<string>();
            var setParts = new List<string>();
            foreach (var r in latest)
            {
                var installed = _cfg.InstalledVersion(r.Component);
                if (r.Ok && r.LatestVersion != null && installed != null &&
                    UpdateChecker.CompareVersions(r.LatestVersion, installed) > 0)
                {
                    newer.Add($"{CompName(r.Component)} {r.LatestVersion} (ติดตั้งอยู่ {installed})");
                    setParts.Add(r.Component + "=" + r.LatestVersion);
                }
            }

            if (newer.Count == 0)
            {
                _btnNewVersion.Visible = false;
            }
            else
            {
                var text = string.Join(", ", newer.Take(2)) + (newer.Count > 2 ? " +อีก " + (newer.Count - 2) : "");
                _btnNewVersion.Text = "มีเวอร์ชันใหม่ ★ " + text;
                _btnNewVersion.Visible = true;

                // balloon เฉพาะเมื่อชุดเวอร์ชันเปลี่ยนจากครั้งก่อน (ไม่ย้ำทุก 6 ชม.)
                var set = string.Join(";", setParts);
                if (_lastNotifiedSet != set)
                {
                    _lastNotifiedSet = set;
                    _tray.ShowBalloonTip(8000, "zServer — มีเวอร์ชันใหม่!",
                        string.Join("\n", newer) + "\n\nกดเพื่อเปิดหน้าอัพเดท", ToolTipIcon.Info);
                    _log.Info("แจ้งเตือนเวอร์ชันใหม่: " + string.Join(", ", newer));
                }
            }

            // ---- ตัวโปรแกรมเอง (ทำงานเมื่อตั้งค่า ProgramUpdateUrl ไว้ใน zserver.json) ----
            if (!string.IsNullOrWhiteSpace(_cfg.ProgramUpdateUrl))
            {
                try
                {
                    var json = await _dl.GetStringAsync(_cfg.ProgramUpdateUrl);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var v = doc.RootElement.TryGetProperty("version", out var vv) ? vv.GetString() : null;
                    var url = doc.RootElement.TryGetProperty("url", out var uu) ? uu.GetString() : null;
                    var cur = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0";
                    if (v != null && url != null && UpdateChecker.CompareVersions(v, cur) > 0 && _lastProgramNotified != v)
                    {
                        _lastProgramNotified = v;
                        if (MessageBox.Show(
                            $"โปรแกรม zServer Manager มีเวอร์ชันใหม่ {v} (ตัวนี้คือ {cur})\n\nเปิดหน้าดาวน์โหลดเลยไหม?",
                            "อัพเดตโปรแกรม", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            StackController.OpenBrowser(url);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _log.Warn("ตรวจเวอร์ชันอัตโนมัติไม่สำเร็จ: " + ex.Message);
        }
    }

    private static string CompName(string c) => c switch
    {
        "apache" => "Apache", "php" => "PHP", "mariadb" => "MariaDB", _ => c,
    };

    private void OpenFirewall()
    {
        try
        {
            FirewallHelper.AllowPort(_cfg.Apache.Port, "zServer Apache HTTP");
            FirewallHelper.AllowPort(_cfg.MariaDb.Port, "zServer MariaDB");
            MessageBox.Show(
                $"เปิดพอร์ต {_cfg.Apache.Port} (HTTP) และ {_cfg.MariaDb.Port} (MariaDB) ใน Windows Firewall เรียบร้อย",
                "Firewall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _log.Info($"เปิดพอร์ต firewall: {_cfg.Apache.Port}, {_cfg.MariaDb.Port}");
        }
        catch (Exception ex)
        {
            MessageBox.Show("เปิดพอร์ตไม่สำเร็จ:\n" + ex.Message, "Firewall",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowDialogForm(Form f)
    {
        HideWhileRunning(f);
    }

    private void HideWhileRunning(Form f)
    {
        f.StartPosition = FormStartPosition.CenterParent;
        f.ShowDialog(this);
        // ถ้าเปลี่ยนธีมในหน้าตั้งค่า ให้หน้าหลักตามทันที
        if (Theme.Dark != _cfg.General.DarkTheme)
        {
            Theme.Dark = _cfg.General.DarkTheme;
            Theme.Apply(this);
        }
        RefreshStatuses();
    }

    private void ToggleTheme()
    {
        Theme.Dark = !Theme.Dark;
        _cfg.General.DarkTheme = Theme.Dark;
        _cfg.Save(_paths);
        Theme.Apply(this);
        _statusLabel.Text = Theme.Dark ? "ใช้ธีมมืด ✓" : "ใช้ธีมสว่าง ✓";
    }

    // ================= ปิดโปรแกรม =================
    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            MessageBox.Show("กำลังมีงานทำอยู่ รอจนเสร็จก่อนแล้วปิดใหม่นะครับ", "zServer Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var running = _lastApache.State == ComponentState.Running || _lastDb.State == ComponentState.Running;
        if (!running) return;

        var ans = MessageBox.Show(
            "Apache/MariaDB ยังทำงานอยู่\n\n" +
            "\"ใช่\" = หยุดเซอร์วิสก่อนปิดโปรแกรม\n" +
            "\"ไม่ใช่\" = ปล่อยให้เซอร์วิสทำงานต่อ (ปิดแค่หน้าต่างนี้)\n" +
            "\"ยกเลิก\" = กลับไปใช้โปรแกรมต่อ",
            "ปิดโปรแกรม", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (ans == DialogResult.Cancel) { e.Cancel = true; return; }
        if (ans == DialogResult.Yes)
        {
            try { _stack.StopAll(); } catch { }
        }
    }
}
