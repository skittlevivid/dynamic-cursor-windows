using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DynamicCursors.Installer
{
    internal static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
        private const uint SPI_SETCURSORS = 0x0057;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isUninstall = false;
            string appName = Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLowerInvariant();
            if (appName.Contains("uninstall") || (args != null && args.Length > 0 && (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))))
            {
                isUninstall = true;
            }

            if (isUninstall)
            {
                RunUninstaller();
            }
            else
            {
                Application.Run(new InstallerForm());
            }
        }

        private static void RunUninstaller()
        {
            var res = MessageBox.Show(
                "are you sure you want to uninstall dynamic cursors?",
                "dynamic cursors uninstaller",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res != DialogResult.Yes) return;

            try
            {
                // 1. Stop running process
                foreach (var p in Process.GetProcessesByName("dynamic-cursors"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }

                // 2. Restore system cursors
                try
                {
                    SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                }
                catch { }

                // 3. Remove Desktop shortcut
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopLnk = Path.Combine(desktopPath, "dynamic cursors.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                // 4. Remove Start Menu shortcut
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startLnk = Path.Combine(startMenuPath, "dynamic cursors.lnk");
                if (File.Exists(startLnk)) File.Delete(startLnk);

                // 5. Remove Startup registry entry
                try
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null && runKey.GetValue("DynamicCursors") != null)
                        {
                            runKey.DeleteValue("DynamicCursors", false);
                        }
                    }
                }
                catch { }

                // 6. Remove Uninstall registry entry
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DynamicCursors", false);
                }
                catch { }

                // 7. Self-delete the installation directory via cmd
                string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"{0}\"", installDir),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                Process.Start(psi);

                MessageBox.Show(
                    "dynamic cursors has been successfully uninstalled from your computer.",
                    "uninstall complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "error during uninstall: " + ex.Message,
                    "uninstall error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    public class InstallerForm : Form
    {
        private TextBox _txtPath;
        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkStartup;
        private CheckBox _chkLaunch;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private Button _btnInstall;
        private Button _btnCancel;

        public InstallerForm()
        {
            Text = "dynamic cursors setup";
            Size = new Size(500, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 252);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            BuildUI();
        }

        private void BuildUI()
        {
            // Header panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(24, 16, 24, 16)
            };

            Label lblTitle = new Label
            {
                Text = "dynamic cursors installer",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 16)
            };
            Label lblSub = new Label
            {
                Text = "smooth physical mouse cursor with inertia, tilt, and shake zoom",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(203, 213, 225),
                AutoSize = true,
                Location = new Point(22, 44)
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            Controls.Add(pnlHeader);

            // Content container
            Panel pnlContent = new Panel
            {
                Location = new Point(20, 95),
                Size = new Size(445, 225)
            };

            // Path row
            Label lblPath = new Label { Text = "install location:", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            pnlContent.Controls.Add(lblPath);

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "dynamic-cursors");
            _txtPath = new TextBox { Text = defaultDir, Location = new Point(0, 24), Width = 345, ReadOnly = true, BackColor = Color.White };
            pnlContent.Controls.Add(_txtPath);

            Button btnBrowse = new Button { Text = "browse...", Location = new Point(355, 23), Width = 90, Height = 26 };
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "select install folder:";
                    fbd.SelectedPath = _txtPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtPath.Text = fbd.SelectedPath;
                    }
                }
            };
            pnlContent.Controls.Add(btnBrowse);

            // Options group
            GroupBox grpOptions = new GroupBox
            {
                Text = "shortcuts and startup options",
                Location = new Point(0, 60),
                Size = new Size(445, 115),
                Font = new Font("Segoe UI", 9f)
            };

            _chkDesktop = new CheckBox { Text = "create shortcut on desktop", Checked = true, AutoSize = true, Location = new Point(16, 24) };
            _chkStartMenu = new CheckBox { Text = "add shortcut to start menu", Checked = true, AutoSize = true, Location = new Point(16, 46) };
            _chkStartup = new CheckBox { Text = "run automatically on windows startup", Checked = true, AutoSize = true, Location = new Point(16, 68) };
            _chkLaunch = new CheckBox { Text = "launch dynamic cursors after installation", Checked = true, AutoSize = true, Location = new Point(16, 90) };

            grpOptions.Controls.Add(_chkDesktop);
            grpOptions.Controls.Add(_chkStartMenu);
            grpOptions.Controls.Add(_chkStartup);
            grpOptions.Controls.Add(_chkLaunch);
            pnlContent.Controls.Add(grpOptions);

            // Progress bar and status
            _progressBar = new ProgressBar
            {
                Location = new Point(0, 185),
                Size = new Size(445, 16),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };
            _lblStatus = new Label
            {
                Text = "ready to install.",
                Location = new Point(0, 204),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            pnlContent.Controls.Add(_progressBar);
            pnlContent.Controls.Add(_lblStatus);

            Controls.Add(pnlContent);

            // Bottom Buttons
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(20, 14, 20, 14)
            };

            _btnCancel = new Button { Text = "cancel", Width = 90, Height = 32, Location = new Point(275, 14) };
            _btnCancel.Click += (s, e) => Close();

            _btnInstall = new Button
            {
                Text = "install",
                Width = 100,
                Height = 32,
                Location = new Point(375, 14),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnInstall.Click += (s, e) => DoInstall();

            pnlBottom.Controls.Add(_btnCancel);
            pnlBottom.Controls.Add(_btnInstall);
            Controls.Add(pnlBottom);
        }

        private void DoInstall()
        {
            if (_btnInstall.Text == "finish")
            {
                Close();
                return;
            }

            _btnInstall.Enabled = false;
            _btnCancel.Enabled = false;
            _progressBar.Visible = true;
            _lblStatus.Text = "installing dynamic cursors...";

            string targetDir = _txtPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("invalid install directory path.", "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstall.Enabled = true;
                _btnCancel.Enabled = true;
                return;
            }

            try
            {
                // 1. Kill any existing instance
                foreach (var p in Process.GetProcessesByName("dynamic-cursors"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }

                // 2. Ensure target dir
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 3. Extract embedded payload.zip
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream zipStream = asm.GetManifestResourceStream("payload.zip"))
                {
                    if (zipStream == null)
                    {
                        throw new InvalidOperationException("embedded payload.zip resource not found.");
                    }

                    using (ZipArchive archive = new ZipArchive(zipStream))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destPath = Path.Combine(targetDir, entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                // Subdirectory
                                Directory.CreateDirectory(destPath);
                            }
                            else
                            {
                                string parent = Path.GetDirectoryName(destPath);
                                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                }

                string mainExe = Path.Combine(targetDir, "dynamic-cursors.exe");
                string uninstallerExe = Path.Combine(targetDir, "uninstall.exe");

                // 4. Copy self as uninstaller
                try
                {
                    File.Copy(Application.ExecutablePath, uninstallerExe, true);
                }
                catch { }

                // 5. Create shortcuts
                if (_chkDesktop.Checked)
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopPath, "dynamic cursors.lnk"), mainExe, targetDir);
                }

                if (_chkStartMenu.Checked)
                {
                    string startPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    CreateShortcut(Path.Combine(startPath, "dynamic cursors.lnk"), mainExe, targetDir);
                }

                // 6. Configure Windows Startup
                try
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null)
                        {
                            if (_chkStartup.Checked)
                            {
                                runKey.SetValue("DynamicCursors", "\"" + mainExe + "\"");
                            }
                            else
                            {
                                if (runKey.GetValue("DynamicCursors") != null) runKey.DeleteValue("DynamicCursors", false);
                            }
                        }
                    }
                }
                catch { }

                // 7. Register in Windows Add/Remove Programs
                try
                {
                    using (RegistryKey unKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DynamicCursors"))
                    {
                        if (unKey != null)
                        {
                            unKey.SetValue("DisplayName", "Dynamic Cursors for Windows");
                            unKey.SetValue("DisplayVersion", "1.0.0");
                            unKey.SetValue("Publisher", "skittlevivid");
                            unKey.SetValue("InstallLocation", targetDir);
                            unKey.SetValue("DisplayIcon", mainExe);
                            unKey.SetValue("UninstallString", "\"" + uninstallerExe + "\" --uninstall");
                            unKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            unKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }

                // 8. Launch if requested
                if (_chkLaunch.Checked && File.Exists(mainExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = mainExe,
                            WorkingDirectory = targetDir,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }

                _progressBar.Visible = false;
                _lblStatus.ForeColor = Color.Green;
                _lblStatus.Text = "installation completed successfully!";
                _btnInstall.Text = "finish";
                _btnInstall.Enabled = true;
                _btnCancel.Visible = false;
            }
            catch (Exception ex)
            {
                _progressBar.Visible = false;
                _lblStatus.ForeColor = Color.Red;
                _lblStatus.Text = "installation failed: " + ex.Message;
                _btnInstall.Enabled = true;
                _btnCancel.Enabled = true;
                MessageBox.Show("failed to install: " + ex.Message, "install error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CreateShortcut(string lnkPath, string targetExe, string workDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workDir;
                shortcut.Description = "Dynamic Cursors for Windows";
                shortcut.Save();
            }
            catch { }
        }
    }
}
