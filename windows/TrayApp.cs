using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SnowFlakeUz
{
    /// <summary>Tray icon + menu, overlay windows and the render loop.</summary>
    internal sealed class TrayApp
    {
        private readonly Simulation sim = Simulation.I;
        private readonly List<OverlayWindow> overlays = new List<OverlayWindow>();
        private readonly List<KeyValuePair<ToolStripMenuItem, Func<bool>>> checks =
            new List<KeyValuePair<ToolStripMenuItem, Func<bool>>>();
        private ToolStripMenuItem pauseItem;
        private NotifyIcon tray;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double last;

        public void Start()
        {
            sim.Init();
            BuildOverlays();
            BuildTray();
            sim.OnFullscreenChange = set =>
            {
                for (int i = 0; i < overlays.Count; i++)
                    overlays[i].Visibility = set.Contains(i) ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
            };
            sim.OnCaptureChange = want =>
            {
                foreach (OverlayWindow o in overlays) o.SetCapture(want);
            };
            System.Windows.Media.CompositionTarget.Rendering += OnFrame;
            SystemEvents.DisplaySettingsChanged += (a, b) =>
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    sim.Init();
                    BuildOverlays();
                }));
        }

        private void BuildOverlays()
        {
            foreach (OverlayWindow o in overlays) o.Close();
            overlays.Clear();
            foreach (Screen s in Screen.AllScreens)
            {
                var w = new OverlayWindow(s);
                w.Show();
                overlays.Add(w);
            }
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(now - last, 0.05);
            last = now;
            if (dt <= 0) return;
            sim.Tick(dt, now);
            foreach (OverlayWindow o in overlays)
                if (o.IsVisible) o.Canvas.InvalidateVisual();
        }

        // ------------------------------------------------------------ tray menu

        private ToolStripMenuItem Add(ToolStripItemCollection parent, string text, Action click, Func<bool> isOn = null)
        {
            var it = new ToolStripMenuItem(text);
            it.Click += (a, b) => click();
            if (isOn != null) checks.Add(new KeyValuePair<ToolStripMenuItem, Func<bool>>(it, isOn));
            parent.Add(it);
            return it;
        }

        private static ToolStripMenuItem Sub(ToolStripItemCollection parent, string text)
        {
            var it = new ToolStripMenuItem(text);
            parent.Add(it);
            return it;
        }

        private void BuildTray()
        {
            var menu = new ContextMenuStrip();

            ToolStripMenuItem snow = Sub(menu.Items, "Qor yog'ishi");
            Add(snow.DropDownItems, "Yengil", () => sim.Snowfall = Intensity.Light, () => sim.Snowfall == Intensity.Light);
            Add(snow.DropDownItems, "Tekis", () => sim.Snowfall = Intensity.Steady, () => sim.Snowfall == Intensity.Steady);
            Add(snow.DropDownItems, "Bo'ron", () => sim.Snowfall = Intensity.Blizzard, () => sim.Snowfall == Intensity.Blizzard);

            ToolStripMenuItem wind = Sub(menu.Items, "Shamol");
            Add(wind.DropDownItems, "Chapga kuchli", () => sim.Wind = -90, () => sim.Wind == -90);
            Add(wind.DropDownItems, "Chapga", () => sim.Wind = -35, () => sim.Wind == -35);
            Add(wind.DropDownItems, "Shamolsiz", () => sim.Wind = 0, () => sim.Wind == 0);
            Add(wind.DropDownItems, "O'ngga", () => sim.Wind = 35, () => sim.Wind == 35);
            Add(wind.DropDownItems, "O'ngga kuchli", () => sim.Wind = 90, () => sim.Wind == 90);

            ToolStripMenuItem tool = Sub(menu.Items, "Asbob");
            Add(tool.DropDownItems, "Qo'lqop (kursor bilan supurish)", () => sim.Tool = ToolMode.Mitten, () => sim.Tool == ToolMode.Mitten);
            Add(tool.DropDownItems, "Qor shari va qorbobo yasash", () => sim.Tool = ToolMode.Snowball, () => sim.Tool == ToolMode.Snowball);
            Add(tool.DropDownItems, "Asbobsiz", () => sim.Tool = ToolMode.None, () => sim.Tool == ToolMode.None);
            tool.DropDownItems.Add(new ToolStripSeparator());
            Add(tool.DropDownItems, "Qorbobolarni o'chirish", () => sim.ClearBalls());

            ToolStripMenuItem plow = Sub(menu.Items, "Qor tozalagich");
            Add(plow.DropDownItems, "Qor tozalagich yoqilgan", () => sim.PlowEnabled = !sim.PlowEnabled, () => sim.PlowEnabled);
            plow.DropDownItems.Add(new ToolStripSeparator());
            Add(plow.DropDownItems, "Traktor", () => sim.CurrentPlow = PlowKind.Tractor, () => sim.CurrentPlow == PlowKind.Tractor);
            Add(plow.DropDownItems, "Qor skuteri", () => sim.CurrentPlow = PlowKind.Snowmobile, () => sim.CurrentPlow == PlowKind.Snowmobile);
            Add(plow.DropDownItems, "Belkurakli pingvin", () => sim.CurrentPlow = PlowKind.Penguin, () => sim.CurrentPlow == PlowKind.Penguin);

            menu.Items.Add(new ToolStripSeparator());
            pauseItem = Add(menu.Items, "Pauza", () => sim.Paused = !sim.Paused);
            Add(menu.Items, "Oynalarda qor yig'ilsin", () =>
            {
                if (sim.Accumulate) sim.ClearAll();
                sim.Accumulate = !sim.Accumulate;
            }, () => sim.Accumulate);
            Add(menu.Items, "Qorni tozalash", () => sim.ClearAll());
            menu.Items.Add(new ToolStripSeparator());
            Add(menu.Items, "Windows bilan birga ishga tushsin", () => SetAutostart(!AutostartOn()), AutostartOn);
            Add(menu.Items, "Chiqish", Quit);

            menu.Opening += (a, b) =>
            {
                foreach (KeyValuePair<ToolStripMenuItem, Func<bool>> kv in checks) kv.Key.Checked = kv.Value();
                pauseItem.Text = sim.Paused ? "Davom ettirish" : "Pauza";
            };

            tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "SnowFlakeUz",
                ContextMenuStrip = menu,
                Visible = true
            };
        }

        private void Quit()
        {
            tray.Visible = false;
            tray.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private static System.Drawing.Icon MakeIcon()
        {
            using (var bmp = new System.Drawing.Bitmap(32, 32))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 185, 255), 3f))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            double a = i * Math.PI / 3;
                            float dx = (float)(Math.Cos(a) * 13), dy = (float)(Math.Sin(a) * 13);
                            g.DrawLine(pen, 16 - dx, 16 - dy, 16 + dx, 16 + dy);
                        }
                    }
                }
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
        }

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static bool AutostartOn()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("SnowFlakeUz") != null;
        }

        private static void SetAutostart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("SnowFlakeUz", "\"" + Environment.ProcessPath + "\"");
                else k.DeleteValue("SnowFlakeUz", false);
            }
        }
    }
}
