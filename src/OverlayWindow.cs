using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DynamicCursors
{
    public class OverlayWindow : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint ULW_ALPHA = 0x00000002;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int WM_HOTKEY = 0x0312;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const uint SPI_SETCURSORS = 0x0057;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private const int VK_SNAPSHOT = 0x2C;
        private const int VK_SHIFT = 0x10;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_S = 0x53;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        private readonly AppConfig _config;
        private readonly CursorRenderer _renderer;
        private readonly TiltSimulator _tiltSim;
        private readonly RotateSimulator _rotateSim;
        private readonly StretchSimulator _stretchSim;
        private readonly ShakeDetector _shakeDetector;
        private readonly Stopwatch _clock;

        private Thread _renderThread;
        private volatile bool _running = false;
        private bool _hotkeysRegistered = false;

        // Persistent DIB and DC for zero-allocation hardware-blitted rendering
        private IntPtr _hdcScreen = IntPtr.Zero;
        private IntPtr _hdcMem = IntPtr.Zero;
        private IntPtr _hDIB = IntPtr.Zero;
        private IntPtr _pDIBBits = IntPtr.Zero;
        private IntPtr _hOldBmp = IntPtr.Zero;
        private Bitmap _dibBitmap = null;
        private Graphics _dibGraphics = null;
        private int _dibSize = 0;

        // Idle state tracking to save CPU/GPU when cursor is static
        private int _lastX = int.MinValue;
        private int _lastY = int.MinValue;
        private CursorShapeType _lastShape = (CursorShapeType)(-1);
        private float _lastRotation = float.MinValue;
        private float _lastScale = float.MinValue;
        private float _lastStretchX = float.MinValue;
        private float _lastStretchAngle = float.MinValue;
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        public static Form ActiveSettingsForm = null;
        private bool _wasInSettings = false;

        public static readonly uint WM_SHOW_SETTINGS = RegisterWindowMessage("DYNAMIC_CURSORS_SHOW_SETTINGS");

        private int _topmostCounter = 0;
        private bool _overlayCleared = false;
        private bool _wasInSpecialApp = false;
        private long _snipCooldownUntilMs = 0;

        public event Action RequestExit;
        public event Action RequestShowSettings;
        public event Action<bool> ToggleEnabled;

        public OverlayWindow(AppConfig config)
        {
            _config = config;
            _renderer = new CursorRenderer(_config.CursorPack);
            _tiltSim = new TiltSimulator();
            _rotateSim = new RotateSimulator();
            _stretchSim = new StretchSimulator();
            _shakeDetector = new ShakeDetector();
            _clock = Stopwatch.StartNew();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-2000, -2000);
            Size = new Size(1024, 1024);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Prevents Explorer from flickering or fighting over taskbar z-order
            SetProp(Handle, "NonRudeHWND", new IntPtr(1));

            // Register Hotkeys: Ctrl+Alt+Q (Quit), Ctrl+Alt+D (Toggle)
            _hotkeysRegistered = RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT, (uint)Keys.Q) &&
                                 RegisterHotKey(Handle, 2, MOD_CONTROL | MOD_ALT, (uint)Keys.D);

            EnsureDIB();
            StartLoop();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)WM_SHOW_SETTINGS)
            {
                if (RequestShowSettings != null) RequestShowSettings();
                return;
            }

            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) // Ctrl+Alt+Q -> Quit
                {
                    if (RequestExit != null) RequestExit();
                    return;
                }
                else if (id == 2) // Ctrl+Alt+D -> Toggle
                {
                    _config.Enabled = !_config.Enabled;
                    _config.Save();
                    if (ToggleEnabled != null) ToggleEnabled(_config.Enabled);
                    return;
                }
            }
            else if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_DISPLAYCHANGE)
            {
                if (_config.Enabled && _config.HideSystemCursor)
                {
                    CursorHook.ForceHideSystemCursors();
                }
            }

            base.WndProc(ref m);
        }

        public CursorRenderer Renderer { get { return _renderer; } }

        public void ReloadAssets()
        {
            _renderer.LoadCustomAssets(_config.CursorPack);
        }

        public void ResetPhysics()
        {
            _tiltSim.Reset();
            _rotateSim.Reset();
            _stretchSim.Reset();
            _shakeDetector.Reset();
        }

        public void ClearOverlay()
        {
            if (_overlayCleared) return;
            _overlayCleared = true;

            _lastX = int.MinValue;
            _lastY = int.MinValue;
            _lastShape = (CursorShapeType)(-1);

            if (IsHandleCreated && !IsDisposed)
            {
                if (_hdcScreen == IntPtr.Zero) _hdcScreen = GetDC(IntPtr.Zero);
                if (_hdcMem == IntPtr.Zero && _hdcScreen != IntPtr.Zero) _hdcMem = CreateCompatibleDC(_hdcScreen);

                if (_hdcScreen != IntPtr.Zero && _hdcMem != IntPtr.Zero)
                {
                    SIZE sz = new SIZE { cx = 1, cy = 1 };
                    POINT ptSrc = new POINT { x = 0, y = 0 };
                    POINT ptDst = new POINT { x = -4000, y = -4000 };
                    BLENDFUNCTION blend = new BLENDFUNCTION
                    {
                        BlendOp = AC_SRC_OVER,
                        BlendFlags = 0,
                        SourceConstantAlpha = 0,
                        AlphaFormat = AC_SRC_ALPHA
                    };
                    UpdateLayeredWindow(Handle, _hdcScreen, ref ptDst, ref sz, _hdcMem, ref ptSrc, 0, ref blend, ULW_ALPHA);
                }
            }
        }

        private const int DIB_CANVAS_SIZE = 1024;

        private void EnsureDIB()
        {
            if (_hDIB != IntPtr.Zero) return;

            int size = DIB_CANVAS_SIZE;

            CleanupDIB();

            if (_hdcScreen == IntPtr.Zero)
            {
                _hdcScreen = GetDC(IntPtr.Zero);
            }
            if (_hdcMem == IntPtr.Zero)
            {
                _hdcMem = CreateCompatibleDC(_hdcScreen);
            }

            _dibSize = size;
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = size;
            bmi.bmiHeader.biHeight = -size; // top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            _hDIB = CreateDIBSection(_hdcMem, ref bmi, 0, out _pDIBBits, IntPtr.Zero, 0);
            _hOldBmp = SelectObject(_hdcMem, _hDIB);

            _dibBitmap = new Bitmap(size, size, size * 4, PixelFormat.Format32bppPArgb, _pDIBBits);
            _dibGraphics = Graphics.FromImage(_dibBitmap);
            _dibGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            _dibGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            _dibGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private void CleanupDIB()
        {
            if (_dibGraphics != null)
            {
                _dibGraphics.Dispose();
                _dibGraphics = null;
            }
            if (_dibBitmap != null)
            {
                _dibBitmap.Dispose();
                _dibBitmap = null;
            }
            if (_hOldBmp != IntPtr.Zero && _hdcMem != IntPtr.Zero)
            {
                SelectObject(_hdcMem, _hOldBmp);
                _hOldBmp = IntPtr.Zero;
            }
            if (_hDIB != IntPtr.Zero)
            {
                DeleteObject(_hDIB);
                _hDIB = IntPtr.Zero;
            }
            _pDIBBits = IntPtr.Zero;
            _dibSize = 0;
        }

        private void FullCleanupGDI()
        {
            CleanupDIB();
            if (_hdcMem != IntPtr.Zero)
            {
                DeleteDC(_hdcMem);
                _hdcMem = IntPtr.Zero;
            }
            if (_hdcScreen != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, _hdcScreen);
                _hdcScreen = IntPtr.Zero;
            }
        }

        private void StartLoop()
        {
            if (_running) return;
            _running = true;

            timeBeginPeriod(1);

            if (_config.Enabled && _config.HideSystemCursor)
            {
                CursorHook.HideSystemCursors();
            }

            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _renderThread.Start();
        }

        private void RenderLoop()
        {
            CursorHook.EnsureDesktopAccess();

            int targetMs = Math.Max(2, 1000 / Math.Max(30, _config.TargetFPS));

            while (_running)
            {
                long startMs = _clock.ElapsedMilliseconds;

                try
                {
                    if (_config.Enabled)
                    {
                        ProcessFrame(startMs);
                    }
                    else
                    {
                        // If disabled, ensure system cursor restored
                        CursorHook.RestoreSystemCursors();
                    }
                }
                catch { }

                long elapsed = _clock.ElapsedMilliseconds - startMs;
                int sleepTime = targetMs - (int)elapsed;
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void ProcessFrame(long nowMs)
        {
            // If mouse is inside active settings form, yield completely to native WinForms!
            bool inSettings = false;
            if (ActiveSettingsForm != null && !ActiveSettingsForm.IsDisposed && ActiveSettingsForm.Visible && ActiveSettingsForm.WindowState != FormWindowState.Minimized)
            {
                CursorHook.POINT curPt;
                if (CursorHook.GetCursorPos(out curPt))
                {
                    if (ActiveSettingsForm.Bounds.Contains(curPt.x, curPt.y))
                    {
                        inSettings = true;
                    }
                }
            }

            if (inSettings)
            {
                if (!_wasInSettings)
                {
                    _wasInSettings = true;
                    ResetPhysics();
                    ClearOverlay();
                    CursorHook.RestoreSystemCursors();
                }
                ClearOverlay();
                return;
            }
            else if (_wasInSettings)
            {
                _wasInSettings = false;
                ResetPhysics();
                if (_config.HideSystemCursor)
                {
                    CursorHook.ForceHideSystemCursors();
                }
            }

            // Immediate screenshot trigger (Win+Shift+S or PrtScn)
            bool isSnipKey = (GetAsyncKeyState(VK_SNAPSHOT) & 0x8000) != 0 ||
                             (((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) &&
                              ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) &&
                              ((GetAsyncKeyState(VK_S) & 0x8000) != 0));
            if (isSnipKey)
            {
                _snipCooldownUntilMs = nowMs + 700;
                ResetPhysics();
                ClearOverlay();
                CursorHook.RestoreSystemCursors();
                _wasInSpecialApp = true;
                return;
            }

            if (nowMs < _snipCooldownUntilMs)
            {
                ResetPhysics();
                ClearOverlay();
                CursorHook.RestoreSystemCursors();
                _wasInSpecialApp = true;
                return;
            }

            IntPtr fg = GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                uint fgPid;
                GetWindowThreadProcessId(fg, out fgPid);
                if (fgPid != 0)
                {
                    if (CursorHook.IsCaptureProcess(fgPid) || CursorHook.IsGameProcess(fgPid))
                    {
                        ResetPhysics();
                        ClearOverlay();
                        CursorHook.RestoreSystemCursors();
                        _wasInSpecialApp = true;
                        return;
                    }
                }
            }

            if (_wasInSpecialApp)
            {
                _wasInSpecialApp = false;
                ResetPhysics();
                if (_config.HideSystemCursor)
                {
                    CursorHook.ForceHideSystemCursors();
                }
            }

            CursorHook.POINT pt;
            CursorShapeType shape;
            IntPtr hCursor;
            if (!CursorHook.IsShowing(out pt, out shape, out hCursor))
            {
                ClearOverlay();
                return;
            }

            _overlayCleared = false;

            Vector2D pos = new Vector2D(pt.x, pt.y);
            ModeResult result = new ModeResult
            {
                Rotation = 0f,
                Scale = 1.0f,
                StretchAngle = 0f,
                StretchX = 1f,
                StretchY = 1f
            };

            // Shake to find magnification
            result.Scale = _shakeDetector.Update(pos, nowMs, _config);

            // Simulation mode
            switch (_config.Mode)
            {
                case SimulationMode.Tilt:
                    result.Rotation = _tiltSim.Update(pos, nowMs, _config);
                    break;

                case SimulationMode.Rotate:
                    result.Rotation = _rotateSim.Update(pos, _config);
                    break;

                case SimulationMode.Stretch:
                    StretchResult st = _stretchSim.Update(pos, nowMs, _config);
                    result.StretchAngle = st.Angle;
                    result.StretchX = st.ScaleX;
                    result.StretchY = st.ScaleY;
                    break;

                case SimulationMode.ShakeOnly:
                    break;
            }

            bool isAnimated = _renderer.IsShapeAnimated(shape);

            // Idle detection: if mouse hasn't moved and physics/animations are settled, skip redraw
            bool isIdle = (pt.x == _lastX && pt.y == _lastY &&
                           shape == _lastShape &&
                           !isAnimated &&
                           Math.Abs(result.Rotation - _lastRotation) < 0.05f &&
                           Math.Abs(result.Scale - _lastScale) < 0.005f &&
                           Math.Abs(result.StretchX - _lastStretchX) < 0.005f &&
                           Math.Abs(result.StretchAngle - _lastStretchAngle) < 0.5f);

            if (isIdle)
            {
                return;
            }

            _lastX = pt.x;
            _lastY = pt.y;
            _lastShape = shape;
            _lastRotation = result.Rotation;
            _lastScale = result.Scale;
            _lastStretchX = result.StretchX;
            _lastStretchAngle = result.StretchAngle;

            EnsureDIB();

            if (_dibGraphics != null && _hdcMem != IntPtr.Zero && _hdcScreen != IntPtr.Zero)
            {
                _renderer.Render(_dibGraphics, _dibSize, shape, result, _config, nowMs);

                SIZE sz = new SIZE { cx = _dibSize, cy = _dibSize };
                POINT ptSrc = new POINT { x = 0, y = 0 };
                POINT ptDst = new POINT { x = pt.x - (_dibSize / 2), y = pt.y - (_dibSize / 2) };

                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, _hdcScreen, ref ptDst, ref sz, _hdcMem, ref ptSrc, 0, ref blend, ULW_ALPHA);
            }

            // Silently re-assert topmost order periodically without SWP_SHOWWINDOW
            if (++_topmostCounter >= 30)
            {
                _topmostCounter = 0;
                if (IsHandleCreated && !IsDisposed)
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_renderThread != null) _renderThread.Join(300);
            }
            catch { }

            timeEndPeriod(1);

            if (_hotkeysRegistered && IsHandleCreated)
            {
                UnregisterHotKey(Handle, 1);
                UnregisterHotKey(Handle, 2);
            }

            FullCleanupGDI();
            CursorHook.RestoreSystemCursors();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Stop();
            _renderer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
