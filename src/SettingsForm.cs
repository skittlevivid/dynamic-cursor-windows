using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicCursors
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly OverlayWindow _overlay;

        private ComboBox _cbMode;
        private ComboBox _cbActivation;
        private NumericUpDown _numTiltAngle;
        private NumericUpDown _numTiltLimit;
        private NumericUpDown _numRotateStick;
        private NumericUpDown _numStretchMax;
        private CheckBox _chkShake;
        private NumericUpDown _numShakeThreshold;
        private NumericUpDown _numShakeLimit;
        private NumericUpDown _numShakeTimeout;
        private NumericUpDown _numCursorScale;
        private CheckBox _chkHideCursor;
        private NumericUpDown _numFPS;
        private ComboBox _cbCursorPack;

        public SettingsForm(AppConfig config, OverlayWindow overlay)
        {
            _config = config;
            _overlay = overlay;

            Text = "Dynamic Cursors Settings";
            Size = new Size(460, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            BuildUI();
            LoadValues();
        }

        private void BuildUI()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Header
            Label lblTitle = new Label
            {
                Text = "dynamic cursors settings",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            mainLayout.Controls.Add(lblTitle, 0, 0);

            // TabControl
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };

            // Tab 1: Physics
            TabPage tabPhysics = new TabPage("Simulation");
            TableLayoutPanel pnlPhysics = CreateGridPanel();

            _cbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            _cbMode.Items.AddRange(new object[] { "Tilt (Air Drag)", "Rotate (Stick)", "Stretch", "Shake Only" });
            AddRow(pnlPhysics, "simulation mode:", _cbMode);

            _numCursorScale = CreateDecimal(1.0m, 100.0m, 0.5m, 1.0m);
            AddRow(pnlPhysics, "cursor size (1 = default, up to 100):", _numCursorScale);

            _cbActivation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            _cbActivation.Items.AddRange(new object[] { "Quadratic (Recommended)", "Linear", "Negative Quadratic" });
            AddRow(pnlPhysics, "activation curve:", _cbActivation);

            _numTiltAngle = CreateNumber(10, 60, 1, 28);
            AddRow(pnlPhysics, "tilt max angle (deg):", _numTiltAngle);

            _numTiltLimit = CreateNumber(1000, 10000, 250, 3500);
            AddRow(pnlPhysics, "tilt speed limit (px/s):", _numTiltLimit);

            _numRotateStick = CreateNumber(10, 80, 2, 24);
            AddRow(pnlPhysics, "stick length (rotate):", _numRotateStick);

            _numStretchMax = CreateDecimal(1.2m, 3.0m, 0.1m, 1.9m);
            AddRow(pnlPhysics, "stretch max scale:", _numStretchMax);

            tabPhysics.Controls.Add(pnlPhysics);
            tabs.TabPages.Add(tabPhysics);

            // Tab 2: Shake to Find
            TabPage tabShake = new TabPage("Shake to Find");
            TableLayoutPanel pnlShake = CreateGridPanel();

            _chkShake = new CheckBox { Text = "enable shake to find", AutoSize = true };
            AddRow(pnlShake, "shake feature:", _chkShake);

            _numShakeThreshold = CreateDecimal(1.5m, 8.0m, 0.2m, 3.0m);
            AddRow(pnlShake, "shake sensitivity (threshold):", _numShakeThreshold);

            _numShakeLimit = CreateDecimal(1.5m, 100.0m, 0.5m, 4.0m);
            AddRow(pnlShake, "maximum zoom factor (up to 100):", _numShakeLimit);

            _numShakeTimeout = CreateDecimal(0.1m, 60.0m, 0.2m, 0.6m);
            AddRow(pnlShake, "seconds before returning to normal size:", _numShakeTimeout);

            tabShake.Controls.Add(pnlShake);
            tabs.TabPages.Add(tabShake);

            // Tab 3: System
            TabPage tabSys = new TabPage("System");
            TableLayoutPanel pnlSys = CreateGridPanel();

            _chkHideCursor = new CheckBox { Text = "hide windows system cursor", AutoSize = true };
            AddRow(pnlSys, "hardware cursor:", _chkHideCursor);

            _numFPS = CreateNumber(30, 240, 10, 120);
            AddRow(pnlSys, "target fps (refresh rate):", _numFPS);

            _cbCursorPack = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            AddRow(pnlSys, "cursor pack (from cursors/):", _cbCursorPack);

            Button btnOpenFolder = new Button { Text = "Open Cursors Folder", Width = 160, Height = 30 };
            btnOpenFolder.Click += (s, e) =>
            {
                string dir = System.IO.Path.Combine(AppConfig.GetAppDir(), "cursors");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            };
            AddRow(pnlSys, "custom .cur / .ani folder:", btnOpenFolder);

            Label lblKeys = new Label
            {
                Text = "hotkeys:\n• ctrl + alt + q : quit and restore cursor\n• ctrl + alt + d : toggle dynamic cursors on/off\n\nhot-reload: drop any .cur, .ani, or .png into cursors/ and it updates live!",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Margin = new Padding(8, 12, 8, 8)
            };
            pnlSys.Controls.Add(lblKeys);
            pnlSys.SetColumnSpan(lblKeys, 2);

            tabSys.Controls.Add(pnlSys);
            tabs.TabPages.Add(tabSys);

            mainLayout.Controls.Add(tabs, 0, 1);

            // Bottom Buttons
            FlowLayoutPanel btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0)
            };

            Button btnCancel = new Button { Text = "Cancel", Width = 90, Height = 32 };
            btnCancel.Click += (s, e) => Close();

            Button btnApply = new Button { Text = "Save & Apply", Width = 110, Height = 32, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnApply.Click += (s, e) =>
            {
                SaveValues();
                _config.Save();
                _overlay.ReloadAssets();
                _overlay.ResetPhysics();
                Close();
            };

            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnApply);
            mainLayout.Controls.Add(btnPanel, 0, 2);

            Controls.Add(mainLayout);
        }

        private TableLayoutPanel CreateGridPanel()
        {
            TableLayoutPanel pnl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
                AutoScroll = true
            };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            return pnl;
        }

        private void AddRow(TableLayoutPanel pnl, string label, Control ctrl)
        {
            pnl.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(4, 6, 4, 6) });
            pnl.Controls.Add(ctrl);
        }

        private NumericUpDown CreateNumber(int min, int max, int step, int val)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Value = val, Width = 110 };
        }

        private NumericUpDown CreateDecimal(decimal min, decimal max, decimal step, decimal val)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Increment = step, DecimalPlaces = 1, Value = val, Width = 110 };
        }

        private void LoadValues()
        {
            _cbMode.SelectedIndex = (int)_config.Mode;
            _cbActivation.SelectedIndex = (int)_config.Activation;
            _numTiltAngle.Value = (decimal)_config.TiltFullAngle;
            _numTiltLimit.Value = (decimal)_config.TiltLimit;
            _numRotateStick.Value = (decimal)_config.RotateStickLength;
            _numStretchMax.Value = (decimal)_config.MaxStretch;
            _chkShake.Checked = _config.ShakeEnabled;
            _numShakeThreshold.Value = (decimal)_config.ShakeThreshold;
            _numShakeLimit.Value = Math.Max(1.5m, Math.Min(100.0m, (decimal)_config.ShakeLimit));
            _numShakeTimeout.Value = Math.Max(0.1m, Math.Min(60.0m, (decimal)_config.ShakeTimeoutMs / 1000m));
            _numCursorScale.Value = Math.Max(1.0m, Math.Min(100.0m, (decimal)_config.CursorScale));
            _chkHideCursor.Checked = _config.HideSystemCursor;
            _numFPS.Value = _config.TargetFPS;

            _cbCursorPack.Items.Clear();
            var packs = CursorRenderer.GetAvailablePacks();
            foreach (var p in packs)
            {
                _cbCursorPack.Items.Add(p);
            }
            int selectedPackIdx = 0;
            if (!string.IsNullOrEmpty(_config.CursorPack))
            {
                for (int i = 0; i < _cbCursorPack.Items.Count; i++)
                {
                    if (string.Equals(_cbCursorPack.Items[i].ToString(), _config.CursorPack, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedPackIdx = i;
                        break;
                    }
                }
            }
            if (_cbCursorPack.Items.Count > 0)
            {
                _cbCursorPack.SelectedIndex = selectedPackIdx;
            }
        }

        private void SaveValues()
        {
            _config.Mode = (SimulationMode)_cbMode.SelectedIndex;
            _config.Activation = (ActivationType)_cbActivation.SelectedIndex;
            _config.TiltFullAngle = (float)_numTiltAngle.Value;
            _config.TiltLimit = (float)_numTiltLimit.Value;
            _config.RotateStickLength = (float)_numRotateStick.Value;
            _config.MaxStretch = (float)_numStretchMax.Value;
            _config.ShakeEnabled = _chkShake.Checked;
            _config.ShakeThreshold = (float)_numShakeThreshold.Value;
            _config.ShakeLimit = (float)_numShakeLimit.Value;
            _config.ShakeTimeoutMs = (int)(_numShakeTimeout.Value * 1000m);
            _config.CursorScale = (float)_numCursorScale.Value;
            _config.HideSystemCursor = _chkHideCursor.Checked;
            _config.TargetFPS = (int)_numFPS.Value;
            _config.CursorPack = (_cbCursorPack.SelectedIndex > 0 && _cbCursorPack.SelectedItem != null)
                ? _cbCursorPack.SelectedItem.ToString()
                : "";
        }
    }
}
