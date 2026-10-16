using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace DynamicCursors
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly OverlayWindow _overlay;

        // Theme colors
        private static readonly Color BgDark = Color.FromArgb(15, 23, 42);       // #0f172a
        private static readonly Color SidebarBg = Color.FromArgb(24, 33, 47);    // #18212f
        private static readonly Color CardBg = Color.FromArgb(30, 41, 59);       // #1e293b
        private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);   // #334155
        private static readonly Color AccentColor = Color.FromArgb(99, 102, 241); // #6366f1
        private static readonly Color AccentHover = Color.FromArgb(79, 70, 229);  // #4f46e5
        private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);// #f8fafc
        private static readonly Color TextSecondary = Color.FromArgb(148, 163, 184); // #94a3b8
        private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);   // #64748b
        private static readonly Color GreenBadge = Color.FromArgb(16, 185, 129);  // #10b981

        // Navigation
        private readonly List<Button> _navButtons = new List<Button>();
        private readonly List<Panel> _tabPanels = new List<Panel>();
        private int _currentTab = 0;

        // Simulation tab controls
        private ComboBox _cbMode;
        private ComboBox _cbActivation;
        private NumericUpDown _numTiltAngle;
        private NumericUpDown _numTiltLimit;
        private NumericUpDown _numRotateStick;
        private NumericUpDown _numStretchMax;
        private NumericUpDown _numCursorScale;

        // Shake to find tab controls
        private CheckBox _chkShake;
        private NumericUpDown _numShakeThreshold;
        private NumericUpDown _numShakeLimit;
        private NumericUpDown _numShakeTimeout;

        // Cursor theme tab controls
        private ComboBox _cbCursorPack;
        private Label _lblDetectedScheme;
        private Label _lblDetectedFile;
        private Label _lblPreviewInfo;
        private PreviewBox _previewBox;
        private CursorShapeType _previewShape = CursorShapeType.Arrow;

        // System tab controls
        private CheckBox _chkHideCursor;
        private NumericUpDown _numFPS;

        public SettingsForm(AppConfig config, OverlayWindow overlay)
        {
            _config = config;
            _overlay = overlay;

            Text = "dynamic cursors settings";
            ClientSize = new Size(760, 540);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDark;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            BuildUI();
            LoadValues();
        }

        private void BuildUI()
        {
            // Root container
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

            // Sidebar
            Panel sidebar = BuildSidebar();
            root.Controls.Add(sidebar, 0, 0);

            // Content Area
            Panel contentArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                Padding = new Padding(20, 16, 20, 10)
            };

            Panel pnlSim = BuildSimulationPanel();
            Panel pnlShake = BuildShakePanel();
            Panel pnlTheme = BuildThemePanel();
            Panel pnlSys = BuildSystemPanel();

            _tabPanels.Add(pnlSim);
            _tabPanels.Add(pnlShake);
            _tabPanels.Add(pnlTheme);
            _tabPanels.Add(pnlSys);

            foreach (var p in _tabPanels)
            {
                p.Dock = DockStyle.Fill;
                contentArea.Controls.Add(p);
            }

            root.Controls.Add(contentArea, 1, 0);

            // Bottom bar
            Panel bottomBar = BuildBottomBar();
            root.Controls.Add(bottomBar, 0, 1);
            root.SetColumnSpan(bottomBar, 2);

            Controls.Add(root);

            SelectTab(2); // default to theme tab to showcase auto-adaptation immediately!
        }

        private Panel BuildSidebar()
        {
            Panel pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Padding = new Padding(12, 16, 12, 12)
            };

            // App branding
            Label lblLogo = new Label
            {
                Text = "dynamic cursors",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(12, 14)
            };
            pnl.Controls.Add(lblLogo);

            Label lblStatus = new Label
            {
                Text = "● active & running",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = GreenBadge,
                AutoSize = true,
                Location = new Point(13, 38)
            };
            pnl.Controls.Add(lblStatus);

            Panel divider = new Panel
            {
                Height = 1,
                BackColor = BorderColor,
                Location = new Point(12, 60),
                Width = 186
            };
            pnl.Controls.Add(divider);

            // Nav buttons
            string[] tabs = new string[] { "🚀  simulation", "⚡  shake to find", "🎨  cursor theme", "⚙️  system & hotkeys" };
            int y = 72;
            for (int i = 0; i < tabs.Length; i++)
            {
                int tabIdx = i;
                Button btn = new Button
                {
                    Text = tabs[i],
                    Location = new Point(6, y),
                    Size = new Size(198, 42),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
                    ForeColor = TextSecondary,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Padding = new Padding(12, 0, 0, 0)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = CardBg;
                btn.Click += (s, e) => SelectTab(tabIdx);

                _navButtons.Add(btn);
                pnl.Controls.Add(btn);
                y += 46;
            }

            // Quick Hotkey Footer
            Label lblFooter = new Label
            {
                Text = "ctrl+alt+d : toggle\nctrl+alt+q : quit",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8f),
                AutoSize = true,
                Location = new Point(14, 430)
            };
            pnl.Controls.Add(lblFooter);

            return pnl;
        }

        private void SelectTab(int index)
        {
            _currentTab = index;
            for (int i = 0; i < _tabPanels.Count; i++)
            {
                bool active = (i == index);
                _tabPanels[i].Visible = active;
                if (active) _tabPanels[i].BringToFront();

                _navButtons[i].BackColor = active ? AccentColor : Color.Transparent;
                _navButtons[i].ForeColor = active ? Color.White : TextSecondary;
                _navButtons[i].Font = new Font("Segoe UI", 9.25f, active ? FontStyle.Bold : FontStyle.Regular);
            }

            if (index == 2 && _previewBox != null)
            {
                _previewBox.Invalidate();
            }
        }

        private Panel BuildSimulationPanel()
        {
            Panel pnl = CreateContentPanel("physics simulation settings", "customize how the cursor bends, tilts and stretches as you move.");
            TableLayoutPanel grid = CreateGrid(7);

            _cbMode = CreateStyledComboBox(new string[] { "tilt (air drag)", "rotate (stick)", "stretch", "shake only" });
            AddGridRow(grid, "simulation mode:", _cbMode, "tilt simulates atmospheric drag, rotate simulates pendulum swinging on a stick, stretch deforms cursor along velocity.");

            _numCursorScale = CreateNumberBox(1.0m, 100.0m, 0.5m, 1.0m, 1);
            AddGridRow(grid, "cursor scale (1.0 = native, up to 100):", _numCursorScale, "base size multiplier applied to the cursor on screen.");

            _cbActivation = CreateStyledComboBox(new string[] { "quadratic (recommended)", "linear", "negative quadratic" });
            AddGridRow(grid, "activation curve:", _cbActivation, "response curve mapping mouse velocity to deformation angle.");

            _numTiltAngle = CreateNumberBox(10, 60, 1, 28, 0);
            AddGridRow(grid, "tilt max angle (degrees):", _numTiltAngle, "maximum tilt rotation applied at top mouse movement speed.");

            _numTiltLimit = CreateNumberBox(1000, 10000, 250, 3500, 0);
            AddGridRow(grid, "tilt speed limit (px/s):", _numTiltLimit, "velocity in pixels per second required to reach max tilt angle.");

            _numRotateStick = CreateNumberBox(10, 80, 2, 24, 0);
            AddGridRow(grid, "stick length (rotate mode):", _numRotateStick, "distance between cursor tip and virtual rotation anchor point.");

            _numStretchMax = CreateNumberBox(1.2m, 3.0m, 0.1m, 1.9m, 1);
            AddGridRow(grid, "max stretch scale:", _numStretchMax, "maximum elongation multiplier in stretch simulation mode.");

            pnl.Controls.Add(grid);
            return pnl;
        }

        private Panel BuildShakePanel()
        {
            Panel pnl = CreateContentPanel("shake to find (macos-style zoom)", "rapidly wiggle or shake your mouse to magnify the cursor so you never lose it.");
            TableLayoutPanel grid = CreateGrid(4);

            _chkShake = new CheckBox
            {
                Text = "enable shake to find",
                AutoSize = true,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            AddGridRow(grid, "shake detection:", _chkShake, "when enabled, rapid back-and-forth mouse movement smoothly zooms the cursor.");

            _numShakeThreshold = CreateNumberBox(1.5m, 8.0m, 0.2m, 3.0m, 1);
            AddGridRow(grid, "shake sensitivity (threshold):", _numShakeThreshold, "lower value triggers zoom more easily; higher value requires vigorous shaking.");

            _numShakeLimit = CreateNumberBox(1.5m, 100.0m, 0.5m, 4.0m, 1);
            AddGridRow(grid, "maximum zoom factor (up to 100x):", _numShakeLimit, "maximum magnification scale reached during shaking.");

            _numShakeTimeout = CreateNumberBox(0.1m, 60.0m, 0.2m, 0.6m, 1);
            AddGridRow(grid, "return timeout (seconds):", _numShakeTimeout, "duration in seconds before enlarged cursor shrinks back to its original size.");

            pnl.Controls.Add(grid);
            return pnl;
        }

        private Panel BuildThemePanel()
        {
            Panel pnl = CreateContentPanel("cursor theme & auto-adaptation", "instantly adapts to your installed windows cursor scheme or custom packs.");

            // Detection Card
            Panel cardDetect = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(14, 10, 14, 10)
            };

            Label lblCardTitle = new Label
            {
                Text = "active windows scheme (auto-adapted)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AccentColor,
                AutoSize = true,
                Location = new Point(12, 10)
            };
            cardDetect.Controls.Add(lblCardTitle);

            _lblDetectedScheme = new Label
            {
                Text = "scheme: detecting...",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(13, 34)
            };
            cardDetect.Controls.Add(_lblDetectedScheme);

            _lblDetectedFile = new Label
            {
                Text = "file: ...",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = TextSecondary,
                AutoSize = true,
                Location = new Point(13, 56)
            };
            cardDetect.Controls.Add(_lblDetectedFile);

            pnl.Controls.Add(cardDetect);

            // Preview & controls container
            TableLayoutPanel mainGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0)
            };
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Left side: interactive preview box
            Panel previewContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 14, 0)
            };

            _previewBox = new PreviewBox(_overlay)
            {
                Dock = DockStyle.Top,
                Height = 160,
                BackColor = Color.FromArgb(11, 17, 32)
            };
            previewContainer.Controls.Add(_previewBox);

            _lblPreviewInfo = new Label
            {
                Text = "move mouse here to test physics live!",
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.25f),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 22
            };
            previewContainer.Controls.Add(_lblPreviewInfo);

            // Shape picker buttons
            FlowLayoutPanel shapeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            string[] shapeLabels = new string[] { "arrow", "hand", "beam", "wait", "cross" };
            CursorShapeType[] shapes = new CursorShapeType[] {
                CursorShapeType.Arrow, CursorShapeType.Hand, CursorShapeType.IBeam, CursorShapeType.Wait, CursorShapeType.Cross
            };

            for (int i = 0; i < shapeLabels.Length; i++)
            {
                var s = shapes[i];
                Button btnShape = new Button
                {
                    Text = shapeLabels[i],
                    Width = 48,
                    Height = 26,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = CardBg,
                    ForeColor = TextSecondary,
                    Font = new Font("Segoe UI", 7.5f),
                    Margin = new Padding(2, 4, 2, 0),
                    Cursor = Cursors.Hand
                };
                btnShape.FlatAppearance.BorderColor = BorderColor;
                btnShape.Click += (sender, e) =>
                {
                    _previewShape = s;
                    _previewBox.SetShape(s);
                    UpdatePreviewInfo();
                };
                shapeFlow.Controls.Add(btnShape);
            }
            previewContainer.Controls.Add(shapeFlow);
            mainGrid.Controls.Add(previewContainer, 0, 0);

            // Right side: Pack selection and import actions
            Panel actionsPnl = new Panel { Dock = DockStyle.Fill };

            Label lblPackTitle = new Label
            {
                Text = "cursor pack source:",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(4, 4)
            };
            actionsPnl.Controls.Add(lblPackTitle);

            _cbCursorPack = CreateStyledComboBox(new string[0]);
            _cbCursorPack.Location = new Point(4, 28);
            _cbCursorPack.Width = 230;
            _cbCursorPack.SelectedIndexChanged += (s, e) =>
            {
                string sel = _cbCursorPack.SelectedItem != null ? _cbCursorPack.SelectedItem.ToString() : "";
                _overlay.Renderer.LoadCustomAssets(sel);
                _previewBox.Invalidate();
                UpdatePreviewInfo();
            };
            actionsPnl.Controls.Add(_cbCursorPack);

            Button btnImport = CreateStyledButton("📥  import cursor pack (.cur / .ani / folder)", CardBg, TextPrimary);
            btnImport.Location = new Point(4, 68);
            btnImport.Width = 230;
            btnImport.Height = 32;
            btnImport.Click += (s, e) => ImportCursorPack();
            actionsPnl.Controls.Add(btnImport);

            Button btnOpenFolder = CreateStyledButton("📁  open cursors/ folder", CardBg, TextSecondary);
            btnOpenFolder.Location = new Point(4, 108);
            btnOpenFolder.Width = 230;
            btnOpenFolder.Height = 32;
            btnOpenFolder.Click += (s, e) =>
            {
                string dir = Path.Combine(AppConfig.GetAppDir(), "cursors");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            };
            actionsPnl.Controls.Add(btnOpenFolder);

            Button btnSyncWin = CreateStyledButton("🔄  sync with active windows scheme", CardBg, TextSecondary);
            btnSyncWin.Location = new Point(4, 148);
            btnSyncWin.Width = 230;
            btnSyncWin.Height = 32;
            btnSyncWin.Click += (s, e) =>
            {
                _cbCursorPack.SelectedIndex = 0; // Windows Active Scheme
                _overlay.Renderer.LoadCustomAssets(CursorRenderer.PACK_WINDOWS_AUTO);
                RefreshSchemeInfo();
                _previewBox.Invalidate();
                UpdatePreviewInfo();
            };
            actionsPnl.Controls.Add(btnSyncWin);

            mainGrid.Controls.Add(actionsPnl, 1, 0);
            pnl.Controls.Add(mainGrid);

            return pnl;
        }

        private Panel BuildSystemPanel()
        {
            Panel pnl = CreateContentPanel("system performance & hotkeys", "configure frame rate and review automatic game/snipping suppression.");
            TableLayoutPanel grid = CreateGrid(4);

            _chkHideCursor = new CheckBox
            {
                Text = "hide windows native hardware cursor",
                AutoSize = true,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            AddGridRow(grid, "hardware cursor:", _chkHideCursor, "hides the static windows cursor so only the dynamic simulated cursor is shown.");

            _numFPS = CreateNumberBox(30, 240, 10, 120, 0);
            AddGridRow(grid, "target fps (refresh rate):", _numFPS, "render loop target framerate (e.g. 120, 144, 240 Hz). zero lag hardware DIB blitting.");

            // Hotkeys card
            Panel cardKeys = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBg,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(14)
            };
            Label lblKeys = new Label
            {
                Text = "shortcuts & automatic integrations:\n\n" +
                       "• ctrl + alt + q : quit dynamic cursors & restore windows cursor\n" +
                       "• ctrl + alt + d : toggle dynamic cursor simulation on / off\n" +
                       "• win + shift + s / prtscn : instant auto-suppress during screenshots\n" +
                       "• full-screen games (e.g. roblox, fps) : automatically hidden when in-game cursor is active\n" +
                       "• hot-reload : changes to cursors/ folder update instantly live!",
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Fill
            };
            cardKeys.Controls.Add(lblKeys);
            grid.Controls.Add(cardKeys);
            grid.SetColumnSpan(cardKeys, 2);

            pnl.Controls.Add(grid);
            return pnl;
        }

        private Panel BuildBottomBar()
        {
            Panel bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Padding = new Padding(16, 10, 16, 10)
            };

            Button btnDefaults = CreateStyledButton("reset to defaults", Color.Transparent, TextSecondary);
            btnDefaults.Location = new Point(16, 12);
            btnDefaults.Size = new Size(130, 32);
            btnDefaults.Click += (s, e) => ResetDefaults();
            bar.Controls.Add(btnDefaults);

            Button btnCancel = CreateStyledButton("cancel", CardBg, TextPrimary);
            btnCancel.Location = new Point(540, 12);
            btnCancel.Size = new Size(88, 32);
            btnCancel.Click += (s, e) => Close();
            bar.Controls.Add(btnCancel);

            Button btnApply = CreateStyledButton("save & apply", AccentColor, Color.White);
            btnApply.Location = new Point(638, 12);
            btnApply.Size = new Size(106, 32);
            btnApply.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            btnApply.Click += (s, e) =>
            {
                SaveValues();
                _config.Save();
                _overlay.ReloadAssets();
                _overlay.ResetPhysics();
                Close();
            };
            bar.Controls.Add(btnApply);

            return bar;
        }

        private Panel CreateContentPanel(string title, string subtitle)
        {
            Panel pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                AutoScroll = true
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            pnl.Controls.Add(lblTitle);

            Label lblSub = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(2, 24)
            };
            pnl.Controls.Add(lblSub);

            Panel div = new Panel
            {
                Location = new Point(0, 48),
                Height = 1,
                Width = 500,
                BackColor = BorderColor
            };
            pnl.Controls.Add(div);

            return pnl;
        }

        private TableLayoutPanel CreateGrid(int rows)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Location = new Point(0, 56),
                Width = 500,
                ColumnCount = 2,
                AutoSize = true,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            return grid;
        }

        private void AddGridRow(TableLayoutPanel grid, string label, Control ctrl, string hint)
        {
            Panel pnlLabel = new Panel { AutoSize = true, Margin = new Padding(0, 6, 8, 6) };
            Label l = new Label { Text = label, AutoSize = true, ForeColor = TextPrimary, Font = new Font("Segoe UI", 9.25f, FontStyle.Regular) };
            pnlLabel.Controls.Add(l);

            if (!string.IsNullOrEmpty(hint))
            {
                Label h = new Label
                {
                    Text = hint,
                    AutoSize = true,
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 7.75f),
                    Location = new Point(0, 18),
                    MaximumSize = new Size(290, 0)
                };
                pnlLabel.Controls.Add(h);
            }

            grid.Controls.Add(pnlLabel);
            grid.Controls.Add(ctrl);
        }

        private ComboBox CreateStyledComboBox(string[] items)
        {
            ComboBox cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CardBg,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Width = 190,
                Font = new Font("Segoe UI", 9.25f)
            };
            if (items != null) cb.Items.AddRange(items);
            return cb;
        }

        private NumericUpDown CreateNumberBox(decimal min, decimal max, decimal step, decimal val, int decimals)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                Value = val,
                DecimalPlaces = decimals,
                BackColor = CardBg,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 100,
                Font = new Font("Segoe UI", 9.25f)
            };
        }

        private Button CreateStyledButton(string text, Color bg, Color fg)
        {
            Button btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.75f)
            };
            btn.FlatAppearance.BorderColor = BorderColor;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            return btn;
        }

        private void RefreshSchemeInfo()
        {
            string schemeName, arrowPath;
            CursorRenderer.GetActiveSchemeInfo(out schemeName, out arrowPath);
            _lblDetectedScheme.Text = "scheme: " + schemeName;
            _lblDetectedFile.Text = string.IsNullOrEmpty(arrowPath)
                ? "file: default system aero cursor"
                : "file: " + Path.GetFileName(arrowPath) + " (" + arrowPath + ")";
        }

        private void UpdatePreviewInfo()
        {
            int w, h, hx, hy;
            _overlay.Renderer.GetAssetInfo(_previewShape, out w, out h, out hx, out hy);
            _lblPreviewInfo.Text = string.Format("{0} • {1}x{2}px • hotspot ({3}, {4})", _previewShape.ToString().ToLowerInvariant(), w, h, hx, hy);
        }

        private void ImportCursorPack()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "select cursor file (.cur, .ani, .png)";
                ofd.Filter = "Cursor Files (*.cur;*.ani;*.png)|*.cur;*.ani;*.png|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string sourceFile = ofd.FileName;
                        string packName = Path.GetFileNameWithoutExtension(sourceFile);
                        string destDir = Path.Combine(AppConfig.GetAppDir(), "cursors", packName);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                        string ext = Path.GetExtension(sourceFile).ToLowerInvariant();
                        File.Copy(sourceFile, Path.Combine(destDir, "arrow" + ext), true);

                        ReloadPacksDropdown(packName);
                        MessageBox.Show("cursor pack imported successfully into cursors/" + packName, "imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("failed to import cursor: " + ex.Message, "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ReloadPacksDropdown(string selectPack = null)
        {
            _cbCursorPack.Items.Clear();
            var packs = CursorRenderer.GetAvailablePacks();
            foreach (var p in packs)
            {
                _cbCursorPack.Items.Add(p);
            }

            int idx = 0;
            string target = selectPack ?? _config.CursorPack;
            if (!string.IsNullOrEmpty(target))
            {
                for (int i = 0; i < _cbCursorPack.Items.Count; i++)
                {
                    if (string.Equals(_cbCursorPack.Items[i].ToString(), target, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            if (_cbCursorPack.Items.Count > 0)
            {
                _cbCursorPack.SelectedIndex = idx;
            }
        }

        private void LoadValues()
        {
            _cbMode.SelectedIndex = (int)_config.Mode;
            _cbActivation.SelectedIndex = (int)_config.Activation;
            _numTiltAngle.Value = (decimal)_config.TiltFullAngle;
            _numTiltLimit.Value = (decimal)_config.TiltLimit;
            _numRotateStick.Value = (decimal)_config.RotateStickLength;
            _numStretchMax.Value = (decimal)_config.MaxStretch;
            _numCursorScale.Value = Math.Max(1.0m, Math.Min(100.0m, (decimal)_config.CursorScale));

            _chkShake.Checked = _config.ShakeEnabled;
            _numShakeThreshold.Value = (decimal)_config.ShakeThreshold;
            _numShakeLimit.Value = Math.Max(1.5m, Math.Min(100.0m, (decimal)_config.ShakeLimit));
            _numShakeTimeout.Value = Math.Max(0.1m, Math.Min(60.0m, (decimal)_config.ShakeTimeoutMs / 1000m));

            _chkHideCursor.Checked = _config.HideSystemCursor;
            _numFPS.Value = Math.Max(30, Math.Min(240, _config.TargetFPS));

            RefreshSchemeInfo();
            ReloadPacksDropdown();
            UpdatePreviewInfo();
        }

        private void SaveValues()
        {
            _config.Mode = (SimulationMode)_cbMode.SelectedIndex;
            _config.Activation = (ActivationType)_cbActivation.SelectedIndex;
            _config.TiltFullAngle = (float)_numTiltAngle.Value;
            _config.TiltLimit = (float)_numTiltLimit.Value;
            _config.RotateStickLength = (float)_numRotateStick.Value;
            _config.MaxStretch = (float)_numStretchMax.Value;
            _config.CursorScale = (float)_numCursorScale.Value;

            _config.ShakeEnabled = _chkShake.Checked;
            _config.ShakeThreshold = (float)_numShakeThreshold.Value;
            _config.ShakeLimit = (float)_numShakeLimit.Value;
            _config.ShakeTimeoutMs = (int)(_numShakeTimeout.Value * 1000m);

            _config.HideSystemCursor = _chkHideCursor.Checked;
            _config.TargetFPS = (int)_numFPS.Value;

            _config.CursorPack = (_cbCursorPack.SelectedIndex > 0 && _cbCursorPack.SelectedItem != null)
                ? _cbCursorPack.SelectedItem.ToString()
                : "";
        }

        private void ResetDefaults()
        {
            AppConfig def = new AppConfig();
            _cbMode.SelectedIndex = (int)def.Mode;
            _cbActivation.SelectedIndex = (int)def.Activation;
            _numTiltAngle.Value = (decimal)def.TiltFullAngle;
            _numTiltLimit.Value = (decimal)def.TiltLimit;
            _numRotateStick.Value = (decimal)def.RotateStickLength;
            _numStretchMax.Value = (decimal)def.MaxStretch;
            _numCursorScale.Value = (decimal)def.CursorScale;

            _chkShake.Checked = def.ShakeEnabled;
            _numShakeThreshold.Value = (decimal)def.ShakeThreshold;
            _numShakeLimit.Value = (decimal)def.ShakeLimit;
            _numShakeTimeout.Value = (decimal)(def.ShakeTimeoutMs / 1000m);

            _chkHideCursor.Checked = def.HideSystemCursor;
            _numFPS.Value = def.TargetFPS;
            _cbCursorPack.SelectedIndex = 0; // Windows Active Scheme
        }

        // Live interactive preview control
        private class PreviewBox : Control
        {
            private readonly OverlayWindow _overlay;
            private CursorShapeType _shape = CursorShapeType.Arrow;
            private Point _prevPos = new Point(130, 80);
            private float _tilt = 0f;

            public PreviewBox(OverlayWindow overlay)
            {
                _overlay = overlay;
                DoubleBuffered = true;
                Cursor = Cursors.Cross;
            }

            public void SetShape(CursorShapeType s)
            {
                _shape = s;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int dx = e.X - _prevPos.X;
                _prevPos = e.Location;

                // Simple responsive tilt for live preview feedback
                _tilt = Math.Max(-35f, Math.Min(35f, _tilt * 0.7f - dx * 2.5f));
                Invalidate();
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _tilt = 0f;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Subtle grid background
                using (SolidBrush bg = new SolidBrush(BackColor))
                {
                    g.FillRectangle(bg, ClientRectangle);
                }

                using (Pen dotPen = new Pen(Color.FromArgb(35, 48, 68), 1f))
                {
                    for (int x = 16; x < Width; x += 16)
                    {
                        for (int y = 16; y < Height; y += 16)
                        {
                            g.DrawLine(dotPen, x, y, x + 1, y);
                        }
                    }
                }

                // Render current adapted cursor at center
                Bitmap frame = _overlay != null && _overlay.Renderer != null
                    ? _overlay.Renderer.GetPreviewFrame(_shape)
                    : null;

                int cx = Width / 2;
                int cy = Height / 2;

                if (frame != null)
                {
                    g.TranslateTransform(cx, cy);
                    if (Math.Abs(_tilt) > 0.1f)
                    {
                        g.RotateTransform(_tilt);
                    }

                    int w, h, hx, hy;
                    _overlay.Renderer.GetAssetInfo(_shape, out w, out h, out hx, out hy);

                    g.DrawImage(frame, -hx, -hy, w, h);
                    g.ResetTransform();
                }
                else
                {
                    using (Pen p = new Pen(AccentColor, 2f))
                    {
                        g.DrawLine(p, cx, cy, cx + 16, cy + 16);
                        g.DrawLine(p, cx, cy, cx, cy + 20);
                        g.DrawLine(p, cx, cy, cx + 20, cy);
                    }
                }
            }
        }
    }
}
