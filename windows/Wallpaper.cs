using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnowFlakeUz
{
    /// <summary>Finds the window behind the desktop icons (the "WorkerW" trick).</summary>
    internal static class DesktopHost
    {
        public static IntPtr Find()
        {
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            // Ask Explorer to spawn a WorkerW window behind the icons.
            UIntPtr res;
            Native.SendMessageTimeout(progman, 0x052C, new UIntPtr(0xD), new IntPtr(1), 0, 1000, out res);

            IntPtr workerW = IntPtr.Zero;
            Native.EnumWindows((h, l) =>
            {
                IntPtr def = Native.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (def != IntPtr.Zero)
                    workerW = Native.FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
                return true;
            }, IntPtr.Zero);

            // Newer Windows 11 builds keep the WorkerW as a child of Progman.
            if (workerW == IntPtr.Zero)
                workerW = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

            return workerW != IntPtr.Zero ? workerW : progman;
        }
    }

    internal sealed class WallpaperCanvas : FrameworkElement
    {
        private readonly System.Drawing.Rectangle bounds;

        public WallpaperCanvas(System.Drawing.Rectangle b)
        {
            bounds = b;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            dc.PushTransform(new ScaleTransform(1.0 / dpi.DpiScaleX, 1.0 / dpi.DpiScaleY));
            // The scene is drawn with y pointing up, origin at the bottom-left corner.
            dc.PushTransform(new MatrixTransform(1, 0, 0, -1, 0, bounds.Height));
            Scene.Draw(dc, bounds.Width, bounds.Height, Environment.TickCount64 / 1000.0);
            dc.Pop();
            dc.Pop();
        }
    }

    /// <summary>One monitor's live wallpaper, embedded behind the desktop icons.</summary>
    internal sealed class WallpaperWindow : Window
    {
        private readonly System.Windows.Forms.Screen screen;
        private IntPtr hwnd = IntPtr.Zero;
        public WallpaperCanvas Canvas { get; private set; }

        public WallpaperWindow(System.Windows.Forms.Screen s)
        {
            screen = s;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            AllowsTransparency = false;
            Background = Brushes.Black;
            Left = 0;
            Top = 0;
            Width = 100;
            Height = 100;
            Canvas = new WallpaperCanvas(s.Bounds);
            Content = Canvas;
        }

        /// <summary>Embeds the window behind the desktop icons. Returns false if Explorer's desktop was not found.</summary>
        public bool Embed()
        {
            IntPtr parent = DesktopHost.Find();
            if (parent == IntPtr.Zero) return false;
            hwnd = new WindowInteropHelper(this).EnsureHandle();

            int st = Native.GetWindowLong(hwnd, Native.GWL_STYLE);
            st = (st & ~Native.WS_POPUP) | Native.WS_CHILD;
            Native.SetWindowLong(hwnd, Native.GWL_STYLE, st);
            Native.SetParent(hwnd, parent);

            Show();
            Reposition();
            return true;
        }

        private void Reposition()
        {
            System.Drawing.Rectangle v = System.Windows.Forms.SystemInformation.VirtualScreen;
            System.Drawing.Rectangle b = screen.Bounds;
            Native.SetWindowPos(hwnd, IntPtr.Zero, b.Left - v.Left, b.Top - v.Top, b.Width, b.Height,
                                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        }
    }

    /// <summary>
    /// Animated winter-night scene: moon, twinkling stars, pines, a log cabin with glowing windows,
    /// blinking string lights and chimney smoke. Original artwork. Coordinates: y points up.
    /// </summary>
    internal static class Scene
    {
        private static Color C(double r, double g, double b, double a = 1.0)
        {
            return Color.FromArgb((byte)(a * 255), (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static Brush B(double r, double g, double b, double a = 1.0) { return Art.Br(r, g, b, a); }
        private static double Frac(double x) { return x - Math.Floor(x); }

        private static Brush VGradient(Color top, Color bottom)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(0, 0) };
            g.GradientStops.Add(new GradientStop(top, 0));
            g.GradientStops.Add(new GradientStop(bottom, 1));
            g.Freeze();
            return g;
        }

        private static readonly Brush Sky = VGradient(C(0.03, 0.05, 0.16), C(0.16, 0.22, 0.46));
        private static readonly Brush GroundFill = VGradient(C(0.94, 0.96, 1.0), C(0.76, 0.83, 0.95));

        private static void Glow(DrawingContext dc, double cx, double cy, double radius, double r, double g, double b, double alpha)
        {
            var br = new RadialGradientBrush(C(r, g, b, alpha), C(r, g, b, 0));
            br.Freeze();
            dc.DrawEllipse(br, null, new Point(cx, cy), radius, radius);
        }

        private static void Rect(DrawingContext dc, Brush br, double x, double y, double w, double h)
        {
            dc.DrawRectangle(br, null, new System.Windows.Rect(x, y, w, h));
        }

        private static void Poly(DrawingContext dc, Brush br, params Point[] pts)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i], true, false);
            }
            g.Freeze();
            dc.DrawGeometry(br, null, g);
        }

        private static void Hill(DrawingContext dc, Brush br, double w, Func<double, double> f)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(0, 0), true, true);
                for (double x = 0; x <= w + 12; x += 12) c.LineTo(new Point(x, f(x)), true, false);
                c.LineTo(new Point(w + 12, 0), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(br, null, g);
        }

        private static void Tree(DrawingContext dc, double x, double bs, double u)
        {
            Brush dark = B(0.06, 0.13, 0.24);
            Brush snow = B(0.93, 0.96, 1.0);
            Rect(dc, B(0.18, 0.11, 0.09), x - 5 * u, bs - 2 * u, 10 * u, 16 * u);
            double[,] tiers = { { 72, 10, 52 }, { 56, 42, 48 }, { 40, 72, 44 } };
            for (int i = 0; i < 3; i++)
            {
                double hw = tiers[i, 0] / 2 * u;
                double yb = bs + tiers[i, 1] * u;
                double yt = bs + (tiers[i, 1] + tiers[i, 2]) * u;
                Poly(dc, dark, new Point(x - hw, yb), new Point(x + hw, yb), new Point(x, yt));
                double capY = bs + (tiers[i, 1] + tiers[i, 2] * 0.52) * u;
                double capHW = hw * 0.48;
                Poly(dc, snow, new Point(x - capHW, capY), new Point(x + capHW, capY), new Point(x, yt));
            }
        }

        public static void Draw(DrawingContext dc, double w, double h, double t)
        {
            double s = h / 900.0;
            double groundY = h * 0.17;

            // Sky
            dc.DrawRectangle(Sky, null, new System.Windows.Rect(0, 0, w, h));

            // Stars
            for (int i = 0; i < 110; i++)
            {
                double sx = Frac(i * 0.6180339887) * w;
                double sy = h * (0.30 + 0.70 * Frac(i * 0.7548776662));
                double speed = 0.7 + (i % 5) * 0.35;
                double tw = 0.5 + 0.5 * Math.Sin(t * speed + i);
                double r = (0.6 + (i % 3) * 0.45) * s * 1.2;
                dc.DrawEllipse(B(1, 1, 0.95, 0.25 + 0.75 * tw), null, new Point(sx, sy), r, r);
            }

            // Moon
            double mx = w * 0.8, my = h * 0.78;
            Glow(dc, mx, my, 170 * s, 0.85, 0.90, 1.0, 0.35);
            dc.DrawEllipse(B(1.0, 0.97, 0.80), null, new Point(mx, my), 36 * s, 36 * s);

            // Hills
            Hill(dc, B(0.11, 0.15, 0.34), w,
                 x => groundY + 55 * s + 38 * s * Math.Sin(x / (210 * s)) + 22 * s * Math.Sin(x / (97 * s) + 1.3));
            Hill(dc, B(0.17, 0.22, 0.45), w,
                 x => groundY + 18 * s + 14 * s * Math.Sin(x / (160 * s) + 2));

            // Trees (back row)
            double[,] back = { { 0.06, 1.0 }, { 0.15, 1.2 }, { 0.30, 0.9 }, { 0.70, 0.9 }, { 0.84, 1.2 }, { 0.94, 1.0 } };
            for (int i = 0; i < back.GetLength(0); i++)
                Tree(dc, back[i, 0] * w, groundY + 10 * s, back[i, 1] * s * 1.1);

            // Snowy ground
            var ground = new StreamGeometry();
            using (StreamGeometryContext c = ground.Open())
            {
                c.BeginFigure(new Point(0, 0), true, true);
                for (double x = 0; x <= w + 12; x += 12)
                    c.LineTo(new Point(x, groundY + 6 * s * Math.Sin(x / (140 * s))), true, false);
                c.LineTo(new Point(w + 12, 0), true, false);
            }
            ground.Freeze();
            dc.DrawGeometry(GroundFill, null, ground);

            // Trees (front row)
            double[,] front = { { 0.22, 1.5 }, { 0.78, 1.6 }, { 0.03, 1.7 }, { 0.97, 1.5 } };
            for (int i = 0; i < front.GetLength(0); i++)
                Tree(dc, front[i, 0] * w, groundY - 6 * s, front[i, 1] * s * 1.1);

            // Cabin geometry
            double cx = w * 0.5;
            double cw = 330 * s, ch = 140 * s;
            double baseY = groundY - 6 * s;
            double wallTop = baseY + ch;
            double eave = 34 * s, peakH = 92 * s;
            var L = new Point(cx - cw / 2 - eave, wallTop - 4 * s);
            var R = new Point(cx + cw / 2 + eave, wallTop - 4 * s);
            var P = new Point(cx, wallTop + peakH);

            // Warm light on the snow
            double flick = 0.5 + 0.5 * Math.Sin(t * 3.1);
            double[] winX = { cx - cw * 0.28, cx + cw * 0.28 };
            foreach (double wx in winX)
                Glow(dc, wx, baseY - 4 * s, 110 * s, 1.0, 0.75, 0.30, 0.22 + 0.06 * flick);

            // Shadow under the cabin
            dc.DrawEllipse(B(0.25, 0.30, 0.55, 0.25), null, new Point(cx, baseY - 1 * s), cw * 0.6, 13 * s);

            // Chimney (behind the roof)
            double chX = cx + 72 * s, chW = 26 * s;
            double chTop = wallTop + 100 * s;
            Rect(dc, B(0.35, 0.20, 0.16), chX, wallTop, chW, 100 * s);
            Rect(dc, B(0.94, 0.96, 1.0), chX - 4 * s, chTop - 2 * s, chW + 8 * s, 9 * s);

            // Walls
            Rect(dc, B(0.33, 0.20, 0.13), cx - cw / 2, baseY, cw, ch);
            for (double py = baseY + 18 * s; py < wallTop; py += 18 * s)
                Rect(dc, B(0.21, 0.12, 0.09), cx - cw / 2, py, cw, 1.6 * s);
            Rect(dc, B(0.27, 0.16, 0.10), cx - cw / 2, baseY, 8 * s, ch);
            Rect(dc, B(0.27, 0.16, 0.10), cx + cw / 2 - 8 * s, baseY, 8 * s, ch);

            // Door
            Rect(dc, B(0.16, 0.09, 0.07), cx - 20 * s, baseY, 40 * s, 78 * s);
            dc.DrawEllipse(B(0.95, 0.80, 0.35), null, new Point(cx + 11 * s, baseY + 38 * s), 2.4 * s, 2.4 * s);

            // Windows
            foreach (double wx in winX)
            {
                double ww = 64 * s, wh = 56 * s, y0 = baseY + 50 * s;
                Brush frame = B(0.20, 0.12, 0.08);
                Rect(dc, frame, wx - ww / 2 - 3 * s, y0 - 3 * s, ww + 6 * s, wh + 6 * s);
                Rect(dc, B(1.0, 0.75 + 0.06 * flick, 0.36, 0.97), wx - ww / 2, y0, ww, wh);
                Rect(dc, frame, wx - 1.5 * s, y0, 3 * s, wh);
                Rect(dc, frame, wx - ww / 2, y0 + wh / 2 - 1.5 * s, ww, 3 * s);
                Rect(dc, B(0.94, 0.96, 1.0), wx - ww / 2 - 6 * s, y0 - 8 * s, ww + 12 * s, 7 * s);
            }

            // Roof
            Poly(dc, B(0.22, 0.12, 0.10), L, P, R);
            var snowPen = new Pen(B(0.95, 0.97, 1.0), 16 * s)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            var roofSnow = new StreamGeometry();
            using (StreamGeometryContext c = roofSnow.Open())
            {
                c.BeginFigure(new Point(L.X, L.Y + 6 * s), false, false);
                c.LineTo(new Point(P.X, P.Y + 6 * s), true, false);
                c.LineTo(new Point(R.X, R.Y + 6 * s), true, false);
            }
            roofSnow.Freeze();
            dc.DrawGeometry(null, snowPen, roofSnow);

            // Blinking string lights along both eaves
            double[,] palette =
            {
                { 1.0, 0.35, 0.35 }, { 1.0, 0.82, 0.30 }, { 0.40, 0.85, 0.50 }, { 0.40, 0.65, 1.0 }, { 0.90, 0.50, 1.0 }
            };
            const int n = 13;
            for (int side = 0; side < 2; side++)
            {
                Point a = side == 0 ? L : R;
                for (int i = 0; i < n; i++)
                {
                    double f = (i + 0.5) / n;
                    double px = a.X + (P.X - a.X) * f;
                    double py = a.Y + (P.Y - a.Y) * f - 11 * s;
                    double on = Math.Max(0, Math.Sin(t * 2.2 + (i + side * 3) * 0.9));
                    int ci = (i + side) % 5;
                    Glow(dc, px, py, 12 * s, palette[ci, 0], palette[ci, 1], palette[ci, 2], 0.45 * on);
                    dc.DrawEllipse(B(palette[ci, 0], palette[ci, 1], palette[ci, 2], 0.35 + 0.65 * on), null,
                                   new Point(px, py), 3.2 * s, 3.2 * s);
                }
            }

            // Chimney smoke
            for (int i = 0; i < 7; i++)
            {
                double p = Frac(t * 0.10 + i / 7.0);
                double sx = chX + chW / 2 + Math.Sin(p * 6 + i) * 14 * s + p * 34 * s;
                double sy = chTop + 8 * s + p * 190 * s;
                double r = (7 + p * 24) * s;
                dc.DrawEllipse(B(0.86, 0.89, 0.98, 0.38 * (1 - p)), null, new Point(sx, sy), r, r);
            }

            // Doorstep snow
            Rect(dc, B(0.94, 0.96, 1.0), cx - 34 * s, baseY - 4 * s, 68 * s, 8 * s);
        }
    }
}
