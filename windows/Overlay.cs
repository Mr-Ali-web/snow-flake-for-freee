using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnowFlakeUz
{
    /// <summary>Draws the simulation for one monitor, in physical pixel coordinates.</summary>
    internal sealed class SnowCanvas : FrameworkElement
    {
        private readonly System.Drawing.Rectangle bounds;

        public SnowCanvas(System.Drawing.Rectangle b)
        {
            bounds = b;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            dc.PushTransform(new ScaleTransform(1.0 / dpi.DpiScaleX, 1.0 / dpi.DpiScaleY));
            dc.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            Simulation.I.Draw(dc, new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            dc.Pop();
            dc.Pop();
        }
    }

    /// <summary>Transparent, always-on-top, click-through window covering one monitor.</summary>
    internal sealed class OverlayWindow : Window
    {
        private IntPtr hwnd = IntPtr.Zero;
        private readonly System.Windows.Forms.Screen screen;
        public SnowCanvas Canvas { get; private set; }

        public OverlayWindow(System.Windows.Forms.Screen s)
        {
            screen = s;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            Left = s.Bounds.Left;
            Top = s.Bounds.Top;
            Width = 100;
            Height = 100;
            Canvas = new SnowCanvas(s.Bounds);
            Content = Canvas;
            SourceInitialized += (a, b) =>
            {
                hwnd = new WindowInteropHelper(this).Handle;
                SetStyles(true);
                Reposition();
            };
        }

        private void SetStyles(bool clickThrough)
        {
            int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            if (clickThrough) ex |= Native.WS_EX_TRANSPARENT;
            else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
        }

        private void Reposition()
        {
            System.Drawing.Rectangle b = screen.Bounds;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, b.Left, b.Top, b.Width, b.Height,
                                Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        }

        /// <summary>The overlay is click-through, except while the cursor is over snow in snowball mode.</summary>
        public void SetCapture(bool capture)
        {
            if (hwnd == IntPtr.Zero) return;
            SetStyles(!capture);
        }
    }
}
