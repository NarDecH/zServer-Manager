namespace ZServer.Manager;

/// <summary>
/// ธีมมืด/สว่างของโปรแกรม — เรียก Theme.Apply(form) ตอนสร้างฟอร์มทุกตัว
/// (กดสลับที่ tray menu หรือหน้าตั้งค่า > ทั่วไป)
/// </summary>
internal static class Theme
{
    public static bool Dark = true;

    public static Color Bg => Dark ? Color.FromArgb(24, 28, 38) : SystemColors.Window;
    public static Color BgAlt => Dark ? Color.FromArgb(32, 38, 52) : SystemColors.Control;
    public static Color InputBg => Dark ? Color.FromArgb(17, 21, 30) : SystemColors.Window;
    public static Color LogBg => Color.FromArgb(24, 26, 32); // กล่อง log ใช้โทน terminal เสมอ
    public static Color Fg => Dark ? Color.FromArgb(222, 230, 242) : SystemColors.ControlText;
    public static Color Dim => Dark ? Color.FromArgb(148, 162, 186) : SystemColors.GrayText;
    public static Color Accent => Dark ? Color.FromArgb(52, 211, 153) : Color.FromArgb(10, 110, 60);

    /// <summary>ทาสีทั้งฟอร์มตามธีมปัจจุบัน (เดิน control ทุกตัวแบบเจาะจงประเภท)</summary>
    public static void Apply(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Fg;
        Walk(form);
    }

    private static void Walk(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case TextBox tb:
                    // กล่อง log (read-only) คงโทน terminal เดิมไว้ทุกธีม
                    if (tb.ReadOnly) tb.BackColor = LogBg;
                    else { tb.BackColor = InputBg; tb.ForeColor = Fg; }
                    break;

                case ComboBox cb:
                    cb.BackColor = InputBg;
                    cb.ForeColor = Fg;
                    break;

                case NumericUpDown nu:
                    nu.BackColor = InputBg;
                    nu.ForeColor = Fg;
                    break;

                case CheckedListBox clb:
                    clb.BackColor = InputBg;
                    clb.ForeColor = Fg;
                    break;

                case ListBox lb:
                    lb.BackColor = InputBg;
                    lb.ForeColor = Fg;
                    break;

                case Label lbl:
                    lbl.BackColor = Bg;
                    // label ที่ตั้งสีจำเพาะไว้แล้ว (สถานะเขียว/ส้ม/แดง) เกงไว้ตามเดิม
                    if (lbl.ForeColor == Color.DimGray || lbl.ForeColor == SystemColors.GrayText)
                        lbl.ForeColor = Dim;
                    else if (Dark && lbl.ForeColor.GetBrightness() < 0.35)
                        lbl.ForeColor = Fg; // สีเข้มบนพื้นเข้มอ่านไม่ออก → ปรับขาว
                    break;

                case GroupBox gb:
                    gb.BackColor = Bg;
                    gb.ForeColor = Fg;
                    break;

                case TabPage tp:
                    tp.BackColor = Bg;
                    tp.ForeColor = Fg;
                    break;

                case StatusStrip ss:
                    ss.BackColor = BgAlt;
                    foreach (System.Windows.Forms.ToolStripItem item in ss.Items)
                        item.ForeColor = Fg;
                    break;

                case Button:
                    break; // ปล่อยปุ่มเป็นสไตล์ระบบ (รวมปุ่มพิเศษที่ตั้งสีเอง)

                default:
                    c.BackColor = Bg;
                    c.ForeColor = Fg;
                    break;
            }

            if (c.HasChildren) Walk(c);
        }
    }
}
