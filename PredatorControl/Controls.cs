using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PredatorControl
{
    /// <summary>胶囊形按钮（风扇模式切换）</summary>
    internal class PillButton : Control
    {
        public bool Checked = false;
        public Color AccentColor = Theme.Accent;

        private bool _hover;
        private bool _press;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f);
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = Theme.Round(r, Height / 2))
            {
                Color text;
                if (Checked)
                {
                    using (var b = new LinearGradientBrush(
                        new Rectangle(0, 0, Math.Max(2, Width), Math.Max(2, Height)),
                        Theme.Alpha(AccentColor, 235), Theme.Alpha(AccentColor, 170), LinearGradientMode.Vertical))
                    {
                        using (var shadow = new SolidBrush(Theme.Alpha(AccentColor, 46)))
                            g.FillPath(shadow, path);
                    }
                    using (var b = new LinearGradientBrush(
                        new Rectangle(0, 1, Math.Max(2, Width), Math.Max(2, Height - 2)),
                        Theme.Alpha(AccentColor, 245), Theme.Alpha(AccentColor, 190), LinearGradientMode.Vertical))
                    {
                        using (var inside = Theme.Round(new Rectangle(1, 1, Width - 3, Height - 3), Height / 2))
                            g.FillPath(b, inside);
                    }
                    text = Color.White;
                }
                else
                {
                    Color fill = _press ? Theme.CardHi
                               : _hover ? Color.FromArgb(36, 41, 54)
                                        : Color.FromArgb(28, 32, 42);
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var p = new Pen(_hover ? Theme.Border : Theme.BorderSoft)) g.DrawPath(p, path);
                    text = _hover ? Theme.Text : Theme.Sub;
                }

                if (!Enabled) text = Theme.Dim;

                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var tb = new SolidBrush(text))
                    g.DrawString(Text, Font, tb, r, sf);
            }
        }
    }

    /// <summary>实时状态胶囊（绿点 + 文字）</summary>
    internal class StatusPill : Control
    {
        public Color DotColor = Theme.Green;
        public string Caption = "实时";

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.F(8.5f);
            Size = new Size(88, 24);
        }

        public void SetState(Color color, string text)
        {
            DotColor = color;
            Caption = text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = Theme.Round(r, Height / 2))
            {
                using (var b = new SolidBrush(Color.FromArgb(26, 30, 40))) g.FillPath(b, path);
                using (var p = new Pen(Theme.BorderSoft)) g.DrawPath(p, path);
            }

            Theme.DrawDot(g, 14, Height / 2f, 3f, DotColor);

            using (var b = new SolidBrush(Theme.Sub))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            })
                g.DrawString(Caption, Font, b, new RectangleF(23, 0, Width - 28, Height), sf);
        }
    }

    /// <summary>极简滑块</summary>
    internal class MiniSlider : Control
    {
        public int Minimum = 0;
        public int Maximum = 100;
        public Color AccentColor = Theme.Cyan;
        public string Label = "";
        public string ValueSuffix = "%";

        private int _value = 50;
        private bool _drag;
        private bool _hover;

        public event EventHandler ValueChanged;

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Clamp(value, Minimum, Maximum);
                if (v == _value) return;
                _value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public MiniSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 42;
            Cursor = Cursors.Hand;
        }

        private Rectangle TrackRect() => new Rectangle(10, Height - 18, Width - 20, 5);

        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            _hover = true;
            if (_drag) SetFromX(e.X);
            Invalidate();
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }

        private void SetFromX(int x)
        {
            if (!Enabled) return;
            var t = TrackRect();
            double k = (double)(x - t.Left) / Math.Max(1, t.Width);
            Value = (int)Math.Round(Minimum + Math.Clamp(k, 0, 1) * (Maximum - Minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var t = TrackRect();
            bool on = Enabled;

            Color labelColor = on ? (Theme.Sub) : Theme.Dim;
            using (var b = new SolidBrush(labelColor))
                g.DrawString(Label, Theme.F(9f), b, new PointF(10, 2));

            using (var sf = new StringFormat { Alignment = StringAlignment.Far })
            using (var b = new SolidBrush(on ? Theme.Text : Theme.Dim))
                g.DrawString(_value + ValueSuffix, Theme.FN(11f, FontStyle.Bold), b, new RectangleF(Width - 74, 0, 64, 20), sf);

            using (var path = Theme.Round(t, 3))
            using (var b = new SolidBrush(Theme.CardHi)) g.FillPath(b, path);

            double ratio = (double)(_value - Minimum) / Math.Max(1, Maximum - Minimum);
            int w = (int)(t.Width * ratio);
            if (w > 2)
            {
                var tr = new Rectangle(t.Left, t.Top, w, t.Height);
                using (var path = Theme.Round(tr, 3))
                using (var b = new LinearGradientBrush(
                    new Rectangle(tr.X, tr.Y, Math.Max(2, tr.Width), tr.Height),
                    Theme.Alpha(AccentColor, on ? 255 : 90),
                    Theme.Alpha(AccentColor, on ? 170 : 60), LinearGradientMode.Horizontal))
                {
                    g.FillPath(b, path);
                }
            }

            float kx = t.Left + (float)(t.Width * ratio);
            float kr = _hover || _drag ? 7.5f : 6.5f;
            using (var halo = new SolidBrush(Theme.Alpha(on ? AccentColor : Theme.Dim, 40)))
                g.FillEllipse(halo, kx - kr - 4, t.Top + t.Height / 2f - kr - 4, (kr + 4) * 2, (kr + 4) * 2);
            using (var b = new SolidBrush(on ? Color.White : Color.FromArgb(150, 160, 180)))
                g.FillEllipse(b, kx - kr, t.Top + t.Height / 2f - kr, kr * 2, kr * 2);
        }
    }

    /// <summary>风扇曲线编辑器：横轴温度，纵轴转速</summary>
    internal class FanCurveEditor : Control
    {
        private readonly FanCurve _curve;
        private int _dragIndex = -1;
        private const int TMin = 20, TMax = 100;

        private static readonly StringFormat SfFar = new StringFormat { Alignment = StringAlignment.Far };
        private static readonly StringFormat SfCenter = new StringFormat { Alignment = StringAlignment.Center };

        /// <summary>当前实测温度，用于在曲线上画参考线</summary>
        public int CurrentTemp = -1;

        public event EventHandler CurveChanged;

        public FanCurveEditor(FanCurve curve)
        {
            _curve = curve;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Cross;
        }

        public void SetTemp(int t)
        {
            if (t == CurrentTemp) return;
            CurrentTemp = t;
            Invalidate();
        }

        private RectangleF Plot() => new RectangleF(34, 8, Width - 46, Height - 26);

        private PointF ToScreen(float temp, float pct)
        {
            var p = Plot();
            return new PointF(
                p.Left + (temp - TMin) / (TMax - TMin) * p.Width,
                p.Bottom - (pct / 100f) * p.Height);
        }

        private PointF FromScreen(Point pt)
        {
            var p = Plot();
            float temp = TMin + Math.Clamp((pt.X - p.Left) / Math.Max(1, p.Width), 0f, 1f) * (TMax - TMin);
            float pct = Math.Clamp((p.Bottom - pt.Y) / Math.Max(1, p.Height), 0f, 1f) * 100f;
            return new PointF(temp, pct);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var p = Plot();
            bool on = Enabled;

            using (var gp = new Pen(Color.FromArgb(31, 36, 47)))
            using (var tb = new SolidBrush(Theme.Dim))
            {
                for (int pct = 0; pct <= 100; pct += 25)
                {
                    var y = ToScreen(TMin, pct).Y;
                    gp.DashStyle = DashStyle.Dot;
                    g.DrawLine(gp, p.Left, y, p.Right, y);
                    gp.DashStyle = DashStyle.Solid;
                    g.DrawString(pct + "%", Theme.FN(7.5f), tb, new RectangleF(0, y - 7, 30, 14), SfFar);
                }
                for (int t = TMin; t <= TMax; t += 20)
                {
                    float x = ToScreen(t, 0).X;
                    g.DrawString(t + "°", Theme.FN(7.5f), tb, new RectangleF(x - 16, p.Bottom + 2, 32, 14), SfCenter);
                }
            }

            // 当前温度参考线
            if (CurrentTemp >= TMin && CurrentTemp <= TMax)
            {
                float x = ToScreen(CurrentTemp, 0).X;
                using (var pen = new Pen(Theme.Alpha(Theme.Warn, 90), 1f) { DashStyle = DashStyle.Dash })
                    g.DrawLine(pen, x, p.Top, x, p.Bottom);

                int target = _curve.Evaluate(CurrentTemp);
                var tp = ToScreen(CurrentTemp, target);
                using (var halo = new SolidBrush(Theme.Alpha(Theme.Warn, 55)))
                    g.FillEllipse(halo, tp.X - 6, tp.Y - 6, 12, 12);
                using (var b = new SolidBrush(Theme.Warn))
                    g.FillEllipse(b, tp.X - 3, tp.Y - 3, 6, 6);
            }

            var pts = new PointF[_curve.Points.Count];
            for (int i = 0; i < _curve.Points.Count; i++)
                pts[i] = ToScreen(_curve.Points[i].Key, _curve.Points[i].Value);

            if (pts.Length > 1)
            {
                var area = new GraphicsPath();
                area.AddLines(pts);
                area.AddLine(pts[pts.Length - 1].X, p.Bottom, pts[0].X, p.Bottom);
                area.CloseFigure();
                using (var b = new LinearGradientBrush(
                    new RectangleF(p.Left, p.Top - 1, Math.Max(2, p.Width), Math.Max(2, p.Height + 2)),
                    Theme.Alpha(Theme.Cyan, on ? 58 : 22), Theme.Alpha(Theme.Cyan, 0), LinearGradientMode.Vertical))
                {
                    g.FillPath(b, area);
                }
                area.Dispose();
            }

            Color line = on ? Theme.Cyan : Theme.Dim;
            using (var pen = new Pen(line, 2f))
            {
                pen.LineJoin = LineJoin.Round;
                if (pts.Length > 1) g.DrawLines(pen, pts);
            }

            for (int i = 0; i < pts.Length; i++)
            {
                bool hot = i == _dragIndex;
                float r = hot ? 5.5f : 4.2f;
                if (hot)
                    using (var halo = new SolidBrush(Theme.Alpha(Theme.Cyan, 55)))
                        g.FillEllipse(halo, pts[i].X - r - 4, pts[i].Y - r - 4, (r + 4) * 2, (r + 4) * 2);

                using (var b = new SolidBrush(hot ? Color.White : line))
                    g.FillEllipse(b, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
                using (var pen = new Pen(Color.FromArgb(17, 20, 26), 1.5f))
                    g.DrawEllipse(pen, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled) return;
            var p = FromScreen(e.Location);

            if (e.Button == MouseButtons.Right)
            {
                int idx = HitTest(e.Location);
                if (idx >= 0 && _curve.Points.Count > 2)
                {
                    _curve.Points.RemoveAt(idx);
                    CurveChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
                return;
            }

            int hit = HitTest(e.Location);
            if (hit < 0)
            {
                int nt = (int)Math.Round(p.X);
                _curve.Points.Add(new System.Collections.Generic.KeyValuePair<int, int>(nt, (int)Math.Round(p.Y)));
                _curve.Points.Sort((a, b) => a.Key.CompareTo(b.Key));
                hit = _curve.Points.FindIndex(kv => kv.Key == nt);
                CurveChanged?.Invoke(this, EventArgs.Empty);
            }
            _dragIndex = hit;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragIndex >= 0 && _dragIndex < _curve.Points.Count)
            {
                var p = FromScreen(e.Location);
                int idx = _dragIndex;
                int minT = idx > 0 ? _curve.Points[idx - 1].Key + 2 : TMin;
                int maxT = idx < _curve.Points.Count - 1 ? _curve.Points[idx + 1].Key - 2 : TMax;
                int newT = Math.Clamp((int)Math.Round(p.X), minT, maxT);
                int newV = Math.Clamp((int)Math.Round(p.Y), 0, 100);
                _curve.Points[idx] = new System.Collections.Generic.KeyValuePair<int, int>(newT, newV);
                CurveChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                _curve.Points.Sort((a, b) => a.Key.CompareTo(b.Key));
                CurveChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _curve.Points.Count; i++)
            {
                var s = ToScreen(_curve.Points[i].Key, _curve.Points[i].Value);
                if (Math.Abs(s.X - pt.X) < 10 && Math.Abs(s.Y - pt.Y) < 10) return i;
            }
            return -1;
        }
    }

    /// <summary>细分隔线</summary>
    internal class Divider : Control
    {
        public Divider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var b = new SolidBrush(Theme.BorderSoft))
                e.Graphics.FillRectangle(b, 0, 0, Width, 1);
        }
    }
}
