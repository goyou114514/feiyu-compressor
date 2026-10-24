using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FeiyuZip
{
    /// <summary>界面主题：颜色、字体、背景图、绘图辅助。</summary>
    internal static class Theme
    {
        public static FontFamily ZcoolFont;
        public static Image Background;
        public static Icon AppIcon;

        // 配色（背景图是蓝白色系，文字用深蓝保证可读）
        public static readonly Color TextDark = Color.FromArgb(38, 54, 100);
        public static readonly Color TextMid = Color.FromArgb(92, 108, 150);
        public static readonly Color TextLight = Color.FromArgb(140, 155, 190);
        public static readonly Color Accent = Color.FromArgb(74, 123, 255);
        public static readonly Color AccentBright = Color.FromArgb(116, 172, 255);
        public static readonly Color AccentDark = Color.FromArgb(46, 88, 215);
        public static readonly Color Danger = Color.FromArgb(225, 70, 85);
        public static readonly Color Success = Color.FromArgb(30, 155, 105);
        public static readonly Color PanelWhite = Color.FromArgb(238, 255, 255, 255);
        public static readonly Color SoftWhite = Color.FromArgb(205, 255, 255, 255);
        public static readonly Color FieldWhite = Color.FromArgb(247, 250, 255);

        public static void Initialize()
        {
            if (ZcoolFont == null)
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ZCOOLKuaiLe-Regular.ttf"))
                {
                    if (s == null) throw new FileNotFoundException("内置字体丢失");
                    byte[] data = new byte[s.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int n = s.Read(data, read, data.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    IntPtr ptr = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, ptr, data.Length);
                    PrivateFontCollection pfc = new PrivateFontCollection();
                    pfc.AddMemoryFont(ptr, data.Length);
                    // 不释放 ptr：字体集合在程序生命周期内持续使用
                    ZcoolFont = pfc.Families[0];
                }
            }
            if (Background == null)
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("bg.jpg"))
                {
                    if (s == null) throw new FileNotFoundException("内置背景图丢失");
                    Background = Image.FromStream(s);
                }
            }
            if (AppIcon == null)
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (s != null) AppIcon = new Icon(s);
                }
            }
        }

        public static Font F(float size)
        {
            return new Font(ZcoolFont, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath path = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, path);
            }
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath path = RoundRect(r, radius))
            using (Pen p = new Pen(color, width))
            {
                g.DrawPath(p, path);
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds,
            StringAlignment align, StringAlignment line)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align;
                sf.LineAlignment = line;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.DrawString(text, font, b, bounds, sf);
                }
            }
        }

        public static void DrawTextWrap(Graphics g, string text, Font font, Color color, Rectangle bounds,
            StringAlignment align, StringAlignment line)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align;
                sf.LineAlignment = line;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.DrawString(text, font, b, bounds, sf);
                }
            }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
