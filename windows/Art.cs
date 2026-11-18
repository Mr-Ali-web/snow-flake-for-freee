using System;
using System.Windows;
using System.Windows.Media;

namespace SnowFlakeUz
{
    /// <summary>Hand-drawn vector art: snowballs/snowmen and snow-plow vehicles (original artwork).</summary>
    internal static class Art
    {
        public static SolidColorBrush Br(double r, double g, double b, double a = 1.0)
        {
            var br = new SolidColorBrush(Color.FromArgb((byte)(a * 255), (byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
            br.Freeze();
            return br;
        }

        private static Pen MakePen(Brush b, double w)
        {
            var p = new Pen(b, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            return p;
        }

        private static RadialGradientBrush MakeBallFill()
        {
            var g = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.35, 0.7),
                RadiusX = 0.62,
                RadiusY = 0.62
            };
            g.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 255), 0));
            g.GradientStops.Add(new GradientStop(Color.FromRgb(204, 222, 247), 1));
            g.Freeze();
            return g;
        }

        private static readonly Brush Coal = Br(0.12, 0.12, 0.15);
        private static readonly Pen Outline = MakePen(Br(0.55, 0.65, 0.82, 0.45), 1);
        private static readonly RadialGradientBrush BallFill = MakeBallFill();

        private static void Fill(DrawingContext dc, Rect r, Brush b, double radius = 0)
        {
            if (radius > 0)
            {
                double rr = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
                dc.DrawRoundedRectangle(b, null, r, rr, rr);
            }
            else
            {
                dc.DrawRectangle(b, null, r);
            }
        }

        private static void Circle(DrawingContext dc, double cx, double cy, double r, Brush b)
        {
            dc.DrawEllipse(b, null, new Point(cx, cy), r, r);
        }

        private static void Line(DrawingContext dc, Point a, Point b, double w, Brush br)
        {
            dc.DrawLine(MakePen(br, w), a, b);
        }

