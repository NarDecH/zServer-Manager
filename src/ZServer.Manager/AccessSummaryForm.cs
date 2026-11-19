using ZServer.Core;

namespace ZServer.Manager;

/// <summary>
/// สรุปสถิติจาก access.log ของ Apache: Top IP / Top หน้าเว็บ / สัดส่วน status code
/// </summary>
public sealed class AccessSummaryForm : Form
{
    private readonly ServerPaths _paths;
    private readonly ListBox _lstIps = new();
    private readonly ListBox _lstPaths = new();
    private readonly ListBox _lstStatus = new();
    private readonly Label _lblInfo = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _btnRefresh = new() { Text = "วิเซตใหม่", Width = 130 };

    public AccessSummaryForm(ServerPaths paths, AppConfig cfg)
    {
        _paths = paths;

        Text = "สรุปสถิติ access.log";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(880, 560);
        MinimumSize = new Size(880, 560);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 0) };
        var btnOpenLog = new Button { Text = "เปิดไฟล์ access.log", Width = 170 };
        btnOpenLog.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _paths.ApacheAccessLog + "\""); } catch { }
        };
        top.Controls.Add(_btnRefresh);
        top.Controls.Add(btnOpenLog);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titles = new[] { "Top IP (ผู้เข้าชม)", "Top หน้าเว็บ (path)", "สถานะ HTTP" };
        var lists = new[] { _lstIps, _lstPaths, _lstStatus };
        for (int i = 0; i < 3; i++)
        {
            lists[i].Dock = DockStyle.Fill;
            grid.Controls.Add(new Label { Text = titles[i], AutoSize = true, Padding = new Padding(6, 6, 0, 2) }, i, 0);
            grid.Controls.Add(lists[i], i, 1);
        }

        _lblInfo.Dock = DockStyle.Top;
        _lblInfo.Height = 24;
        _lblInfo.Padding = new Padding(10, 2, 0, 0);

        var bottomHint = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
        bottomHint.Controls.Add(new Label
        {
            Text = "วิเซตจาก combined log format — ไฟล์ใหญ่มากจะวิเซตเฉพาะ 300,000 บรรทัดล่าสุดพอ",
            AutoSize = true,
            ForeColor = Color.DimGray,
        });

        Controls.Add(grid);
        Controls.Add(_lblInfo);
        Controls.Add(top);
        Controls.Add(bottomHint);
        Theme.Apply(this);

        _btnRefresh.Click += (s, e) => RefreshStats();
        Load += (s, e) => RefreshStats();
    }

    private async void RefreshStats()
    {
        _btnRefresh.Enabled = false;
        _lblInfo.Text = "กำลังวิเซต access.log…";
        try
        {
            var (stats, error) = await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_paths.ApacheAccessLog))
                        return (null as AccessLogStats.Stats, "ยังไม่มีไฟล์ access.log — เริ่ม Apache แล้วมีคนเข้าเว็บก่อน");
                    return (AccessLogStats.Parse(_paths.ApacheAccessLog), "");
                }
                catch (Exception ex)
                {
                    return (null as AccessLogStats.Stats, ex.Message);
                }
            });

            if (stats == null)
            {
                _lblInfo.Text = error;
                return;
            }

            FillList(_lstIps, stats.TopIps);
            FillList(_lstPaths, stats.TopPaths);
            FillList(_lstStatus, stats.StatusCounts);
            _lblInfo.Text = $"วิเซต {stats.Lines:N0} บรรทัด" +
                (stats.SkippedLines > 0 ? $" (ข้าม {stats.SkippedLines:N0} บรรทัดที่อ่านไม่ได้)" : "");
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    private static void FillList(ListBox list, List<(string Key, int Count)> items)
    {
        list.Items.Clear();
        foreach (var (key, count) in items)
            list.Items.Add($"{count,8:N0}  ×  {key}");
        if (items.Count == 0)
            list.Items.Add("(ยังไม่มีข้อมูล)");
    }
}
