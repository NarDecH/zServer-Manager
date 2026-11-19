using System.Text;
using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// แก้ไขไฟล์ config ดิบ ๆ (httpd.conf / php.ini / my.ini) — บันทึกทุกครั้งสำรองไฟล์เดิมให้อัตโนมัติ
/// </summary>
public sealed class ConfigEditorForm : Form
{
    private readonly ServerPaths _paths;
    private readonly Logger _log;
    private readonly ConfigWriter _cw;

    private readonly ComboBox _cboFile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly TextBox _txtEditor = new();
    private readonly Button _btnSave = new() { Text = "บันทึก (สำรองไฟล์เดิมอัตโนมัติ)" };
    private readonly Button _btnReload = new() { Text = "โหลดใหม่" };
    private readonly Button _btnOpenFolder = new() { Text = "เปิดโฟลเดอร์ไฟล์นี้" };
    private readonly Label _lblHint = new() { Text = "หมายเหตุ: ค่าที่จัดการโดยโปรแกรม (ส่วนที่อยู่ใน managed block ของ php.ini และค่าหลักใน httpd.conf/my.ini) อาจถูกเขียนทับเมื่อกดบันทึกในหน้า \"ตั้งค่า\"" };

    public ConfigEditorForm(ServerPaths paths, AppConfig cfg, Logger log, ConfigWriter cw)
    {
        _paths = paths;
        _log = log;
        _cw = cw;

        Text = "แก้ไขไฟล์ Config";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(880, 640);

        _cboFile.Items.AddRange(new object[]
        {
            new FileChoice("httpd.conf (Apache)", _paths.ApacheConf, false),
            new FileChoice("php.ini (PHP)", _paths.PhpIni, false),
            new FileChoice("my.ini (MariaDB)", _paths.MariaDbMyIni, false),
            new FileChoice("zserver.json (ค่าตั้งของโปรแกรมนี้)", _paths.ConfigFile, true),
        });
        _cboFile.SelectedIndex = 0;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 0) };
        top.Controls.Add(_cboFile);
        top.Controls.Add(_btnSave);
        top.Controls.Add(_btnReload);
        top.Controls.Add(_btnOpenFolder);

        _lblHint.Dock = DockStyle.Top;
        _lblHint.Height = 40;
        _lblHint.Padding = new Padding(12, 4, 12, 0);
        _lblHint.ForeColor = Color.DimGray;

        _txtEditor.Dock = DockStyle.Fill;
        _txtEditor.Multiline = true;
        _txtEditor.ScrollBars = ScrollBars.Both;
        _txtEditor.WordWrap = false;
        _txtEditor.AcceptsReturn = true;
        _txtEditor.Font = new Font("Consolas", 10f);

        var bottomHint = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, WrapContents = false, Padding = new Padding(6) };
        bottomHint.Controls.Add(_lblHint);

        Controls.Add(_txtEditor);
        Controls.Add(_lblHint);
        Controls.Add(top);
        Theme.Apply(this);

        _cboFile.SelectedIndexChanged += (s, e) => LoadFile();
        _btnReload.Click += (s, e) => LoadFile();
        _btnSave.Click += (s, e) => SaveFile();
        _btnOpenFolder.Click += (s, e) =>
        {
            try
            {
                var choice = CurrentChoice();
                if (File.Exists(choice.Path))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + choice.Path + "\"");
            }
            catch { }
        };

        Load += (s, e) => LoadFile();
    }

    private FileChoice CurrentChoice() => (FileChoice)_cboFile.SelectedItem!;

    private sealed record FileChoice(string Name, string Path, bool IsJson)
    {
        public override string ToString() => Name;
    }

    private void LoadFile()
    {
        var choice = CurrentChoice();
        if (!File.Exists(choice.Path))
        {
            _txtEditor.Text = "(ยังไม่มีไฟล์นี้ — ติดตั้งคอมโพเนนต์ก่อน)";
            _txtEditor.ReadOnly = true;
            return;
        }
        try
        {
            _txtEditor.ReadOnly = false;
            _txtEditor.Text = File.ReadAllText(choice.Path);
            _txtEditor.SelectionStart = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("อ่านไฟล์ไม่สำเร็จ: " + ex.Message, "ผิดพลาด");
        }
    }

    private void SaveFile()
    {
        var choice = CurrentChoice();
        try
        {
            if (choice.IsJson)
            {
                // ตรวจ JSON ก่อนเขียน
                System.Text.Json.JsonDocument.Parse(_txtEditor.Text);
            }
            _cw.BackupConfig(choice.Path);
            File.WriteAllText(choice.Path, _txtEditor.Text, new UTF8Encoding(false));
            _log.Info("บันทึกไฟล์ config ผ่านหน้าแก้ไข: " + choice.Path);

            // config ของ apache ที่แก้เอง ควรทดสอบด้วย httpd -t
            if (choice.Path == _paths.ApacheConf && File.Exists(_paths.ApacheBin))
            {
                var apache = new ApacheManager(_paths, AppConfig.Load(_paths), _log, new Downloader(_log));
                var err = apache.TestConfig();
                MessageBox.Show(err == null
                        ? "บันทึกเรียบร้อย ✓ และ httpd -t ผ่าน"
                        : "บันทึกแล้ว แต่ httpd -t รายงานปัญหา:\n\n" + err + "\n\n(ถ้าเซอร์วิสกำลังรันจะรีสตาร์ทไม่สำเร็จจนกว่าจะแก้ให้ถูก)",
                    err == null ? "สำเร็จ" : "มีปัญหา config",
                    MessageBoxButtons.OK, err == null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("บันทึกเรียบร้อย (ไฟล์เดิมสำรองไว้ใน backups\\configs\\)", "สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("บันทึกไม่สำเร็จ:\n" + ex.Message, "ผิดพลาด",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