        private static void Tri(DrawingContext dc, Point a, Point b, Point c, Brush br)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext ctx = g.Open())
            {
                ctx.BeginFigure(a, true, true);
                ctx.LineTo(b, true, false);
                ctx.LineTo(c, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(br, null, g);
        }

        // ---------- Snowball / snowman (local coordinates: y points up) ----------

        public static void DrawBall(DrawingContext dc, Ball ball)
        {
            double r = ball.R;
            dc.PushTransform(new MatrixTransform(1, 0, 0, -1, ball.X, ball.Y));
            dc.DrawEllipse(BallFill, Outline, new Point(0, 0), r, r);

            bool hasAbove = ball.Above != null;
            bool isHead = ball.Below != null && ball.Above == null;

            if (hasAbove && r > 9)
            {
                for (int i = 0; i < 3; i++)
                    Circle(dc, 0, r * 0.35 - i * r * 0.38, Math.Max(1.2, r * 0.09), Coal);
            }
            if (ball.Above != null && ball.Above.Above == null && r > 8)
            {
                Brush brown = Br(0.38, 0.24, 0.14);
                double w = Math.Max(1.5, r * 0.09);
                Line(dc, new Point(-r * 0.85, r * 0.2), new Point(-r * 1.6, r * 0.75), w, brown);
                Line(dc, new Point(r * 0.85, r * 0.2), new Point(r * 1.6, r * 0.75), w, brown);
            }
            if (isHead && r > 5)
            {
                double er = Math.Max(1.2, r * 0.09);
                Circle(dc, -r * 0.3, r * 0.2, er, Coal);
                Circle(dc, r * 0.3, r * 0.2, er, Coal);
                Tri(dc, new Point(0, r * 0.04), new Point(r * 0.8, -r * 0.1), new Point(0, -r * 0.16), Br(0.95, 0.45, 0.10));
                for (int i = -2; i <= 2; i++)
                    Circle(dc, i * r * 0.17, -r * 0.42 + i * i * r * 0.035, Math.Max(0.9, r * 0.05), Coal);
                Fill(dc, new Rect(-r * 0.85, r * 0.86, r * 1.7, r * 0.14), Coal);
                Fill(dc, new Rect(-r * 0.5, r * 0.98, r * 1.0, r * 0.75), Coal);
                Fill(dc, new Rect(-r * 0.5, r * 0.98, r * 1.0, r * 0.14), Br(0.80, 0.15, 0.18));
            }
            dc.Pop();
        }

        // ---------- Snow plow vehicles (facing +x, origin = ground contact, y up) ----------

        private static void Wheel(DrawingContext dc, double cx, double cy, double r, double t)
        {
            Circle(dc, cx, cy, r, Br(0.10, 0.11, 0.14));
            Circle(dc, cx, cy, r * 0.5, Br(0.62, 0.65, 0.70));
            for (int i = 0; i < 4; i++)
            {
                double a = t * 6 + i * Math.PI / 2;
                Line(dc, new Point(cx, cy), new Point(cx + Math.Cos(a) * r * 0.45, cy + Math.Sin(a) * r * 0.45),
                     1.6, Br(0.25, 0.27, 0.32));
            }
        }

        public static void DrawVehicle(DrawingContext dc, PlowKind kind, double t, double x, double groundY, double dir)
        {
            dc.PushTransform(new MatrixTransform(dir, 0, 0, -1, x, groundY + 2));
            Brush gray = Br(0.58, 0.63, 0.70);
            Brush dark = Br(0.12, 0.13, 0.17);
            switch (kind)
            {
                case PlowKind.Tractor:
                    {
                        Brush orange = Br(0.86, 0.36, 0.22);
                        Fill(dc, new Rect(-30, 14, 84, 22), orange, 5);
                        Fill(dc, new Rect(-26, 34, 34, 26), orange, 4);
                        Fill(dc, new Rect(-22, 38, 26, 18), Br(0.75, 0.88, 0.98), 3);
                        Fill(dc, new Rect(38, 34, 5, 16), dark);
                        Wheel(dc, -14, 17, 17, t);
                        Wheel(dc, 44, 10, 10, t);
                        Line(dc, new Point(52, 18), new Point(66, 12), 4, gray);
                        Fill(dc, new Rect(64, 0, 7, 28), gray, 2);
                        break;
                    }
                case PlowKind.Snowmobile:
                    {
                        Fill(dc, new Rect(-34, 0, 62, 12), dark, 6);
                        Fill(dc, new Rect(-30, 8, 72, 18), Br(0.20, 0.45, 0.85), 7);
                        Fill(dc, new Rect(-24, 24, 30, 7), dark, 3);
                        Tri(dc, new Point(22, 26), new Point(34, 26), new Point(26, 38), Br(0.75, 0.88, 0.98));
                        Line(dc, new Point(24, 5), new Point(70, 9), 5, dark);
                        Line(dc, new Point(58, 12), new Point(78, 12), 4, gray);
                        Fill(dc, new Rect(76, 0, 7, 24), gray, 2);
                        break;
                    }
                default:
                    {
                        double bob = Math.Abs(Math.Sin(t * 9)) * 3;
                        Brush navy = Br(0.10, 0.12, 0.20);
                        Brush orange = Br(0.96, 0.60, 0.15);
                        Fill(dc, new Rect(-12 + bob, 0, 12, 5), orange, 2);
                        Fill(dc, new Rect(2 - bob, 0, 12, 5), orange, 2);
                        dc.DrawEllipse(navy, null, new Point(0, 21 + bob), 14, 17);
                        dc.DrawEllipse(Br(0.96, 0.97, 1.0), null, new Point(1.5, 19 + bob), 9.5, 13);
                        Circle(dc, 2, 42 + bob, 10, navy);
                        Circle(dc, 5, 44 + bob, 2.2, Br(1, 1, 1));
                        Circle(dc, 5.6, 44 + bob, 1, navy);
                        Tri(dc, new Point(10, 43 + bob), new Point(19, 40 + bob), new Point(10, 38 + bob), orange);
                        Line(dc, new Point(6, 22 + bob), new Point(52, 6), 3, Br(0.45, 0.30, 0.18));
                        Fill(dc, new Rect(52, 0, 7, 22), gray, 2);
                        break;
                    }
            }
            dc.Pop();
        }
    }
}
