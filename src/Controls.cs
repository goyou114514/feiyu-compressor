using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FeiyuZip
{
    internal abstract class PaintBase : Control
    {
        protected PaintBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.F(10f);
        }

        protected MainForm Host
        {
            get { return Parent as MainForm; }
        }

        /// <summary>把父窗口的背景图对应区域画到本控件上，让圆角外露出的角落也显示背景。</summary>
        protected void PaintBackground(Graphics g)
        {
            MainForm host = Host;
            if (host != null && host.BackgroundBitmap != null)
            {
                g.DrawImage(host.BackgroundBitmap,
                    new Rectangle(0, 0, Width, Height),
                    new Rectangle(Left, Top, Width, Height),
                    GraphicsUnit.Pixel);
            }
            else
            {
                g.Clear(Color.White);
            }
        }
    }

    /// <summary>标题栏上的最小化 / 关闭按钮。</summary>
    internal sealed class WindowButton : PaintBase
    {
        public bool IsClose;
        private bool _hover;

        public WindowButton(bool isClose)
        {
            IsClose = isClose;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            Rectangle r = new Rectangle(1, 1, Width - 2, Height - 2);
            if (_hover)
            {
                Color bg = IsClose ? Color.FromArgb(240, 232, 90, 100) : Color.FromArgb(230, 214, 225, 245);
                Theme.FillRound(g, r, 8, bg);
            }
            using (Pen p = new Pen(IsClose && _hover ? Color.White : Theme.TextMid, 1.7f))
            {
                if (IsClose)
                {
                    int m = 7;
                    g.DrawLine(p, m, m, Width - m - 1, Height - m - 1);
                    g.DrawLine(p, Width - m - 1, m, m, Height - m - 1);
                }
                else
                {
                    int y = Height / 2;
                    g.DrawLine(p, 7, y, Width - 8, y);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    /// <summary>压缩 / 解压 模式切换的胶囊按钮。</summary>
    internal sealed class PillButton : PaintBase
    {
        public bool Selected;
        private bool _hover;

        public PillButton()
        {
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundRect(r, Height / 2))
            {
                if (Selected)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new Rectangle(0, 0, Width, Height + 1), Theme.Accent, Theme.AccentBright, 90f))
                    {
                        g.FillPath(b, path);
                    }
                    using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawPath(p, path);
                }
                else
                {
                    Color bg = _hover ? Color.FromArgb(248, 255, 255, 255) : Color.FromArgb(210, 255, 255, 255);
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb(170, 190, 235), 1f)) g.DrawPath(p, path);
                }
            }
            Font font = Selected ? Theme.F(11.5f) : Theme.F(11f);
            Theme.DrawText(g, Text, font, Selected ? Color.White : Theme.TextMid, r,
                StringAlignment.Center, StringAlignment.Center);
            font.Dispose();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    /// <summary>主操作按钮（渐变蓝，圆角）。</summary>
    internal sealed class GradientButton : PaintBase
    {
        private bool _hover;
        private bool _pressed;

        public GradientButton()
        {
            Cursor = Cursors.Hand;
            Height = 40;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundRect(r, 20))
            {
                if (!Enabled)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 220))) g.FillPath(b, path);
                }
                else
                {
                    Color c1 = _pressed ? Theme.AccentDark : ( _hover ? Theme.AccentBright : Theme.Accent);
                    Color c2 = _pressed ? Theme.Accent : ( _hover ? Color.FromArgb(150, 200, 255) : Theme.AccentBright);
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new Rectangle(0, 0, Width, Height + 1), c1, c2, 90f))
                    {
                        g.FillPath(b, path);
                    }
                    using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawPath(p, path);
                }
            }
            Font font = Theme.F(12.5f);
            Theme.DrawText(g, Text, font, Enabled ? Color.White : Color.FromArgb(240, 245, 255), r,
                StringAlignment.Center, StringAlignment.Center);
            font.Dispose();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>“选择文件 / 选择文件夹”这种小按钮。</summary>
    internal sealed class MiniButton : PaintBase
    {
        private bool _hover;

        public MiniButton()
        {
            Cursor = Cursors.Hand;
            Height = 28;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundRect(r, 13))
            {
                using (SolidBrush b = new SolidBrush(_hover
                    ? Color.FromArgb(245, 255, 255, 255)
                    : Color.FromArgb(215, 255, 255, 255)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(165, 190, 235), 1f)) g.DrawPath(p, path);
            }
            Font font = Theme.F(9.5f);
            Theme.DrawText(g, Text, font, Theme.TextMid, r, StringAlignment.Center, StringAlignment.Center);
            font.Dispose();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    /// <summary>密码输入框右侧的“眼睛”按钮。</summary>
    internal sealed class EyeButton : PaintBase
    {
        public bool VisiblePassword;
        private bool _hover;

        public EyeButton()
        {
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            if (_hover) Theme.FillRound(g, new Rectangle(1, 1, Width - 2, Height - 2), 8, Color.FromArgb(230, 220, 232, 252));

            int cx = Width / 2;
            int cy = Height / 2;
            int w = Width - 12;
            int h = (int)(w * 0.56);
            Rectangle eye = new Rectangle(cx - w / 2, cy - h / 2, w, h);
            using (Pen p = new Pen(Theme.TextMid, 1.4f))
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(eye.X, eye.Y, eye.Width, eye.Height, 200, 140);
                    path.AddArc(eye.X, eye.Y, eye.Width, eye.Height, 20, 140);
                    g.DrawPath(p, path);
                }
                int pr = (int)(h * 0.34);
                if (pr < 2) pr = 2;
                g.DrawEllipse(p, cx - pr, cy - pr, pr * 2, pr * 2);
            }
            if (!VisiblePassword)
            {
                using (Pen p = new Pen(Theme.TextMid, 1.4f))
                {
                    g.DrawLine(p, cx - w / 2 - 1, cy - h / 2 - 2, cx + w / 2 + 1, cy + h / 2 + 2);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    /// <summary>圆角进度条。</summary>
    internal sealed class RoundedProgressBar : PaintBase
    {
        private int _value;

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v != _value)
                {
                    _value = v;
                    Invalidate();
                }
            }
        }

        public RoundedProgressBar()
        {
            Height = 8;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            int radius = Height / 2;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, radius, Color.FromArgb(170, 255, 255, 255));
            int w = (int)((Width - 2) * _value / 100.0);
            if (w > 0)
            {
                Rectangle fill = new Rectangle(1, 1, Math.Max(w, Height - 2), Height - 3);
                if (fill.Width > Width - 2) fill.Width = Width - 2;
                using (GraphicsPath path = Theme.RoundRect(fill, (Height - 3) / 2))
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(fill.Width, 2), Height + 1), Theme.Accent, Theme.AccentBright, 0f))
                {
                    g.FillPath(b, path);
                }
            }
        }
    }

    /// <summary>拖拽 / 点击选择的区域。</summary>
    internal sealed class DragDropPanel : PaintBase
    {
        public event Action<string[]> FilesDropped;

        public string MainText = "把文件 / 文件夹拖到这里";
        public string SubText = "或点击这里选择";
        public bool Busy;

        private bool _hoverDrag;
        private bool _hoverMouse;
        private bool _hasSelection;

        public DragDropPanel()
        {
            AllowDrop = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        public bool HasSelection
        {
            get { return _hasSelection; }
            set { _hasSelection = value; Invalidate(); }
        }

        public void SetText(string main, string sub, bool hasSelection)
        {
            MainText = main;
            SubText = sub;
            _hasSelection = hasSelection;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintBackground(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool active = _hoverDrag || _hoverMouse;
            using (GraphicsPath path = Theme.RoundRect(r, 16))
            {
                Color bg = _hoverDrag ? Color.FromArgb(235, 255, 255, 255)
                    : (active ? Color.FromArgb(215, 255, 255, 255) : Color.FromArgb(180, 255, 255, 255));
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, path);
                using (Pen p = new Pen(active ? Theme.Accent : Color.FromArgb(150, 105, 150, 230), 1.6f))
                {
                    if (!active) p.DashStyle = DashStyle.Dash;
                    g.DrawPath(p, path);
                }
            }

            if (!_hasSelection)
            {
                Font mainFont = Theme.F(10.5f);
                Font subFont = Theme.F(8.5f);
                Rectangle mainRect = new Rectangle(10, Height / 2 - 20, Width - 20, 24);
                Rectangle subRect = new Rectangle(10, Height / 2 + 4, Width - 20, 20);
                Theme.DrawText(g, _hoverDrag ? "松开即可" : MainText, mainFont, Theme.TextDark, mainRect,
                    StringAlignment.Center, StringAlignment.Center);
                Theme.DrawText(g, SubText, subFont, Theme.TextLight, subRect,
                    StringAlignment.Center, StringAlignment.Center);
                mainFont.Dispose();
                subFont.Dispose();
            }
            else
            {
                Font mainFont = Theme.F(10f);
                Font subFont = Theme.F(8f);
                Rectangle mainRect = new Rectangle(12, 12, Width - 24, Height - 46);
                Rectangle subRect = new Rectangle(12, Height - 30, Width - 24, 20);
                Theme.DrawTextWrap(g, MainText, mainFont, Theme.TextDark, mainRect,
                    StringAlignment.Center, StringAlignment.Center);
                Theme.DrawText(g, SubText, subFont, Theme.TextLight, subRect,
                    StringAlignment.Center, StringAlignment.Center);
                mainFont.Dispose();
                subFont.Dispose();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hoverMouse = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hoverMouse = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (!Busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _hoverDrag = true;
                Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
            base.OnDragEnter(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (!Busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _hoverDrag = true;
                Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
            base.OnDragOver(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            _hoverDrag = false;
            Invalidate();
            base.OnDragLeave(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            _hoverDrag = false;
            Invalidate();
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0 && FilesDropped != null)
            {
                FilesDropped(files);
            }
            base.OnDragDrop(e);
        }
    }
}
