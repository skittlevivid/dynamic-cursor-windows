using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DynamicCursors
{
    public class CursorAsset : IDisposable
    {
        public List<Bitmap> Frames = new List<Bitmap>();
        public int HotspotX;
        public int HotspotY;
        public int NativeWidth = 32;
        public int NativeHeight = 32;
        public bool AllowRotation;
        public int FrameDelayMs = 60;
        private long _lastFrameTick = 0;
        private int _currentFrame = 0;

        public float BaseHotspotX
        {
            get
            {
                if (NativeWidth <= 0) return HotspotX;
                return HotspotX * (32.0f / NativeWidth);
            }
        }

        public float BaseHotspotY
        {
            get
            {
                if (NativeHeight <= 0) return HotspotY;
                return HotspotY * (32.0f / NativeHeight);
            }
        }

        public CursorAsset(List<Bitmap> frames, int hx, int hy, int nw, int nh, bool allowRot)
        {
            Frames = frames;
            HotspotX = hx;
            HotspotY = hy;
            NativeWidth = nw > 0 ? nw : 32;
            NativeHeight = nh > 0 ? nh : 32;
            AllowRotation = allowRot;
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

        private int _canvasSize = 512;
        private int _center = 256;

        public int CanvasSize { get { return _canvasSize; } }
        public int Center { get { return _center; } }

        private Bitmap _canvas;
        private Graphics _graphics;
        private readonly Dictionary<CursorShapeType, CursorAsset> _assets = new Dictionary<CursorShapeType, CursorAsset>();

        private FileSystemWatcher _watcher;
        private System.Threading.Timer _debounceTimer;
        private float _waitSpinnerAngle = 0f;

        public event Action CursorsAutoReloaded;

        public CursorRenderer()
        {
            _canvasSize = 512;
            _center = 256;
            _canvas = new Bitmap(_canvasSize, _canvasSize, PixelFormat.Format32bppPArgb);
            _graphics = Graphics.FromImage(_canvas);
            _graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            _graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            LoadCustomAssets();
            SetupWatcher();
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
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cursors");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _watcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
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
                                LoadCustomAssets();
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

        public void LoadCustomAssets()
        {
            lock (_assets)
            {
                foreach (var kv in _assets)
                {
                    if (kv.Value != null) kv.Value.Dispose();
                }
                _assets.Clear();

                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cursors");
                if (!Directory.Exists(dir)) return;

                // Flexible multi-candidate lookup for .cur, .ani, and .png (loads highest resolution frame up to 128x128)
                _assets[CursorShapeType.Arrow] = LoadCurAniPng(dir,
                    new string[] { "arrow.cur", "arrow.ani", "arrow_xl.cur", "arrow_l.cur", "pointer.cur", "pointer.ani", "normal.cur", "normal.ani", "default.cur", "pointer.png", "arrow.png" },
                    4, 4, true);

                _assets[CursorShapeType.Hand] = LoadCurAniPng(dir,
                    new string[] { "link.cur", "link.ani", "link_xl.cur", "link_l.cur", "hand.cur", "hand.ani", "pointer.cur", "link.png", "hand.png" },
                    10, 2, true);

                _assets[CursorShapeType.IBeam] = LoadCurAniPng(dir,
                    new string[] { "ibeam.cur", "ibeam.ani", "ibeam_xl.cur", "ibeam_l.cur", "text.cur", "text.ani", "beam.cur", "text.png", "ibeam.png" },
                    15, 15, false);

                _assets[CursorShapeType.Cross] = LoadCurAniPng(dir,
                    new string[] { "cross.cur", "cross.ani", "cross_xl.cur", "cross_l.cur", "crosshair.cur", "crosshair.ani" },
                    15, 15, false);

                _assets[CursorShapeType.SizeWE] = LoadCurAniPng(dir,
                    new string[] { "ew.cur", "ew.ani", "ew_xl.cur", "ew_l.cur", "sizewe.cur", "horz.cur", "horz.png" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNS] = LoadCurAniPng(dir,
                    new string[] { "ns.cur", "ns.ani", "ns_xl.cur", "ns_l.cur", "sizens.cur", "vert.cur", "vert.png" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNWSE] = LoadCurAniPng(dir,
                    new string[] { "nwse.cur", "nwse.ani", "nwse_xl.cur", "nwse_l.cur", "sizenwse.cur" },
                    15, 15, false);

                _assets[CursorShapeType.SizeNESW] = LoadCurAniPng(dir,
                    new string[] { "nesw.cur", "nesw.ani", "nesw_xl.cur", "nesw_l.cur", "sizenesw.cur" },
                    15, 15, false);

                _assets[CursorShapeType.SizeAll] = LoadCurAniPng(dir,
                    new string[] { "move.cur", "move.ani", "move_xl.cur", "move_l.cur", "sizeall.cur", "size.cur" },
                    15, 15, false);

                _assets[CursorShapeType.Wait] = LoadCurAniPng(dir,
                    new string[] { "working.ani", "busy.ani", "working_xl.ani", "working_l.ani", "busy.cur", "working.cur", "wait.ani", "wait.cur" },
                    15, 15, false);

                _assets[CursorShapeType.No] = LoadCurAniPng(dir,
                    new string[] { "unavail.cur", "unavail.ani", "unavail_xl.cur", "unavail_l.cur", "no.cur", "no.ani" },
                    15, 15, false);

                _assets[CursorShapeType.Help] = LoadCurAniPng(dir,
                    new string[] { "helpsel.cur", "helpsel.ani", "helpsel_xl.cur", "helpsel_l.cur", "help.cur", "help.ani" },
                    4, 4, true);
            }
        }

        private CursorAsset LoadCurAniPng(string dir, string[] candidates, int fallbackHx, int fallbackHy, bool allowRot)
        {
            foreach (string file in candidates)
            {
                string path = Path.Combine(dir, file);
                if (!File.Exists(path)) continue;

                string ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".cur" || ext == ".ani")
                {
                    // Load high-resolution 128x128 image if available in the file
                    IntPtr hCur = LoadImage(IntPtr.Zero, path, IMAGE_CURSOR, 128, 128, LR_LOADFROMFILE);
                    if (hCur == IntPtr.Zero)
                    {
                        hCur = LoadCursorFromFile(path);
                    }

                    if (hCur != IntPtr.Zero)
                    {
                        int curW = 32;
                        int curH = 32;
                        int hx = fallbackHx;
                        int hy = fallbackHy;
                        CursorHook.ICONINFO ii;
                        if (GetIconInfo(hCur, out ii))
                        {
                            hx = ii.xHotspot;
                            hy = ii.yHotspot;

                            BITMAP bm;
                            if (ii.hbmColor != IntPtr.Zero)
                            {
                                if (GetObject(ii.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out bm) != 0)
                                {
                                    curW = Math.Max(16, bm.bmWidth);
                                    curH = Math.Max(16, bm.bmHeight);
                                }
                                DeleteObject(ii.hbmColor);
                            }
                            else if (ii.hbmMask != IntPtr.Zero)
                            {
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
                            if (ext == ".cur") break; // static cursor only needs 1 frame
                        }

                        DestroyIcon(hCur);

                        if (frames.Count > 0)
                        {
                            return new CursorAsset(frames, hx, hy, curW, curH, allowRot);
                        }
                    }
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
                            return new CursorAsset(frames, fallbackHx, fallbackHy, img.Width, img.Height, allowRot);
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        public void Render(Graphics g, int canvasSize, CursorShapeType shape, ModeResult result, AppConfig config, long nowMs)
        {
            if (g == null) return;
            float totalScale = result.Scale * (config != null ? config.CursorScale : 1.0f);
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
                float hx = asset.BaseHotspotX;
                float hy = asset.BaseHotspotY;
                float baseW = 32.0f;
                float baseH = 32.0f;
                if (asset.NativeWidth > 0 && asset.NativeHeight > 0 && asset.NativeWidth != asset.NativeHeight)
                {
                    baseH = 32.0f * ((float)asset.NativeHeight / asset.NativeWidth);
                }
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
