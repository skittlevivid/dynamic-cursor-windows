using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace DynamicCursors
{
    public class CursorAsset : IDisposable
    {
        public List<Bitmap> Frames = new List<Bitmap>();
        public float HotspotX;
        public float HotspotY;
        public int DisplayWidth = 32;
        public int DisplayHeight = 32;
        public int NativeWidth = 32;
        public int NativeHeight = 32;
        public bool AllowRotation;
        public int FrameDelayMs = 60;
        private long _lastFrameTick = 0;
        private int _currentFrame = 0;

        public float BaseHotspotX
        {
            get { return HotspotX; }
        }

        public float BaseHotspotY
        {
            get { return HotspotY; }
        }

        public CursorAsset(List<Bitmap> frames, float hx, float hy, int dispW, int dispH, int nw, int nh, bool allowRot)
        {
            Frames = frames;
            HotspotX = hx;
            HotspotY = hy;
            DisplayWidth = dispW > 0 ? dispW : 32;
            DisplayHeight = dispH > 0 ? dispH : 32;
            NativeWidth = nw > 0 ? nw : DisplayWidth;
            NativeHeight = nh > 0 ? nh : DisplayHeight;
            AllowRotation = allowRot;
        }

        public CursorAsset(List<Bitmap> frames, float hx, float hy, int nw, int nh, bool allowRot)
            : this(frames, hx, hy, Math.Min(32, nw > 0 ? nw : 32), Math.Min(32, nh > 0 ? nh : 32), nw, nh, allowRot)
        {
        }

        public Bitmap GetCurrentFrame(long nowMs)
        {
            if (Frames == null || Frames.Count == 0) return null;
            if (Frames.Count == 1) return Frames[0];

            if (nowMs - _lastFrameTick >= FrameDelayMs)
            {
                _currentFrame = (_currentFrame + 1) % Frames.Count;
                _lastFrameTick = nowMs;
            }

            return Frames[_currentFrame];
        }

        public bool IsAnimated
        {
            get { return Frames != null && Frames.Count > 1; }
        }

        public void Dispose()
        {
            if (Frames != null)
            {
                foreach (var f in Frames)
                {
                    if (f != null) f.Dispose();
                }
                Frames.Clear();
            }
        }
    }

    public class CursorRenderer : IDisposable
    {
        public const uint DI_NORMAL = 0x0003;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        public const uint IMAGE_CURSOR = 2;
        public const uint LR_LOADFROMFILE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out CursorHook.ICONINFO piconinfo);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public short bmPlanes;
            public short bmBitsPixel;
            public IntPtr bmBits;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

        private int _canvasSize = 1024;
        private int _center = 512;
        private string _currentPack = "";

        public int CanvasSize { get { return _canvasSize; } }
        public int Center { get { return _center; } }

        private Bitmap _canvas;
        private Graphics _graphics;
        private readonly Dictionary<CursorShapeType, CursorAsset> _assets = new Dictionary<CursorShapeType, CursorAsset>();

        private FileSystemWatcher _watcher;
        private System.Threading.Timer _debounceTimer;
        private float _waitSpinnerAngle = 0f;

        public event Action CursorsAutoReloaded;

        public CursorRenderer(string packName = null)
        {
            _canvasSize = 1024;
            _center = 512;
            _canvas = new Bitmap(_canvasSize, _canvasSize, PixelFormat.Format32bppPArgb);
            _graphics = Graphics.FromImage(_canvas);
            _graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            _graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            _currentPack = packName ?? "";
            LoadCustomAssets(_currentPack);
            SetupWatcher();
        }

        public const string PACK_WINDOWS_AUTO = "Windows Active Scheme (Auto)";
        public const string PACK_CUSTOM_ROOT = "Custom (cursors/ folder)";

        public static List<string> GetAvailablePacks()
        {
            List<string> packs = new List<string> { PACK_WINDOWS_AUTO, PACK_CUSTOM_ROOT };
            try
            {
                string dir = Path.Combine(AppConfig.GetAppDir(), "cursors");
                if (Directory.Exists(dir))
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        string name = Path.GetFileName(sub);
                        if (!string.IsNullOrEmpty(name))
                        {
                            packs.Add(name);
                        }
                    }
                }
            }
            catch { }
            return packs;
        }

        public Bitmap GetPreviewFrame(CursorShapeType shape)
        {
            lock (_assets)
            {
                CursorAsset asset;
                if (_assets.TryGetValue(shape, out asset) && asset != null)
                {
                    return asset.GetCurrentFrame(0);
                }
                if (_assets.TryGetValue(CursorShapeType.Arrow, out asset) && asset != null)
                {
                    return asset.GetCurrentFrame(0);
                }
            }
            return null;
        }

        public bool GetAssetInfo(CursorShapeType shape, out int width, out int height, out int hx, out int hy)
        {
            width = 32;
            height = 32;
            hx = 4;
            hy = 4;
            lock (_assets)
            {
                CursorAsset asset;
                if ((_assets.TryGetValue(shape, out asset) && asset != null) ||
                    (_assets.TryGetValue(CursorShapeType.Arrow, out asset) && asset != null))
                {
                    width = asset.DisplayWidth;
                    height = asset.DisplayHeight;
                    hx = (int)Math.Round(asset.HotspotX);
                    hy = (int)Math.Round(asset.HotspotY);
                    return true;
                }
            }
            return false;
        }

        public static void GetActiveSchemeInfo(out string schemeName, out string arrowPath)
        {
            schemeName = "Windows Default";
            arrowPath = "";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
                {
                    if (key != null)
                    {
                        object def = key.GetValue("");
                        if (def != null && !string.IsNullOrEmpty(def.ToString().Trim()))
                        {
                            schemeName = def.ToString().Trim();
                        }
                        object arrow = key.GetValue("Arrow");
                        if (arrow != null)
                        {
                            string p = Environment.ExpandEnvironmentVariables(arrow.ToString().Trim());
                            if (File.Exists(p)) arrowPath = p;
                        }
                    }
                }
            }
            catch { }
        }

        public void EnsureCanvasSize(int neededSize)
        {
            if (neededSize <= _canvasSize && _canvas != null) return;

            _canvasSize = neededSize;
            _center = _canvasSize / 2;

            if (_graphics != null) _graphics.Dispose();
            if (_canvas != null) _canvas.Dispose();

            _canvas = new Bitmap(_canvasSize, _canvasSize, PixelFormat.Format32bppPArgb);
            _graphics = Graphics.FromImage(_canvas);
            _graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            _graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public bool IsShapeAnimated(CursorShapeType shape)
        {
            lock (_assets)
            {
                CursorAsset asset;
                if (_assets.TryGetValue(shape, out asset) && asset != null)
                {
                    return asset.IsAnimated;
                }
                if (shape == CursorShapeType.Wait) return true;
            }
            return false;
        }

        private void SetupWatcher()
        {
            try
            {
                string dir = Path.Combine(AppConfig.GetAppDir(), "cursors");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _watcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                FileSystemEventHandler handler = (s, e) =>
                {
                    string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                    if (ext == ".cur" || ext == ".ani" || ext == ".png")
                    {
                        if (_debounceTimer != null) _debounceTimer.Dispose();
                        _debounceTimer = new System.Threading.Timer((state) =>
                        {
                            try
                            {
                                LoadCustomAssets(_currentPack);
                                if (CursorsAutoReloaded != null) CursorsAutoReloaded();
                            }
                            catch { }
                        }, null, 250, Timeout.Infinite);
                    }
                };

                _watcher.Created += handler;
                _watcher.Changed += handler;
                _watcher.Deleted += handler;
                _watcher.Renamed += (s, e) => handler(s, e);
            }
            catch { }
        }

        public void LoadCustomAssets(string packName = null)
        {
            if (packName != null) _currentPack = packName;

            lock (_assets)
            {
                foreach (var kv in _assets)
                {
                    if (kv.Value != null) kv.Value.Dispose();
                }
                _assets.Clear();

                // If user selected Windows Active Scheme (or default auto) -> adapt directly to active Windows cursors!
                if (string.IsNullOrEmpty(_currentPack) ||
                    _currentPack.Equals(PACK_WINDOWS_AUTO, StringComparison.OrdinalIgnoreCase) ||
                    _currentPack.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    LoadWindowsSchemeAssets();
                    return;
                }

                string baseDir = Path.Combine(AppConfig.GetAppDir(), "cursors");
                if (!Directory.Exists(baseDir)) return;

                string packDir = null;
                if (!_currentPack.Equals(PACK_CUSTOM_ROOT, StringComparison.OrdinalIgnoreCase) &&
                    !_currentPack.Equals("Default (Root)", StringComparison.OrdinalIgnoreCase))
                {
                    string target = Path.Combine(baseDir, _currentPack);
                    if (Directory.Exists(target))
                    {
                        packDir = target;
                    }
                }

                // Flexible multi-candidate lookup for .cur, .ani, and .png (loads highest resolution frame up to 128x128)
                _assets[CursorShapeType.Arrow] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "normal select.cur", "normal select.ani", "normal.cur", "normal.ani", "arrow.cur", "arrow.ani", "arrow_xl.cur", "arrow_l.cur", "pointer.cur", "pointer.ani", "default.cur", "pointer.png", "arrow.png" },
                    4, 4, true);

                _assets[CursorShapeType.Hand] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "link select.cur", "link select.ani", "link.cur", "link.ani", "hand.cur", "hand.ani", "link_xl.cur", "link_l.cur", "pointer.cur", "link.png", "hand.png" },
                    10, 2, true);

                _assets[CursorShapeType.IBeam] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "text select.cur", "text select.ani", "text.cur", "text.ani", "ibeam.cur", "ibeam.ani", "ibeam_xl.cur", "ibeam_l.cur", "beam.cur", "text.png", "ibeam.png" },
                    15, 15, false);

                _assets[CursorShapeType.Cross] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "precision select.cur", "precision select.ani", "precision.cur", "precision.ani", "cross.cur", "cross.ani", "cross_xl.cur", "cross_l.cur", "crosshair.cur", "crosshair.ani" },
                    15, 15, false);

                _assets[CursorShapeType.SizeWE] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "horizontal resize.cur", "horizontal resize.ani", "horizontal.cur", "horizontal.ani", "ew.cur", "ew.ani", "ew_xl.cur", "ew_l.cur", "sizewe.cur", "horz.cur", "horz.png" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNS] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "vertical resize.cur", "vertical resize.ani", "vertical.cur", "vertical.ani", "ns.cur", "ns.ani", "ns_xl.cur", "ns_l.cur", "sizens.cur", "vert.cur", "vert.png" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNWSE] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "diagonal resize 1.cur", "diagonal resize 1.ani", "nwse.cur", "nwse.ani", "nwse_xl.cur", "nwse_l.cur", "sizenwse.cur" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNESW] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "diagonal resize 2.cur", "diagonal resize 2.ani", "nesw.cur", "nesw.ani", "nesw_xl.cur", "nesw_l.cur", "sizenesw.cur" },
                    15, 15, false);

                _assets[CursorShapeType.SizeAll] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "move.cur", "move.ani", "move_xl.cur", "move_l.cur", "sizeall.cur", "size.cur" },
                    15, 15, false);

                _assets[CursorShapeType.Wait] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "busy.ani", "busy.cur", "working in background.ani", "working in background.cur", "working.ani", "working_xl.ani", "working_l.ani", "wait.ani", "wait.cur" },
                    15, 15, false);

                _assets[CursorShapeType.No] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "unavailable.cur", "unavailable.ani", "unavail.cur", "unavail.ani", "unavail_xl.cur", "unavail_l.cur", "no.cur", "no.ani" },
                    15, 15, false);

                _assets[CursorShapeType.Help] = LoadCurAniPng(packDir, baseDir,
                    new string[] { "help select.cur", "help select.ani", "helpsel.cur", "helpsel.ani", "helpsel_xl.cur", "helpsel_l.cur", "help.cur", "help.ani" },
                    4, 4, true);
            }
        }

        private void LoadWindowsSchemeAssets()
        {
            string winCursors = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Cursors");
            string baseDir = Path.Combine(AppConfig.GetAppDir(), "cursors");

            Dictionary<string, string> regMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
                {
                    if (key != null)
                    {
                        foreach (string valName in key.GetValueNames())
                        {
                            object val = key.GetValue(valName);
                            if (val != null)
                            {
                                string str = Environment.ExpandEnvironmentVariables(val.ToString().Trim());
                                if (!string.IsNullOrEmpty(str) && File.Exists(str))
                                {
                                    regMap[valName] = str;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            Func<string, string[], string> resolve = (regKey, candidates) =>
            {
                string p;
                if (regMap.TryGetValue(regKey, out p) && File.Exists(p)) return p;
                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(baseDir))
                    {
                        string bp = Path.Combine(baseDir, c);
                        if (File.Exists(bp)) return bp;
                    }
                    if (!string.IsNullOrEmpty(winCursors))
                    {
                        string wp = Path.Combine(winCursors, c);
                        if (File.Exists(wp)) return wp;
                    }
                }
                return null;
            };

            // Load all 12 shapes from active Windows Scheme
            string arrow = resolve("Arrow", new string[] { "Normal Select.cur", "arrow.cur", "aero_arrow.cur" });
            if (arrow != null) _assets[CursorShapeType.Arrow] = LoadSingleFile(arrow, 4, 4, true);

            string hand = resolve("Hand", new string[] { "Link Select.cur", "link.cur", "aero_link.cur" });
            if (hand != null) _assets[CursorShapeType.Hand] = LoadSingleFile(hand, 10, 2, true);

            string ibeam = resolve("IBeam", new string[] { "Text Select.cur", "ibeam.cur", "beam_r.cur" });
            if (ibeam != null) _assets[CursorShapeType.IBeam] = LoadSingleFile(ibeam, 15, 15, false);

            string cross = resolve("Crosshair", new string[] { "Precision Select.cur", "cross.cur", "cross_r.cur" });
            if (cross != null) _assets[CursorShapeType.Cross] = LoadSingleFile(cross, 15, 15, false);

            string sizewe = resolve("SizeWE", new string[] { "Horizontal Resize.cur", "ew.cur", "aero_ew.cur" });
            if (sizewe != null) _assets[CursorShapeType.SizeWE] = LoadSingleFile(sizewe, 15, 15, false);

            string sizens = resolve("SizeNS", new string[] { "Vertical Resize.cur", "ns.cur", "aero_ns.cur" });
            if (sizens != null) _assets[CursorShapeType.SizeNS] = LoadSingleFile(sizens, 15, 15, false);

            string sizenwse = resolve("SizeNWSE", new string[] { "Diagonal Resize 1.cur", "nwse.cur", "aero_nwse.cur" });
            if (sizenwse != null) _assets[CursorShapeType.SizeNWSE] = LoadSingleFile(sizenwse, 15, 15, false);

            string sizenesw = resolve("SizeNESW", new string[] { "Diagonal Resize 2.cur", "nesw.cur", "aero_nesw.cur" });
            if (sizenesw != null) _assets[CursorShapeType.SizeNESW] = LoadSingleFile(sizenesw, 15, 15, false);

            string sizeall = resolve("SizeAll", new string[] { "Move.cur", "move.cur", "aero_move.cur" });
            if (sizeall != null) _assets[CursorShapeType.SizeAll] = LoadSingleFile(sizeall, 15, 15, false);

            string wait = resolve("Wait", new string[] { "Busy.ani", "busy.ani", "aero_busy.ani", "Working In Background.ani", "working.ani" });
            if (wait == null) wait = resolve("AppStarting", new string[] { "Working In Background.ani", "working.ani", "aero_working.ani" });
            if (wait != null) _assets[CursorShapeType.Wait] = LoadSingleFile(wait, 15, 15, false);

            string no = resolve("No", new string[] { "Unavailable.cur", "unavail.cur", "aero_unavail.cur" });
            if (no != null) _assets[CursorShapeType.No] = LoadSingleFile(no, 15, 15, false);

            string help = resolve("Help", new string[] { "Help Select.cur", "helpsel.cur", "aero_helpsel.cur" });
            if (help != null) _assets[CursorShapeType.Help] = LoadSingleFile(help, 4, 4, true);
        }

        private static string FindFileCaseInsensitive(string dir, string candidate)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            string direct = Path.Combine(dir, candidate);
            if (File.Exists(direct)) return direct;

            try
            {
                string[] files = Directory.GetFiles(dir);
                foreach (string f in files)
                {
                    if (string.Equals(Path.GetFileName(f), candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }
            }
            catch { }
            return null;
        }

        private CursorAsset LoadCurAniPng(string packDir, string baseDir, string[] candidates, int fallbackHx, int fallbackHy, bool allowRot)
        {
            foreach (string file in candidates)
            {
                string path = null;
                if (!string.IsNullOrEmpty(packDir)) path = FindFileCaseInsensitive(packDir, file);
                if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(baseDir)) path = FindFileCaseInsensitive(baseDir, file);

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var asset = LoadSingleFile(path, fallbackHx, fallbackHy, allowRot);
                    if (asset != null) return asset;
                }
            }
            return null;
        }

        private CursorAsset LoadSingleFile(string path, int fallbackHx, int fallbackHy, bool allowRot)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".cur")
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    if (bytes.Length > 22 && bytes[0] == 0 && bytes[1] == 0 && BitConverter.ToInt16(bytes, 2) == 2)
                    {
                        int count = BitConverter.ToInt16(bytes, 4);
                        if (count > 0)
                        {
                            int bestIdx = 0;
                            int maxW = -1;
                            int minW = 9999;
                            for (int i = 0; i < count; i++)
                            {
                                int off = 6 + (i * 16);
                                int w = bytes[off]; if (w == 0) w = 256;
                                if (w > maxW) { maxW = w; bestIdx = i; }
                                if (w < minW) { minW = w; }
                            }

                            int rawHx, rawHy, masterW, masterH;
                            Bitmap bmp = ParseCurDIB(bytes, bestIdx, out rawHx, out rawHy, out masterW, out masterH);
                            if (bmp != null)
                            {
                                int dispW = (count > 1 && minW >= 24 && minW <= 48) ? minW : (count > 1 ? 32 : masterW);
                                int dispH = dispW;
                                float hx = (float)rawHx * ((float)dispW / (float)masterW);
                                float hy = (float)rawHy * ((float)dispH / (float)masterH);
                                return new CursorAsset(new List<Bitmap> { bmp }, hx, hy, dispW, dispH, masterW, masterH, allowRot);
                            }
                        }
                    }
                }
                catch { }

                return LoadViaWin32(path, fallbackHx, fallbackHy, allowRot);
            }
            else if (ext == ".ani")
            {
                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    List<Bitmap> frames = new List<Bitmap>();
                    float hx = fallbackHx;
                    float hy = fallbackHy;
                    int dispW = 32;
                    int dispH = 32;
                    int masterW = 32;
                    int masterH = 32;

                    for (int i = 0; i < data.Length - 8; i++)
                    {
                        if (data[i] == 'i' && data[i + 1] == 'c' && data[i + 2] == 'o' && data[i + 3] == 'n')
                        {
                            int chunkSize = BitConverter.ToInt32(data, i + 4);
                            if (chunkSize > 22 && i + 8 + chunkSize <= data.Length)
                            {
                                byte[] chunk = new byte[chunkSize];
                                Array.Copy(data, i + 8, chunk, 0, chunkSize);

                                int count = BitConverter.ToInt16(chunk, 4);
                                int bestIdx = 0;
                                int maxW = -1;
                                int minW = 9999;
                                for (int c = 0; c < count; c++)
                                {
                                    int off = 6 + (c * 16);
                                    int w = chunk[off]; if (w == 0) w = 256;
                                    if (w > maxW) { maxW = w; bestIdx = c; }
                                    if (w < minW) { minW = w; }
                                }

                                int rawHx, rawHy, mw, mh;
                                Bitmap f = ParseCurDIB(chunk, bestIdx, out rawHx, out rawHy, out mw, out mh);
                                if (f != null)
                                {
                                    frames.Add(f);
                                    masterW = mw; masterH = mh;
                                    dispW = (count > 1 && minW >= 24 && minW <= 48) ? minW : (count > 1 ? 32 : mw);
                                    dispH = dispW;
                                    hx = (float)rawHx * ((float)dispW / (float)mw);
                                    hy = (float)rawHy * ((float)dispH / (float)mh);
                                }
                                i += 8 + chunkSize - 1;
                            }
                        }
                    }

                    if (frames.Count > 0)
                    {
                        return new CursorAsset(frames, hx, hy, dispW, dispH, masterW, masterH, allowRot);
                    }
                }
                catch { }

                return LoadViaWin32(path, fallbackHx, fallbackHy, allowRot);
            }
            else if (ext == ".png")
            {
                try
                {
                    using (Image img = Image.FromFile(path))
                    {
                        Bitmap bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.DrawImage(img, 0, 0);
                        }
                        List<Bitmap> frames = new List<Bitmap> { bmp };
                        int dispW = Math.Min(32, img.Width);
                        int dispH = Math.Min(32, img.Height);
                        float hx = (float)fallbackHx * ((float)dispW / (float)img.Width);
                        float hy = (float)fallbackHy * ((float)dispH / (float)img.Height);
                        return new CursorAsset(frames, hx, hy, dispW, dispH, img.Width, img.Height, allowRot);
                    }
                }
                catch { }
            }

            return null;
        }

        private static Bitmap ParseCurDIB(byte[] b, int entryIdx, out int hx, out int hy, out int w, out int h)
        {
            hx = 0; hy = 0; w = 32; h = 32;
            try
            {
                int count = BitConverter.ToInt16(b, 4);
                if (entryIdx >= count) entryIdx = 0;
                int off = 6 + (entryIdx * 16);
                w = b[off]; if (w == 0) w = 256;
                h = b[off + 1]; if (h == 0) h = 256;
                hx = BitConverter.ToInt16(b, off + 4);
                hy = BitConverter.ToInt16(b, off + 6);
                int imgOff = BitConverter.ToInt32(b, off + 12);

                if (imgOff + 40 > b.Length) return null;

                int biSize = BitConverter.ToInt32(b, imgOff);
                short biBpp = BitConverter.ToInt16(b, imgOff + 14);

                if (biBpp == 32)
                {
                    int pixelStart = imgOff + biSize;
                    int rowBytes = w * 4;
                    if (pixelStart + (w * h * 4) > b.Length) return null;

                    bool hasRealAlpha = false;
                    for (int i = 0; i < w * h; i++)
                    {
                        if (b[pixelStart + (i * 4) + 3] > 0)
                        {
                            hasRealAlpha = true;
                            break;
                        }
                    }

                    int maskRowBytes = ((w + 31) / 32) * 4;
                    int maskStart = pixelStart + (w * h * 4);

                    Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                    byte[] dest = new byte[w * h * 4];

                    for (int y = 0; y < h; y++)
                    {
                        int srcRow = (h - 1 - y);
                        int srcIndex = pixelStart + (srcRow * rowBytes);
                        int dstIndex = y * rowBytes;
                        int maskIndex = maskStart + (srcRow * maskRowBytes);

                        for (int x = 0; x < w; x++)
                        {
                            int si = srcIndex + (x * 4);
                            int di = dstIndex + (x * 4);

                            byte blue  = b[si];
                            byte green = b[si + 1];
                            byte red   = b[si + 2];
                            byte alpha;

                            if (hasRealAlpha)
                            {
                                alpha = b[si + 3];
                            }
                            else
                            {
                                if (maskStart < b.Length && maskIndex + (x / 8) < b.Length)
                                {
                                    byte maskByte = b[maskIndex + (x / 8)];
                                    bool isTransparent = ((maskByte >> (7 - (x % 8))) & 1) != 0;
                                    alpha = isTransparent ? (byte)0 : (byte)255;
                                }
                                else
                                {
                                    alpha = 255;
                                }
                            }

                            dest[di]     = (byte)((blue * alpha) / 255);
                            dest[di + 1] = (byte)((green * alpha) / 255);
                            dest[di + 2] = (byte)((red * alpha) / 255);
                            dest[di + 3] = alpha;
                        }
                    }

                    Marshal.Copy(dest, 0, bd.Scan0, dest.Length);
                    bmp.UnlockBits(bd);
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private CursorAsset LoadViaWin32(string path, int fallbackHx, int fallbackHy, bool allowRot)
        {
            IntPtr hCur = LoadImage(IntPtr.Zero, path, IMAGE_CURSOR, 0, 0, LR_LOADFROMFILE);
            if (hCur == IntPtr.Zero) hCur = LoadCursorFromFile(path);
            if (hCur == IntPtr.Zero) return null;

            int curW = 32;
            int curH = 32;
            int hx = fallbackHx;
            int hy = fallbackHy;

            CursorHook.ICONINFO ii;
            if (GetIconInfo(hCur, out ii))
            {
                hx = ii.xHotspot;
                hy = ii.yHotspot;

                if (ii.hbmColor != IntPtr.Zero)
                {
                    BITMAP bm;
                    if (GetObject(ii.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out bm) != 0)
                    {
                        curW = Math.Max(16, bm.bmWidth);
                        curH = Math.Max(16, bm.bmHeight);
                    }
                    DeleteObject(ii.hbmColor);
                }
                else if (ii.hbmMask != IntPtr.Zero)
                {
                    BITMAP bm;
                    if (GetObject(ii.hbmMask, Marshal.SizeOf(typeof(BITMAP)), out bm) != 0)
                    {
                        curW = Math.Max(16, bm.bmWidth);
                        curH = Math.Max(16, bm.bmHeight / 2);
                    }
                }
                if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            }

            List<Bitmap> frames = new List<Bitmap>();
            for (uint i = 0; i < 120; i++)
            {
                Bitmap bmp = new Bitmap(curW, curH, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    bool ok = DrawIconEx(hdc, 0, 0, hCur, curW, curH, i, IntPtr.Zero, DI_NORMAL);
                    g.ReleaseHdc(hdc);
                    if (!ok) { bmp.Dispose(); break; }
                }
                frames.Add(bmp);
                if (Path.GetExtension(path).Equals(".cur", StringComparison.OrdinalIgnoreCase)) break;
            }

            DestroyIcon(hCur);
            if (frames.Count > 0)
            {
                return new CursorAsset(frames, hx, hy, curW, curH, curW, curH, allowRot);
            }
            return null;
        }

        public void Render(Graphics g, int canvasSize, CursorShapeType shape, ModeResult result, AppConfig config, long nowMs)
        {
            if (g == null) return;
            float totalScale = result.Scale * (config != null ? config.CursorScale : 1.0f);
            float maxScale = (canvasSize - 24) / 32.0f;
            if (totalScale > maxScale) totalScale = maxScale;
            int center = canvasSize / 2;

            g.Clear(Color.Transparent);
            g.ResetTransform();

            // 1. Shake / base zoom (centered at center, center)
            if (Math.Abs(totalScale - 1.0f) > 0.005f)
            {
                g.TranslateTransform(center, center);
                g.ScaleTransform(totalScale, totalScale);
                g.TranslateTransform(-center, -center);
            }

            CursorAsset asset = null;
            lock (_assets)
            {
                _assets.TryGetValue(shape, out asset);
                if (asset == null && shape != CursorShapeType.Arrow)
                {
                    _assets.TryGetValue(CursorShapeType.Arrow, out asset);
                }
            }

            bool allowPhysics = asset == null || asset.AllowRotation;

            // 2. Stretch deformation
            if (allowPhysics && (result.StretchX > 1.01f || result.StretchY < 0.99f))
            {
                g.TranslateTransform(center, center);
                g.RotateTransform(-result.StretchAngle);
                g.ScaleTransform(result.StretchX, result.StretchY);
                g.RotateTransform(result.StretchAngle);
                g.TranslateTransform(-center, -center);
            }

            // 3. Rotation / Tilt
            if (allowPhysics && Math.Abs(result.Rotation) > 0.1f)
            {
                g.TranslateTransform(center, center);
                g.RotateTransform(result.Rotation);
                g.TranslateTransform(-center, -center);
            }

            // Draw cursor asset frame
            Bitmap currentFrame = asset != null ? asset.GetCurrentFrame(nowMs) : null;
            if (currentFrame != null)
            {
                float hx = asset.HotspotX;
                float hy = asset.HotspotY;
                float baseW = asset.DisplayWidth > 0 ? (float)asset.DisplayWidth : 32.0f;
                float baseH = asset.DisplayHeight > 0 ? (float)asset.DisplayHeight : 32.0f;
                RectangleF destRect = new RectangleF(center - hx, center - hy, baseW, baseH);
                g.DrawImage(currentFrame, destRect);
            }
            else
            {
                DrawFallbackShape(g, center, shape);
            }
        }

        private void DrawFallbackShape(Graphics g, int center, CursorShapeType shape)
        {
            switch (shape)
            {
                case CursorShapeType.Hand:
                    DrawVectorHand(g, center);
                    break;
                case CursorShapeType.IBeam:
                    DrawVectorIBeam(g, center);
                    break;
                case CursorShapeType.Cross:
                    DrawVectorCross(g, center);
                    break;
                case CursorShapeType.Wait:
                    DrawVectorSpinner(g, center);
                    break;
                case CursorShapeType.Arrow:
                default:
                    DrawVectorArrow(g, center);
                    break;
            }
        }

        private void DrawVectorArrow(Graphics g, int center)
        {
            // Compact standard-sized arrow
            PointF tip = new PointF(center, center);

            PointF[] polygon = new PointF[]
            {
                tip,
                new PointF(tip.X, tip.Y + 20f),
                new PointF(tip.X + 5.5f, tip.Y + 15.5f),
                new PointF(tip.X + 9.5f, tip.Y + 24f),
                new PointF(tip.X + 13f, tip.Y + 22.5f),
                new PointF(tip.X + 9f, tip.Y + 14f),
                new PointF(tip.X + 15f, tip.Y + 14f)
            };

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(polygon);

                using (Matrix shadowMat = new Matrix())
                {
                    shadowMat.Translate(1.2f, 1.5f);
                    using (GraphicsPath shadowPath = (GraphicsPath)path.Clone())
                    {
                        shadowPath.Transform(shadowMat);
                        using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                        {
                            g.FillPath(shadowBrush, shadowPath);
                        }
                    }
                }

                using (SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(250, 250, 250)))
                {
                    g.FillPath(bodyBrush, path);
                }

                using (Pen borderPen = new Pen(Color.FromArgb(240, 24, 24, 27), 1.8f) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(borderPen, path);
                }
            }
        }

        private void DrawVectorHand(Graphics g, int center)
        {
            PointF tip = new PointF(center, center);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(tip.X, tip.Y, tip.X + 5, tip.Y);
                path.AddLine(tip.X + 5, tip.Y, tip.X + 5, tip.Y + 8);
                path.AddLine(tip.X + 5, tip.Y + 8, tip.X + 10, tip.Y + 8);
                path.AddLine(tip.X + 10, tip.Y + 8, tip.X + 10, tip.Y + 18);
                path.AddLine(tip.X + 10, tip.Y + 18, tip.X - 3, tip.Y + 18);
                path.AddLine(tip.X - 3, tip.Y + 18, tip.X - 5, tip.Y + 12);
                path.AddLine(tip.X - 5, tip.Y + 12, tip.X, tip.Y);
                path.CloseFigure();

                using (SolidBrush fill = new SolidBrush(Color.FromArgb(250, 250, 250)))
                using (Pen pen = new Pen(Color.FromArgb(240, 24, 24, 27), 1.6f) { LineJoin = LineJoin.Round })
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawVectorIBeam(Graphics g, int center)
        {
            int half = 9;
            using (Pen pen = new Pen(Color.FromArgb(240, 24, 24, 27), 2.0f))
            {
                g.DrawLine(pen, center - 4, center - half, center + 4, center - half);
                g.DrawLine(pen, center, center - half, center, center + half);
                g.DrawLine(pen, center - 4, center + half, center + 4, center + half);
            }
        }

        private void DrawVectorCross(Graphics g, int center)
        {
            int r = 8;
            using (Pen pen = new Pen(Color.FromArgb(240, 24, 24, 27), 1.8f))
            {
                g.DrawLine(pen, center - r, center, center + r, center);
                g.DrawLine(pen, center, center - r, center, center + r);
            }
        }

        private void DrawVectorSpinner(Graphics g, int center)
        {
            _waitSpinnerAngle = (_waitSpinnerAngle + 12f) % 360f;
            Rectangle rect = new Rectangle(center - 11, center - 11, 22, 22);
            using (Pen pen = new Pen(Color.FromArgb(220, 60, 130, 246), 2.5f))
            {
                g.DrawArc(pen, rect, _waitSpinnerAngle, 260f);
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }

            lock (_assets)
            {
                foreach (var kv in _assets)
                {
                    if (kv.Value != null) kv.Value.Dispose();
                }
                _assets.Clear();
            }

            if (_graphics != null) _graphics.Dispose();
            if (_canvas != null) _canvas.Dispose();
        }
    }
}
