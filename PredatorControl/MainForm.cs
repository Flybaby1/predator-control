using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PredatorControl
{
    public sealed class MainForm : Form
    {
        // ---------------- Win32 ----------------
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // ---------------- state ----------------
        private readonly MonitorService _svc = new MonitorService();

        private GaugeCard _gCpu, _gGpu, _gCpuFan, _gGpuFan;
        private LineChart _chart;
        private MiniSlider _sCpu, _sGpu;
        private FanCurveEditor _curveEditor;
        private PillButton _bAuto, _bMax, _bManual, _bCurve;
        private StatusPill _pill;
        private Label _lblHwMode, _lblCurveRead, _lblStatusLeft, _lblStatusRight, _lblHint;
        private NotifyIcon _tray;
        private bool _exiting;

        public MainForm()
        {
            BuildUi();
            if (Program.DemoMode || Program.RenderPath != null) StartDemo();
            else InitService();
            if (Program.RenderPath != null) ScheduleRender(Program.RenderPath);
        }

        private bool _suppressBalloon;
        private bool _trayStarted;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Program.StartInTray && !_trayStarted)
            {
                _trayStarted = true;
                _suppressBalloon = true;
                WindowState = FormWindowState.Minimized;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }
            catch { }
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            SuspendLayout();

            Text = "掠夺者控制台 · Predator Control";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9f);
            ClientSize = new Size(1200, 786);
            MinimumSize = new Size(1010, 706);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            Icon = AppIcon.Create(64);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildGauges(), 0, 1);
            root.Controls.Add(BuildContent(), 0, 2);
            root.Controls.Add(BuildStatus(), 0, 3);
            Controls.Add(root);

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开主界面", null, (s, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => Close());

            _tray = new NotifyIcon
            {
                Icon = AppIcon.Create(32),
                Text = "掠夺者控制台",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => RestoreFromTray();

            ResumeLayout();
        }

        private Control BuildHeader()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

            // 标题栏底部一条品牌色渐变细线
            p.Paint += (s, e) =>
            {
                Theme.Smooth(e.Graphics);
                using (var b = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(2, p.Width), 1),
                    Theme.Alpha(Theme.Accent, 160), Theme.Alpha(Theme.Accent, 0), LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(b, 20, p.Height - 1, p.Width - 40, 1);
                }
            };

            var title = new Label
            {
                Text = "掠夺者控制台",
                Font = Theme.F(17f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(26, 14),
                BackColor = Theme.Bg
            };

            _pill = new StatusPill { Location = new Point(26, 48), Size = new Size(92, 22) };
            _pill.SetState(Theme.Green, "连接中");

            var sub = new Label
            {
                Text = GetMachineText(),
                Font = Theme.F(8.5f),
                ForeColor = Theme.Sub,
                AutoSize = true,
                Location = new Point(128, 50),
                BackColor = Theme.Bg
            };

            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 330,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Theme.Bg,
                Padding = new Padding(0, 24, 22, 0)
            };

            var btnExit = SmallButton("退出", 60);
            btnExit.Click += (s, e) => Close();
            var btnDiag = SmallButton("诊断", 60);
            btnDiag.Click += (s, e) => Diagnostics.ShowDialog(_svc);
            var btnMin = SmallButton("托盘", 60);
            btnMin.Click += (s, e) =>
            {
                Hide();
                _tray.ShowBalloonTip(1200, "掠夺者控制台", "已最小化到托盘，双击图标恢复。", ToolTipIcon.Info);
            };

            right.Controls.Add(btnExit);
            right.Controls.Add(btnDiag);
            right.Controls.Add(btnMin);

            p.Controls.Add(right);
            p.Controls.Add(title);
            p.Controls.Add(_pill);
            p.Controls.Add(sub);
            return p;
        }

        private Control BuildGauges()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Theme.Bg,
                Padding = new Padding(20, 2, 20, 6)
            };
            for (int i = 0; i < 4; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            _gCpu = new GaugeCard
            {
                Caption = "CPU 温度", Unit = "°C", Max = 100, AccentColor = Theme.Accent,
                DynamicColor = true, Margin = new Padding(6, 0, 6, 0), Dock = DockStyle.Fill
            };
            _gGpu = new GaugeCard
            {
                Caption = "GPU 温度", Unit = "°C", Max = 100, AccentColor = Theme.Blue,
                DynamicColor = true, Margin = new Padding(6, 0, 6, 0), Dock = DockStyle.Fill
            };
            _gCpuFan = new GaugeCard
            {
                Caption = "CPU 风扇", Unit = "%", Max = 100, AccentColor = Theme.Cyan,
                Margin = new Padding(6, 0, 6, 0), Dock = DockStyle.Fill
            };
            _gGpuFan = new GaugeCard
            {
                Caption = "GPU 风扇", Unit = "%", Max = 100, AccentColor = Theme.Cyan,
                Margin = new Padding(6, 0, 6, 0), Dock = DockStyle.Fill
            };

            t.Controls.Add(_gCpu, 0, 0);
            t.Controls.Add(_gGpu, 1, 0);
            t.Controls.Add(_gCpuFan, 2, 0);
            t.Controls.Add(_gGpuFan, 3, 0);
            return t;
        }

        private Control BuildContent()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Bg,
                Padding = new Padding(20, 0, 20, 8)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _chart = new LineChart(180) { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0) };
            t.Controls.Add(_chart, 0, 0);
            t.Controls.Add(BuildFanCard(), 1, 0);
            return t;
        }

        private Control BuildFanCard()
        {
            var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0) };
            card.Resize += (s, e) =>
            {
                if (card.Width < 4 || card.Height < 4) return;
                using (var path = Theme.Round(new Rectangle(0, 0, card.Width, card.Height), 12))
                    card.Region = new Region(path);
            };

            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                BackColor = Theme.Card,
                Padding = new Padding(16, 12, 16, 10)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 0 标题
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 1 模式按钮
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));   // 2 分隔
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 3 CPU 滑块
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 4 GPU 滑块
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // 5 曲线标题
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 6 曲线
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));   // 7 分隔
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 8 提示

            // --- 标题行
            var titleRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            var lblTitle = new Label
            {
                Text = "风扇控制",
                Font = Theme.F(11f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(0, 4)
            };
            _lblHwMode = new Label
            {
                Text = "读取中…",
                Font = Theme.F(8.5f),
                ForeColor = Theme.Dim,
                BackColor = Theme.Card,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 150
            };
            titleRow.Controls.Add(_lblHwMode);
            titleRow.Controls.Add(lblTitle);

            // --- 模式按钮
            var pills = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.Card,
                Padding = new Padding(0, 3, 0, 0)
            };
            _bAuto = MakePill("自动", Theme.Green);
            _bMax = MakePill("最大", Theme.Accent);
            _bManual = MakePill("手动", Theme.Cyan);
            _bCurve = MakePill("曲线", Theme.Blue);
            _bAuto.Checked = true;
            _bAuto.Click += (s, e) => SetMode(FanMode.Auto);
            _bMax.Click += (s, e) => SetMode(FanMode.Max);
            _bManual.Click += (s, e) => SetMode(FanMode.Manual);
            _bCurve.Click += (s, e) => SetMode(FanMode.Curve);
            pills.Controls.Add(_bAuto);
            pills.Controls.Add(_bMax);
            pills.Controls.Add(_bManual);
            pills.Controls.Add(_bCurve);

            var div1 = new Divider { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 6) };

            _sCpu = new MiniSlider { Dock = DockStyle.Fill, Label = "CPU 风扇", AccentColor = Theme.Cyan, Value = 55, Margin = new Padding(0) };
            _sGpu = new MiniSlider { Dock = DockStyle.Fill, Label = "GPU 风扇", AccentColor = Theme.Cyan, Value = 55, Margin = new Padding(0) };
            _sCpu.ValueChanged += (s, e) => { _svc.ManualCpu = _sCpu.Value; };
            _sGpu.ValueChanged += (s, e) => { _svc.ManualGpu = _sGpu.Value; };

            // --- 曲线标题行
            var curveHead = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            var lblCurve = new Label
            {
                Text = "温度-转速曲线",
                Font = Theme.F(9f),
                ForeColor = Theme.Sub,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(0, 3)
            };
            _lblCurveRead = new Label
            {
                Text = "",
                Font = Theme.FN(8.5f),
                ForeColor = Theme.Dim,
                BackColor = Theme.Card,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 160
            };
            curveHead.Controls.Add(_lblCurveRead);
            curveHead.Controls.Add(lblCurve);

            _curveEditor = new FanCurveEditor(_svc.Curve) { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4) };

            var div2 = new Divider { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5) };

            _lblHint = new Label
            {
                Text = "自动模式：由硬件根据温度自行调速",
                Font = Theme.F(8f),
                ForeColor = Theme.Dim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            t.Controls.Add(titleRow, 0, 0);
            t.Controls.Add(pills, 0, 1);
            t.Controls.Add(div1, 0, 2);
            t.Controls.Add(_sCpu, 0, 3);
            t.Controls.Add(_sGpu, 0, 4);
            t.Controls.Add(curveHead, 0, 5);
            t.Controls.Add(_curveEditor, 0, 6);
            t.Controls.Add(div2, 0, 7);
            t.Controls.Add(_lblHint, 0, 8);

            card.Controls.Add(t);
            return card;
        }

        private Control BuildStatus()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            p.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(Theme.BorderSoft))
                    e.Graphics.FillRectangle(b, 0, 0, p.Width, 1);
            };

            _lblStatusLeft = new Label
            {
                Text = "正在连接硬件…",
                Font = Theme.F(8.5f),
                ForeColor = Theme.Sub,
                AutoSize = false,
                Size = new Size(720, 18),
                Location = new Point(26, 9),
                BackColor = Theme.Bg
            };
            _lblStatusRight = new Label
            {
                Text = "",
                Font = Theme.FN(8.5f),
                ForeColor = Theme.Dim,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(0, 9),
                Height = 18,
                Width = 360,
                BackColor = Theme.Bg
            };
            p.Resize += (s, e) => _lblStatusRight.Left = p.Width - _lblStatusRight.Width - 26;

            p.Controls.Add(_lblStatusLeft);
            p.Controls.Add(_lblStatusRight);
            return p;
        }

        private PillButton MakePill(string text, Color accent)
        {
            return new PillButton
            {
                Text = text,
                AccentColor = accent,
                Width = 72,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Theme.Card
            };
        }

        private PillButton SmallButton(string text, int w)
        {
            return new PillButton
            {
                Text = text,
                AccentColor = Theme.Accent,
                Width = w,
                Height = 30,
                Font = Theme.F(8.5f),
                Margin = new Padding(8, 0, 0, 0),
                BackColor = Theme.Bg
            };
        }

        // ------------------------------------------------------------ service

        private void InitService()
        {
            _svc.ManualCpu = _sCpu.Value;
            _svc.ManualGpu = _sGpu.Value;

            bool ok = _svc.OpenHardware();
            _svc.Updated += OnSnapshot;
            _svc.Start();

            if (ok)
            {
                Diagnostics.Save(_svc);
                _pill.SetState(Theme.Green, "实时监控");
            }
            else
            {
                _pill.SetState(Theme.Warn, "权限受限");
                _lblStatusRight.Text = "硬件接口未就绪";
                _lblStatusRight.ForeColor = Theme.Warn;
                _lblHint.Text = Program.IsAdmin
                    ? "硬件接口不可用，请点右上角「诊断」查看原因"
                    : "未获得管理员权限，无法读取或控制硬件";
                _lblHint.ForeColor = Theme.Warn;
            }
            UpdatePillStates();
            _sCpu.Enabled = false;
            _sGpu.Enabled = false;
            _curveEditor.Enabled = false;
        }

        private void OnSnapshot(Snapshot s)
        {
            if (!IsHandleCreated || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<Snapshot>(OnSnapshot), s); return; }

            _gCpu.SetValue(s.CpuTemp, "i9-14900HX");
            _gGpu.SetValue(s.GpuTemp, string.IsNullOrEmpty(s.GpuName) ? "GPU" : ShortGpu(s.GpuName));
            ApplyFanGauge(_gCpuFan, s.CpuFanRpm, s.CpuFanPct);
            ApplyFanGauge(_gGpuFan, s.GpuFanRpm, s.GpuFanPct);

            _chart.Add(s.CpuTemp, s.GpuTemp);

            if (s.AcerFanMode > 0)
                _lblHwMode.Text = "硬件：" + MonitorService.ModeName(s.AcerFanMode);
            else if (!s.HardwareOk)
                _lblHwMode.Text = "硬件：不可用";

            int maxT = Math.Max(s.CpuTemp, s.GpuTemp);
            _curveEditor.SetTemp(maxT);
            if (maxT > 0)
            {
                int tgt = _svc.Curve.Evaluate(maxT);
                _lblCurveRead.Text = maxT + "°C → " + tgt + "%";
                _lblCurveRead.ForeColor = _svc.Mode == FanMode.Curve ? Theme.Cyan : Theme.Dim;
            }

            _lblStatusLeft.Text = string.Format(CultureInfo.InvariantCulture,
                "采样 {0:0.0}s   ·   CPU {1}   ·   GPU {2}   ·   GPU 功耗 {3}   ·   占用 {4}",
                _svc.IntervalMs / 1000.0,
                s.CpuTemp >= 0 ? s.CpuTemp + "°C" : "--",
                s.GpuTemp >= 0 ? s.GpuTemp + "°C" : "--",
                s.GpuPower >= 0 ? s.GpuPower.ToString("0") + "W" : "--",
                s.GpuUtil >= 0 ? s.GpuUtil + "%" : "--");

            if (!string.IsNullOrEmpty(s.Error))
            {
                _lblStatusRight.Text = s.Error;
                _lblStatusRight.ForeColor = Theme.Warn;
                _pill.SetState(Theme.Warn, "读取异常");
            }
            else
            {
                string mem = s.GpuMemTotal > 0
                    ? "  ·  " + (s.GpuMemUsed / 1024).ToString("0.0") + "/" + (s.GpuMemTotal / 1024).ToString("0.0") + "GB"
                    : "";
                _lblStatusRight.Text = (s.GpuClock > 0 ? "GPU " + s.GpuClock.ToString("0") + " MHz" : "") + mem;
                _lblStatusRight.ForeColor = Theme.Dim;
                _pill.SetState(Theme.Green, "实时监控");
            }

            _tray.Text = s.CpuFanRpm > 0
                ? string.Format("CPU {0}°C · GPU {1}°C · 风扇 {2}/{3} RPM", s.CpuTemp, s.GpuTemp, s.CpuFanRpm, s.GpuFanRpm)
                : string.Format("CPU {0}°C · GPU {1}°C · 风扇 {2}%/{3}%", s.CpuTemp, s.GpuTemp, s.CpuFanPct, s.GpuFanPct);
        }

        private static string Fmt(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>
        /// 风扇仪表优先显示实际 RPM。自动模式下固件不回传占空比（读数恒为 0），
        /// 因此百分比只在读得到时才作为副标题展示。
        /// </summary>
        private static void ApplyFanGauge(GaugeCard card, int rpm, int pct)
        {
            if (rpm > 0)
            {
                card.Unit = "RPM";
                card.Max = 6000f;
                card.SetValue(rpm, pct > 0 ? pct + "% 占空比" : "自动控制");
            }
            else
            {
                card.Unit = "%";
                card.Max = 100f;
                card.SetValue(pct, "占用百分比");
            }
        }

        private static string ShortGpu(string name)
        {
            if (string.IsNullOrEmpty(name)) return "GPU";
            return name.Replace("NVIDIA ", "").Replace(" Laptop GPU", "");
        }

        private void SetMode(FanMode m)
        {
            _svc.Mode = m;
            UpdatePillStates();

            switch (m)
            {
                case FanMode.Auto:
                    _lblHint.Text = "自动模式：由硬件根据温度自行调速";
                    _lblHint.ForeColor = Theme.Dim;
                    break;
                case FanMode.Max:
                    _lblHint.Text = "最大模式：双风扇全速运转";
                    _lblHint.ForeColor = Theme.Dim;
                    break;
                case FanMode.Manual:
                    _lblHint.Text = "手动模式已接管风扇，拖动滑块调整转速";
                    _lblHint.ForeColor = Theme.Cyan;
                    break;
                default:
                    _lblHint.Text = "曲线模式已接管风扇，按温度自动调速";
                    _lblHint.ForeColor = Theme.Blue;
                    break;
            }
            if (!Program.IsAdmin || !_svc.Wmi.Available)
            {
                _lblHint.Text = "未获得硬件控制权限，风扇模式无法写入";
                _lblHint.ForeColor = Theme.Warn;
            }
        }

        private void UpdatePillStates()
        {
            _bAuto.Checked = _svc.Mode == FanMode.Auto;
            _bMax.Checked = _svc.Mode == FanMode.Max;
            _bManual.Checked = _svc.Mode == FanMode.Manual;
            _bCurve.Checked = _svc.Mode == FanMode.Curve;
            foreach (var p in new[] { _bAuto, _bMax, _bManual, _bCurve }) p.Invalidate();

            bool canControl = _svc.Wmi.Available;
            bool manual = _svc.Mode == FanMode.Manual;
            _sCpu.Enabled = manual && canControl;
            _sGpu.Enabled = manual && canControl;
            _curveEditor.Enabled = _svc.Mode == FanMode.Curve && canControl;
            _sCpu.Invalidate();
            _sGpu.Invalidate();
            _curveEditor.Invalidate();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ------------------------------------------------------------- demo

        private System.Windows.Forms.Timer _demoTimer;
        private int _demoTick;

        private void StartDemo()
        {
            _svc.ManualCpu = _sCpu.Value;
            _svc.ManualGpu = _sGpu.Value;
            var rnd = new Random(7);

            for (int i = 0; i < 180; i++)
            {
                double p0 = i * 0.12;
                _chart.Add((int)(72 + Math.Sin(p0) * 9), (int)(62 + Math.Sin(p0 * 0.7 + 1) * 7));
            }
            _demoTick = 180;

            _demoTimer = new System.Windows.Forms.Timer { Interval = 120 };
            _demoTimer.Tick += (s, e) =>
            {
                _demoTick++;
                double ph = _demoTick * 0.12;
                var snap = new Snapshot
                {
                    Tick = _demoTick,
                    Time = DateTime.Now,
                    HardwareOk = true,
                    CpuTemp = (int)(72 + Math.Sin(ph) * 9 + rnd.NextDouble()),
                    GpuTemp = (int)(62 + Math.Sin(ph * 0.7 + 1) * 7 + rnd.NextDouble()),
                    CpuFanPct = (int)(58 + Math.Sin(ph * 0.9) * 12),
                    GpuFanPct = (int)(54 + Math.Sin(ph * 0.9 + 0.5) * 12),
                    CpuFanRpm = (int)(3100 + Math.Sin(ph) * 600),
                    GpuFanRpm = (int)(2900 + Math.Sin(ph) * 500),
                    AcerFanMode = AcerWmi.MODE_AUTO,
                    GpuName = "NVIDIA GeForce RTX 4060 Laptop GPU",
                    GpuPower = 62 + rnd.NextDouble() * 8,
                    GpuClock = 2100 + rnd.NextDouble() * 200,
                    GpuUtil = 40 + rnd.Next(20),
                    GpuMemUsed = 4300,
                    GpuMemTotal = 8188,
                };
                OnSnapshot(snap);
            };
            _demoTimer.Start();
            _pill.SetState(Theme.Blue, "演示模式");
        }

        private void ScheduleRender(string path)
        {
            var t = new System.Windows.Forms.Timer { Interval = 2600 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    Application.DoEvents();
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    try { System.IO.File.WriteAllText(path + ".err.txt", ex.ToString()); } catch { }
                }
                Close();
            };
            t.Start();
        }

        // ------------------------------------------------------------- events

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && _tray != null && !_exiting)
            {
                Hide();
                if (!_suppressBalloon)
                    _tray.ShowBalloonTip(1200, "掠夺者控制台", "已最小化到托盘，双击图标恢复。", ToolTipIcon.Info);
                _suppressBalloon = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Program.RenderPath != null) { base.OnFormClosing(e); return; }

            if (!_exiting)
            {
                var r = MessageBox.Show("退出后将自动把风扇交还给系统自动控制。\n\n确定退出吗？",
                    "掠夺者控制台", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }

                _exiting = true;
                try { _svc.RestoreAuto(); } catch { }
                try { _svc.Dispose(); } catch { }
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            }
            base.OnFormClosing(e);
        }

        private static string GetMachineText()
        {
            try
            {
                string model = "", cpu = "";
                using (var s = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))
                using (var c = s.Get())
                    foreach (ManagementObject o in c) { model = o["Model"]?.ToString() ?? ""; break; }
                using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                using (var c = s.Get())
                    foreach (ManagementObject o in c) { cpu = o["Name"]?.ToString() ?? ""; break; }

                cpu = cpu.Replace("(R)", "").Replace("(TM)", "").Replace("CPU ", "").Trim();
                if (cpu.Length > 26) cpu = cpu.Substring(0, 26);
                return model + "   ·   " + cpu;
            }
            catch { return "Acer Predator"; }
        }
    }
}
