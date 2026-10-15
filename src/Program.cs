using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DynamicCursors
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private const string AppGuid = "dynamic-cursors-windows-7f3c2b81-64d9-4b62-9e32-fc2048967e81";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        e.ExceptionObject != null ? e.ExceptionObject.ToString() : "unknown unhandled exception");
                }
                catch { }
            };

            AttachConsole(-1);

            // Command line arguments check
            if (args != null && args.Length > 0)
            {
                foreach (string arg in args)
                {
                    string a = arg.ToLowerInvariant().Trim();
                    if (a == "--restore" || a == "-r" || a == "/restore")
                    {
                        CursorHook.RestoreSystemCursors();
                        Console.WriteLine("system cursors restored successfully.");
                        return;
                    }

                    if (a == "--help" || a == "-h" || a == "/?")
                    {
                        string help =
                            "dynamic cursors for windows (hypr-dynamic-cursors port)\n\n" +
                            "arguments:\n" +
                            "  --restore, -r        immediately restore default windows cursors\n" +
                            "  --mode=tilt          start in tilt (air drag) mode\n" +
                            "  --mode=rotate        start in rotate (stick) mode\n" +
                            "  --mode=stretch       start in stretch mode\n" +
                            "  --mode=shake_only    start in shake to find only mode\n" +
                            "  --no-hide            do not hide native system cursor\n" +
                            "  --help, -h           show this help message\n\n" +
                            "hotkeys:\n" +
                            "  ctrl + alt + q       quit and restore cursors\n" +
                            "  ctrl + alt + d       toggle dynamic cursors on/off";

                        Console.WriteLine(help);
                        return;
                    }
                }
            }

            // Single instance mutex
            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppGuid, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("dynamic cursors is already running! check your system tray (notification area).", "dynamic cursors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppConfig config = new AppConfig();
                config.Load();

                // Apply any command line overrides
                if (args != null)
                {
                    foreach (string arg in args)
                    {
                        string a = arg.ToLowerInvariant().Trim();
                        if (a == "--mode=tilt") config.Mode = SimulationMode.Tilt;
                        else if (a == "--mode=rotate") config.Mode = SimulationMode.Rotate;
                        else if (a == "--mode=stretch") config.Mode = SimulationMode.Stretch;
                        else if (a == "--mode=shake_only") config.Mode = SimulationMode.ShakeOnly;
                        else if (a == "--no-hide") config.HideSystemCursor = false;
                    }
                }

                try
                {
                    using (OverlayWindow overlay = new OverlayWindow(config))
                    using (TrayContext tray = new TrayContext(config, overlay))
                    {
                        Application.Run(tray);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
                    }
                    catch { }
                }
            }
        }
    }
}
