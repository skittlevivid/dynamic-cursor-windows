using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicCursors
{
    public enum CursorShapeType
    {
        Arrow,
        Hand,
        IBeam,
        SizeWE,
        SizeNS,
        SizeNWSE,
        SizeNESW,
        SizeAll,
        Cross,
        Wait,
        Help,
        No,
        Unknown
    }

    public static class CursorHook
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public const int CURSOR_SHOWING = 0x00000001;
        public const int CURSOR_SUPPRESSED = 0x00000002;
        public const uint SPI_SETCURSORS = 0x0057;

        public const uint OCR_NORMAL = 32512;
        public const uint OCR_IBEAM = 32513;
        public const uint OCR_WAIT = 32514;
        public const uint OCR_CROSS = 32515;
        public const uint OCR_UP = 32516;
        public const uint OCR_SIZENWSE = 32642;
        public const uint OCR_SIZENESW = 32643;
        public const uint OCR_SIZEWE = 32644;
        public const uint OCR_SIZENS = 32645;
        public const uint OCR_SIZEALL = 32646;
        public const uint OCR_NO = 32648;
        public const uint OCR_HAND = 32649;
        public const uint OCR_APPSTARTING = 32650;
        public const uint OCR_HELP = 32651;

        public const int IDC_ARROW = 32512;
        public const int IDC_IBEAM = 32513;
        public const int IDC_WAIT = 32514;
        public const int IDC_CROSS = 32515;
        public const int IDC_UPARROW = 32516;
        public const int IDC_SIZE = 32640;
        public const int IDC_ICON = 32641;
        public const int IDC_SIZENWSE = 32642;
        public const int IDC_SIZENESW = 32643;
        public const int IDC_SIZEWE = 32644;
        public const int IDC_SIZENS = 32645;
        public const int IDC_SIZEALL = 32646;
        public const int IDC_NO = 32648;
        public const int IDC_HAND = 32649;
        public const int IDC_APPSTARTING = 32650;
        public const int IDC_HELP = 32651;

        private static readonly uint[] AllOcrIds = new uint[]
        {
            OCR_NORMAL, OCR_IBEAM, OCR_HAND, OCR_SIZEWE, OCR_SIZENS,
            OCR_SIZENWSE, OCR_SIZENESW, OCR_SIZEALL, OCR_CROSS,
            OCR_WAIT, OCR_APPSTARTING, OCR_NO, OCR_HELP, OCR_UP
        };

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool GetClipCursor(out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr OpenInputDesktop(int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        public static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        public static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const uint GA_ROOT = 2;
        private static readonly uint CurrentPid = (uint)Process.GetCurrentProcess().Id;
        private static readonly Dictionary<uint, bool> ShellPidCache = new Dictionary<uint, bool>();
        private static readonly StringBuilder ClassNameBuffer = new StringBuilder(128);

        private static bool _systemCursorHidden = false;
        private static IntPtr _hBlankCursor = IntPtr.Zero;

        private static IntPtr _hIdcArrow;
        private static IntPtr _hIdcHand;
        private static IntPtr _hIdcIBeam;
        private static IntPtr _hIdcCross;
        private static IntPtr _hIdcSizeWE;
        private static IntPtr _hIdcSizeNS;
        private static IntPtr _hIdcSizeAll;
        private static IntPtr _hIdcSizeNWSE;
        private static IntPtr _hIdcSizeNESW;
        private static IntPtr _hIdcWait;
        private static IntPtr _hIdcAppStarting;
        private static IntPtr _hIdcNo;
        private static IntPtr _hIdcHelp;

        static CursorHook()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreSystemCursors();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => RestoreSystemCursors();

            try
            {
                _hIdcArrow = LoadCursor(IntPtr.Zero, IDC_ARROW);
                _hIdcHand = LoadCursor(IntPtr.Zero, IDC_HAND);
                _hIdcIBeam = LoadCursor(IntPtr.Zero, IDC_IBEAM);
                _hIdcCross = LoadCursor(IntPtr.Zero, IDC_CROSS);
                _hIdcSizeWE = LoadCursor(IntPtr.Zero, IDC_SIZEWE);
                _hIdcSizeNS = LoadCursor(IntPtr.Zero, IDC_SIZENS);
                _hIdcSizeAll = LoadCursor(IntPtr.Zero, IDC_SIZEALL);
                _hIdcSizeNWSE = LoadCursor(IntPtr.Zero, IDC_SIZENWSE);
                _hIdcSizeNESW = LoadCursor(IntPtr.Zero, IDC_SIZENESW);
                _hIdcWait = LoadCursor(IntPtr.Zero, IDC_WAIT);
                _hIdcAppStarting = LoadCursor(IntPtr.Zero, IDC_APPSTARTING);
                _hIdcNo = LoadCursor(IntPtr.Zero, IDC_NO);
                _hIdcHelp = LoadCursor(IntPtr.Zero, IDC_HELP);
            }
            catch { }
        }

        public static void EnsureDesktopAccess()
        {
            try
            {
                IntPtr hdesk = OpenInputDesktop(0, false, 0x01FF);
                if (hdesk != IntPtr.Zero)
                {
                    SetThreadDesktop(hdesk);
                }
            }
            catch { }
        }

        public static bool HideSystemCursors()
        {
            if (_systemCursorHidden) return true;

            EnsureDesktopAccess();

            if (_hBlankCursor == IntPtr.Zero)
            {
                byte[] andMask = new byte[128];
                for (int i = 0; i < 128; i++) andMask[i] = 0xFF; // Transparent mask
                byte[] xorMask = new byte[128]; // All zeros

                _hBlankCursor = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, xorMask);
            }

            if (_hBlankCursor == IntPtr.Zero) return false;

            foreach (uint id in AllOcrIds)
            {
                IntPtr copy = CopyIcon(_hBlankCursor);
                if (copy != IntPtr.Zero)
                {
                    SetSystemCursor(copy, id);
                }
            }

            _systemCursorHidden = true;
            return true;
        }

        public static void RestoreSystemCursors()
        {
            try
            {
                EnsureDesktopAccess();
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                _systemCursorHidden = false;

                if (_hBlankCursor != IntPtr.Zero)
                {
                    DestroyIcon(_hBlankCursor);
                    _hBlankCursor = IntPtr.Zero;
                }
            }
            catch { }
        }

        public static bool ForceHideSystemCursors()
        {
            _systemCursorHidden = false;
            return HideSystemCursors();
        }

        public static bool IsShellOrMenuWindow(POINT pt)
        {
            try
            {
                IntPtr hwnd = WindowFromPoint(pt);
                if (hwnd == IntPtr.Zero) return false;

                IntPtr rootHwnd = GetAncestor(hwnd, GA_ROOT);
                if (rootHwnd == IntPtr.Zero) rootHwnd = hwnd;

                uint pid = 0;
                GetWindowThreadProcessId(rootHwnd, out pid);
                if (pid == 0) return false;

                // 1. Any window belonging to our own process (Tray context menu, Settings form)
                if (pid == CurrentPid) return true;

                // 2. Class name checks (context menus, tray overflow flyouts)
                lock (ClassNameBuffer)
                {
                    ClassNameBuffer.Length = 0;
                    GetClassName(rootHwnd, ClassNameBuffer, 128);
                    string rootCls = ClassNameBuffer.ToString();
                    if (IsShellOrMenuClass(rootCls)) return true;

                    if (rootHwnd != hwnd)
                    {
                        ClassNameBuffer.Length = 0;
                        GetClassName(hwnd, ClassNameBuffer, 128);
                        string childCls = ClassNameBuffer.ToString();
                        if (IsShellOrMenuClass(childCls)) return true;
                    }
                }

                // 3. Process check (cached)
                bool isShell;
                lock (ShellPidCache)
                {
                    if (!ShellPidCache.TryGetValue(pid, out isShell))
                    {
                        try
                        {
                            string pname = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
                            isShell = (pname == "startmenuexperiencehost" ||
                                       pname == "searchhost" ||
                                       pname == "searchapp" ||
                                       pname == "shellexperiencehost" ||
                                       pname == "shellhost");
                        }
                        catch
                        {
                            isShell = false;
                        }
                        ShellPidCache[pid] = isShell;
                    }
                }

                return isShell;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsShellOrMenuClass(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return false;

            // Win32 context menus & modern XAML popups
            if (cls == "#32768" || cls == "Xaml_WindowedPopupClass") return true;

            // Tray overflow flyouts
            if (cls.IndexOf("NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cls.IndexOf("TopLevelWindowForOverflow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        public static bool IsShowing(out POINT pos, out CursorShapeType shape, out IntPtr hCursor)
        {
            pos = new POINT();
            shape = CursorShapeType.Arrow;
            hCursor = IntPtr.Zero;

            // 1. Always get true mouse cursor position
            if (!GetCursorPos(out pos))
            {
                return false;
            }

            CURSORINFO ci = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
            if (GetCursorInfo(ref ci))
            {
                hCursor = ci.hCursor;
                if (ci.ptScreenPos.x != 0 || ci.ptScreenPos.y != 0)
                {
                    pos = ci.ptScreenPos;
                }

                // Normal showing cursor
                if ((ci.flags & CURSOR_SHOWING) != 0)
                {
                    shape = IdentifyShape(ci.hCursor);
                    return true;
                }

                // If suppressed by Win key, keyboard navigation, or touch: keep showing so cursor doesn't vanish
                if ((ci.flags & CURSOR_SUPPRESSED) != 0)
                {
                    shape = ci.hCursor != IntPtr.Zero ? IdentifyShape(ci.hCursor) : CursorShapeType.Arrow;
                    return true;
                }

                // If flags == 0 (hidden): check if a 3D/FPS game has locked/clipped mouse cursor
                RECT clip;
                if (GetClipCursor(out clip))
                {
                    int clipW = clip.Right - clip.Left;
                    int clipH = clip.Bottom - clip.Top;
                    // Locked to tiny box (FPS shooter captured mouse) -> hide cursor
                    if (clipW > 0 && clipW <= 4 && clipH > 0 && clipH <= 4)
                    {
                        return false;
                    }
                }

                // Not an FPS lock -> keep showing Arrow cursor (Start Menu, Windows shell UI, etc.)
                shape = ci.hCursor != IntPtr.Zero ? IdentifyShape(ci.hCursor) : CursorShapeType.Arrow;
                return true;
            }

            // Fallback: we have mouse pos, default to Arrow
            return true;
        }

        private static CursorShapeType IdentifyShape(IntPtr hCursor)
        {
            if (hCursor == IntPtr.Zero) return CursorShapeType.Arrow;

            if (hCursor == _hIdcArrow) return CursorShapeType.Arrow;
            if (hCursor == _hIdcHand) return CursorShapeType.Hand;
            if (hCursor == _hIdcIBeam) return CursorShapeType.IBeam;
            if (hCursor == _hIdcCross) return CursorShapeType.Cross;
            if (hCursor == _hIdcSizeWE) return CursorShapeType.SizeWE;
            if (hCursor == _hIdcSizeNS) return CursorShapeType.SizeNS;
            if (hCursor == _hIdcSizeAll) return CursorShapeType.SizeAll;
            if (hCursor == _hIdcSizeNWSE) return CursorShapeType.SizeNWSE;
            if (hCursor == _hIdcSizeNESW) return CursorShapeType.SizeNESW;
            if (hCursor == _hIdcWait) return CursorShapeType.Wait;
            if (hCursor == _hIdcAppStarting) return CursorShapeType.Wait;
            if (hCursor == _hIdcNo) return CursorShapeType.No;
            if (hCursor == _hIdcHelp) return CursorShapeType.Help;

            // Inspect hotspot for custom cursors
            ICONINFO ii;
            if (GetIconInfo(hCursor, out ii))
            {
                try
                {
                    if (ii.xHotspot <= 5 && ii.yHotspot <= 5)
                        return CursorShapeType.Arrow;
                    if (ii.xHotspot >= 7 && ii.xHotspot <= 14 && ii.yHotspot <= 4)
                        return CursorShapeType.Hand;
                    if (ii.xHotspot >= 12 && ii.xHotspot <= 18 && ii.yHotspot >= 12 && ii.yHotspot <= 18)
                        return CursorShapeType.IBeam;
                }
                finally
                {
                    if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
                    if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
                }
            }

            return CursorShapeType.Arrow;
        }
    }
}
