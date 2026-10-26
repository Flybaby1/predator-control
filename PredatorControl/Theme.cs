using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PredatorControl
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(11, 13, 18);
        public static readonly Color Card = Color.FromArgb(23, 26, 35);
        public static readonly Color CardTop = Color.FromArgb(30, 34, 45);
        public static readonly Color CardHi = Color.FromArgb(34, 39, 51);
        public static readonly Color Border = Color.FromArgb(44, 50, 64);
        public static readonly Color BorderSoft = Color.FromArgb(33, 38, 50);
        public static readonly Color Text = Color.FromArgb(237, 241, 248);
        public static readonly Color Sub = Color.FromArgb(132, 142, 163);
        public static readonly Color Dim = Color.FromArgb(96, 105, 124);
        public static readonly Color Accent = Color.FromArgb(255, 96, 36);   // 掠夺者橙
        public static readonly Color Cyan = Color.FromArgb(52, 211, 197);
        public static readonly Color Blue = Color.FromArgb(94, 162, 250);
        public static readonly Color Green = Color.FromArgb(74, 222, 128);
        public static readonly Color Warn = Color.FromArgb(250, 204, 21);
        public static readonly Color Danger = Color.FromArgb(248, 84, 84);

        // 字体缓存：重绘时高频调用，必须复用实例，否则每次 new Font 都会产生 GDI 句柄泄漏
        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>(32);

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            string key = "F|" + size.ToString("0.##") + "|" + (int)style;
            if (!FontCache.TryGetValue(key, out Font f))
            {
                f = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
                FontCache[key] = f;
            }
            return f;
        }

        public static Font FN(float size, FontStyle style = FontStyle.Regular)
        {
            string key = "N|" + size.ToString("0.##") + "|" + (int)style;
            if (!FontCache.TryGetValue(key, out Font f))
            {
                f = new Font("Consolas", size, style, GraphicsUnit.Point);
                FontCache[key] = f;
            }
            return f;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (r.Width <= 1 || r.Height <= 1) return path;
            int d = Math.Max(1, radius * 2);
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color TempColor(double t)
        {
            if (t < 0) return Sub;
            if (t < 55) return Green;
            if (t < 68) return Color.FromArgb(163, 230, 53);
            if (t < 78) return Warn;
            if (t < 88) return Color.FromArgb(251, 146, 60);
            return Danger;
        }

        public static Color Alpha(Color c, int a) => Color.FromArgb(a, c.R, c.G, c.B);

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>圆角卡片底：竖向渐变 + 顶部高光 + 细边框</summary>
        public static void PaintCard(Graphics g, Rectangle r, int radius = 12, bool flat = false)
        {
            using (var path = Round(r, radius))
            {
                if (flat)
                {
                    using (var b = new SolidBrush(Card)) g.FillPath(b, path);
                }
                else
                {
                    using (var b = new LinearGradientBrush(
                        new Rectangle(r.X, r.Y, Math.Max(2, r.Width), Math.Max(2, r.Height)),
                        CardTop, Card, LinearGradientMode.Vertical))
                    {
                        g.FillPath(b, path);
                    }
                }

                using (var hi = new Pen(Color.FromArgb(16, 255, 255, 255), 1f))
                {
                    var state = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    g.DrawLine(hi, r.X + radius, r.Y + 1, r.Right - radius, r.Y + 1);
                    g.Restore(state);
                }

                using (var p = new Pen(Border)) g.DrawPath(p, path);
            }
        }

        public static void DrawDot(Graphics g, float x, float y, float r, Color c)
        {
            using (var glow = new SolidBrush(Alpha(c, 40)))
                g.FillEllipse(glow, x - r * 2.1f, y - r * 2.1f, r * 4.2f, r * 4.2f);
            using (var b = new SolidBrush(c))
                g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
        }
    }

    /// <summary>程序运行期生成的图标</summary>
    internal static class AppIcon
    {
        public static Icon Create(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);
                using (var path = Theme.Round(new Rectangle(1, 1, size - 2, size - 2), size / 5))
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, size, size),
                        Color.FromArgb(255, 128, 52), Color.FromArgb(198, 32, 14), 45f))
                {
                    g.FillPath(b, path);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI", size * 0.56f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var tb = new SolidBrush(Color.White))
                {
                    g.DrawString("P", f, tb, new RectangleF(0, -size * 0.03f, size, size), sf);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    internal class Card : Control
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            Theme.PaintCard(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1));
            base.OnPaint(e);
        }
    }

    /// <summary>圆弧仪表卡片</summary>
    internal class GaugeCard : Card
    {
        public string Caption = "";
        public string Unit = "";
        public string SubText = "";
        public float Max = 100f;
        public Color AccentColor = Theme.Accent;
        public bool DynamicColor = false;

        private float _value = float.NaN;
        private float _peak = float.NaN;

        public void SetValue(float v, string sub)
        {
            if (v < 0) v = float.NaN;
            if (!float.IsNaN(v) && (float.IsNaN(_peak) || v > _peak)) _peak = v;
            SubText = sub;
            _value = v;
            Invalidate();
        }

        private Color ArcColor()
        {
            if (DynamicColor && !float.IsNaN(_value)) return Theme.TempColor(_value);
            return AccentColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.PaintCard(g, new Rectangle(0, 0, Width - 1, Height - 1));

            const int pad = 16;

            using (var b = new SolidBrush(Theme.Sub))
                g.DrawString(Caption, Theme.F(9.5f), b, new PointF(pad, pad - 3));

            if (!float.IsNaN(_peak))
            {
                using (var b = new SolidBrush(Theme.Dim))
                using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                {
                    string pt = "峰值 " + (int)Math.Round(_peak) + (Unit == "°C" ? "°" : (Unit == "RPM" ? "" : Unit));
                    g.DrawString(pt, Theme.F(8f), b, new RectangleF(Width - pad - 90, pad - 2, 90, 16), sf);
                }
            }

            int size = Math.Min(Width - 86, Height - 74);
            if (size < 54) size = Math.Max(54, Math.Min(Width - 40, Height - 56));
            var arcRect = new RectangleF((Width - size) / 2f, pad + 22, size, size);

            using (var track = new Pen(Color.FromArgb(37, 42, 55), 8f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                g.DrawArc(track, arcRect, 135f, 270f);
            }

            float ratio = float.IsNaN(_value) ? 0f : (float)Math.Max(0, Math.Min(1, _value / Max));
            Color ac = ArcColor();

            if (ratio > 0.004f)
            {
                using (var glow = new Pen(Theme.Alpha(ac, 42), 15f))
                {
                    glow.StartCap = LineCap.Round;
                    glow.EndCap = LineCap.Round;
                    g.DrawArc(glow, arcRect, 135f, 270f * ratio);
                }
                using (var pen = new Pen(ac, 8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, arcRect, 135f, 270f * ratio);
                }

                double ang = (135 + 270 * ratio) * Math.PI / 180.0;
                float cx = (float)(arcRect.X + size / 2.0 + size / 2.0 * Math.Cos(ang));
                float cy = (float)(arcRect.Y + size / 2.0 + size / 2.0 * Math.Sin(ang));
                using (var b = new SolidBrush(Color.White))
                    g.FillEllipse(b, cx - 2.5f, cy - 2.5f, 5, 5);
            }

            string txt = float.IsNaN(_value) ? "--" : ((int)Math.Round(_value)).ToString();
            float valueFontSize = txt.Length >= 4 ? size * 0.225f : size * 0.29f;
            using (var b = new SolidBrush(float.IsNaN(_value) ? Theme.Dim : Theme.Text))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var box = new RectangleF(arcRect.X, arcRect.Y - size * 0.04f, arcRect.Width, arcRect.Height);
                g.DrawString(txt, Theme.FN(valueFontSize, FontStyle.Bold), b, box, sf);
            }
            using (var b = new SolidBrush(Theme.Sub))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                var box = new RectangleF(arcRect.X, arcRect.Y + arcRect.Height * 0.60f, arcRect.Width, 16);
                g.DrawString(Unit, Theme.F(8f), b, box, sf);
            }

            if (!string.IsNullOrEmpty(SubText))
            {
                using (var b = new SolidBrush(Theme.Dim))
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far })
                {
                    g.DrawString(SubText, Theme.F(8f), b, new RectangleF(pad, Height - pad - 16, Width - pad * 2, 16), sf);
                }
            }
        }
    }

    /// <summary>温度历史曲线</summary>
    internal class LineChart : Card
    {
        private sealed class Series
        {
            public string Name;
            public Color Color;
            public float[] Values;
            public int Count;
            public int Head;

            public float Last
            {
                get { return Count == 0 ? -1 : Values[(Head - 1 + Values.Length) % Values.Length]; }
            }
        }

        private readonly Series[] _series;
        private readonly int _capacity;
        private readonly PointF[] _pts;
        private readonly bool[] _ok;

        // 复用 StringFormat，避免每次重绘创建原生对象
        private static readonly StringFormat SfFar = new StringFormat { Alignment = StringAlignment.Far };
        private static readonly StringFormat SfCenter = new StringFormat { Alignment = StringAlignment.Center };
        private static readonly StringFormat SfNear = new StringFormat { Alignment = StringAlignment.Near };
        private static readonly StringFormat SfLast = new StringFormat { Alignment = StringAlignment.Far };

        public LineChart(int capacity = 180)
        {
            _capacity = capacity;
            _pts = new PointF[capacity];
            _ok = new bool[capacity];
            _series = new[]
            {
                new Series { Name = "CPU", Color = Theme.Accent, Values = new float[capacity] },
                new Series { Name = "GPU", Color = Theme.Blue, Values = new float[capacity] },
            };
        }

        public void Add(float cpu, float gpu)
        {
            Push(_series[0], cpu);
            Push(_series[1], gpu);
            Invalidate();
        }

        private void Push(Series s, float v)
        {
            s.Values[s.Head] = v;
            s.Head = (s.Head + 1) % _capacity;
            if (s.Count < _capacity) s.Count++;
        }

        public void Clear()
        {
            foreach (var s in _series) { s.Count = 0; s.Head = 0; }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.PaintCard(g, new Rectangle(0, 0, Width - 1, Height - 1));

            const int padL = 44, padR = 18, padT = 46, padB = 26;
            var plot = new RectangleF(padL, padT, Width - padL - padR, Height - padT - padB);
            if (plot.Width < 20 || plot.Height < 20) return;

            using (var b = new SolidBrush(Theme.Sub))
                g.DrawString("温度趋势", Theme.F(9.5f), b, new PointF(16, 14));

            float x = 104;
            foreach (var s in _series)
            {
                Theme.DrawDot(g, x + 3, 22, 3f, s.Color);
                using (var b = new SolidBrush(Theme.Sub))
                    g.DrawString(s.Name, Theme.F(8.5f), b, new PointF(x + 11, 13));

                string v = s.Last < 0 ? "--" : ((int)Math.Round(s.Last)) + "°";
                using (var b = new SolidBrush(s.Color))
                    g.DrawString(v, Theme.FN(10f, FontStyle.Bold), b, new PointF(x + 40, 11));

                x += 92;
            }

            using (var b = new SolidBrush(Theme.Dim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString("近 " + _capacity + " 秒", Theme.F(8f), b, new RectangleF(Width - 96, 15, 80, 16), sf);

            using (var grid = new Pen(Color.FromArgb(31, 36, 47)))
            using (var txt = new SolidBrush(Theme.Dim))
            {
                for (int t = 30; t <= 90; t += 15)
                {
                    float y = plot.Bottom - (t / 100f) * plot.Height;
                    grid.DashStyle = DashStyle.Dot;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    grid.DashStyle = DashStyle.Solid;
                    g.DrawString(t + "°", Theme.FN(7.5f), txt, new RectangleF(0, y - 7, padL - 10, 14), SfFar);
                }
            }

            using (var txt = new SolidBrush(Theme.Dim))
            {
                for (int k = 0; k <= 4; k++)
                {
                    float px = plot.Left + plot.Width * k / 4f;
                    string lab = k == 4 ? "现在" : "-" + (_capacity - _capacity * k / 4) + "s";
                    var sf = k == 0 ? SfNear : (k == 4 ? SfLast : SfCenter);
                    g.DrawString(lab, Theme.FN(7.5f), txt, new RectangleF(px - 22, plot.Bottom + 5, 44, 14), sf);
                }
            }

            float stepX = plot.Width / (_capacity - 1);
            foreach (var s in _series)
            {
                if (s.Count < 2) continue;
                int start = s.Count == _capacity ? s.Head : 0;

                var pts = _pts;
                var ok = _ok;
                for (int i = 0; i < s.Count; i++)
                {
                    int idx = (start + i) % _capacity;
                    float v = s.Values[idx];
                    ok[i] = v >= 0;
                    float y = plot.Bottom - Math.Min(1f, Math.Max(0f, v) / 100f) * plot.Height;
                    pts[i] = new PointF(plot.Left + i * stepX, y);
                }

                int seg = 0;
                while (seg < s.Count)
                {
                    while (seg < s.Count && !ok[seg]) seg++;
                    int from = seg;
                    while (seg < s.Count && ok[seg]) seg++;
                    int to = seg - 1;
                    if (to - from < 1) continue;

                    var sub = new PointF[to - from + 1];
                    Array.Copy(pts, from, sub, 0, sub.Length);

                    var area = new GraphicsPath();
                    area.AddLines(sub);
                    area.AddLine(sub[sub.Length - 1].X, plot.Bottom, sub[0].X, plot.Bottom);
                    area.CloseFigure();
                    using (var b = new LinearGradientBrush(
                        new RectangleF(plot.Left, plot.Top - 1, plot.Width, plot.Height + 2),
                        Theme.Alpha(s.Color, 52), Theme.Alpha(s.Color, 0), LinearGradientMode.Vertical))
                    {
                        g.FillPath(b, area);
                    }
                    area.Dispose();

                    var line = new GraphicsPath();
                    line.AddLines(sub);
                    using (var pen = new Pen(s.Color, 2f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, line); }
                    line.Dispose();
                }

                for (int i = s.Count - 1; i >= 0; i--)
                {
                    if (!ok[i]) continue;
                    var lp = pts[i];
                    using (var halo = new SolidBrush(Theme.Alpha(s.Color, 60)))
                        g.FillEllipse(halo, lp.X - 6, lp.Y - 6, 12, 12);
                    using (var b = new SolidBrush(s.Color)) g.FillEllipse(b, lp.X - 3.2f, lp.Y - 3.2f, 6.4f, 6.4f);
                    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, lp.X - 1.4f, lp.Y - 1.4f, 2.8f, 2.8f);
                    break;
                }
            }
        }
    }
}
