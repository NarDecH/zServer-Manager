using System.Text.RegularExpressions;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// หน้าตั้งค่าทั่วไป: พอร์ต, DocumentRoot, ค่า PHP, ค่า MariaDB, โหมดเซอร์วิส
/// บันทึกแล้วจะเขียน config ให้ใหม่พร้อมสำรองไฟล์เดิมอัตโนมัติ
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly ConfigWriter _cw;

    // Apache
    private readonly NumericUpDown _numPort = MakeNum(1, 65535);
    private readonly TextBox _txtServerName = new();
    private readonly TextBox _txtServerAdmin = new();
    private readonly TextBox _txtDocRoot = new();
    private readonly Button _btnBrowse = new() { Text = "เลือกโฟลเดอร์…" };
    private readonly CheckBox _chkKeepAlive = new() { Text = "เปิด KeepAlive (ทำให้โหลดไวขึ้น)" };
    private readonly NumericUpDown _numKeepAlive = MakeNum(1, 60);
    private readonly NumericUpDown _numThreads = MakeNum(50, 2000);
    private readonly CheckBox _chkRewrite = new() { Text = "เปิด mod_rewrite (จำเป็นของ WordPress/Laravel ฯลฯ)" };
    private readonly CheckBox _chkDeflate = new() { Text = "เปิดบีบอัดข้อมูล (deflate)" };
    private readonly CheckBox _chkExpires = new() { Text = "เปิด cache ฝั่ง browser (expires)" };
    private readonly CheckBox _chkAutoIndex = new() { Text = "แสดงรายการไฟล์เมื่อไม่มี index (directory listing)" };
    private readonly Button _btnHttpsManager = new() { Text = "จัดการ HTTPS… (สร้างใบรับรอง/เปิด-ปิด)" };

    // PHP
    private readonly ComboBox _cboMemory = MakeEditableCombo("128M", "256M", "512M", "1024M", "2048M");
    private readonly ComboBox _cboUpload = MakeEditableCombo("8M", "16M", "32M", "64M", "128M", "256M");
    private readonly ComboBox _cboPost = MakeEditableCombo("8M", "16M", "32M", "64M", "128M", "256M");
    private readonly NumericUpDown _numMaxExec = MakeNum(0, 3600);
    private readonly NumericUpDown _numMaxInput = MakeNum(0, 3600);
    private readonly NumericUpDown _numMaxVars = MakeNum(100, 100000);
    private readonly CheckBox _chkDisplayErrors = new() { Text = "แสดง error บนหน้าเว็บ (เปิดเฉพาะตอนพัฒนา)" };
    private readonly CheckBox _chkLogErrors = new() { Text = "เก็บ error ลงไฟล์ log" };
    private readonly CheckBox _chkOpCache = new() { Text = "เปิด OPcache (เว็บไวขึ้น)" };
    private readonly ComboBox _cboTimezone = MakeEditableCombo(
        "Asia/Bangkok", "UTC", "Asia/Singapore", "Asia/Tokyo", "Asia/Ho_Chi_Minh", "Asia/Yangon", "Asia/Dubai", "Europe/London", "America/New_York");
    private readonly FlowLayoutPanel _pnlExtensions = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown, // แต่ละรายการอยู่บรรทัดของตัวเอง เรียงตรงแนวเดียวกัน
        WrapContents = false,
        AutoScroll = true,
        MinimumSize = new Size(0, 400),
    };

    // MariaDB
    private readonly CheckBox _chkBindLocal = new() { Text = "อนุญาตเฉพาะเครื่องนี้ (127.0.0.1 — แนะนำ)" };
    private readonly NumericUpDown _numDbPort = MakeNum(1, 65535);
    private readonly CheckBox _chkAutoBackup = new() { Text = "เปิดสำรองอัตโนมัติทุกวัน" };
    private readonly TextBox _txtBackupTime = new() { Width = 90 };
    private readonly NumericUpDown _numBackupRetention = MakeNum(0, 365);
    private readonly Button _btnBackupNow = new() { Text = "สำรองเดี๋ยวนี้" };
    private readonly Button _btnOpenBackupFolder = new() { Text = "เปิดโฟลเดอร์สำรอง" };
    private readonly Label _lblBackupState = new() { AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(6, 8, 0, 0) };
    private readonly Button _btnUpgradeTables = new() { Text = "อัพเกรดตารางระบบ (mariadb-upgrade)", Width = 280 };
    private readonly TextBox _txtRootPass = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly Label _lblRootPassState = new() { AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(6, 8, 0, 0) };
    private readonly TextBox _txtDataDir = new() { ReadOnly = true, BackColor = Color.Gainsboro };
    private readonly ComboBox _cboCharset = MakeEditableCombo("utf8mb4", "utf8", "latin1", "tis620");
    private readonly ComboBox _cboCollation = MakeEditableCombo("utf8mb4_unicode_ci", "utf8mb4_general_ci", "utf8_general_ci", "latin1_swedish_ci", "utf8_tis_620");
    private readonly ComboBox _cboBufferPool = MakeEditableCombo("64M", "128M", "256M", "512M", "1024M");
    private readonly ComboBox _cboKeyBuffer = MakeEditableCombo("16M", "32M", "64M", "128M");
    private readonly NumericUpDown _numMaxConn = MakeNum(10, 10000);
    private readonly NumericUpDown _numLowercase = MakeNum(0, 2);
    private readonly Button _btnRootPassword = new() { Text = "ตั้งรหัสผ่าน root…" };

    // เซอร์วิส
    private readonly RadioButton _rbModeService = new() { Text = "Windows Service (แนะนำ — ทำงานเบื้องหลัง เปิดเครื่องมาทำงานเอง)" };
    private readonly RadioButton _rbModeProcess = new() { Text = "Process mode (แบบ XAMPP — ทำงานเฉพาะตอนเปิดโปรแกรม)" };
    private readonly TextBox _txtSvcApache = new();
    private readonly TextBox _txtSvcDb = new();
    private readonly ComboBox _cboStartup = MakeCombo("auto", "demand");
    private readonly Button _btnInstallApacheSvc = new() { Text = "ติดตั้งเซอร์วิส Apache" };
    private readonly Button _btnUninstallApacheSvc = new() { Text = "ถอนเซอร์วิส Apache" };
    private readonly Button _btnInstallDbSvc = new() { Text = "ติดตั้งเซอร์วิส MariaDB" };
    private readonly Button _btnUninstallDbSvc = new() { Text = "ถอนเซอร์วิส MariaDB" };

    private readonly Button _btnSave = new() { Text = "บันทึกและใช้ค่าใหม่" };
    private readonly Button _btnCancel = new() { Text = "ยกเลิก" };

    // Virtual Hosts
    private readonly CheckedListBox _clbVhosts = new() { CheckOnClick = true, DisplayMember = "Name" };
    private readonly TextBox _txtVhostName = new();
    private readonly TextBox _txtVhostPath = new();
    private readonly Button _btnBrowseVhost = new() { Text = "เลือกโฟลเดอร์…" };
    private readonly Button _btnAddVhost = new() { Text = "เพิ่มเว็บ" };
    private readonly Button _btnDelVhost = new() { Text = "ลบที่เลือก" };

    // ทั่วไป
    private readonly CheckBox _chkDarkTheme = new() { Text = "ใช้ธีมมืด (หน้าหลักปรับทันที, หน้าอื่นเมื่อเปิดครั้งถัดไป)" };
    private readonly NumericUpDown _numLogRetention = new() { Minimum = 0, Maximum = 365, Width = 90 };
    private readonly CheckBox _chkWatchdog = new() { Text = "ตรวจจับเซอร์วิสล้มแล้วเริ่มให้ใหม่อัตโนมัติ (พร้อมแจ้งเตือน)" };
    private readonly CheckBox _chkXdebug = new() { Text = "เปิด Xdebug (debug ผ่าน VS Code)" };
    private readonly NumericUpDown _numXdebugPort = new() { Minimum = 1, Maximum = 65535, Width = 90 };
    private readonly Label _lblXdebugState = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly ComboBox _cboPhpVersion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Button _btnSwitchPhp = new() { Text = "สลับเวอร์ชัน" };

    private readonly Downloader _dl;

    // ทั่วไป
    private readonly CheckBox _chkStartWithWindows = new() { Text = "เปิดโปรแกรมอัตโนมัติเมื่อเข้าสู่ระบบ Windows" };
    private readonly CheckBox _chkAutoStartServices = new() { Text = "เริ่มเซอร์วิส Apache + MariaDB ทันทีที่เปิดโปรแกรม" };
    private readonly CheckBox _chkMinimizeToTray = new() { Text = "ย่อ/ปิดหน้าต่างแล้วซ่อนไว้ใน tray" };

    public SettingsForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _cw = new ConfigWriter(paths, cfg, log);
        _dl = dl;

        Text = "ตั้งค่า zServer";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        MinimumSize = new Size(760, 620);

        BuildUi();
        LoadValues();
    }

    private static NumericUpDown MakeNum(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Dock = DockStyle.Fill,
    };

    private static ComboBox MakeEditableCombo(params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        c.Items.AddRange(items);
        return c;
    }

    private static ComboBox MakeCombo(params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        c.Items.AddRange(items);
        return c;
    }

    private void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        // ---- แท็บทั่วไป ----
        var tabGeneral = new TabPage("ทั่วไป");
        var layoutG = MakeTwoColumnLayout();
        AddRow(layoutG, "เมื่อเข้าสู่ระบบ Windows", _chkStartWithWindows);
        AddRow(layoutG, "เมื่อเปิดโปรแกรมนี้", _chkAutoStartServices);
        AddRow(layoutG, "หน้าต่าง", _chkMinimizeToTray);
        AddRow(layoutG, "ธีมหน้าจอ", _chkDarkTheme);
        AddRow(layoutG, "Watchdog เซอร์วิส", _chkWatchdog);
        AddRow(layoutG, "เก็บไฟล์ log ไว้กี่วัน (0 = ไม่จำกัด)", _numLogRetention);
        AddRow(layoutG, "หมายเหตุ", MakeNote(
            "เปิดอัตโนมัติตอนเข้าสู่ระบบ ทำผ่าน Task Scheduler แบบ Run with highest privileges\n" +
            "→ ได้สิทธิ์ admin ทันทีโดยไม่เด้งขอ UAC ทุกครั้งที่เปิดเครื่อง\n" +
            "ถ้าเลือก \"เริ่มเซอร์วิสทันที\" ด้วย เว็บจะพร้อมใช้หลัง logon ในไม่กี่วินาที\n" +
            "log เก่าจะถูกบีบอัดเก็บที่ backups\\log-archives\\ ตอนเปิดโปรแกรม"));
        tabGeneral.Controls.Add(layoutG);

        var tabApache = new TabPage("Apache");
        var layout = MakeTwoColumnLayout();
        AddRow(layout, "พอร์ต (HTTP)", _numPort);
        AddRow(layout, "ServerName", _txtServerName);
        AddRow(layout, "อีเมลผู้ดูแล (ServerAdmin)", _txtServerAdmin);
        AddRow(layout, "DocumentRoot (โฟลเดอร์เว็บ)", Wrap(_txtDocRoot, _btnBrowse));
        AddRow(layout, "KeepAlive timeout (วินาที)", Wrap(_chkKeepAlive, _numKeepAlive));
        AddRow(layout, "ThreadsPerChild (จำนวนเธรด)", _numThreads);
        AddRow(layout, "โมดูลเสริม", Wrap(_chkRewrite, _chkDeflate, _chkExpires, _chkAutoIndex));
        AddRow(layout, "HTTPS", _btnHttpsManager);
        tabApache.Controls.Add(layout);

        var tabPhp = new TabPage("PHP");
        var layout2 = MakeTwoColumnLayout();
        AddRow(layout2, "เวอร์ชัน PHP ที่ใช้งาน (ระบบหลายเวอร์ชัน)", Wrap(_cboPhpVersion, _btnSwitchPhp));
        AddRow(layout2, "memory_limit (หน่วยความจำต่อสคริปต์)", _cboMemory);
        AddRow(layout2, "upload_max_filesize (อัพโหลดไฟล์ใหญ่สุด)", _cboUpload);
        AddRow(layout2, "post_max_size (ข้อมูล POST ใหญ่สุด)", _cboPost);
        AddRow(layout2, "max_execution_time (วินาที)", _numMaxExec);
        AddRow(layout2, "max_input_time (วินาที)", _numMaxInput);
        AddRow(layout2, "max_input_vars", _numMaxVars);
        AddRow(layout2, "การแสดง/เก็บ error", Wrap(_chkDisplayErrors, _chkLogErrors));
        AddRow(layout2, "OPcache", _chkOpCache);
        AddRow(layout2, "Xdebug (VS Code)", Wrap(_chkXdebug, _numXdebugPort));
        AddRow(layout2, "สถานะ Xdebug", _lblXdebugState);
        AddRow(layout2, "เขตเวลา (date.timezone)", _cboTimezone);
        AddRow(layout2, "Extensions (โมดูลเสริม)", _pnlExtensions);
        tabPhp.Controls.Add(layout2);

        var tabDb = new TabPage("MariaDB");
        var layout3 = MakeTwoColumnLayout();
        AddRow(layout3, "พอร์ต", _numDbPort);
        AddRow(layout3, "การเข้าถึง", _chkBindLocal);
        AddRow(layout3, "สำรองข้อมูลอัตโนมัติ", Wrap(_chkAutoBackup, new Label { Text = "เวลา:", AutoSize = true, Padding = new Padding(6, 8, 0, 0) }, _txtBackupTime, new Label { Text = "เก็บ (วัน):", AutoSize = true, Padding = new Padding(6, 8, 0, 0) }, _numBackupRetention));
        AddRow(layout3, "ทำสำรองเดี๋ยวนี้", Wrap(_btnBackupNow, _btnOpenBackupFolder, _lblBackupState));
        AddRow(layout3, "หลังอัพเดทเวอร์ชัน MariaDB", _btnUpgradeTables);
        AddRow(layout3, "รหัสผ่าน root (งานอัตโนมัติ)", Wrap(_txtRootPass, _lblRootPassState));
        AddRow(layout3, "โฟลเดอร์ข้อมูล (datadir) — ไม่ควรเปลี่ยน", _txtDataDir);
        AddRow(layout3, "character-set-server", _cboCharset);
        AddRow(layout3, "collation-server", _cboCollation);
        AddRow(layout3, "innodb_buffer_pool_size", _cboBufferPool);
        AddRow(layout3, "key_buffer_size", _cboKeyBuffer);
        AddRow(layout3, "max_connections", _numMaxConn);
        AddRow(layout3, "lower_case_table_names (0/1/2)", _numLowercase);
        AddRow(layout3, "ความปลอดภัย", _btnRootPassword);
        tabDb.Controls.Add(layout3);

        var tabSvc = new TabPage("เซอร์วิส");
        var layout4 = MakeTwoColumnLayout();
        AddRow(layout4, "โหมดการทำงาน", Wrap(_rbModeService, _rbModeProcess));
        AddRow(layout4, "ชื่อเซอร์วิส Apache", _txtSvcApache);
        AddRow(layout4, "ชื่อเซอร์วิส MariaDB", _txtSvcDb);
        AddRow(layout4, "เริ่มอัตโนมัติตอนเปิดเครื่อง", _cboStartup);
        AddRow(layout4, "จัดการเซอร์วิสโดยตรง", Wrap(_btnInstallApacheSvc, _btnUninstallApacheSvc, _btnInstallDbSvc, _btnUninstallDbSvc));
        AddRow(layout4, "หมายเหตุ", MakeNote(
            "เปลี่ยนโหมด/ชื่อเซอร์วิสแล้วต้องกด \"บันทึก\" และถอน-ติดตั้งเซอร์วิสใหม่จึงจะมีผล\n" +
            "โหมด Process เหมาะกับการทดสอบชั่วคราว เพราะเซอร์วิสจะหยุดทันทีเมื่อปิดโปรแกรมนี้"));
        tabSvc.Controls.Add(layout4);

        // ---- แท็บ Virtual Hosts ----
        var tabVhosts = new TabPage("Virtual Hosts");
        var layoutV = MakeTwoColumnLayout();
        AddRow(layoutV, "รายการเว็บ (เครื่องหมายถูก = เปิดใช้)", _clbVhosts);
        AddRow(layoutV, "ชื่อโดเมน เช่น myproject.local", _txtVhostName);
        AddRow(layoutV, "โฟลเดอร์เว็บ (DocumentRoot)", Wrap(_txtVhostPath, _btnBrowseVhost));
        AddRow(layoutV, "จัดการ", Wrap(_btnAddVhost, _btnDelVhost));
        AddRow(layoutV, "หมายเหตุ", MakeNote(
            "เว็บแต่ละตัวเข้าได้ที่ http://ชื่อโดเมน — hosts file ถูกอัพเดตให้อัตโนมัติตอนบันทึก\n" +
            "กดบันทึกแล้วรีสตาร์ท Apache จึงจะใช้ได้จริง (โปรแกรมสร้าง conf\\zserver-vhosts.conf ให้)"));
        tabVhosts.Controls.Add(layoutV);

        tabs.TabPages.Add(tabGeneral);
        tabs.TabPages.Add(tabApache);
        tabs.TabPages.Add(tabPhp);
        tabs.TabPages.Add(tabDb);
        tabs.TabPages.Add(tabVhosts);
        tabs.TabPages.Add(tabSvc);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        bottom.Controls.Add(_btnCancel);
        bottom.Controls.Add(_btnSave);
        _btnCancel.Click += (s, e) => Close();
        _btnSave.Click += (s, e) => SaveAll();

        _btnBrowse.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _txtDocRoot.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK) _txtDocRoot.Text = dlg.SelectedPath;
        };
        _btnRootPassword.Click += (s, e) => SetRootPasswordFlow();
        _btnHttpsManager.Click += (s, e) =>
        {
            new HttpsManagerForm(_paths, _cfg, _log, _dl).ShowDialog(this);
        };
        _btnSwitchPhp.Click += (s, e) => SwitchPhpVersion();
        _btnBrowseVhost.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _txtVhostPath.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK) _txtVhostPath.Text = dlg.SelectedPath;
        };
        _btnAddVhost.Click += (s, e) => AddVhost();
        _btnDelVhost.Click += (s, e) =>
        {
            if (_clbVhosts.SelectedIndex >= 0) _clbVhosts.Items.RemoveAt(_clbVhosts.SelectedIndex);
        };
        _btnBackupNow.Click += (s, e) => BackupNow();
        _btnUpgradeTables.Click += (s, e) => UpgradeTables();
        _btnOpenBackupFolder.Click += (s, e) =>
        {
            var dir = Path.Combine(_paths.BackupsDir, "databases");
            Directory.CreateDirectory(dir);
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\""); } catch { }
        };

        _btnInstallApacheSvc.Click += (s, e) => ServiceAction(installApache: true);
        _btnUninstallApacheSvc.Click += (s, e) => ServiceAction(installApache: false);
        _btnInstallDbSvc.Click += (s, e) => ServiceAction(installDb: true);
        _btnUninstallDbSvc.Click += (s, e) => ServiceAction(installDb: false);

        Controls.Add(tabs);
        Controls.Add(bottom);
        Theme.Apply(this);
    }

    private static TableLayoutPanel MakeTwoColumnLayout()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private int _row = 0;
    private void AddRow(TableLayoutPanel t, string label, Control input)
    {
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Padding = new Padding(0, 6, 8, 4),
        };
        input.Margin = new Padding(0, 3, 0, 3);
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(lbl, 0, _row);
        t.Controls.Add(input, 1, _row);
        _row++;
    }

    private static Control Wrap(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true };
        foreach (var c in controls) { c.Margin = new Padding(0, 3, 10, 3); flow.Controls.Add(c); }
        return flow;
    }

    private static Label MakeNote(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Color.DimGray,
        AutoSize = true,
    };

    private void LoadValues()
    {
        // ---- ค่าทั่วไป (autostart sync กับสถานะจริงใน Task Scheduler กันกรณีผู้ใช้ลบ task เอง) ----
        _chkMinimizeToTray.Checked = _cfg.General.MinimizeToTray;
        _chkAutoStartServices.Checked = _cfg.General.AutoStartServices;
        _chkDarkTheme.Checked = _cfg.General.DarkTheme;
        _chkWatchdog.Checked = _cfg.General.WatchdogAutoRestart;
        var taskOn = StartupHelper.IsEnabled();
        if (taskOn != _cfg.General.StartWithWindows)
        {
            _cfg.General.StartWithWindows = taskOn;
            _cfg.Save(_paths);
        }
        _chkStartWithWindows.Checked = taskOn;
        _numLogRetention.Value = Math.Clamp(_cfg.General.LogRetentionDays, 0, 365);

        var a = _cfg.Apache;
        _numPort.Value = Clamp(a.Port, 1, 65535);
        _txtServerName.Text = a.ServerName;
        _txtServerAdmin.Text = a.ServerAdmin;
        _txtDocRoot.Text = a.DocumentRoot;
        _chkKeepAlive.Checked = a.KeepAlive;
        _numKeepAlive.Value = Clamp(a.KeepAliveTimeout, 1, 60);
        _numThreads.Value = Clamp(a.ThreadsPerChild, 50, 2000);
        _chkRewrite.Checked = a.EnableRewrite;
        _chkDeflate.Checked = a.EnableDeflate;
        _chkExpires.Checked = a.EnableExpires;
        _chkAutoIndex.Checked = a.EnableAutoIndex;

        var p = _cfg.Php;
        _cboMemory.Text = p.MemoryLimit;
        _cboUpload.Text = p.UploadMaxFilesize;
        _cboPost.Text = p.PostMaxSize;
        _numMaxExec.Value = Clamp(p.MaxExecutionTime, 0, 3600);
        _numMaxInput.Value = Clamp(p.MaxInputTime, 0, 3600);
        _numMaxVars.Value = Clamp(p.MaxInputVars, 100, 100000);
        _chkDisplayErrors.Checked = p.DisplayErrors;
        _chkLogErrors.Checked = p.LogErrors;
        _chkOpCache.Checked = p.OpCache;
        _cboTimezone.Text = p.Timezone;
        BuildExtensionsPanel();
        FillPhpVersions();
        _chkXdebug.Checked = p.XdebugEnabled;
        _numXdebugPort.Value = Clamp(p.XdebugPort, 1, 65535);
        UpdateXdebugState();

        var m = _cfg.MariaDb;
        _chkBindLocal.Checked = m.BindAddress == "127.0.0.1";
        _numDbPort.Value = Clamp(m.Port, 1, 65535);
        _chkAutoBackup.Checked = m.AutoBackupEnabled;
        _txtBackupTime.Text = m.AutoBackupTime;
        _numBackupRetention.Value = Clamp(m.AutoBackupRetentionDays, 0, 365);
        UpdateRootPassState();
        UpdateBackupState();
        _txtDataDir.Text = m.DataDir;
        _cboCharset.Text = m.CharacterSet;
        _cboCollation.Text = m.Collation;
        _cboBufferPool.Text = m.InnoDBBufferPoolSize;
        _cboKeyBuffer.Text = m.KeyBufferSize;
        _numMaxConn.Value = Clamp(m.MaxConnections, 10, 10000);
        _numLowercase.Value = Clamp(m.LowerCaseTableNames, 0, 2);

        _clbVhosts.Items.Clear();
        foreach (var v in _cfg.Apache.VirtualHosts)
            _clbVhosts.Items.Add(v, v.Enabled);

        var s = _cfg.Services;
        _rbModeService.Checked = s.Mode == "service";
        _rbModeProcess.Checked = s.Mode != "service";
        _txtSvcApache.Text = s.ApacheName;
        _txtSvcDb.Text = s.MariaDbName;
        _cboStartup.SelectedItem = s.StartupType == "auto" ? "auto" : "demand";
    }

    // ================= แผง Extensions (โมดูลเสริม) =================
    private static readonly Dictionary<string, string> ExtInfo = new()
    {
        ["mysqli"] = "เชื่อมต่อ MariaDB/MySQL แบบมาตรฐาน",
        ["pdo_mysql"] = "เชื่อมต่อฐานข้อมูลแบบ PDO",
        ["mbstring"] = "จัดการข้อความหลายภาษา (ไทย/ญี่ปุ่น ฯลฯ)",
        ["intl"] = "จัดรูปแบบวันที่/ตัวเลขสากล",
        ["curl"] = "เรียก URL ภายนอก / API",
        ["gd"] = "สร้าง/แต่งรูปภาพ",
        ["openssl"] = "เข้ารหัส SSL/TLS",
        ["zip"] = "อ่าน/สร้างไฟล์ zip",
        ["fileinfo"] = "ตรวจชนิดไฟล์ที่อัพโหลด",
        ["exif"] = "อ่านข้อมูล EXIF ในรูปภาพ",
        ["soap"] = "SOAP web services",
        ["xsl"] = "แปลง XML ด้วย XSLT",
    };

    private static readonly (string Category, string[] Items)[] ExtGroups =
    [
        ("ฐานข้อมูล", ["mysqli", "pdo_mysql"]),
        ("ข้อความ / ภาษา", ["mbstring", "intl"]),
        ("เว็บ / ไฟล์ / ความปลอดภัย", ["curl", "gd", "openssl", "zip", "fileinfo", "exif", "soap", "xsl"]),
    ];

    /// <summary>สร้างรายการ checkbox จัดกลุ่มตามหมวด พร้อมคำอธิบาย + ตรวจว่าไฟล์ extension มีจริงไหม</summary>
    private void BuildExtensionsPanel()
    {
        _pnlExtensions.SuspendLayout();
        _pnlExtensions.Controls.Clear();

        var phpInstalled = File.Exists(_paths.PhpBin);
        var labelFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        var dimColor = Color.FromArgb(70, 100, 150);

        foreach (var (category, items) in ExtGroups)
        {
            _pnlExtensions.Controls.Add(new Label
            {
                Text = category,
                AutoSize = true,
                Font = labelFont,
                ForeColor = dimColor,
                Margin = new Padding(0, 10, 0, 2),
            });

            foreach (var name in items)
            {
                var desc = ExtInfo.TryGetValue(name, out var d) ? " — " + d : "";
                var cb = new CheckBox
                {
                    AutoSize = true,
                    Tag = name,
                    Text = name + desc,
                    Margin = new Padding(14, 2, 14, 2),
                    Checked = _cfg.Php.Extensions.TryGetValue(name, out var on) && on,
                };

                // PHP ติดตั้งแล้วแต่ไม่มีไฟล์ extension นี้ → ล็อกไว้พร้อมหมายเหตุ (กันเปิดแล้วไม่มีผล)
                if (phpInstalled)
                {
                    var dll = Path.Combine(_paths.PhpDir, "ext", "php_" + name + ".dll");
                    if (!File.Exists(dll))
                    {
                        cb.Checked = false;
                        cb.Enabled = false;
                        cb.Text += "  (ไม่มีในรุ่นที่ติดตั้ง)";
                    }
                }

                _pnlExtensions.Controls.Add(cb);
            }
        }

        _pnlExtensions.ResumeLayout();
    }

    private static decimal Clamp(int v, int min, int max) => Math.Clamp(v, min, max);

    private void SaveAll()
    {
        try
        {
            // ---- ตรวจพอร์ตซ้ำกันหรือไม่ ----
            if (_numPort.Value == _numDbPort.Value)
            {
                MessageBox.Show("พอร์ตของ Apache และ MariaDB ต้องไม่ซ้ำกัน", "ตรวจสอบ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var a = _cfg.Apache;
            a.Port = (int)_numPort.Value;
            a.ServerName = string.IsNullOrWhiteSpace(_txtServerName.Text) ? "localhost" : _txtServerName.Text.Trim();
            a.ServerAdmin = _txtServerAdmin.Text.Trim();
            a.DocumentRoot = _txtDocRoot.Text.Trim();
            a.KeepAlive = _chkKeepAlive.Checked;
            a.KeepAliveTimeout = (int)_numKeepAlive.Value;
            a.ThreadsPerChild = (int)_numThreads.Value;
            a.EnableRewrite = _chkRewrite.Checked;
            a.EnableDeflate = _chkDeflate.Checked;
            a.EnableExpires = _chkExpires.Checked;
            a.EnableAutoIndex = _chkAutoIndex.Checked;

            var vhosts = new List<VhostInfo>();
            foreach (var item in _clbVhosts.Items.Cast<VhostInfo>())
            {
                item.Enabled = _clbVhosts.GetItemChecked(_clbVhosts.Items.IndexOf(item));
                vhosts.Add(item);
            }
            a.VirtualHosts = vhosts;

            var p = _cfg.Php;
            p.MemoryLimit = _cboMemory.Text.Trim();
            p.UploadMaxFilesize = _cboUpload.Text.Trim();
            p.PostMaxSize = _cboPost.Text.Trim();
            p.MaxExecutionTime = (int)_numMaxExec.Value;
            p.MaxInputTime = (int)_numMaxInput.Value;
            p.MaxInputVars = (int)_numMaxVars.Value;
            p.DisplayErrors = _chkDisplayErrors.Checked;
            p.LogErrors = _chkLogErrors.Checked;
            p.OpCache = _chkOpCache.Checked;
            p.Timezone = string.IsNullOrWhiteSpace(_cboTimezone.Text) ? "Asia/Bangkok" : _cboTimezone.Text.Trim();
            p.XdebugEnabled = _chkXdebug.Checked;
            p.XdebugPort = (int)_numXdebugPort.Value;
            // เก็บ extension ทั้งหมด (รวมตัวที่ไม่อยู่ในกลุ่มแสดงผล) แล้วทับด้วยค่าจาก checkbox
            var ext = new Dictionary<string, bool>(_cfg.Php.Extensions);
            foreach (var cb in _pnlExtensions.Controls.OfType<CheckBox>())
                if (cb.Tag is string n) ext[n] = cb.Checked;
            p.Extensions = ext;

            var m = _cfg.MariaDb;
            m.Port = (int)_numDbPort.Value;
            m.CharacterSet = _cboCharset.Text.Trim();
            m.Collation = _cboCollation.Text.Trim();
            m.InnoDBBufferPoolSize = _cboBufferPool.Text.Trim();
            m.KeyBufferSize = _cboKeyBuffer.Text.Trim();
            m.MaxConnections = (int)_numMaxConn.Value;
            m.LowerCaseTableNames = (int)_numLowercase.Value;

            if (!TimeSpan.TryParse(_txtBackupTime.Text.Trim(), out _))
                throw new InvalidOperationException("เวลาสำรองอัตโนมัติต้องเป็นรูปแบบ HH:mm เช่น 03:00");
            m.AutoBackupEnabled = _chkAutoBackup.Checked;
            m.AutoBackupTime = _txtBackupTime.Text.Trim();
            m.AutoBackupRetentionDays = (int)_numBackupRetention.Value;
            if (_txtRootPass.Text.Length > 0)
            {
                m.RootPasswordEnc = SecretProtector.Protect(_txtRootPass.Text);
                _txtRootPass.Clear();
            }

            var bindLocal = _chkBindLocal.Checked;
            if (!bindLocal && MessageBox.Show(
                "ปิด \"อนุญาตเฉพาะเครื่องนี้\" = เครื่องอื่นในเครือข่ายจะเชื่อมต่อฐานข้อมูลได้\n" +
                "ต้องมีรหัสผ่านที่แข็งแรง! ยืนยันหรือไม่?",
                "ความปลอดภัย", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                bindLocal = true;
            m.BindAddress = bindLocal ? "127.0.0.1" : "";

            // DataDir ตั้งค่าครั้งแรกตอนติดตั้งเท่านั้น

            var s = _cfg.Services;
            s.Mode = _rbModeService.Checked ? "service" : "process";
            s.ApacheName = string.IsNullOrWhiteSpace(_txtSvcApache.Text) ? "ZServerApache" : _txtSvcApache.Text.Trim();
            s.MariaDbName = string.IsNullOrWhiteSpace(_txtSvcDb.Text) ? "ZServerMariaDB" : _txtSvcDb.Text.Trim();
            s.StartupType = (_cboStartup.SelectedItem as string ?? "auto") == "demand" ? "demand" : "auto";

            _cfg.General.MinimizeToTray = _chkMinimizeToTray.Checked;
            _cfg.General.AutoStartServices = _chkAutoStartServices.Checked;
            _cfg.General.StartWithWindows = _chkStartWithWindows.Checked;
            _cfg.General.LogRetentionDays = (int)_numLogRetention.Value;
            _cfg.General.DarkTheme = _chkDarkTheme.Checked;
            _cfg.General.WatchdogAutoRestart = _chkWatchdog.Checked;
            Theme.Dark = _chkDarkTheme.Checked;
            Theme.Apply(this);

            // ---- เปิด/ปิด autostart ตอนเข้าสู่ระบบ (Task Scheduler) ----
            try
            {
                if (_chkStartWithWindows.Checked)
                {
                    StartupHelper.Enable(Application.ExecutablePath);
                    _log.Info("เปิด autostart เมื่อเข้าสู่ระบบ Windows (Task Scheduler: " + StartupHelper.TaskName + ")");
                }
                else
                {
                    StartupHelper.Disable();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ตั้งค่าเปิดอัตโนมัติตอนเข้าสู่ระบบไม่สำเร็จ:\n" + ex.Message,
                    "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _cfg.Save(_paths);
            _log.Info("บันทึกการตั้งค่าแล้ว — เขียน config ใหม่");

            // ---- เขียน config ใหม่เฉพาะตัวที่ติดตั้งแล้ว ----
            if (File.Exists(_paths.ApacheBin)) _cw.WriteApacheConf();
            if (File.Exists(_paths.PhpBin)) _cw.WritePhpIni();
            if (File.Exists(_paths.MariaDbBin)) _cw.WriteMariaDbIni();
            _cw.WriteVhostsConf();

            // ---- ซิงก์ hosts file (สำรองเดิมก่อน — แก้เฉพาะส่วนของ zServer) ----
            try
            {
                var hostsPath = HostsHelper.HostsPath;
                if (File.Exists(hostsPath))
                    File.Copy(hostsPath, Path.Combine(_paths.BackupsDir,
                        "hosts." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak"), true);
                HostsHelper.Sync(a.VirtualHosts.Where(v => v.Enabled).Select(v => v.Name));
            }
            catch (Exception exHosts)
            {
                MessageBox.Show("อัพเดต hosts file ไม่สำเร็จ:\n" + exHosts.Message +
                    "\n\nเว็บ vhost จะยังไม่เปิดได้จนกว่าจะเพิ่มบรรทัด \"127.0.0.1  ชื่อโดเมน\" ใน hosts เอง",
                    "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // ---- ถ้าเซอร์วิสกำลังรันอยู่ ถามรีสตาร์ท ----
            var stack = new StackController(_paths, _cfg, _log);
            var apRunning = stack.GetApacheStatus().State == ComponentState.Running;
            var dbRunning = stack.GetMariaDbStatus().State == ComponentState.Running;
            if (apRunning || dbRunning)
            {
                var ans = MessageBox.Show(
                    "บันทึกแล้ว ✓ (config ถูกเขียนใหม่พร้อมสำรองไฟล์เดิมไว้ใน backups\\configs\\)\n\n" +
                    "เซอร์วิสที่กำลังทำงานต้องรีสตาร์ทค่าใหม่จึงจะมีผล — รีสตาร์ทเลยหรือไม่?",
                    "บันทึกสำเร็จ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans == DialogResult.Yes)
                {
                    RunQuiet(() => { if (apRunning) stack.RestartApache(); });
                    RunQuiet(() => { if (dbRunning) stack.RestartMariaDb(); });
                }
            }
            else
            {
                MessageBox.Show("บันทึกการตั้งค่าเรียบร้อย\n(config ถูกเขียนใหม่ ไฟล์เดิมสำรองไว้ใน backups\\configs\\)",
                    "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("บันทึกไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RunQuiet(Action a)
    {
        try { a(); } catch (Exception ex) { MessageBox.Show(ex.Message, "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ================= สำรองฐานข้อมูล / สถานะ =================
    private void UpdateRootPassState()
    {
        _lblRootPassState.Text = string.IsNullOrWhiteSpace(_cfg.MariaDb.RootPasswordEnc)
            ? "ยังไม่ได้บันทึก — เว้นว่างได้ถ้า root ไม่มีรหัสผ่าน"
            : "บันทึกไว้แล้ว ✓ (เข้ารหัส DPAPI) — พิมพ์ทับเพื่อเปลี่ยน";
    }

    private void UpdateXdebugState()
    {
        if (!File.Exists(_paths.PhpBin))
        {
            _lblXdebugState.Text = "ยังไม่ได้ติดตั้ง PHP";
            return;
        }
        var dll = Path.Combine(_paths.PhpDir, "ext", "php_xdebug.dll");
        _lblXdebugState.Text = File.Exists(dll)
            ? "พบ php_xdebug.dll ✓ — ติ๊กเปิด กดบันทึก รีสตาร์ท Apache แล้ว debug ผ่าน VS Code ได้"
            : "ยังไม่มี php_xdebug.dll — โหลดจาก xdebug.org/wizard ตามเวอร์ชัน PHP แล้ววางที่ php\\ext\\php_xdebug.dll";
    }

    private void UpdateBackupState()
    {
        var dir = Path.Combine(_paths.BackupsDir, "databases");
        var count = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.sql").Length : 0;
        var last = _cfg.MariaDb.LastDbBackupDate?.ToString("dd/MM/yyyy HH:mm") ?? "ยังไม่เคย";
        _lblBackupState.Text = $"สำรองล่าสุด: {last} | ไฟล์ {count} ตัว";
    }

    private async void BackupNow()
    {
        if (!File.Exists(_paths.MariaDbBin))
        {
            MessageBox.Show("ยังไม่ได้ติดตั้ง MariaDB", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _btnBackupNow.Enabled = false;
        _lblBackupState.Text = "กำลังสำรอง…";
        try
        {
            var db = new MariaDbManager(_paths, _cfg, _log, _dl);
            var dir = Path.Combine(_paths.BackupsDir, "databases");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "all-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".sql");
            var done = await Task.Run(() => db.DumpAll(file));
            _cfg.MariaDb.LastDbBackupDate = DateTime.Now;
            _cfg.Save(_paths);
            _log.Info("ผู้ใช้สำรองฐานข้อมูลด้วยมือ: " + done);
            MessageBox.Show(
                $"สำรองสำเร็จ ✓\n{done}\nขนาด {new FileInfo(done).Length / 1024.0 / 1024.0:F1} MB",
                "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("สำรองไม่สำเร็จ (ต้องเริ่ม MariaDB ก่อน):\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _lblBackupState.Text = "สำรองไม่สำเร็จ";
        }
        finally
        {
            _btnBackupNow.Enabled = true;
            UpdateBackupState();
        }
    }

    // ================= สลับเวอร์ชัน PHP (ระบบหลายเวอร์ชัน) =================
    private void FillPhpVersions()
    {
        _cboPhpVersion.Items.Clear();
        foreach (var v in PhpVersionManager.ListVersions(_paths))
            _cboPhpVersion.Items.Add(v);
        var active = PhpVersionManager.ActiveVersion(_paths);
        if (active != null && _cboPhpVersion.Items.Contains(active))
            _cboPhpVersion.SelectedItem = active;
    }

    private async void SwitchPhpVersion()
    {
        var ver = _cboPhpVersion.SelectedItem as string;
        if (string.IsNullOrEmpty(ver))
        {
            MessageBox.Show("ยังไม่มีเวอร์ชัน PHP ในระบบหลายเวอร์ชัน — ติดตั้ง PHP ผ่านหน้า \"ติดตั้ง\" ก่อน (โปรแกรมจะย้ายเข้าระบบให้เอง)",
                "ตรวจสอบ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var active = PhpVersionManager.ActiveVersion(_paths);
        if (ver == active)
        {
            MessageBox.Show("ใช้เวอร์ชัน " + ver + " อยู่แล้ว", "แจ้งทราบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(_paths.ApacheBin))
        {
            MessageBox.Show("ยังไม่ได้ติดตั้ง Apache", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var stack = new StackController(_paths, _cfg, _log);
        var wasRunning = stack.GetApacheStatus().State == ComponentState.Running;
        if (MessageBox.Show(
            $"สลับ PHP จาก {active ?? "?"} เป็น {ver}?\n" +
            (wasRunning ? "Apache จะหยุดชั่วคราวแล้วเริ่มใหม่ให้อัตโนมัติ" : "Apache ปัจจุบันไม่ได้รันอยู่ (จะเขียน config ให้อย่างเดียว)"),
            "ยืนยันการสลับ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _btnSwitchPhp.Enabled = false;
        UseWaitCursor = true;
        try
        {
            if (wasRunning) stack.StopApache();
            PhpVersionManager.SwitchTo(_paths, ver, _log);
            _cw.WritePhpIni(); // เขียน config ผ่าน junction → เข้าเวอร์ชันที่สลับไป
            if (_cfg.PhpInstalled != null)
            {
                _cfg.PhpInstalled.Version = ver;
                _cfg.Save(_paths);
            }
            if (wasRunning) stack.StartApache();
            _log.Info("สลับเวอร์ชัน PHP เป็น " + ver + " สำเร็จ");
            MessageBox.Show($"สลับเป็น PHP {ver} เรียบร้อย ✓\nตรวจผลที่ phpinfo.php ได้เลย",
                "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            FillPhpVersions();
        }
        catch (Exception ex)
        {
            MessageBox.Show("สลับเวอร์ชันไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSwitchPhp.Enabled = true;
            UseWaitCursor = false;
        }
    }

    // ================= อัพเกรดตารางระบบ (mariadb-upgrade) =================
    private async void UpgradeTables()
    {
        if (!File.Exists(_paths.MariaDbBin))
        {
            MessageBox.Show("ยังไม่ได้ติดตั้ง MariaDB", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var stack = new StackController(_paths, _cfg, _log);
        var running = stack.GetMariaDbStatus().State == ComponentState.Running;
        if (!running)
        {
            if (MessageBox.Show("MariaDB ยังไม่ทำงานอยู่ — เริ่มก่อนแล้วอัพเกรดตารางระบบต่อไหม?",
                "ตรวจสอบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                RunQuiet(stack.StartMariaDb);
                await Task.Delay(1500);
            }
            else return;
        }

        if (MessageBox.Show(
            "รัน mariadb-upgrade เพื่ออัพเกรดตารางระบบ?\n\n" +
            "ทำเมื่อ: เพิ่งอัพเดท MariaDB ข้ามเวอร์ชัน (เช่น 11.4 → 11.8)\n" +
            "ใช้เวลา: ไม่กี่วินาที–นาที (ขึ้นกับขนาดข้อมูล)\n" +
            "แนะนำ: กด \"สำรองเดี๋ยวนี้\" ก่อนหน้านี้หนึ่งครั้ง",
            "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _btnUpgradeTables.Enabled = false;
        _lblBackupState.Text = "กำลังอัพเกรดตารางระบบ…";
        UseWaitCursor = true;
        try
        {
            var db = new MariaDbManager(_paths, _cfg, _log, _dl);
            var (code, output) = await Task.Run(() => db.RunUpgrade());
            if (code != 0)
                throw new InvalidOperationException(output.Length > 1200 ? output[..1200] + "…" : output);

            _log.Info("mariadb-upgrade สำเร็จ");
            _lblBackupState.Text = "อัพเกรดตารางระบบสำเร็จ ✓";
            var tail = output.Length > 800 ? "…" + output[^800..] : output;
            MessageBox.Show("อัพเกรดตารางระบบสำเร็จ ✓\n\n" + tail,
                "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _log.Error("mariadb-upgrade ล้มเหลว: " + ex.Message);
            _lblBackupState.Text = "อัพเกรดไม่สำเร็จ";
            MessageBox.Show("อัพเกรดตารางระบบไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnUpgradeTables.Enabled = true;
            UseWaitCursor = false;
        }
    }

    // ================= Virtual Hosts =================
    private void AddVhost()
    {
        var name = _txtVhostName.Text.Trim();
        if (!Regex.IsMatch(name, @"^[a-zA-Z0-9][a-zA-Z0-9.-]*$"))
        {
            MessageBox.Show("ชื่อโดเมนใช้ได้เฉพาะ a-z, 0-9, จุด และขีด (ขึ้นต้นด้วยตัวเลข/ตัวอักษร)",
                "ตรวจสอบ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var path = _txtVhostPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(_paths.WwwDir, name);
        Directory.CreateDirectory(path);
        _txtVhostPath.Text = path;

        _clbVhosts.Items.Add(new VhostInfo { Name = name, Path = path, Enabled = true }, true);
        _txtVhostName.Text = "";
    }

    // ================= รหัสผ่าน root ของ MariaDB =================
    private void SetRootPasswordFlow()
    {
        if (!File.Exists(_paths.MariaDbBin))
        {
            MessageBox.Show("ยังไม่ได้ติดตั้ง MariaDB", "ตรวจสอบ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var stack = new StackController(_paths, _cfg, _log);
        if (stack.GetMariaDbStatus().State != ComponentState.Running)
        {
            var ans = MessageBox.Show("MariaDB ยังไม่ทำงานอยู่ — เริ่มก่อนแล้วตั้งรหัสผ่านต่อไหม?",
                "ตรวจสอบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans == DialogResult.Yes)
            {
                RunQuiet(stack.StartMariaDb);
                System.Threading.Thread.Sleep(1500);
            }
            else return;
        }

        using var dlg = new PasswordDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var db = new MariaDbManager(_paths, _cfg, _log, _dl);
            db.SetRootPassword(dlg.NewPassword, dlg.OldPassword);
            MessageBox.Show("ตั้งรหัสผ่าน root เรียบร้อย", "สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ตั้งรหัสผ่านไม่สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ================= จัดการเซอร์วิสโดยตรง =================
    private void ServiceAction(bool installApache = false, bool uninstallApache = false, bool installDb = false, bool uninstallDb = false)
    {
        try
        {
            var apache = new ApacheManager(_paths, _cfg, _log, _dl);
            var db = new MariaDbManager(_paths, _cfg, _log, _dl);
            var auto = _cfg.Services.StartupType == "auto";

            if (installApache)
            {
                if (!File.Exists(_paths.ApacheBin)) throw new InvalidOperationException("ยังไม่ได้ติดตั้ง Apache");
                var cw = new ConfigWriter(_paths, _cfg, _log);
                cw.WriteApacheConf(); // config ต้องผ่านก่อนถึงติดตั้งเซอร์วิสได้
                var err = apache.TestConfig();
                if (err != null) throw new InvalidOperationException("config ของ Apache ยังไม่ผ่าน (httpd -t):\n" + err);
                apache.InstallService(auto);
            }
            if (uninstallApache)
            {
                try { new StackController(_paths, _cfg, _log).StopApache(); } catch { }
                apache.UninstallService();
            }
            if (installDb)
            {
                if (!File.Exists(_paths.MariaDbBin)) throw new InvalidOperationException("ยังไม่ได้ติดตั้ง MariaDB");
                var cw = new ConfigWriter(_paths, _cfg, _log);
                cw.WriteMariaDbIni();
                db.InstallService(auto);
            }
            if (uninstallDb)
            {
                try { new StackController(_paths, _cfg, _log).StopMariaDb(); } catch { }
                db.UninstallService();
            }

            MessageBox.Show("ทำรายการเรียบร้อย", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _log.Info("จัดการเซอร์วิสสำเร็จ (ผู้ใช้กดปุ่มในหน้าตั้งค่า)");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

/// <summary>กล่องตั้งรหัสผ่าน (รหัสเดิม + รหัสใหม่ 2 ครั้ง)</summary>
public sealed class PasswordDialog : Form
{
    private readonly TextBox _txtOld = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox _txtNew = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox _txtNew2 = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };

    public string OldPassword => _txtOld.Text;
    public string NewPassword => _txtNew.Text;

    public PasswordDialog()
    {
        Text = "ตั้งรหัสผ่าน root ของ MariaDB";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        Size = new Size(420, 220);

        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        void Row(int i, string lbl, Control c)
        {
            t.Controls.Add(new Label { Text = lbl, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i);
            t.Controls.Add(c, 1, i);
        }

        Row(0, "รหัสผ่านเดิม (ถ้ามี)", _txtOld);
        Row(1, "รหัสผ่านใหม่", _txtNew);
        Row(2, "ยืนยันรหัสผ่านใหม่", _txtNew2);

        var btnOk = new Button { Text = "ตกลง", DialogResult = DialogResult.OK, Width = 90 };
        var btnCancel = new Button { Text = "ยกเลิก", DialogResult = DialogResult.Cancel, Width = 90 };
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        flow.Controls.Add(btnCancel);
        flow.Controls.Add(btnOk);
        t.Controls.Add(new Label(), 0, 3);
        t.Controls.Add(flow, 1, 3);
        t.Controls.Add(new Label
        {
            Text = "เว้นว่าง \"รหัสผ่านใหม่\" เพื่อลบรหัสผ่าน (ไม่แนะนำ)",
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill,
        }, 1, 4);

        Controls.Add(t);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Theme.Apply(this);

        btnOk.Click += (s, e) =>
        {
            if (_txtNew.Text != _txtNew2.Text)
            {
                MessageBox.Show("รหัสผ่านใหม่สองช่องไม่ตรงกัน", "ตรวจสอบ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };
    }
}
