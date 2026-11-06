using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SplitExplorer
{
    #region Native

    public static class Native
    {
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYFIRST = 0x0100;
        public const int WM_KEYLAST = 0x0109;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;

        public const int WS_CLIPCHILDREN = 0x02000000;
        public const int WS_CLIPSIBLINGS = 0x04000000;

        public const uint SIGDN_NORMALDISPLAY = 0x00000000;
        public const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
        public const uint SIGDN_FILESYSPATH = 0x80058000;

        public const int FVM_AUTO = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FOLDERSETTINGS
        {
            public uint ViewMode;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr Hwnd;
            public int Msg;
            public IntPtr WParam;
            public IntPtr LParam;
            public int Time;
            public int PtX;
            public int PtY;

            public static MSG FromMessage(Message message)
            {
                MSG result = new MSG();
                result.Hwnd = message.HWnd;
                result.Msg = message.Msg;
                result.WParam = message.WParam;
                result.LParam = message.LParam;
                result.Time = GetMessageTime();
                uint position = GetMessagePos();
                result.PtX = unchecked((short)(position & 0xFFFF));
                result.PtY = unchecked((short)((position >> 16) & 0xFFFF));
                return result;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItem ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHGetNameFromIDList(
            IntPtr pidl,
            uint sigdnName,
            out IntPtr ppszName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern int GetMessageTime();

        [DllImport("user32.dll")]
        private static extern uint GetMessagePos();

        [DllImport("user32.dll")]
        public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        public static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
            catch (Exception)
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch (Exception)
            {
            }
        }

        public static IntPtr FindViewFocusWindow(IntPtr parent)
        {
            IntPtr fallback = IntPtr.Zero;
            IntPtr directUi = IntPtr.Zero;
            IntPtr defView = IntPtr.Zero;

            EnumChildProc proc = delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd) || !IsWindowEnabled(hWnd))
                {
                    return true;
                }

                RECT rect;
                if (!GetWindowRect(hWnd, out rect))
                {
                    return true;
                }
                if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
                {
                    return true;
                }

                string cls = GetWindowClass(hWnd);
                if (cls.IndexOf("ScrollBar", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (cls.IndexOf("DirectUIHWND", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (directUi == IntPtr.Zero)
                    {
                        directUi = hWnd;
                    }
                }
                else if (cls.IndexOf("SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (defView == IntPtr.Zero)
                    {
                        defView = hWnd;
                    }
                }

                if (fallback == IntPtr.Zero)
                {
                    fallback = hWnd;
                }
                return true;
            };

            EnumChildWindows(parent, proc, IntPtr.Zero);
            if (directUi != IntPtr.Zero)
            {
                return directUi;
            }
            if (defView != IntPtr.Zero)
            {
                return defView;
            }
            return fallback;
        }

        private static string GetWindowClass(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            int len = GetClassName(hWnd, sb, sb.Capacity);
            if (len <= 0)
            {
                return string.Empty;
            }
            return sb.ToString();
        }

        public static float GetScale(Control control)
        {
            if (control == null)
            {
                return 1f;
            }
            try
            {
                if (control.IsHandleCreated)
                {
                    using (Graphics g = Graphics.FromHwnd(control.Handle))
                    {
                        return g.DpiX / 96f;
                    }
                }
            }
            catch (Exception)
            {
            }
            return 1f;
        }

        public static int Scale(Control control, int pixels)
        {
            int scaled = (int)Math.Round(pixels * GetScale(control));
            return scaled < 1 ? 1 : scaled;
        }
    }

    #endregion
}
