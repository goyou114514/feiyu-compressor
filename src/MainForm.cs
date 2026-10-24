using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FeiyuZip
{
    internal enum WorkMode
    {
        Compress,
        Extract
    }

    internal sealed class MainForm : Form
    {
        // ---------- 布局常量 ----------
        private const int ClientW = 500;
        private const int ClientH = 400;
        private const int PassX = 20, PassY = 96, PassW = 460, PassH = 40;
        private const int DropX = 20, DropY = 146, DropW = 460, DropH = 96;
        private const int StatusY = 302;

        private Bitmap _background;
        private WorkMode _mode = WorkMode.Compress;

        private WindowButton _closeButton;
        private WindowButton _minButton;
        private PillButton _tabCompress;
        private PillButton _tabExtract;
        private TextBox _passwordBox;
        private EyeButton _eyeButton;
        private DragDropPanel _drop;
        private MiniButton _btnFiles;
        private MiniButton _btnFolder;
        private RoundedProgressBar _progress;
        private GradientButton _startButton;

        private readonly List<string> _selected = new List<string>();
        private bool _running;
        private bool _showPassword;
        private string _lastOutput;
        private string _statusText = "输入密码，把文件拖进来就会自动开始";
        private Color _statusColor = Theme.TextMid;
        private bool _dragging;
        private Point _dragOffset;

        [DllImport("user32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        public Bitmap BackgroundBitmap
        {
            get { return _background; }
        }

        // 供截图自检用：切换模式、选中文件
        public void TestSetMode(WorkMode mode) { SetMode(mode); }
        public void TestSetSelection(string[] files) { SetSelection(files); }
        public void TestSetPassword(string password) { _passwordBox.Text = password; }
        public bool IsBusy { get { return _running; } }

        public MainForm()
        {
            Theme.Initialize();

            Text = "肥鱼压缩器";
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(ClientW, ClientH);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = true;
            if (Theme.AppIcon != null) Icon = Theme.AppIcon;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            KeyPreview = true;

            PrepareBackground();
            BuildControls();
        }

        // ---------- 背景 ----------

        private void PrepareBackground()
        {
            if (_background != null) _background.Dispose();
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);
            _background = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(_background))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(Theme.Background, new Rectangle(0, 0, w, h));
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                {
                    g.FillRectangle(veil, 0, 0, w, h);
                }
            }
        }

        // ---------- 控件 ----------

        private void BuildControls()
        {
            _closeButton = new WindowButton(true);
            _closeButton.Size = new Size(24, 24);
            _closeButton.Location = new Point(ClientW - 44, 10);
            _closeButton.Click += delegate { Close(); };
            Controls.Add(_closeButton);

            _minButton = new WindowButton(false);
            _minButton.Size = new Size(24, 24);
            _minButton.Location = new Point(ClientW - 76, 10);
            _minButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            Controls.Add(_minButton);

            _tabCompress = new PillButton();
            _tabCompress.Text = "压缩";
            _tabCompress.Size = new Size(104, 36);
            _tabCompress.Location = new Point(142, 50);
            _tabCompress.TabStop = true;
            _tabCompress.Click += delegate { SetMode(WorkMode.Compress); };
            Controls.Add(_tabCompress);

            _tabExtract = new PillButton();
            _tabExtract.Text = "解压";
            _tabExtract.Size = new Size(104, 36);
            _tabExtract.Location = new Point(254, 50);
            _tabExtract.Click += delegate { SetMode(WorkMode.Extract); };
            Controls.Add(_tabExtract);

            _passwordBox = new TextBox();
            _passwordBox.BorderStyle = BorderStyle.None;
            _passwordBox.BackColor = Theme.FieldWhite;
            _passwordBox.ForeColor = Theme.TextDark;
            _passwordBox.Font = Theme.F(10.5f);
            _passwordBox.Location = new Point(PassX + 14, PassY + 9);
            _passwordBox.Size = new Size(PassW - 14 - 54, 24);
            _passwordBox.PasswordChar = '●';
            _passwordBox.MaxLength = 128;
            _passwordBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    StartWork();
                }
            };
            Controls.Add(_passwordBox);

            _eyeButton = new EyeButton();
            _eyeButton.Size = new Size(42, 32);
            _eyeButton.Location = new Point(PassX + PassW - 50, PassY + 4);
            _eyeButton.Click += delegate
            {
                _showPassword = !_showPassword;
                _passwordBox.PasswordChar = _showPassword ? '\0' : '●';
                _eyeButton.VisiblePassword = _showPassword;
                _eyeButton.Invalidate();
            };
            Controls.Add(_eyeButton);

            _drop = new DragDropPanel();
            _drop.Location = new Point(DropX, DropY);
            _drop.Size = new Size(DropW, DropH);
            _drop.FilesDropped += OnFilesDropped;
            Controls.Add(_drop);

            _btnFiles = new MiniButton();
            _btnFiles.Text = "选择文件";
            _btnFiles.Size = new Size(108, 28);
            _btnFiles.Location = new Point(132, 250);
            _btnFiles.Click += delegate { ChooseFiles(); };
            Controls.Add(_btnFiles);

            _btnFolder = new MiniButton();
            _btnFolder.Text = "选择文件夹";
            _btnFolder.Size = new Size(108, 28);
            _btnFolder.Location = new Point(260, 250);
            _btnFolder.Click += delegate { ChooseFolder(); };
            Controls.Add(_btnFolder);

            _progress = new RoundedProgressBar();
            _progress.Location = new Point(20, 290);
            _progress.Size = new Size(460, 8);
            Controls.Add(_progress);

            _startButton = new GradientButton();
            _startButton.Text = "开始压缩";
            _startButton.Size = new Size(180, 40);
            _startButton.Location = new Point(160, 330);
            _startButton.Click += delegate { StartWork(); };
            Controls.Add(_startButton);

            SetMode(WorkMode.Compress);
        }

        // ---------- 绘制 ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_background != null) g.DrawImage(_background, 0, 0);
            else g.Clear(Color.White);

            DrawTitle(g);
            DrawPasswordFrame(g);
            DrawStatus(g);
            DrawFooter(g);

            base.OnPaint(e);
        }

        private void DrawTitle(Graphics g)
        {
            Font font = Theme.F(16f);
            Rectangle r = new Rectangle(24, 5, 300, 34);
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush outline = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    g.DrawString("肥鱼压缩器", font, outline, new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height), sf);
                    g.DrawString("肥鱼压缩器", font, outline, new Rectangle(r.X - 1, r.Y + 1, r.Width, r.Height), sf);
                    g.DrawString("肥鱼压缩器", font, outline, new Rectangle(r.X + 1, r.Y - 1, r.Width, r.Height), sf);
                    g.DrawString("肥鱼压缩器", font, outline, new Rectangle(r.X - 1, r.Y - 1, r.Width, r.Height), sf);
                }
                using (SolidBrush brush = new SolidBrush(Theme.TextDark))
                {
                    g.DrawString("肥鱼压缩器", font, brush, r, sf);
                }
            }
            font.Dispose();
        }

        private void DrawPasswordFrame(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(PassX, PassY, PassW, PassH);
            Theme.FillRound(g, r, 12, Theme.FieldWhite);
            Color border = _passwordBox != null && _passwordBox.Focused
                ? Theme.Accent
                : Color.FromArgb(165, 188, 226);
            Theme.DrawRound(g, r, 12, border, 1.5f);
        }

        private void DrawStatus(Graphics g)
        {
            Rectangle r = new Rectangle(20, StatusY, 460, 22);
            Font font = Theme.F(9.5f);
            Theme.DrawText(g, _statusText, font, _statusColor, r, StringAlignment.Center, StringAlignment.Center);
            font.Dispose();
        }

        private void DrawFooter(Graphics g)
        {
            string tip = _mode == WorkMode.Compress
                ? "输出：原名.mp4.7z（两层加密，后缀伪装）"
                : "解压：还原到压缩包所在位置（自动两层）";
            Rectangle r = new Rectangle(20, 376, 460, 18);
            Font font = Theme.F(8.5f);
            Theme.DrawText(g, tip, font, Theme.TextMid, r, StringAlignment.Center, StringAlignment.Center);
            font.Dispose();
        }

        // ---------- 窗口圆角与拖动 ----------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundRegion();
            if (_passwordBox != null && _passwordBox.Handle != IntPtr.Zero)
            {
                SendMessage(_passwordBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, "输入密码（两层加密共用）");
            }
        }

        private void ApplyRoundRegion()
        {
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 22, 22);
                SetWindowRgn(Handle, rgn, true);
            }
            catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = e.Location;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point screen = PointToScreen(e.Location);
                Location = new Point(screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
                return;
            }
            bool overStatus = e.Y >= StatusY - 2 && e.Y <= StatusY + 24;
            Cursor = overStatus && !string.IsNullOrEmpty(_lastOutput) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Y >= StatusY - 2 && e.Y <= StatusY + 24 && !string.IsNullOrEmpty(_lastOutput))
            {
                OpenOutput();
            }
        }

        private void OpenOutput()
        {
            if (string.IsNullOrEmpty(_lastOutput)) return;
            try
            {
                if (Directory.Exists(_lastOutput))
                {
                    Process.Start("explorer.exe", "\"" + _lastOutput + "\"");
                }
                else if (File.Exists(_lastOutput))
                {
                    Process.Start("explorer.exe", "/select,\"" + _lastOutput + "\"");
                }
            }
            catch { }
        }

        // ---------- 状态与模式 ----------

        private void SetStatus(string text, Color color)
        {
            _statusText = text;
            _statusColor = color;
            Invalidate(new Rectangle(0, StatusY - 4, ClientW, 30));
        }

        private void SafeStatus(string text, Color color)
        {
            SafeInvoke(delegate { SetStatus(text, color); });
        }

        private void SafeProgress(int percent)
        {
            SafeInvoke(delegate { _progress.Value = percent; });
        }

        private void SafeInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void SetMode(WorkMode mode)
        {
            if (_running) return;
            _mode = mode;
            _tabCompress.Selected = mode == WorkMode.Compress;
            _tabExtract.Selected = mode == WorkMode.Extract;
            _tabCompress.Invalidate();
            _tabExtract.Invalidate();

            _selected.Clear();
            _lastOutput = null;
            _progress.Value = 0;
            _btnFiles.Text = mode == WorkMode.Compress ? "选择文件" : "选择压缩包";
            _btnFolder.Visible = mode == WorkMode.Compress;
            if (mode == WorkMode.Compress)
            {
                _btnFiles.Location = new Point(132, 250);
                _btnFolder.Location = new Point(260, 250);
                SetStatus("把文件或文件夹拖进来，会自动加密压缩", Theme.TextMid);
            }
            else
            {
                _btnFiles.Location = new Point(196, 250);
                SetStatus("把 .mp4.7z / .7z 文件拖进来，会自动两层解压", Theme.TextMid);
            }
            UpdateDropText();
            _startButton.Text = mode == WorkMode.Compress ? "开始压缩" : "开始解压";
            Invalidate();
        }

        private void UpdateDropText()
        {
            if (_selected.Count == 0)
            {
                if (_mode == WorkMode.Compress)
                    _drop.SetText("把文件 / 文件夹拖到这里", "或点击选择（支持多选）", false);
                else
                    _drop.SetText("把压缩包拖到这里", "或点击选择 .7z / .mp4.7z 文件", false);
                return;
            }
            if (_selected.Count == 1)
            {
                string name = Path.GetFileName(_selected[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                bool isDir = Directory.Exists(_selected[0]);
                string dir = Path.GetDirectoryName(_selected[0]);
                _drop.SetText(name, (isDir ? "文件夹 · " : "文件 · ") + dir, true);
            }
            else
            {
                _drop.SetText("已选择 " + _selected.Count + " 项", "点这里可以重新选择", true);
            }
        }

        // ---------- 选择文件 ----------

        private void ChooseFiles()
        {
            if (_running) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = _mode == WorkMode.Compress ? "选择要压缩的文件" : "选择要解压的压缩包";
                dlg.Multiselect = true;
                if (_mode == WorkMode.Compress) dlg.Filter = "所有文件|*.*";
                else dlg.Filter = "本程序的压缩包|*.7z;*.mp4.7z|所有文件|*.*";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    SetSelection(dlg.FileNames);
                }
            }
        }

        private void ChooseFolder()
        {
            if (_running) return;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择要压缩的文件夹";
                dlg.ShowNewFolderButton = false;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktop)) dlg.SelectedPath = desktop;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    SetSelection(new string[] { dlg.SelectedPath });
                }
            }
        }

        private void OnFilesDropped(string[] files)
        {
            if (_running) return;
            SetSelection(files);
        }

        private void SetSelection(string[] files)
        {
            _selected.Clear();
            bool skippedFolder = false;
            foreach (string f in files)
            {
                if (File.Exists(f)) _selected.Add(f);
                else if (Directory.Exists(f))
                {
                    if (_mode == WorkMode.Compress) _selected.Add(f);
                    else skippedFolder = true;
                }
            }
            if (_selected.Count == 0)
            {
                SetStatus(skippedFolder ? "解压请选择压缩文件，而不是文件夹" : "没有有效的文件或文件夹", Theme.Danger);
                return;
            }
            UpdateDropText();
            if (string.IsNullOrEmpty(_passwordBox.Text))
            {
                SetStatus("已选中，请输入密码后点开始", Theme.Accent);
                _passwordBox.Focus();
                return;
            }
            StartWork();
        }

        // ---------- 执行 ----------

        private async void StartWork()
        {
            if (_running) return;
            if (string.IsNullOrEmpty(_passwordBox.Text))
            {
                SetStatus("请先输入密码", Theme.Danger);
                _passwordBox.Focus();
                return;
            }
            if (_selected.Count == 0)
            {
                SetStatus("请先拖入或选择文件", Theme.Danger);
                return;
            }

            string password = _passwordBox.Text;
            List<string> inputs = new List<string>(_selected);
            _running = true;
            _lastOutput = null;
            _progress.Value = 0;
            UpdateRunningState(true);
            SetStatus(_mode == WorkMode.Compress ? "正在加密压缩…" : "正在解压…", Theme.Accent);

            try
            {
                if (_mode == WorkMode.Compress)
                {
                    List<CompressionResult> results = await Task.Run(delegate
                    {
                        return CompressionFlow.Run(inputs, password,
                            delegate (string s) { SafeStatus(s, Theme.TextMid); },
                            delegate (int p) { SafeProgress(p); },
                            delegate (string s) { SafeStatus(s, Theme.TextMid); });
                    });
                    if (results.Count > 0)
                    {
                        _lastOutput = results[0].FinalArchivePath;
                        string name = Path.GetFileName(results[0].FinalArchivePath);
                        SetStatus(results.Count == 1
                            ? "完成！已生成 " + name + "（点这里查看）"
                            : "完成！共生成 " + results.Count + " 个压缩包（点这里查看）", Theme.Success);
                    }
                }
                else
                {
                    List<ExtractionResult> results = await Task.Run(delegate
                    {
                        return ExtractionFlow.Run(inputs, password,
                            delegate (string s) { SafeStatus(s, Theme.TextMid); },
                            delegate (int p) { SafeProgress(p); },
                            delegate (string s) { SafeStatus(s, Theme.TextMid); });
                    });
                    if (results.Count > 0)
                    {
                        _lastOutput = results[0].OutputDir;
                        string folder = Path.GetFileName(results[0].OutputDir.TrimEnd(Path.DirectorySeparatorChar));
                        if (string.IsNullOrEmpty(folder)) folder = results[0].OutputDir;
                        SetStatus("完成！已还原到「" + folder + "」（点这里查看）", Theme.Success);
                    }
                }
                _progress.Value = 100;
            }
            catch (InvalidPasswordException)
            {
                SetStatus("密码错误，请检查后重试", Theme.Danger);
            }
            catch (Exception ex)
            {
                SetStatus("出错了：" + Shorten(ex.Message, 60), Theme.Danger);
            }
            finally
            {
                _running = false;
                UpdateRunningState(false);
            }
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            if (text.Length <= max) return text;
            return text.Substring(0, max) + "…";
        }

        private void UpdateRunningState(bool running)
        {
            _startButton.Enabled = !running;
            _tabCompress.Enabled = !running;
            _tabExtract.Enabled = !running;
            _btnFiles.Enabled = !running;
            _btnFolder.Enabled = !running;
            _passwordBox.ReadOnly = running;
            _eyeButton.Enabled = !running;
            _drop.Busy = running;
            _startButton.Invalidate();
            _tabCompress.Invalidate();
            _tabExtract.Invalidate();
            _btnFiles.Invalidate();
            _btnFolder.Invalidate();
        }
    }
}
