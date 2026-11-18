using System;
using System.Threading;
using System.Windows;

namespace SnowFlakeUz
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "SnowFlakeUz.SingleInstance", out created))
            {
                if (!created) return;
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                new TrayApp().Start();
                app.Run();
            }
        }
    }
}
