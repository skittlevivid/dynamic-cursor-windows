using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DynamicCursors
{
    public class TrayContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly OverlayWindow _overlay;
        private readonly NotifyIcon _trayIcon;

        private ToolStripMenuItem _itemStatus;
        private ToolStripMenuItem _itemEnabled;
        private ToolStripMenuItem _itemModeTilt;
        private ToolStripMenuItem _itemModeRotate;
        private ToolStripMenuItem _itemModeStretch;
        private ToolStripMenuItem _itemModeShakeOnly;
        private ToolStripMenuItem _itemShake;
        private SettingsForm _settingsForm = null;

        public TrayContext(AppConfig config, OverlayWindow overlay)
        {
            _config = config;
            _overlay = overlay;

            _overlay.RequestExit += ExitApp;
            _overlay.ToggleEnabled += (en) => UpdateMenuState();
            _overlay.RequestShowSettings += () => OpenSettings();

            _trayIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "dynamic cursors (hyprland port for windows)",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            _trayIcon.DoubleClick += (s, e) => OpenSettings();
            UpdateMenuState();

            _overlay.Show();

            try
            {
                _trayIcon.ShowBalloonTip(2500, "dynamic cursors", "active in system tray (double-click to open settings)", ToolTipIcon.Info);
            }
            catch { }
        }

        private Icon CreateTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Draw a sleek angled cursor arrow
                PointF[] pts = new PointF[]
                {
                    new PointF(4, 4),
                    new PointF(4, 26),
                    new PointF(11, 20),
                    new PointF(17, 30),
                    new PointF(22, 27),
                    new PointF(16, 17),
                    new PointF(24, 17)
                };

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(99, 102, 241)))
                using (Pen pen = new Pen(Color.White, 2.0f) { LineJoin = LineJoin.Round })
                {
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Closed += (s, e) =>
            {
                if (_config.HideSystemCursor)
                {
                    CursorHook.ForceHideSystemCursors();
                }
            };

            _itemStatus = new ToolStripMenuItem("dynamic cursors: active") { Enabled = false };
            menu.Items.Add(_itemStatus);
            menu.Items.Add(new ToolStripSeparator());

            _itemEnabled = new ToolStripMenuItem("enabled", null, (s, e) =>
            {
                _config.Enabled = !_config.Enabled;
                _config.Save();
                UpdateMenuState();
            });
            menu.Items.Add(_itemEnabled);

            // Simulation Modes Submenu
            ToolStripMenuItem menuModes = new ToolStripMenuItem("simulation mode");

            _itemModeTilt = new ToolStripMenuItem("tilt (air drag)", null, (s, e) => SetMode(SimulationMode.Tilt));
            _itemModeRotate = new ToolStripMenuItem("rotate (stick)", null, (s, e) => SetMode(SimulationMode.Rotate));
            _itemModeStretch = new ToolStripMenuItem("stretch", null, (s, e) => SetMode(SimulationMode.Stretch));
            _itemModeShakeOnly = new ToolStripMenuItem("shake only", null, (s, e) => SetMode(SimulationMode.ShakeOnly));

            menuModes.DropDownItems.AddRange(new ToolStripItem[] {
                _itemModeTilt, _itemModeRotate, _itemModeStretch, _itemModeShakeOnly
            });
            menu.Items.Add(menuModes);

            _itemShake = new ToolStripMenuItem("shake to find", null, (s, e) =>
            {
                _config.ShakeEnabled = !_config.ShakeEnabled;
                _config.Save();
                UpdateMenuState();
            });
            menu.Items.Add(_itemShake);

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("settings...", null, (s, e) => OpenSettings()));
            menu.Items.Add(new ToolStripMenuItem("open cursors folder...", null, (s, e) =>
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cursors");
                if (System.IO.Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
            }));
            menu.Items.Add(new ToolStripMenuItem("reload config / cursors", null, (s, e) =>
            {
                _config.Load();
                _overlay.ResetPhysics();
                _overlay.ReloadAssets();
                UpdateMenuState();
            }));
            menu.Items.Add(new ToolStripMenuItem("restore system cursor", null, (s, e) =>
            {
                CursorHook.RestoreSystemCursors();
            }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("exit (ctrl+alt+q)", null, (s, e) => ExitApp()));

            return menu;
        }

        private void SetMode(SimulationMode mode)
        {
            _config.Mode = mode;
            _config.Save();
            _overlay.ResetPhysics();
            UpdateMenuState();
        }

        private void UpdateMenuState()
        {
            _itemStatus.Text = _config.Enabled ? "dynamic cursors: active" : "dynamic cursors: paused";
            _itemEnabled.Checked = _config.Enabled;

            _itemModeTilt.Checked = _config.Mode == SimulationMode.Tilt;
            _itemModeRotate.Checked = _config.Mode == SimulationMode.Rotate;
            _itemModeStretch.Checked = _config.Mode == SimulationMode.Stretch;
            _itemModeShakeOnly.Checked = _config.Mode == SimulationMode.ShakeOnly;

            _itemShake.Checked = _config.ShakeEnabled;

            _trayIcon.Text = string.Format("dynamic cursors ({0})", _config.Mode.ToString().ToLowerInvariant());
        }

        private void OpenSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_config, _overlay);
                _settingsForm.FormClosed += (s, e) =>
                {
                    UpdateMenuState();
                    if (_config.HideSystemCursor)
                    {
                        CursorHook.ForceHideSystemCursors();
                    }
                };
                _settingsForm.Show();
            }
            else
            {
                _settingsForm.BringToFront();
                _settingsForm.Activate();
            }
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            _overlay.Stop();
            _overlay.Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_settingsForm != null) _settingsForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
