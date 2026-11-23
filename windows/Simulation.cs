using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SnowFlakeUz
{
    internal enum Intensity { Light, Steady, Blizzard }
    internal enum ToolMode { Mitten, Snowball, None }
    internal enum PlowKind { Tractor, Snowmobile, Penguin }
    internal enum BallState { Held, Falling, Resting }

    internal struct Flake
    {
        public double X, Y, Vx, Vy, R, Phase, Free;
    }

    internal sealed class Surface
    {
        public int Id;
        public Rect Rect;       // screen pixels, y grows downward; Rect.Y is the top edge
        public bool IsGround;
    }

    /// <summary>A snowball. Balls stack into snowmen through Below / Above.</summary>
    internal sealed class Ball
    {
        public double X, Y, R;      // Y = centre
        public double Vy;
        public BallState State = BallState.Held;
        public int SurfaceId;
        public bool HasSurface;
        public double LocalX;
        public double GrabDx, GrabDy;
        public Ball Below, Above;
        public bool Removed;
    }

    /// <summary>Snow drift: height-map along the top edge of a window / the bottom of the screen.</summary>
    internal sealed class Pile
    {
        public const double Step = 3;
        public double[] H;
        public readonly double Max;

        public Pile(double width, double max)
        {
            H = new double[Math.Max(2, (int)(width / Step))];
            Max = max;
        }

        public int Index(double lx) { return Math.Min(H.Length - 1, Math.Max(0, (int)(lx / Step))); }
        public double HeightAt(double lx) { return H[Index(lx)]; }

        public void Add(double lx, double amount)
        {
            int i = Index(lx);
            for (int d = -2; d <= 2; d++)
            {
                int j = i + d;
                if (j < 0 || j >= H.Length) continue;
                double w = 1.0 - Math.Abs(d) / 3.0;
                H[j] = Math.Min(Max, H[j] + amount * w * 0.45);
            }
        }

        public void Relax()
        {
            int n = H.Length;
            if (n < 3) return;
            for (int i = 0; i < n - 1; i++)
            {
                double diff = H[i] - H[i + 1];
                if (Math.Abs(diff) > 1.8)
                {
                    double m = (Math.Abs(diff) - 1.8) * 0.2;
                    if (diff > 0) { H[i] -= m; H[i + 1] += m; }
                    else { H[i] += m; H[i + 1] -= m; }
                }
            }
            H[0] = Math.Min(H[0], H[1]);
            H[n - 1] = Math.Min(H[n - 1], H[n - 2]);
        }

        public void Resize(double width)
        {
            int n = Math.Max(2, (int)(width / Step));
            if (n == H.Length) return;
            var nh = new double[n];
            for (int i = 0; i < n; i++) nh[i] = H[Math.Min(H.Length - 1, i * H.Length / n)];
            H = nh;
        }
    }

    internal sealed class Simulation
    {
        public static readonly Simulation I = new Simulation();

        // Settings (changed from the tray menu)
        public Intensity Snowfall = Intensity.Steady;
        public double Wind;
        public bool Paused;
        public ToolMode Tool = ToolMode.Mitten;
        public bool Accumulate = true;
        public bool PlowEnabled = true;
        public PlowKind CurrentPlow = PlowKind.Tractor;
        public bool WallpaperOn;

        // State
        public readonly List<Flake> Flakes = new List<Flake>();
        public readonly Dictionary<int, Pile> Piles = new Dictionary<int, Pile>();
        public List<Surface> Surfaces = new List<Surface>();   // front-to-back windows, then ground surfaces
        public readonly List<Ball> Balls = new List<Ball>();
        public Rect Union = new Rect(0, 0, 1920, 1080);
        public Action<HashSet<int>> OnFullscreenChange;
        public Action<bool> OnCaptureChange;

        private static readonly Random Rng = new Random();
        private readonly Dictionary<int, Rect> lastRects = new Dictionary<int, Rect>();
        private HashSet<int> fullscreen = new HashSet<int>();
        private readonly Dictionary<int, double> plowX = new Dictionary<int, double>();
        private readonly Dictionary<int, double> plowDir = new Dictionary<int, double>();
        private readonly Dictionary<int, double> plowCooldown = new Dictionary<int, double>();
        private double spawnAcc;
        private double clock;
        private int tick;
        private bool capturing;
        private bool wasDown;
        private Point lastMouse;
        private const int MaxFlakes = 4500;
        private const int MaxBalls = 40;

        private static double Rand(double a, double b) { return a + Rng.NextDouble() * (b - a); }

        private static double Rate(Intensity i)
        {
            switch (i)
            {
                case Intensity.Light: return 35;
                case Intensity.Blizzard: return 380;
                default: return 110;
            }
        }

        private static double BladeOffset(PlowKind k)
        {
            switch (k)
            {
                case PlowKind.Snowmobile: return 80;
                case PlowKind.Penguin: return 58;
                default: return 70;
            }
        }

        // ------------------------------------------------------------ lifecycle

        public void Init()
        {
            Rect u = Rect.Empty;
            foreach (System.Windows.Forms.Screen s in System.Windows.Forms.Screen.AllScreens)
            {
                System.Drawing.Rectangle b = s.Bounds;
                u = Rect.Union(u, new Rect(b.Left, b.Top, b.Width, b.Height));
            }
            if (!u.IsEmpty) Union = u;
            Flakes.Clear();
            int count = (int)(Rate(Snowfall) * (Union.Width / 1500.0) * 6);
            for (int i = 0; i < count; i++) Flakes.Add(MakeFlake(Rand(Union.Top, Union.Bottom)));
            RefreshWindows();
        }

        private Flake MakeFlake(double y)
        {
            double r = Rand(1.1, 3.4);
            return new Flake
            {
                X = Rand(Union.Left - 100, Union.Right + 100),
                Y = y,
                Vx = Wind,
                Vy = 40 + r * 22,
                R = r,
                Phase = Rand(0, 6.28),
                Free = 0
            };
        }

        public void Tick(double dt, double now)
        {
            clock = now;
            tick++;
            if (tick % 3 == 0) RefreshWindows();
            if (!Paused)
            {
                Spawn(dt);
                Step(dt);
                foreach (Pile p in Piles.Values) p.Relax();
                UpdatePlows(dt);
                Mitten();
                UpdateBalls(dt);
            }
            UpdateMouse();
        }

        private void Spawn(double dt)
        {
            spawnAcc += Rate(Snowfall) * dt * (Union.Width / 1500.0);
            while (spawnAcc >= 1)
            {
                spawnAcc -= 1;
                if (Flakes.Count >= MaxFlakes) break;
                Flakes.Add(MakeFlake(Union.Top - 8));
            }
        }

        private bool Hidden(Point p, int k)
        {
            for (int j = 0; j < k && j < Surfaces.Count; j++)
                if (Surfaces[j].Rect.Contains(p)) return true;
            return false;
        }

        private void Step(double dt)
        {
            int i = 0;
            while (i < Flakes.Count)
            {
                Flake f = Flakes[i];
                double oldY = f.Y;
                double tvx = Wind * (0.5 + f.R / 4) + Math.Sin(clock * 1.3 + f.Phase) * 12;
                double tvy = 40 + f.R * 22;
                f.Vx += (tvx - f.Vx) * Math.Min(1, dt * 1.5);
                f.Vy += (tvy - f.Vy) * Math.Min(1, dt * 2.5);
                f.X += f.Vx * dt;
                f.Y += f.Vy * dt;
                if (f.Free > 0) f.Free -= dt;

                bool landed = false;
                if (Accumulate && f.Free <= 0 && f.Vy > 0)
                {
                    for (int k = 0; k < Surfaces.Count; k++)
                    {
                        Surface s = Surfaces[k];
                        if (f.X < s.Rect.X || f.X > s.Rect.Right) continue;
                        Pile p;
                        if (!Piles.TryGetValue(s.Id, out p)) continue;
                        double top = s.Rect.Y - p.HeightAt(f.X - s.Rect.X);
                        if (oldY <= top && f.Y > top)
                        {
                            if (Hidden(new Point(f.X, s.Rect.Y - 0.5), k)) continue;
                            p.Add(f.X - s.Rect.X, f.R * 0.9);
                            landed = true;
                            break;
                        }
                    }
                }
                bool gone = f.Y > Union.Bottom + 40 || f.X < Union.Left - 150 || f.X > Union.Right + 150;
                if (landed || gone)
                {
                    int last = Flakes.Count - 1;
                    Flakes[i] = Flakes[last];
                    Flakes.RemoveAt(last);
                }
                else
                {
                    Flakes[i] = f;
                    i++;
                }
            }
        }

        // ------------------------------------------------------------ windows

        private void RefreshWindows()
        {
            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            var wins = new List<Surface>();
            var full = new HashSet<int>();
            uint me = (uint)Environment.ProcessId;
            var sb = new StringBuilder(128);

            Native.EnumWindows((h, l) =>
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0 || (ex & Native.WS_EX_NOACTIVATE) != 0) return true;
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid == me) return true;
                int cloaked;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;

                sb.Clear();
                Native.GetClassName(h, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" ||
                    cls == "Windows.UI.Core.CoreWindow" || cls == "XamlExplorerHostIslandWindow") return true;

                RECT r;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out r, 16) != 0)
                {
                    if (!Native.GetWindowRect(h, out r)) return true;
                }
                double w = r.Right - r.Left, hh = r.Bottom - r.Top;
                if (w < 120 || hh < 60) return true;

                // A window exactly the size of a monitor = full-screen app: hide the snow on that monitor.
                for (int i = 0; i < screens.Length; i++)
                {
                    System.Drawing.Rectangle b = screens[i].Bounds;
                    if (Math.Abs(b.Left - r.Left) < 2 && Math.Abs(b.Top - r.Top) < 2 &&
                        Math.Abs(b.Width - w) < 2 && Math.Abs(b.Height - hh) < 2)
                    {
                        full.Add(i);
                        return true;
                    }
                }
                wins.Add(new Surface { Id = (int)(h.ToInt64() & 0x7fffffff), Rect = new Rect(r.Left, r.Top, w, hh), IsGround = false });
                return true;
            }, IntPtr.Zero);

            bool changed = full.Count != fullscreen.Count;
            if (!changed) foreach (int x in full) if (!fullscreen.Contains(x)) { changed = true; break; }
            if (changed)
            {
                fullscreen = full;
                if (OnFullscreenChange != null) OnFullscreenChange(full);
            }

            if (!Accumulate)
            {
                Surfaces = new List<Surface>();
                Piles.Clear();
                lastRects.Clear();
                return;
            }

            var surfaces = new List<Surface>(wins);
            var seen = new HashSet<int>();
            foreach (Surface s in wins)
            {
                seen.Add(s.Id);
                Pile p;
                if (Piles.TryGetValue(s.Id, out p))
                {
                    p.Resize(s.Rect.Width);
                    Rect old;
                    if (lastRects.TryGetValue(s.Id, out old) &&
                        Math.Abs(old.X - s.Rect.X) + Math.Abs(old.Y - s.Rect.Y) > 7)
                        Shed(p, s.Rect, 0.18);      // window is being dragged: snow slides off
                }
                else
                {
                    Piles[s.Id] = new Pile(s.Rect.Width, 16);
                }
                lastRects[s.Id] = s.Rect;
            }
            for (int i = 0; i < screens.Length; i++)
            {
                int id = -(i + 1);
                System.Drawing.Rectangle b = screens[i].Bounds;
                System.Drawing.Rectangle wa = screens[i].WorkingArea;
                var rect = new Rect(b.Left, wa.Bottom, b.Width, 1);
                if (!Piles.ContainsKey(id)) Piles[id] = new Pile(rect.Width, 26);
                surfaces.Add(new Surface { Id = id, Rect = rect, IsGround = true });
                seen.Add(id);
            }
            // Closed / minimised windows: their snow drops down.
            var stale = new List<int>();
            foreach (int id in Piles.Keys) if (!seen.Contains(id)) stale.Add(id);
            foreach (int id in stale)
            {
                Rect lr;
                if (lastRects.TryGetValue(id, out lr)) Shed(Piles[id], lr, 1.0);
                Piles.Remove(id);
                lastRects.Remove(id);
                plowX.Remove(id);
            }
            Surfaces = surfaces;
        }

        /// <summary>Turns part of a drift back into falling flakes.</summary>
        private void Shed(Pile p, Rect rect, double fraction)
        {
            for (int i = 0; i < p.H.Length; i++)
            {
                double h = p.H[i];
                if (h < 0.6) continue;
                double take = h * fraction;
                p.H[i] -= take;
                int n = (int)(take / 1.6);
                for (int k = 0; k < n; k++)
                {
                    if (Flakes.Count >= MaxFlakes) return;
                    Flakes.Add(new Flake
                    {
                        X = rect.X + i * Pile.Step + Rand(0, Pile.Step),
                        Y = rect.Y - h - Rand(0, 3),
                        Vx = Rand(-25, 25),
                        Vy = Rand(20, 70),
                        R = Rand(1.2, 2.6),
                        Phase = Rand(0, 6.28),
                        Free = 0.6
                    });
                }
            }
        }

        public void ClearAll()
        {
            foreach (Surface s in Surfaces)
            {
                Pile p;
                if (Piles.TryGetValue(s.Id, out p)) Shed(p, s.Rect, 1.0);
            }
        }

        public void ClearBalls()
        {
            foreach (Ball b in Balls) { b.Above = null; b.Below = null; }
            Balls.Clear();
        }

        // ------------------------------------------------------------ mitten

        private static Point CursorPos()
        {
            POINT p;
            Native.GetCursorPos(out p);
            return new Point(p.X, p.Y);
        }

        private void Mitten()
        {
            Point m = CursorPos();
            double dx = m.X - lastMouse.X, dy = m.Y - lastMouse.Y;
            lastMouse = m;
            double speed = Math.Sqrt(dx * dx + dy * dy);
            if (Tool != ToolMode.Mitten || speed < 1.5) return;
            foreach (Surface s in Surfaces)
            {
                Pile p;
                if (!Piles.TryGetValue(s.Id, out p)) continue;
                double lx = m.X - s.Rect.X;
                if (lx < -30 || lx > s.Rect.Width + 30) continue;
                if (m.Y > s.Rect.Y + 6 || m.Y < s.Rect.Y - p.Max - 40) continue;
                int center = (int)(lx / Pile.Step);
                const int radius = 9;
                for (int d = -radius; d <= radius; d++)
                {
                    int j = center + d;
                    if (j < 0 || j >= p.H.Length) continue;
                    double w = 1.0 - Math.Abs(d) / (double)(radius + 1);
                    double take = Math.Min(p.H[j], 2.2 * w * Math.Min(speed / 8, 2));
                    if (take < 0.05) continue;
                    p.H[j] -= take;
                    if (Rng.Next(3) == 0 && Flakes.Count < MaxFlakes)
                    {
                        Flakes.Add(new Flake
                        {
                            X = s.Rect.X + j * Pile.Step,
                            Y = s.Rect.Y - p.H[j] - 2,
                            Vx = dx * 6 + Rand(-30, 30),
                            Vy = -Rand(20, 90),
                            R = Rand(1.0, 2.2),
                            Phase = Rand(0, 6.28),
                            Free = 0.5
                        });
                    }
                }
            }
        }

        // ------------------------------------------------------------ snow plow

        private void UpdatePlows(double dt)
        {
            foreach (Surface s in Surfaces)
            {
                if (!s.IsGround) continue;
                Pile p;
                if (!Piles.TryGetValue(s.Id, out p)) continue;
                double x;
                if (plowX.TryGetValue(s.Id, out x))
                {
                    double dir = plowDir[s.Id];
                    double nx = x + dir * 150 * dt;
                    plowX[s.Id] = nx;
                    double bladeX = nx + dir * BladeOffset(CurrentPlow);
                    int c = (int)((bladeX - s.Rect.X) / Pile.Step);
                    for (int d = -10; d <= 10; d++)
                    {
                        int j = c + d;
                        if (j < 0 || j >= p.H.Length) continue;
                        double h = p.H[j];
                        if (h < 0.4) continue;
                        p.H[j] = 0;
                        if (Rng.Next(2) == 0 && Flakes.Count < MaxFlakes)
                        {
                            Flakes.Add(new Flake
                            {
                                X = bladeX + Rand(-6, 6),
                                Y = s.Rect.Y - h,
                                Vx = dir * Rand(20, 110),
                                Vy = -Rand(30, 120),
                                R = Rand(1.1, 2.4),
                                Phase = Rand(0, 6.28),
                                Free = 0.5
                            });
                        }
                    }
                    if ((dir > 0 && nx > s.Rect.Right + 140) || (dir < 0 && nx < s.Rect.X - 140))
                    {
                        plowX.Remove(s.Id);
                        plowCooldown[s.Id] = 25;
                    }
                }
                else
                {
                    double cd;
                    if (plowCooldown.TryGetValue(s.Id, out cd) && cd > 0)
                    {
                        plowCooldown[s.Id] = cd - dt;
                        continue;
                    }
                    if (!PlowEnabled || tick % 30 != 0) continue;
                    double sum = 0;
                    for (int i = 0; i < p.H.Length; i++) sum += p.H[i];
                    if (sum / p.H.Length > 6)
                    {
                        double dir = Rng.Next(2) == 0 ? 1 : -1;
                        plowDir[s.Id] = dir;
                        plowX[s.Id] = dir > 0 ? s.Rect.X - 120 : s.Rect.Right + 120;
                    }
                }
            }
        }

        // ------------------------------------------------------------ snowballs & snowmen

        private static Ball BaseOf(Ball b)
        {
            Ball c = b;
            while (c.Below != null) c = c.Below;
            return c;
        }

        private static bool InChain(Ball baseBall, Ball target)
        {
            Ball cur = baseBall;
            while (cur != null)
            {
                if (cur == target) return true;
                cur = cur.Above;
            }
            return false;
        }

        private static void RemoveChain(Ball baseBall)
        {
            Ball cur = baseBall;
            while (cur != null)
            {
                cur.Removed = true;
                cur = cur.Above;
            }
        }

        private static void Detach(Ball b)
        {
            if (b.Below != null) b.Below.Above = null;
            b.Below = null;
        }

        /// <summary>While a ball is dragged along a drift it picks up snow and grows.</summary>
        private double GatherSnow(Ball b)
        {
            double taken = 0;
            foreach (Surface s in Surfaces)
            {
                Pile p;
                if (!Piles.TryGetValue(s.Id, out p)) continue;
                double lx = b.X - s.Rect.X;
                if (lx < 0 || lx > s.Rect.Width) continue;
                double bottom = b.Y + b.R;
                double top = s.Rect.Y - p.HeightAt(lx);
                if (bottom < top - 8 || bottom > s.Rect.Y + 12) continue;
                int c = (int)(lx / Pile.Step);
                int rad = Math.Max(2, (int)(b.R / Pile.Step));
                for (int d = -rad; d <= rad; d++)
                {
                    int j = c + d;
                    if (j < 0 || j >= p.H.Length) continue;
                    double t = Math.Min(p.H[j], 0.5);
                    p.H[j] -= t;
                    taken += t;
                }
            }
            return taken;
        }

        private void UpdateBalls(double dt)
        {
            if (Balls.Count == 0) return;
            Point m = CursorPos();

            for (int bi = 0; bi < Balls.Count; bi++)
            {
                Ball b = Balls[bi];
                if (b.Below != null || b.Removed) continue;

                if (b.State == BallState.Held)
                {
                    double ox = b.X, oy = b.Y;
                    b.X = m.X + b.GrabDx;
                    b.Y = m.Y + b.GrabDy;
                    double speed = Math.Sqrt((b.X - ox) * (b.X - ox) + (b.Y - oy) * (b.Y - oy));
                    if (speed > 1 && b.Above == null)
                        b.R = Math.Min(30, b.R + GatherSnow(b) * 0.03);
                }
                else if (b.State == BallState.Falling)
                {
                    double oldBottom = b.Y + b.R;
                    b.Vy = Math.Min(b.Vy + 1400 * dt, 1200);
                    b.Y += b.Vy * dt;
                    double newBottom = b.Y + b.R;
                    bool settled = false;

                    // onto the top of another snowman / ball
                    foreach (Ball t in Balls)
                    {
                        if (t == b || t.Above != null || t.Removed || InChain(b, t)) continue;
                        if (BaseOf(t).State != BallState.Resting) continue;
                        if (Math.Abs(b.X - t.X) < t.R * 0.85 && b.R <= t.R * 1.1)
                        {
                            double topY = t.Y - t.R;
                            if (newBottom >= topY && oldBottom <= t.Y - t.R * 0.6)
                            {
                                b.Below = t;
                                t.Above = b;
                                b.State = BallState.Resting;
                                b.Vy = 0;
                                settled = true;
                                break;
                            }
                        }
                    }
                    // onto a drift (window top / ground)
                    if (!settled)
                    {
                        for (int k = 0; k < Surfaces.Count; k++)
                        {
                            Surface s = Surfaces[k];
                            if (b.X < s.Rect.X || b.X > s.Rect.Right) continue;
                            Pile p;
                            if (!Piles.TryGetValue(s.Id, out p)) continue;
                            double top = s.Rect.Y - p.HeightAt(b.X - s.Rect.X);
                            if (oldBottom <= top + 1 && newBottom >= top)
                            {
                                if (Hidden(new Point(b.X, s.Rect.Y - 0.5), k)) continue;
                                b.State = BallState.Resting;
                                b.SurfaceId = s.Id;
                                b.HasSurface = true;
                                b.LocalX = b.X - s.Rect.X;
                                b.Y = top - b.R * 0.9;
                                b.Vy = 0;
                                break;
                            }
                        }
                    }
                    if (b.Y > Union.Bottom + 120) RemoveChain(b);
                }
                else // resting
                {
                    Surface s = null;
                    if (b.HasSurface)
                        foreach (Surface c in Surfaces) if (c.Id == b.SurfaceId) { s = c; break; }
                    Pile p;
                    if (s != null && Piles.TryGetValue(s.Id, out p))
                    {
                        b.X = s.Rect.X + b.LocalX;
                        b.Y = s.Rect.Y - p.HeightAt(b.LocalX) - b.R * 0.9;
                    }
                    else
                    {
                        b.State = BallState.Falling;
                        b.Vy = 0;
                    }
                }

                // lay out the balls stacked on top
                Ball lower = b;
                Ball cur = b.Above;
                while (cur != null)
                {
                    cur.X = lower.X;
                    cur.Y = lower.Y - lower.R - cur.R * 0.85;
                    lower = cur;
                    cur = cur.Above;
                }
            }

            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                if (Balls[i].Removed)
                {
                    Balls[i].Above = null;
                    Balls[i].Below = null;
                    Balls.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------ mouse

        private bool PileHit(Point m)
        {
            for (int k = 0; k < Surfaces.Count; k++)
            {
                Surface s = Surfaces[k];
                Pile p;
                if (!Piles.TryGetValue(s.Id, out p)) continue;
                double lx = m.X - s.Rect.X;
                if (lx < 0 || lx > s.Rect.Width) continue;
                double h = p.HeightAt(lx);
                if (h > 1.5 && m.Y <= s.Rect.Y + 2 && m.Y >= s.Rect.Y - h - 4)
                {
                    if (!Hidden(m, k)) return true;
                }
            }
            return false;
        }

        private bool WantsMouse(Point m)
        {
            foreach (Ball b in Balls)
            {
                if (b.State == BallState.Held) return true;
                double dx = b.X - m.X, dy = b.Y - m.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < b.R + 4) return true;
            }
            return PileHit(m);
        }

        /// <summary>Polls the cursor and the left button; makes the overlay interactive only over snow.</summary>
        private void UpdateMouse()
        {
            Point m = CursorPos();
            bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool want = Tool == ToolMode.Snowball && WantsMouse(m);
            if (want != capturing)
            {
                capturing = want;
                if (OnCaptureChange != null) OnCaptureChange(want);
            }
            if (Tool == ToolMode.Snowball)
            {
                if (down && !wasDown && capturing) MouseDown(m);
            }
            if (!down) MouseUp();
            wasDown = down;
        }

        private void MouseDown(Point m)
        {
            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                Ball b = Balls[i];
                if (b.Removed) continue;
                double dx = b.X - m.X, dy = b.Y - m.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < b.R + 4)
                {
                    Detach(b);
                    b.State = BallState.Held;
                    b.HasSurface = false;
                    b.Vy = 0;
                    b.GrabDx = b.X - m.X;
                    b.GrabDy = b.Y - m.Y;
                    return;
                }
            }
            if (Balls.Count < MaxBalls && PileHit(m))
            {
                var nb = new Ball { X = m.X, Y = m.Y - 3, R = 6, State = BallState.Held };
                Balls.Add(nb);
            }
        }

        private void MouseUp()
        {
            foreach (Ball b in Balls)
            {
                if (b.State == BallState.Held)
                {
                    b.State = BallState.Falling;
                    b.Vy = 0;
                }
            }
        }

        // ------------------------------------------------------------ drawing

        private static readonly Brush PileFill = Art.Br(0.96, 0.98, 1.0, 0.97);
        private static readonly Pen PileStroke = MakeStroke();
        private static readonly Brush FlakeBrush = Art.Br(1, 1, 1, 0.93);

        private static Pen MakeStroke()
        {
            var p = new Pen(Art.Br(0.55, 0.65, 0.82, 0.35), 1);
            p.Freeze();
            return p;
        }

        /// <summary>Clips out the windows that are in front of surface k (snow sits behind them).</summary>
        private bool PushClipBehind(DrawingContext dc, int k, Rect screen, Rect area)
        {
            Geometry g = null;
            for (int j = 0; j < k; j++)
            {
                if (!Surfaces[j].Rect.IntersectsWith(area)) continue;
                if (g == null)
                {
                    Rect big = screen;
                    big.Inflate(50, 50);
                    g = new RectangleGeometry(big);
                }
                g = new CombinedGeometry(GeometryCombineMode.Exclude, g, new RectangleGeometry(Surfaces[j].Rect));
            }
            if (g == null) return false;
            dc.PushClip(g);
            return true;
        }

        public void Draw(DrawingContext dc, Rect screen)
        {
            // Drifts
            for (int k = 0; k < Surfaces.Count; k++)
            {
                Surface s = Surfaces[k];
                Pile p;
                if (!Piles.TryGetValue(s.Id, out p)) continue;
                bool any = false;
                for (int i = 0; i < p.H.Length; i++) if (p.H[i] > 0.3) { any = true; break; }
                if (!any) continue;
                var area = new Rect(s.Rect.X - 40, s.Rect.Y - 100, s.Rect.Width + 80, 130);
                if (!area.IntersectsWith(screen)) continue;

                bool clipped = PushClipBehind(dc, k, screen, area);
                var g = new StreamGeometry();
                using (StreamGeometryContext c = g.Open())
                {
                    c.BeginFigure(new Point(s.Rect.X, s.Rect.Y + 1), true, true);
                    for (int i = 0; i < p.H.Length; i++)
                        c.LineTo(new Point(Math.Min(s.Rect.Right, s.Rect.X + i * Pile.Step), s.Rect.Y - p.H[i]), true, false);
                    c.LineTo(new Point(s.Rect.Right, s.Rect.Y + 1), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(PileFill, PileStroke, g);
                if (clipped) dc.Pop();
            }

            // Snowballs and snowmen
            foreach (Ball b in Balls)
            {
                if (b.Below != null || b.Removed) continue;
                var near = new Rect(screen.X - 80, screen.Y - 80, screen.Width + 160, screen.Height + 160);
                if (!near.Contains(new Point(b.X, b.Y))) continue;
                int k = -1;
                if (b.State == BallState.Resting && b.HasSurface)
                    for (int i = 0; i < Surfaces.Count; i++) if (Surfaces[i].Id == b.SurfaceId) { k = i; break; }
                bool clipped = false;
                if (k > 0) clipped = PushClipBehind(dc, k, screen, new Rect(b.X - 200, b.Y - 200, 400, 400));
                Ball cur = b;
                while (cur != null)
                {
                    Art.DrawBall(dc, cur);
                    cur = cur.Above;
                }
                if (clipped) dc.Pop();
            }

            // Snow plows
            foreach (Surface s in Surfaces)
            {
                if (!s.IsGround) continue;
                double x;
                if (!plowX.TryGetValue(s.Id, out x)) continue;
                if (x < screen.Left - 160 || x > screen.Right + 160) continue;
                if (s.Rect.Y < screen.Top || s.Rect.Y > screen.Bottom) continue;
                double dir = 1;
                plowDir.TryGetValue(s.Id, out dir);
                Art.DrawVehicle(dc, CurrentPlow, clock, x, s.Rect.Y, dir);
            }

            // Falling flakes (drawn over everything)
            Rect vis = screen;
            vis.Inflate(6, 6);
            for (int i = 0; i < Flakes.Count; i++)
            {
                Flake f = Flakes[i];
                if (f.X < vis.Left || f.X > vis.Right || f.Y < vis.Top || f.Y > vis.Bottom) continue;
                dc.DrawEllipse(FlakeBrush, null, new Point(f.X, f.Y), f.R, f.R);
            }
        }
    }
}
