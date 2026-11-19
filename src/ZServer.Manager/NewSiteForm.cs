using System.Text;
using System.Text.RegularExpressions;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// สร้างเว็บใหม่จากเทมเพลต: เว็บเปล่า หรือ WordPress
/// — สร้างโฟลเดอร์ + database/user + Virtual Host และแก้ hosts ให้ครบในคลิกเดียว
/// </summary>
public sealed class NewSiteForm : Form
{
    private readonly ServerPaths _paths;
    private readonly AppConfig _cfg;
    private readonly Logger _log;
    private readonly Downloader _dl;

    private readonly TextBox _txtSiteName = new();
    private readonly RadioButton _rdoBlank = new() { Text = "เว็บเปล่า (index.php พร้อมใช้)", AutoSize = true };
    private readonly RadioButton _rdoWordpress = new() { Text = "WordPress (ดาวน์โหลด ~25MB จาก wordpress.org)", AutoSize = true };
    private readonly CheckBox _chkDb = new() { Text = "สร้าง database + user ให้เว็บนี้ด้วย", Checked = true, AutoSize = true };
    private readonly CheckBox _chkVhost = new() { Text = "สร้าง Virtual Host ให้เข้าผ่านชื่อโดเมน", Checked = true, AutoSize = true };
    private readonly TextBox _txtRootPass = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly Button _btnCreate = new() { Text = "สร้างเว็บใหม่", Width = 200 };
    private readonly TextBox _txtResult = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9.5f),
        BackColor = Theme.LogBg,
        ForeColor = Color.FromArgb(180, 220, 190),
        Dock = DockStyle.Fill,
    };
    private readonly Button _btnOpenSite = new() { Text = "เปิดเว็บ", Width = 140, Enabled = false };
    private readonly Button _btnOpenFolder = new() { Text = "เปิดโฟลเดอร์เว็บ", Width = 160, Enabled = false };
    private string _siteUrl = "";

    public NewSiteForm(ServerPaths paths, AppConfig cfg, Logger log, Downloader dl)
    {
        _paths = paths;
        _cfg = cfg;
        _log = log;
        _dl = dl;

        Text = "สร้างเว็บใหม่";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 640);
        MinimumSize = new Size(720, 640);

        BuildUi();
        Theme.Apply(this);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        // ---- ชื่อเว็บ ----
        var gName = new GroupBox { Text = " ชื่อเว็บ (ใช้เป็นชื่อโฟลเดอร์ + โดเมน) ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        _txtSiteName.Dock = DockStyle.Fill;
        _txtSiteName.Margin = new Padding(0, 6, 0, 0);
        gName.Controls.Add(_txtSiteName);
        root.Controls.Add(gName);

        // ---- เทมเพลต + ตัวเลือก ----
        var gTemplate = new GroupBox { Text = " เทมเพลต & ตัวเลือก ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        var layT = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layT.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layT.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layT.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layT.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layT.Controls.Add(_rdoBlank, 0, 0);
        layT.Controls.Add(_rdoWordpress, 0, 1);
        layT.Controls.Add(_chkVhost, 0, 2);
        layT.Controls.Add(_chkDb, 0, 3);
        gTemplate.Controls.Add(layT);
        root.Controls.Add(gTemplate);

        // ---- รหัสผ่าน root (ถ้าสร้าง DB) ----
        var gRoot = new GroupBox { Text = " รหัสผ่าน root ของ MariaDB (ถ้ามี — เว้นว่างถ้าไม่เคยตั้ง) ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        _txtRootPass.Dock = DockStyle.Fill;
        _txtRootPass.Margin = new Padding(0, 6, 0, 0);
        gRoot.Controls.Add(_txtRootPass);
        root.Controls.Add(gRoot);

        // ---- ผลลัพธ์ ----
        var gResult = new GroupBox { Text = " ผลลัพธ์ ", Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4) };
        gResult.Controls.Add(_txtResult);
        root.Controls.Add(gResult);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2) };
        _btnCreate.Click += (s, e) => Create();
        _btnOpenSite.Click += (s, e) => { try { StackController.OpenBrowser(_siteUrl); } catch { } };
        _btnOpenFolder.Click += (s, e) =>
        {
            var folder = Path.Combine(_paths.WwwDir, _txtSiteName.Text.Trim());
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\""); } catch { }
        };
        bottom.Controls.Add(_btnCreate);
        bottom.Controls.Add(_btnOpenSite);
        bottom.Controls.Add(_btnOpenFolder);
        bottom.Controls.Add(new Label { Text = "WordPress จะให้ทำ setup ใน browser โดยใช้ DB/user ตามที่แสดงในผลลัพธ์", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(10, 10, 0, 0) });
        root.Controls.Add(bottom);

        Controls.Add(root);
    }

    private async void Create()
    {
        var site = _txtSiteName.Text.Trim().ToLowerInvariant();
        var folder = Path.Combine(_paths.WwwDir, site);
        var domain = site + ".local";
        var dbName = site.Replace('-', '_');
        var dbUser = dbName;
        var dbPass = GeneratePassword();
        var rootPass = _txtRootPass.Text.Length > 0 ? _txtRootPass.Text : null;

        if (!Regex.IsMatch(site, @"^[a-z0-9][a-z0-9-]*$"))
        {
            MessageBox.Show("ชื่อเว็บใช้ได้เฉพาะ a-z, 0-9 และขีด (-) เท่านั้น", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            MessageBox.Show($"โฟลเดอร์ {folder} มีไฟล์อยู่แล้ว — ใช้ชื่ออื่นหรือลบโฟลเดอร์ก่อน", "ตรวจสอบ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnCreate.Enabled = false;
        UseWaitCursor = true;
        _txtResult.Clear();
        try
        {
            Directory.CreateDirectory(folder);
            _log.Info($"สร้างเว็บใหม่: {site} (เทมเพลต: {(_rdoWordpress.Checked ? "WordPress" : "เว็บเปล่า")})");

            // ---- เทมเพลต ----
            if (_rdoWordpress.Checked)
            {
                _txtResult.AppendText("ดาวน์โหลด WordPress จาก wordpress.org…\n");
                var zip = Path.Combine(_paths.DownloadsDir, "wordpress-latest.zip");
                var dlProgress = new Progress<DownloadProgress>(p =>
                    _txtResult.AppendText($"  ดาวน์โหลด {p.Bytes / 1024.0 / 1024.0:F1} MB — {p.SpeedText}\n"));
                await _dl.DownloadAsync("https://wordpress.org/latest.zip", zip, dlProgress);
                _txtResult.AppendText("แตกไฟล์ลงเว็บ…\n");
                var staging = await Task.Run(() => ZipExtractor.ExtractToStaging(zip, _paths.StagingDir));
                ZipExtractor.MoveContents(staging, folder);
                try { Directory.Delete(Path.GetDirectoryName(staging)!, true); } catch { }
            }
            else
            {
                File.WriteAllText(Path.Combine(folder, "index.php"),
                    "<?php\n// เว็บใหม่ของคุณ — เริ่มเขียนโค้ดได้เลย!\necho \"<h1>" + site + "</h1>\";\n",
                    new UTF8Encoding(false));
            }

            // ---- Database + user ----
            string? dbInfo = null;
            if (_chkDb.Checked && File.Exists(_paths.MariaDbBin))
            {
                _txtResult.AppendText("สร้าง database + user…\n");
                var db = new MariaDbManager(_paths, _cfg, _log, _dl);
                var (ok, output) = db.CreateDatabaseAndUser(dbName, dbUser, dbPass, rootPass);
                if (!ok)
                    throw new InvalidOperationException("สร้าง database/user ไม่สำเร็จ:\n" + output);
                dbInfo = $"DB:   {dbName}\nUser: {dbUser}\nPass: {dbPass}\nHost: 127.0.0.1  Port: {_cfg.MariaDb.Port}";
            }

            // ---- Virtual Host ----
            if (_chkVhost.Checked)
            {
                _cfg.Apache.VirtualHosts.Add(new VhostInfo { Name = domain, Path = folder, Enabled = true });
                _cfg.Save(_paths);
                _cwWriteVhosts();
                try
                {
                    var hostsPath = HostsHelper.HostsPath;
                    if (File.Exists(hostsPath))
                        File.Copy(hostsPath, Path.Combine(_paths.BackupsDir,
                            "hosts." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak"), true);
                    HostsHelper.Sync(_cfg.Apache.VirtualHosts.Where(v => v.Enabled).Select(v => v.Name));
                }
                catch (Exception exHosts)
                {
                    _log.Warn("แก้ hosts ไม่สำเร็จ: " + exHosts.Message);
                    _txtResult.AppendText("⚠ แก้ hosts file ไม่สำเร็จ — เพิ่มบรรทัด \"127.0.0.1  " +
                        domain + "\" ใน hosts เองหรือรีรันโปรแกรมแบบ admin\n");
                }
                _siteUrl = "http://" + domain;
            }
            else
            {
                _siteUrl = $"http://localhost:{_cfg.Apache.Port}/{site}/";
            }

            _txtResult.AppendText($"\nสร้างเว็บ \"{site}\" เสร็จ ✓\nURL: {_siteUrl}\n");
            if (dbInfo != null) _txtResult.AppendText("\nข้อมูลฐานข้อมูล (ใช้ตอน setup WordPress):\n" + dbInfo + "\n");
            if (_rdoWordpress.Checked)
                _txtResult.AppendText("\nขั้นตอนต่อ: เปิด URL แล้วทำตามตัวติดตั้ง WordPress โดยกรอกข้อมูล DB ด้านบน\n");

            _btnOpenSite.Enabled = _chkVhost.Checked;
            _btnOpenFolder.Enabled = true;
            _log.Info($"สร้างเว็บใหม่สำเร็จ: {site} → {folder}");
        }
        catch (Exception ex)
        {
            _txtResult.AppendText("✗ สร้างไม่สำเร็จ: " + ex.Message + "\n");
            MessageBox.Show("สร้างเว็บใหม่ไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCreate.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void _cwWriteVhosts()
    {
        new ConfigWriter(_paths, _cfg, _log).WriteVhostsConf();
        try
        {
            var hostsPath = HostsHelper.HostsPath;
            if (File.Exists(hostsPath))
                File.Copy(hostsPath, Path.Combine(_paths.BackupsDir,
                    "hosts." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak"), true);
            HostsHelper.Sync(_cfg.Apache.VirtualHosts.Where(v => v.Enabled).Select(v => v.Name));
        }
        catch (Exception ex)
        {
            _log.Warn("แก้ hosts ไม่สำเร็จ: " + ex.Message);
            _txtResult.AppendText("⚠ แก้ hosts file ไม่สำเร็จ — เพิ่มบรรทัด \"127.0.0.1  " +
                _txtSiteName.Text.Trim() + ".local\" ใน hosts เองหรือรีรันโปรแกรมแบบ admin\n");
        }
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%_-";
        var sb = new StringBuilder();
        for (int i = 0; i < 16; i++) sb.Append(chars[Random.Shared.Next(chars.Length)]);
        return sb.ToString();
    }
}
