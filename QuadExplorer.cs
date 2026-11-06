using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QuadExplorer
{
    #region Program

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Native.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.OleRequired();

            AppState state = StateStore.Load();
            string[] initial = StateStore.NormalizeArguments(args);
            MainForm form = new MainForm(state, initial);
            Application.AddMessageFilter(form);
            Application.Run(form);
        }
    }

    #endregion

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

    #region ShellInterop

    internal static class ShellIds
    {
        public static readonly Guid ExplorerBrowserClsid =
            new Guid("71F96385-DDD6-48D3-A0C1-AE06E8B055FB");

        public static readonly Guid IID_IExplorerBrowser =
            new Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6");

        public static readonly Guid IID_IShellItem =
            new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    }

    [ComImport]
    [Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowser
    {
        void Initialize(IntPtr hwndParent, ref Native.RECT prc, ref Native.FOLDERSETTINGS pfs);
        void Destroy();
        void SetRect(ref IntPtr phdwp, Native.RECT rcBrowser);
        void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string pszPropertyBag);
        void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string pszEmptyText);
        void SetFolderSettings(ref Native.FOLDERSETTINGS pfs);
        void Advise(IExplorerBrowserEvents psbe, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint dwFlag);
        void GetOptions(out uint pdwFlag);
        void BrowseToIDList(IntPtr pidl, uint uFlags);
        void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
        void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int dwFlags);
        void RemoveAll();
        void GetCurrentView(ref Guid riid, out IntPtr ppv);
    }

    [ComImport]
    [Guid("361BBDC7-E6EE-4E13-BE58-58E2240C810F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowserEvents
    {
        void OnNavigationPending(IntPtr pidlFolder);
        void OnViewCreated([MarshalAs(UnmanagedType.IUnknown)] object psv);
        void OnNavigationComplete(IntPtr pidlFolder);
        void OnNavigationFailed(IntPtr pidlFolder);
    }

    [ComImport]
    [Guid("68284FAA-6A48-11D0-8C78-00C04FD918B4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInputObject
    {
        [PreserveSig]
        int UIActivateIO(int fActivate, ref Native.MSG pMsg);

        [PreserveSig]
        int HasFocusIO();

        [PreserveSig]
        int TranslateAcceleratorIO(ref Native.MSG pMsg);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class ExplorerBrowserEvents : IExplorerBrowserEvents
    {
        private ExplorerHost _owner;

        internal ExplorerBrowserEvents(ExplorerHost owner)
        {
            _owner = owner;
        }

        internal void Detach()
        {
            _owner = null;
        }

        public void OnNavigationPending(IntPtr pidlFolder)
        {
        }

        public void OnViewCreated(object psv)
        {
        }

        public void OnNavigationComplete(IntPtr pidlFolder)
        {
            try
            {
                ExplorerHost owner = _owner;
                if (owner != null)
                {
                    owner.HandleNavigationComplete(pidlFolder);
                }
            }
            catch (Exception)
            {
            }
        }

        public void OnNavigationFailed(IntPtr pidlFolder)
        {
            try
            {
                ExplorerHost owner = _owner;
                if (owner != null)
                {
                    owner.HandleNavigationFailed(pidlFolder);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class ComUtil
    {
        public static void Release(object comObject)
        {
            if (comObject == null)
            {
                return;
            }
            try
            {
                int count;
                do
                {
                    count = Marshal.ReleaseComObject(comObject);
                }
                while (count > 0);
            }
            catch (Exception)
            {
            }
        }
    }

    #endregion

    #region ExplorerHost

    internal sealed class ExplorerHost : IDisposable
    {
        private IExplorerBrowser _browser;
        private ExplorerBrowserEvents _events;
        private Control _hostControl;
        private uint _cookie;
        private bool _advised;
        private bool _disposed;
        private bool _hasBounds;
        private Native.RECT _lastBounds;
        private string _parsingName = string.Empty;
        private string _displayName = string.Empty;
        private string _fileSysPath = string.Empty;

        public event Action<ExplorerHost> NavigationComplete;
        public event Action<ExplorerHost> NavigationFailed;

        public string CurrentParsingName { get { return _parsingName; } }
        public string CurrentDisplayName { get { return _displayName; } }
        public string CurrentFileSysPath { get { return _fileSysPath; } }

        public ExplorerHost(Control hostControl)
        {
            if (hostControl == null)
            {
                throw new ArgumentNullException("hostControl");
            }

            _hostControl = hostControl;
            if (!_hostControl.IsHandleCreated)
            {
                // Accessing Handle forces creation even while the tab is hidden.
                IntPtr forcedHandle = _hostControl.Handle;
                if (forcedHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("The host control has no window handle.");
                }
            }

            Native.RECT rect = GetHostRect();
            Native.FOLDERSETTINGS settings = new Native.FOLDERSETTINGS();
            settings.ViewMode = unchecked((uint)Native.FVM_AUTO);
            settings.Flags = 0;

            try
            {
                Type type = Type.GetTypeFromCLSID(ShellIds.ExplorerBrowserClsid);
                object instance = Activator.CreateInstance(type);
                _browser = (IExplorerBrowser)instance;
                _browser.Initialize(_hostControl.Handle, ref rect, ref settings);
                _browser.SetOptions(0);

                _events = new ExplorerBrowserEvents(this);
                _browser.Advise(_events, out _cookie);
                _advised = true;

                _lastBounds = rect;
                _hasBounds = true;
            }
            catch (Exception)
            {
                if (_browser != null)
                {
                    try
                    {
                        _browser.Destroy();
                    }
                    catch (Exception)
                    {
                    }
                    ComUtil.Release(_browser);
                    _browser = null;
                }
                _events = null;
                throw;
            }
        }

        public void Navigate(string parsingName)
        {
            if (_disposed || _browser == null)
            {
                throw new ObjectDisposedException("ExplorerHost");
            }
            if (string.IsNullOrEmpty(parsingName))
            {
                throw new ArgumentException("A shell parsing name is required.", "parsingName");
            }

            IShellItem item = CreateItem(parsingName);
            try
            {
                _browser.BrowseToObject(item, 0);
            }
            finally
            {
                ComUtil.Release(item);
            }
        }

        public void NavigateToParent()
        {
            if (_disposed || _browser == null)
            {
                throw new ObjectDisposedException("ExplorerHost");
            }
            if (string.IsNullOrEmpty(_parsingName))
            {
                return;
            }

            IShellItem item = null;
            IShellItem parent = null;
            try
            {
                item = CreateItem(_parsingName);
                item.GetParent(out parent);
                if (parent == null)
                {
                    throw new COMException("The current location has no parent.");
                }
                _browser.BrowseToObject(parent, 0);
            }
            finally
            {
                ComUtil.Release(parent);
                ComUtil.Release(item);
            }
        }

        internal bool TranslateAccelerator(ref Native.MSG msg)
        {
            if (_disposed || _browser == null)
            {
                return false;
            }

            try
            {
                IInputObject input = _browser as IInputObject;
                if (input == null)
                {
                    return false;
                }
                if (input.HasFocusIO() != 0)
                {
                    return false;
                }
                return input.TranslateAcceleratorIO(ref msg) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void SetBounds(Rectangle rcInHost)
        {
            if (_disposed || _browser == null)
            {
                return;
            }

            int width = rcInHost.Width;
            int height = rcInHost.Height;
            if (width < 1)
            {
                width = 1;
            }
            if (height < 1)
            {
                height = 1;
            }

            Native.RECT rect = new Native.RECT(0, 0, width, height);
            if (_hasBounds &&
                _lastBounds.Left == rect.Left &&
                _lastBounds.Top == rect.Top &&
                _lastBounds.Right == rect.Right &&
                _lastBounds.Bottom == rect.Bottom)
            {
                return;
            }

            IntPtr hdwp = IntPtr.Zero;
            _browser.SetRect(ref hdwp, rect);
            _lastBounds = rect;
            _hasBounds = true;
        }

        internal void HandleNavigationComplete(IntPtr pidl)
        {
            if (_disposed)
            {
                return;
            }

            UpdateNames(pidl);
            Action<ExplorerHost> handler = NavigationComplete;
            if (handler != null)
            {
                try
                {
                    handler(this);
                }
                catch (Exception)
                {
                }
            }
        }

        internal void HandleNavigationFailed(IntPtr pidl)
        {
            if (_disposed)
            {
                return;
            }

            Action<ExplorerHost> handler = NavigationFailed;
            if (handler != null)
            {
                try
                {
                    handler(this);
                }
                catch (Exception)
                {
                }
            }
        }

        private void UpdateNames(IntPtr pidl)
        {
            string parsing = GetName(pidl, Native.SIGDN_DESKTOPABSOLUTEPARSING);
            if (parsing.Length == 0)
            {
                parsing = GetName(pidl, Native.SIGDN_FILESYSPATH);
            }
            if (parsing.Length == 0)
            {
                parsing = GetName(pidl, Native.SIGDN_NORMALDISPLAY);
            }

            string display = GetName(pidl, Native.SIGDN_NORMALDISPLAY);
            if (display.Length == 0)
            {
                display = GetName(pidl, Native.SIGDN_FILESYSPATH);
            }
            if (display.Length == 0)
            {
                display = parsing;
            }

            _parsingName = parsing;
            _displayName = display;
            _fileSysPath = GetName(pidl, Native.SIGDN_FILESYSPATH);
        }

        private static string GetName(IntPtr pidl, uint sigdn)
        {
            if (pidl == IntPtr.Zero)
            {
                return string.Empty;
            }

            IntPtr text = IntPtr.Zero;
            try
            {
                int hr = Native.SHGetNameFromIDList(pidl, sigdn, out text);
                if (hr < 0 || text == IntPtr.Zero)
                {
                    return string.Empty;
                }
                string value = Marshal.PtrToStringUni(text);
                return value == null ? string.Empty : value;
            }
            catch (Exception)
            {
                return string.Empty;
            }
            finally
            {
                if (text != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(text);
                }
            }
        }

        private static IShellItem CreateItem(string parsingName)
        {
            Guid riid = ShellIds.IID_IShellItem;
            IShellItem item;
            int hr = Native.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref riid, out item);
            if (hr < 0)
            {
                throw new COMException("The shell item could not be created: " + parsingName, hr);
            }
            if (item == null)
            {
                throw new COMException("The shell item could not be created: " + parsingName);
            }
            return item;
        }

        private Native.RECT GetHostRect()
        {
            int width = _hostControl.ClientSize.Width;
            int height = _hostControl.ClientSize.Height;
            if (width < 1)
            {
                width = 1;
            }
            if (height < 1)
            {
                height = 1;
            }
            return new Native.RECT(0, 0, width, height);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            ExplorerBrowserEvents events = _events;
            _events = null;
            if (events != null)
            {
                events.Detach();
            }

            if (_browser != null)
            {
                if (_advised)
                {
                    try
                    {
                        _browser.Unadvise(_cookie);
                    }
                    catch (Exception)
                    {
                    }
                    _advised = false;
                }

                try
                {
                    _browser.Destroy();
                }
                catch (Exception)
                {
                }

                ComUtil.Release(_browser);
                _browser = null;
            }

            _hostControl = null;
            NavigationComplete = null;
            NavigationFailed = null;
        }
    }

    #endregion

    #region Host Controls

    internal sealed class HostPanel : Control
    {
        public HostPanel()
        {
            SetStyle(
                ControlStyles.Opaque |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = SystemColors.Window;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SystemColors.Window);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= Native.WS_CLIPCHILDREN | Native.WS_CLIPSIBLINGS;
                return cp;
            }
        }
    }

    internal sealed class ViewPanel : Control
    {
        public ViewPanel()
        {
            SetStyle(
                ControlStyles.Opaque |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = SystemColors.Window;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SystemColors.Window);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= Native.WS_CLIPCHILDREN | Native.WS_CLIPSIBLINGS;
                return cp;
            }
        }
    }

    #endregion

    #region TabStrip

    internal sealed class TabStrip : Control
    {
        private readonly Pane _pane;
        private readonly List<Rectangle> _tabBounds = new List<Rectangle>();
        private readonly List<Rectangle> _closeBounds = new List<Rectangle>();
        private Rectangle _plusBounds;
        private int _hoverTab = -1;
        private bool _hoverClose;
        private bool _hoverPlus;
        private float _scale = 1f;
        private bool _layoutDirty = true;

        public TabStrip(Pane pane)
        {
            _pane = pane;
            SetStyle(
                ControlStyles.Opaque |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = SystemColors.Control;
            TabStop = false;
        }

        public void InvalidateLayout()
        {
            _layoutDirty = true;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Native.GetScale(this);
            _layoutDirty = true;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _layoutDirty = true;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _layoutDirty = true;
            Invalidate();
        }

        private int Scale(int pixels)
        {
            int value = (int)Math.Round(pixels * _scale);
            return value < 1 ? 1 : value;
        }

        private void EnsureLayout()
        {
            int count = _pane.TabCount;
            if (!_layoutDirty &&
                _tabBounds.Count == count)
            {
                return;
            }

            _tabBounds.Clear();
            _closeBounds.Clear();

            int plusWidth = Scale(24);
            int tabArea = Width - plusWidth;
            if (tabArea < 0)
            {
                tabArea = 0;
            }
            _plusBounds = new Rectangle(Width - plusWidth, 0, plusWidth, Height);

            int minWidth = Scale(80);
            int maxWidth = Scale(200);
            int closeWidth = Scale(18);
            int padding = Scale(9);

            List<int> widths = new List<int>();
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                string title = _pane.GetTabTitle(i);
                if (title == null)
                {
                    title = string.Empty;
                }
                Size textSize = TextRenderer.MeasureText(
                    title,
                    Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                int desired = textSize.Width + padding * 2 + closeWidth;
                if (desired < minWidth)
                {
                    desired = minWidth;
                }
                if (desired > maxWidth)
                {
                    desired = maxWidth;
                }
                widths.Add(desired);
                total += desired;
            }

            int guard = 0;
            while (total > tabArea && guard < 20000)
            {
                int widest = -1;
                int widestValue = minWidth;
                for (int i = 0; i < widths.Count; i++)
                {
                    if (widths[i] > widestValue)
                    {
                        widestValue = widths[i];
                        widest = i;
                    }
                }
                if (widest < 0)
                {
                    break;
                }
                widths[widest] = widths[widest] - 1;
                total--;
                guard++;
            }

            int x = 0;
            for (int i = 0; i < widths.Count; i++)
            {
                Rectangle tabRect = new Rectangle(x, 0, widths[i], Height);
                _tabBounds.Add(tabRect);
                _closeBounds.Add(new Rectangle(
                    tabRect.Right - closeWidth - Scale(2),
                    Scale(4),
                    closeWidth,
                    Math.Max(1, Height - Scale(8))));
                x += widths[i];
            }

            _layoutDirty = false;
        }

        private void HitTest(Point point, out int tabIndex, out bool onClose, out bool onPlus)
        {
            EnsureLayout();
            tabIndex = -1;
            onClose = false;
            onPlus = _plusBounds.Contains(point);
            if (onPlus || point.X >= _plusBounds.Left)
            {
                return;
            }

            for (int i = 0; i < _tabBounds.Count; i++)
            {
                if (_tabBounds[i].Contains(point))
                {
                    tabIndex = i;
                    onClose = _closeBounds[i].Contains(point);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureLayout();
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Control);

            for (int i = 0; i < _tabBounds.Count; i++)
            {
                Rectangle rect = _tabBounds[i];
                bool selected = i == _pane.SelectedTabIndex;
                bool hovered = i == _hoverTab;

                Color backColor = selected ? Color.White : SystemColors.Control;
                if (hovered && !selected)
                {
                    backColor = SystemColors.ControlLight;
                }

                using (SolidBrush brush = new SolidBrush(backColor))
                {
                    g.FillRectangle(brush, rect);
                }

                using (Pen border = new Pen(SystemColors.ControlDark))
                {
                    g.DrawLine(border, rect.Right - 1, rect.Top + Scale(4), rect.Right - 1, rect.Bottom - Scale(4));
                }

                int closeWidth = Scale(18);
                Rectangle closeRect = _closeBounds[i];
                Rectangle textRect = new Rectangle(
                    rect.Left + Scale(9),
                    rect.Top,
                    Math.Max(1, closeRect.Left - rect.Left - Scale(12)),
                    rect.Height);
                TextRenderer.DrawText(
                    g,
                    _pane.GetTabTitle(i),
                    Font,
                    textRect,
                    selected ? SystemColors.ControlText : SystemColors.GrayText,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine);

                bool closeHovered = hovered && _hoverClose;
                if (closeHovered)
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(232, 17, 35)))
                    {
                        g.FillRectangle(brush, closeRect);
                    }
                }

                int cx = closeRect.Left + closeRect.Width / 2;
                int cy = closeRect.Top + closeRect.Height / 2;
                int arm = Scale(3);
                Color closeColor = closeHovered ? Color.White : SystemColors.GrayText;
                using (Pen pen = new Pen(closeColor, Math.Max(1f, Scale(1))))
                {
                    g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                    g.DrawLine(pen, cx - arm, cy + arm, cx + arm, cy - arm);
                }

                if (selected)
                {
                    using (Pen pen = new Pen(Color.White, 2f))
                    {
                        g.DrawLine(pen, rect.Left + 1, rect.Bottom - 1, rect.Right - 2, rect.Bottom - 1);
                    }
                }
            }

            bool plusHovered = _hoverPlus;
            if (plusHovered)
            {
                using (SolidBrush brush = new SolidBrush(SystemColors.ControlLight))
                {
                    g.FillRectangle(brush, _plusBounds);
                }
            }

            TextRenderer.DrawText(
                g,
                "+",
                Font,
                _plusBounds,
                plusHovered ? SystemColors.ControlText : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);

            using (Pen bottom = new Pen(SystemColors.ControlDark))
            {
                g.DrawLine(bottom, 0, Height - 1, Width, Height - 1);
            }
        }

        private void UpdateHover(Point point)
        {
            EnsureLayout();
            int tabIndex;
            bool onClose;
            bool onPlus;
            HitTest(point, out tabIndex, out onClose, out onPlus);

            bool changed = tabIndex != _hoverTab || onClose != _hoverClose || onPlus != _hoverPlus;
            _hoverTab = tabIndex;
            _hoverClose = onClose;
            _hoverPlus = onPlus;
            Cursor = (tabIndex >= 0 && onClose) || onPlus ? Cursors.Hand : Cursors.Default;

            if (changed)
            {
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateHover(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverTab = -1;
            _hoverClose = false;
            _hoverPlus = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pane.ActivatePane();

            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle)
            {
                return;
            }

            int tabIndex;
            bool onClose;
            bool onPlus;
            HitTest(e.Location, out tabIndex, out onClose, out onPlus);

            if (e.Button == MouseButtons.Middle)
            {
                if (tabIndex >= 0)
                {
                    _pane.CloseTab(tabIndex);
                }
                return;
            }

            if (onPlus)
            {
                _pane.NewTab();
                return;
            }
            if (tabIndex >= 0)
            {
                if (onClose)
                {
                    _pane.CloseTab(tabIndex);
                }
                else
                {
                    _pane.SelectTab(tabIndex);
                }
            }
        }
    }

    #endregion

    #region Tab

    internal sealed class Tab : IDisposable
    {
        private readonly Pane _pane;
        private readonly List<string> _history = new List<string>();
        private HostPanel _hostPanel;
        private ExplorerHost _host;
        private string _title = "(unknown)";
        private string _requestedPath = string.Empty;
        private string _initialFallback = string.Empty;
        private int _historyIndex = -1;
        private int _historyRevertIndex = -1;
        private bool _suppressHistory;
        private bool _disposed;

        internal Tab(Pane pane, HostPanel hostPanel)
        {
            _pane = pane;
            _hostPanel = hostPanel;
        }

        internal HostPanel HostPanelControl
        {
            get { return _hostPanel; }
        }

        internal string Title
        {
            get { return _title; }
        }

        internal string CurrentParsingName
        {
            get
            {
                if (_host != null && _host.CurrentParsingName.Length > 0)
                {
                    return _host.CurrentParsingName;
                }
                return _requestedPath;
            }
        }

        internal string CurrentAddress
        {
            get
            {
                if (_host != null && _host.CurrentFileSysPath.Length > 0)
                {
                    return _host.CurrentFileSysPath;
                }
                if (_host != null && _host.CurrentParsingName.Length > 0)
                {
                    return _host.CurrentParsingName;
                }
                return _requestedPath;
            }
        }

        internal bool CanGoBack
        {
            get { return _historyIndex > 0; }
        }

        internal bool CanGoForward
        {
            get { return _historyIndex >= 0 && _historyIndex < _history.Count - 1; }
        }

        internal void SetHostBounds(Rectangle bounds)
        {
            if (!_disposed && _host != null)
            {
                _host.SetBounds(bounds);
            }
        }

        internal bool TranslateAccelerator(ref Native.MSG msg)
        {
            return !_disposed && _host != null && _host.TranslateAccelerator(ref msg);
        }

        internal bool Start(string path, string fallbackPath)
        {
            if (_disposed || _hostPanel == null)
            {
                return false;
            }

            try
            {
                _host = new ExplorerHost(_hostPanel);
            }
            catch (Exception)
            {
                return false;
            }

            _host.NavigationComplete += OnHostNavigationComplete;
            _host.NavigationFailed += OnHostNavigationFailed;
            _initialFallback = fallbackPath == null ? string.Empty : fallbackPath;
            _requestedPath = path;
            _title = GuessTitle(path);
            NavigateInternal(path);
            return true;
        }

        private static string GuessTitle(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "(unknown)";
            }
            string trimmed = path.TrimEnd('\\', '/');
            int slash = trimmed.LastIndexOf('\\');
            if (slash < 0)
            {
                slash = trimmed.LastIndexOf('/');
            }
            string title = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
            return title.Length == 0 ? "(unknown)" : title;
        }

        private void OnHostNavigationComplete(ExplorerHost host)
        {
            if (_disposed)
            {
                return;
            }

            _initialFallback = string.Empty;
            string parsing = host.CurrentParsingName;
            if (parsing.Length > 0)
            {
                _requestedPath = parsing;
            }

            if (_suppressHistory)
            {
                _suppressHistory = false;
                _historyRevertIndex = -1;
                if (_historyIndex >= 0 && _historyIndex < _history.Count && parsing.Length > 0)
                {
                    _history[_historyIndex] = parsing;
                }
            }
            else if (parsing.Length > 0)
            {
                string current = _historyIndex >= 0 && _historyIndex < _history.Count
                    ? _history[_historyIndex]
                    : string.Empty;

                if (!string.Equals(current, parsing, StringComparison.OrdinalIgnoreCase))
                {
                    if (_historyIndex < _history.Count - 1)
                    {
                        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                    }
                    _history.Add(parsing);
                    if (_history.Count > 100)
                    {
                        _history.RemoveAt(0);
                    }
                    _historyIndex = _history.Count - 1;
                }
            }

            string display = host.CurrentDisplayName;
            if (display.Length == 0)
            {
                display = parsing;
            }
            _title = display.Length == 0 ? "(unknown)" : display;

            if (!_disposed)
            {
                _pane.OnTabNavigated(this);
            }
        }

        private void OnHostNavigationFailed(ExplorerHost host)
        {
            HandleNavigationFailure();
        }

        private void NavigateInternal(string path)
        {
            if (_disposed || _host == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            _requestedPath = path;
            try
            {
                _host.Navigate(path);
            }
            catch (Exception)
            {
                HandleNavigationFailure();
            }
        }

        internal void NavigateTo(string path)
        {
            if (_disposed || _host == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            _suppressHistory = false;
            _historyRevertIndex = -1;
            _initialFallback = string.Empty;
            NavigateInternal(path);
        }

        internal void GoBack()
        {
            if (_disposed || _host == null || !CanGoBack)
            {
                return;
            }

            int previousIndex = _historyIndex;
            _historyIndex--;
            _suppressHistory = true;
            _historyRevertIndex = previousIndex;
            try
            {
                _host.Navigate(_history[_historyIndex]);
            }
            catch (Exception)
            {
                HandleNavigationFailure();
            }
        }

        internal void GoForward()
        {
            if (_disposed || _host == null || !CanGoForward)
            {
                return;
            }

            int previousIndex = _historyIndex;
            _historyIndex++;
            _suppressHistory = true;
            _historyRevertIndex = previousIndex;
            try
            {
                _host.Navigate(_history[_historyIndex]);
            }
            catch (Exception)
            {
                HandleNavigationFailure();
            }
        }

        internal void GoUp()
        {
            if (_disposed || _host == null)
            {
                return;
            }

            _initialFallback = string.Empty;
            _suppressHistory = false;
            _historyRevertIndex = -1;
            try
            {
                _host.NavigateToParent();
            }
            catch (Exception)
            {
                HandleNavigationFailure();
            }
        }

        private void HandleNavigationFailure()
        {
            if (_disposed)
            {
                return;
            }

            if (_suppressHistory)
            {
                _suppressHistory = false;
                if (_historyRevertIndex >= 0)
                {
                    _historyIndex = _historyRevertIndex;
                }
                _historyRevertIndex = -1;
            }

            string fallback = _initialFallback;
            _initialFallback = string.Empty;
            if (fallback.Length > 0)
            {
                ScheduleNavigate(fallback);
            }

            if (!_disposed)
            {
                _pane.OnTabNavigationFailed(this);
            }
        }

        private void ScheduleNavigate(string path)
        {
            if (_pane == null || _pane.IsDisposed || _pane.Handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                _pane.BeginInvoke(new MethodInvoker(delegate
                {
                    if (!_disposed)
                    {
                        NavigateInternal(path);
                    }
                }));
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_host != null)
            {
                _host.NavigationComplete -= OnHostNavigationComplete;
                _host.NavigationFailed -= OnHostNavigationFailed;
                _host.Dispose();
                _host = null;
            }

            if (_hostPanel != null)
            {
                _hostPanel.Dispose();
                _hostPanel = null;
            }
        }
    }

    #endregion

    #region Pane

    internal sealed class Pane : UserControl
    {
        private const int MaxTabs = 20;

        private readonly MainForm _main;
        private readonly int _index;
        private readonly TabStrip _tabStrip;
        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly Button _upButton;
        private readonly TextBox _addressBox;
        private readonly ViewPanel _viewHost;
        private readonly List<Tab> _tabs = new List<Tab>();
        private readonly List<string> _recentlyClosed = new List<string>();
        private readonly Timer _addressFlashTimer;

        private int _selectedIndex = -1;
        private bool _active;
        private bool _shutDown;
        private bool _layoutReady;
        private float _scale = 1f;
        private IntPtr _lastViewFocus = IntPtr.Zero;

        internal Pane(MainForm main, int index)
        {
            _main = main;
            _index = index;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = SystemColors.Control;
            Font = SystemFonts.MessageBoxFont;

            _tabStrip = new TabStrip(this);
            _tabStrip.Font = SystemFonts.MessageBoxFont;

            _backButton = CreateNavButton("\u25C0");
            _forwardButton = CreateNavButton("\u25B6");
            _upButton = CreateNavButton("\u25B2");

            _addressBox = new TextBox();
            _addressBox.BorderStyle = BorderStyle.FixedSingle;
            _addressBox.Font = SystemFonts.MessageBoxFont;
            _addressBox.BackColor = SystemColors.Window;

            _viewHost = new ViewPanel();

            _backButton.Click += delegate { GoBack(); };
            _forwardButton.Click += delegate { GoForward(); };
            _upButton.Click += delegate { GoUp(); };
            _addressBox.KeyDown += AddressBoxKeyDown;

            Controls.Add(_viewHost);
            Controls.Add(_tabStrip);
            Controls.Add(_backButton);
            Controls.Add(_forwardButton);
            Controls.Add(_upButton);
            Controls.Add(_addressBox);

            _addressFlashTimer = new Timer();
            _addressFlashTimer.Interval = 1200;
            _addressFlashTimer.Tick += delegate
            {
                _addressFlashTimer.Stop();
                _addressBox.BackColor = SystemColors.Window;
            };

            _layoutReady = true;
        }

        internal int PaneIndex
        {
            get { return _index; }
        }

        internal int TabCount
        {
            get { return _tabs.Count; }
        }

        internal int SelectedTabIndex
        {
            get { return _selectedIndex; }
        }

        internal Tab ActiveTab
        {
            get
            {
                if (_selectedIndex < 0 || _selectedIndex >= _tabs.Count)
                {
                    return null;
                }
                return _tabs[_selectedIndex];
            }
        }

        internal bool IsActivePane
        {
            get { return _active; }
        }

        private Button CreateNavButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.BackColor = SystemColors.Control;
            button.TabStop = false;
            button.Font = SystemFonts.MessageBoxFont;
            return button;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= Native.WS_CLIPCHILDREN | Native.WS_CLIPSIBLINGS;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Native.GetScale(this);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(SystemColors.Control);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int thickness = _active ? Scale(2) : 1;
            Color color = _active ? SystemColors.Highlight : SystemColors.ControlDark;
            using (SolidBrush brush = new SolidBrush(color))
            {
                e.Graphics.FillRectangle(brush, 0, 0, Width, thickness);
                e.Graphics.FillRectangle(brush, 0, Height - thickness, Width, thickness);
                e.Graphics.FillRectangle(brush, 0, 0, thickness, Height);
                e.Graphics.FillRectangle(brush, Width - thickness, 0, thickness, Height);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutChildren();
        }

        private int Scale(int pixels)
        {
            int value = (int)Math.Round(pixels * _scale);
            return value < 1 ? 1 : value;
        }

        private void LayoutChildren()
        {
            if (_shutDown || !_layoutReady)
            {
                return;
            }

            int border = Scale(2);
            int tabHeight = Scale(26);
            int navHeight = Scale(28);
            int buttonWidth = Scale(29);
            int buttonGap = Scale(2);
            int inset = Scale(1);

            int innerWidth = ClientSize.Width - border * 2;
            if (innerWidth < 1)
            {
                innerWidth = 1;
            }

            _tabStrip.SetBounds(border, border, innerWidth, tabHeight);

            int navY = border + tabHeight;
            _backButton.SetBounds(border, navY, buttonWidth, navHeight);
            _forwardButton.SetBounds(border + buttonWidth + buttonGap, navY, buttonWidth, navHeight);
            _upButton.SetBounds(border + (buttonWidth + buttonGap) * 2, navY, buttonWidth, navHeight);

            int addressX = border + (buttonWidth + buttonGap) * 3 + inset;
            int addressWidth = ClientSize.Width - addressX - border;
            if (addressWidth < Scale(40))
            {
                addressWidth = Scale(40);
            }
            int addressHeight = _addressBox.PreferredHeight;
            int addressY = navY + Math.Max(0, (navHeight - addressHeight) / 2);
            _addressBox.SetBounds(addressX, addressY, addressWidth, addressHeight);

            int viewY = navY + navHeight;
            int viewHeight = ClientSize.Height - viewY - border;
            if (viewHeight < 1)
            {
                viewHeight = 1;
            }
            _viewHost.SetBounds(border, viewY, innerWidth, viewHeight);

            Tab tab = ActiveTab;
            if (tab != null && tab.HostPanelControl != null)
            {
                tab.HostPanelControl.Bounds = _viewHost.ClientRectangle;
                tab.SetHostBounds(_viewHost.ClientRectangle);
            }
        }

        internal void ActivatePane()
        {
            _main.SetActivePane(this);
        }

        internal void SetActive(bool active)
        {
            if (_active == active)
            {
                return;
            }
            _active = active;
            Invalidate();
        }

        internal bool ContainsViewFocus(IntPtr focus)
        {
            if (focus == IntPtr.Zero || !_viewHost.IsHandleCreated)
            {
                return false;
            }
            return _viewHost.Handle == focus || Native.IsChild(_viewHost.Handle, focus);
        }

        internal void NoteViewFocus(IntPtr focus)
        {
            if (focus != IntPtr.Zero)
            {
                _lastViewFocus = focus;
            }
        }

        internal void FocusView()
        {
            Tab tab = ActiveTab;
            if (tab == null || tab.HostPanelControl == null)
            {
                _addressBox.Focus();
                return;
            }

            IntPtr target = _lastViewFocus;
            if (target == IntPtr.Zero || !Native.IsChild(tab.HostPanelControl.Handle, target))
            {
                target = Native.FindViewFocusWindow(tab.HostPanelControl.Handle);
            }

            if (target != IntPtr.Zero)
            {
                Native.SetFocus(target);
            }

            IntPtr focused = Native.GetFocus();
            if (focused == IntPtr.Zero || !Native.IsChild(tab.HostPanelControl.Handle, focused))
            {
                _addressBox.Focus();
                _addressBox.SelectAll();
            }
        }

        internal void FocusAddressBar()
        {
            _addressBox.Focus();
            _addressBox.SelectAll();
        }

        internal bool TranslateViewAccelerator(IntPtr hwnd, ref Native.MSG msg)
        {
            if (_shutDown)
            {
                return false;
            }

            Tab tab = ActiveTab;
            if (tab == null || tab.HostPanelControl == null)
            {
                return false;
            }

            IntPtr host = tab.HostPanelControl.Handle;
            if (hwnd != host && !Native.IsChild(host, hwnd))
            {
                return false;
            }
            return tab.TranslateAccelerator(ref msg);
        }

        internal string GetTabTitle(int index)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                return string.Empty;
            }
            return _tabs[index].Title;
        }

        internal List<string> GetTabPaths()
        {
            List<string> paths = new List<string>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                string path = _tabs[i].CurrentParsingName;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            return paths;
        }

        internal void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                return;
            }

            if (index == _selectedIndex)
            {
                Tab active = _tabs[index];
                if (active.HostPanelControl != null)
                {
                    active.HostPanelControl.Visible = true;
                    active.HostPanelControl.BringToFront();
                    active.HostPanelControl.Bounds = _viewHost.ClientRectangle;
                    active.SetHostBounds(_viewHost.ClientRectangle);
                }
                UpdateAddressAndNav();
                _main.SetActivePane(this);
                return;
            }

            Tab oldTab = ActiveTab;
            if (oldTab != null && oldTab.HostPanelControl != null)
            {
                oldTab.HostPanelControl.Visible = false;
            }

            _selectedIndex = index;
            Tab newTab = _tabs[index];
            if (newTab.HostPanelControl != null)
            {
                newTab.HostPanelControl.Visible = true;
                newTab.HostPanelControl.BringToFront();
                newTab.HostPanelControl.Bounds = _viewHost.ClientRectangle;
                newTab.SetHostBounds(_viewHost.ClientRectangle);
            }

            UpdateAddressAndNav();
            _tabStrip.InvalidateLayout();
            _main.SetActivePane(this);
        }

        private void UpdateAddressAndNav()
        {
            Tab tab = ActiveTab;
            if (tab != null)
            {
                _addressBox.Text = tab.CurrentAddress;
            }
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            Tab tab = ActiveTab;
            _backButton.Enabled = tab != null && tab.CanGoBack;
            _forwardButton.Enabled = tab != null && tab.CanGoForward;
            _upButton.Enabled = tab != null;
        }

        internal void NewTab()
        {
            if (_tabs.Count >= MaxTabs)
            {
                FlashAddress(Color.FromArgb(255, 238, 180));
                return;
            }

            Tab current = ActiveTab;
            string path;
            if (current != null && current.CurrentParsingName.Length > 0)
            {
                path = current.CurrentParsingName;
            }
            else
            {
                path = StateStore.GetDefaultPath(_index);
            }

            Tab tab = AddTab(path, true);
            if (tab != null)
            {
                FocusView();
            }
        }

        internal Tab AddTab(string path, bool select)
        {
            if (_tabs.Count >= MaxTabs)
            {
                return null;
            }
            if (string.IsNullOrEmpty(path))
            {
                path = StateStore.GetDefaultPath(_index);
            }
            HostPanel hostPanel = new HostPanel();
            hostPanel.Dock = DockStyle.Fill;
            hostPanel.Visible = false;
            _viewHost.Controls.Add(hostPanel);

            Rectangle bounds = _viewHost.ClientRectangle;
            if (bounds.Width < 1)
            {
                bounds.Width = 1;
            }
            if (bounds.Height < 1)
            {
                bounds.Height = 1;
            }
            hostPanel.Bounds = bounds;
            hostPanel.CreateControl();

            Tab tab = new Tab(this, hostPanel);
            _tabs.Add(tab);

            string fallback = StateStore.GetUserProfilePath();
            if (!tab.Start(path, fallback))
            {
                _tabs.Remove(tab);
                tab.Dispose();
                return null;
            }

            _tabStrip.InvalidateLayout();
            if (select)
            {
                SelectTab(_tabs.Count - 1);
            }
            return tab;
        }

        internal void CloseActiveTab()
        {
            if (_tabs.Count > 1 && _selectedIndex >= 0)
            {
                CloseTab(_selectedIndex);
            }
        }

        internal void CloseTab(int index)
        {
            if (index < 0 || index >= _tabs.Count || _tabs.Count <= 1)
            {
                return;
            }

            Tab closing = _tabs[index];
            string closedPath = closing.CurrentParsingName;
            if (closedPath.Length > 0)
            {
                if (_recentlyClosed.Count >= 10)
                {
                    _recentlyClosed.RemoveAt(0);
                }
                _recentlyClosed.Add(closedPath);
            }

            bool wasSelected = index == _selectedIndex;
            if (closing.HostPanelControl != null)
            {
                closing.HostPanelControl.Visible = false;
            }

            _tabs.RemoveAt(index);

            int newIndex = -1;
            if (wasSelected)
            {
                if (index < _tabs.Count)
                {
                    newIndex = index;
                }
                else
                {
                    newIndex = _tabs.Count - 1;
                }
                _selectedIndex = -1;
            }
            else if (index < _selectedIndex)
            {
                _selectedIndex--;
            }

            closing.Dispose();
            _tabStrip.InvalidateLayout();

            if (wasSelected)
            {
                SelectTab(newIndex);
            }
            else
            {
                UpdateAddressAndNav();
            }
        }

        internal void CycleTab(int direction)
        {
            if (_tabs.Count <= 1)
            {
                return;
            }
            int count = _tabs.Count;
            int index = (_selectedIndex + direction) % count;
            if (index < 0)
            {
                index += count;
            }
            SelectTab(index);
        }

        internal void RestoreClosedTab()
        {
            if (_recentlyClosed.Count == 0 || _tabs.Count >= MaxTabs)
            {
                return;
            }
            string path = _recentlyClosed[_recentlyClosed.Count - 1];
            _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
            if (AddTab(path, true) != null)
            {
                FocusView();
            }
        }

        internal void GoBack()
        {
            Tab tab = ActiveTab;
            if (tab != null)
            {
                tab.GoBack();
                UpdateNavButtons();
            }
        }

        internal void GoForward()
        {
            Tab tab = ActiveTab;
            if (tab != null)
            {
                tab.GoForward();
                UpdateNavButtons();
            }
        }

        internal void GoUp()
        {
            Tab tab = ActiveTab;
            if (tab != null)
            {
                tab.GoUp();
                UpdateNavButtons();
            }
        }

        internal void OnTabNavigated(Tab tab)
        {
            if (_shutDown)
            {
                return;
            }

            int index = _tabs.IndexOf(tab);
            if (index < 0)
            {
                return;
            }

            if (index == _selectedIndex)
            {
                if (!_addressBox.Focused)
                {
                    _addressBox.Text = tab.CurrentAddress;
                }
                _addressBox.BackColor = SystemColors.Window;
                _addressFlashTimer.Stop();
                UpdateNavButtons();
            }
            _tabStrip.InvalidateLayout();
        }

        internal void OnTabNavigationFailed(Tab tab)
        {
            if (_shutDown)
            {
                return;
            }

            if (tab == ActiveTab)
            {
                _addressBox.Text = tab.CurrentAddress;
                UpdateNavButtons();
                FlashAddress(Color.MistyRose);
            }
            _tabStrip.InvalidateLayout();
        }

        private void FlashAddress(Color color)
        {
            _addressBox.BackColor = color;
            _addressFlashTimer.Stop();
            _addressFlashTimer.Start();
        }

        private void AddressBoxKeyDown(object sender, KeyEventArgs e)
        {
            Tab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;

                string input = _addressBox.Text.Trim();
                if (input.Length >= 2 && input[0] == '"' && input[input.Length - 1] == '"')
                {
                    input = input.Substring(1, input.Length - 2);
                }
                if (input.Length == 0)
                {
                    return;
                }

                input = Environment.ExpandEnvironmentVariables(input);
                tab.NavigateTo(input);
                UpdateNavButtons();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                _addressBox.Text = tab.CurrentAddress;
                _addressBox.BackColor = SystemColors.Window;
                _addressFlashTimer.Stop();
            }
        }

        internal void Shutdown()
        {
            if (_shutDown)
            {
                return;
            }
            _shutDown = true;

            _addressFlashTimer.Stop();
            for (int i = _tabs.Count - 1; i >= 0; i--)
            {
                _tabs[i].Dispose();
            }
            _tabs.Clear();
            _selectedIndex = -1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
                _addressFlashTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion

    #region MainForm

    internal enum SplitterKind
    {
        Vertical,
        Horizontal,
        Both
    }

    internal sealed class SplitterControl : Control
    {
        private readonly MainForm _form;
        private readonly SplitterKind _kind;
        private bool _dragging;

        public SplitterControl(MainForm form, SplitterKind kind)
        {
            _form = form;
            _kind = kind;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = SystemColors.Control;
            TabStop = false;

            if (kind == SplitterKind.Vertical)
            {
                Cursor = Cursors.SizeWE;
            }
            else if (kind == SplitterKind.Horizontal)
            {
                Cursor = Cursors.SizeNS;
            }
            else
            {
                Cursor = Cursors.SizeAll;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Control);

            using (Pen pen = new Pen(SystemColors.ControlDark))
            {
                if (_kind == SplitterKind.Vertical)
                {
                    int x = Width / 2;
                    g.DrawLine(pen, x, 0, x, Height);
                }
                else if (_kind == SplitterKind.Horizontal)
                {
                    int y = Height / 2;
                    g.DrawLine(pen, 0, y, Width, y);
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(SystemColors.ControlDark))
                    {
                        g.FillRectangle(brush, 1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                _form.DragSplitter(_kind, Cursor.Position);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = false;
                Capture = false;
                _form.ResetSplit(_kind);
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                _dragging = false;
            }
        }
    }

    internal sealed class MainForm : Form, IMessageFilter
    {
        private sealed class RestoreJob
        {
            public int PaneIndex;
            public int TabIndex;
            public string Path;
        }

        private readonly Pane[] _panes = new Pane[4];
        private readonly SplitterControl _verticalSplitter;
        private readonly SplitterControl _horizontalSplitter;
        private readonly SplitterControl _crossSplitter;
        private readonly Timer _focusTimer;
        private readonly Timer _restoreTimer;
        private readonly Queue<RestoreJob> _restoreQueue = new Queue<RestoreJob>();
        private readonly int[] _restoreActive = new int[4];
        private readonly AppState _state;
        private readonly string[] _initialArgs;

        private Pane _activePane;
        private double _splitX = 0.5;
        private double _splitY = 0.5;
        private float _scale = 1f;
        private bool _layingOut;
        private bool _uiReady;
        private bool _restoreComplete;
        private bool _closing;
        private bool _shutDown;

        internal MainForm(AppState state, string[] initialArgs)
        {
            _state = state == null ? new AppState() : state;
            _initialArgs = initialArgs == null ? new string[0] : initialArgs;

            Text = "QuadExplorer";
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            DoubleBuffered = true;
            MinimumSize = new Size(800, 500);

            _splitX = ClampRatio(_state.SplitX);
            _splitY = ClampRatio(_state.SplitY);

            ApplyInitialBounds(_state);

            for (int i = 0; i < 4; i++)
            {
                Pane pane = new Pane(this, i);
                _panes[i] = pane;
                Controls.Add(pane);
            }

            _verticalSplitter = new SplitterControl(this, SplitterKind.Vertical);
            _horizontalSplitter = new SplitterControl(this, SplitterKind.Horizontal);
            _crossSplitter = new SplitterControl(this, SplitterKind.Both);
            Controls.Add(_verticalSplitter);
            Controls.Add(_horizontalSplitter);
            Controls.Add(_crossSplitter);
            _crossSplitter.BringToFront();

            _focusTimer = new Timer();
            _focusTimer.Interval = 100;
            _focusTimer.Tick += FocusTimerTick;

            _restoreTimer = new Timer();
            _restoreTimer.Interval = 25;
            _restoreTimer.Tick += RestoreTimerTick;

            BuildRestorePlan();

            _activePane = _panes[0];
            _panes[0].SetActive(true);
            _uiReady = true;
        }

        private void ApplyInitialBounds(AppState state)
        {
            if (state.HasWindow && IsUsableBounds(state.Window))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = state.Window;
                if (state.Maximized)
                {
                    WindowState = FormWindowState.Maximized;
                }
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(1600, 900);
            }
        }

        private static bool IsUsableBounds(Rectangle bounds)
        {
            if (bounds.Width < 400 || bounds.Height < 300)
            {
                return false;
            }

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                Rectangle overlap = Rectangle.Intersect(screens[i].WorkingArea, bounds);
                if (overlap.Width >= 100 && overlap.Height >= 100)
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildRestorePlan()
        {
            List<string>[] plans = new List<string>[4];
            for (int i = 0; i < 4; i++)
            {
                List<string> source = _state.PaneTabs[i];
                List<string> plan = source == null ? new List<string>() : new List<string>(source);
                if (plan.Count == 0)
                {
                    plan.Add(StateStore.GetDefaultPath(i));
                }
                if (plan.Count > 20)
                {
                    plan.RemoveRange(20, plan.Count - 20);
                }
                plans[i] = plan;

                int active = _state.ActiveTab[i];
                if (active < 0)
                {
                    active = 0;
                }
                if (active >= plan.Count)
                {
                    active = plan.Count - 1;
                }
                _restoreActive[i] = active;
            }

            for (int i = 0; i < _initialArgs.Length && i < 4; i++)
            {
                if (!string.IsNullOrEmpty(_initialArgs[i]))
                {
                    plans[i][0] = _initialArgs[i];
                }
            }

            int maxCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (plans[i].Count > maxCount)
                {
                    maxCount = plans[i].Count;
                }
            }

            for (int tabIndex = 0; tabIndex < maxCount; tabIndex++)
            {
                for (int paneIndex = 0; paneIndex < 4; paneIndex++)
                {
                    if (tabIndex < plans[paneIndex].Count)
                    {
                        RestoreJob job = new RestoreJob();
                        job.PaneIndex = paneIndex;
                        job.TabIndex = tabIndex;
                        job.Path = plans[paneIndex][tabIndex];
                        _restoreQueue.Enqueue(job);
                    }
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= Native.WS_CLIPCHILDREN | Native.WS_CLIPSIBLINGS;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Native.GetScale(this);
            LayoutAll();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _scale = Native.GetScale(this);
            LayoutAll();
            SetActivePane(_panes[0]);
            _focusTimer.Start();

            if (_restoreQueue.Count > 0)
            {
                _restoreTimer.Start();
            }
            else
            {
                FinishRestore();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutAll();
        }

        private int Scale(int pixels)
        {
            int value = (int)Math.Round(pixels * _scale);
            return value < 1 ? 1 : value;
        }

        private static double ClampRatio(double value)
        {
            if (value < 0.1)
            {
                return 0.1;
            }
            if (value > 0.9)
            {
                return 0.9;
            }
            return value;
        }

        internal void LayoutAll()
        {
            if (!_uiReady || _layingOut || _shutDown)
            {
                return;
            }

            _layingOut = true;
            SuspendLayout();
            try
            {
                int width = ClientSize.Width;
                int height = ClientSize.Height;
                int thickness = Scale(4);
                int usableWidth = width - thickness;
                int usableHeight = height - thickness;
                if (usableWidth < 2)
                {
                    usableWidth = 2;
                }
                if (usableHeight < 2)
                {
                    usableHeight = 2;
                }

                int left = (int)Math.Round(usableWidth * _splitX);
                int top = (int)Math.Round(usableHeight * _splitY);
                if (left < 1)
                {
                    left = 1;
                }
                if (left > usableWidth - 1)
                {
                    left = usableWidth - 1;
                }
                if (top < 1)
                {
                    top = 1;
                }
                if (top > usableHeight - 1)
                {
                    top = usableHeight - 1;
                }

                int right = width - thickness - left;
                int bottom = height - thickness - top;
                if (right < 1)
                {
                    right = 1;
                }
                if (bottom < 1)
                {
                    bottom = 1;
                }

                _panes[0].SetBounds(0, 0, left, top);
                _panes[1].SetBounds(left + thickness, 0, right, top);
                _panes[2].SetBounds(0, top + thickness, left, bottom);
                _panes[3].SetBounds(left + thickness, top + thickness, right, bottom);

                _verticalSplitter.SetBounds(left, 0, thickness, height);
                _horizontalSplitter.SetBounds(0, top, width, thickness);
                _crossSplitter.SetBounds(left, top, thickness, thickness);

                _verticalSplitter.BringToFront();
                _horizontalSplitter.BringToFront();
                _crossSplitter.BringToFront();
            }
            finally
            {
                ResumeLayout(false);
                _layingOut = false;
            }
        }

        internal void DragSplitter(SplitterKind kind, Point screenPoint)
        {
            if (_shutDown)
            {
                return;
            }

            Point point = PointToClient(screenPoint);
            int thickness = Scale(4);
            int usableWidth = ClientSize.Width - thickness;
            int usableHeight = ClientSize.Height - thickness;
            if (usableWidth < 1)
            {
                usableWidth = 1;
            }
            if (usableHeight < 1)
            {
                usableHeight = 1;
            }

            if (kind == SplitterKind.Vertical || kind == SplitterKind.Both)
            {
                double ratio = (point.X - thickness / 2.0) / usableWidth;
                _splitX = ClampRatio(ratio);
            }
            if (kind == SplitterKind.Horizontal || kind == SplitterKind.Both)
            {
                double ratio = (point.Y - thickness / 2.0) / usableHeight;
                _splitY = ClampRatio(ratio);
            }
            LayoutAll();
        }

        internal void ResetSplit(SplitterKind kind)
        {
            if (kind == SplitterKind.Vertical || kind == SplitterKind.Both)
            {
                _splitX = 0.5;
            }
            if (kind == SplitterKind.Horizontal || kind == SplitterKind.Both)
            {
                _splitY = 0.5;
            }
            LayoutAll();
        }

        internal void SetActivePane(Pane pane)
        {
            if (pane == null || _shutDown || _closing)
            {
                return;
            }
            if (_activePane == pane)
            {
                return;
            }

            if (_activePane != null)
            {
                _activePane.SetActive(false);
            }
            _activePane = pane;
            _activePane.SetActive(true);
        }

        private void FocusTimerTick(object sender, EventArgs e)
        {
            if (_closing || _shutDown || !IsHandleCreated)
            {
                return;
            }
            if (!IsForegroundForm())
            {
                return;
            }

            IntPtr focus = Native.GetFocus();
            if (focus == IntPtr.Zero)
            {
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                Pane pane = _panes[i];
                if (!pane.IsHandleCreated)
                {
                    continue;
                }
                if (pane.Handle == focus || Native.IsChild(pane.Handle, focus))
                {
                    if (pane.ContainsViewFocus(focus))
                    {
                        pane.NoteViewFocus(focus);
                    }
                    SetActivePane(pane);
                    return;
                }
            }
        }

        private bool IsForegroundForm()
        {
            if (!IsHandleCreated)
            {
                return false;
            }
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }
            return foreground == Handle || Native.IsChild(Handle, foreground);
        }

        private bool IsWindowOfThisForm(IntPtr hWnd)
        {
            if (!IsHandleCreated || hWnd == IntPtr.Zero)
            {
                return false;
            }
            return hWnd == Handle || Native.IsChild(Handle, hWnd);
        }

        private Pane FindPaneForWindow(IntPtr hWnd)
        {
            for (int i = 0; i < 4; i++)
            {
                Pane pane = _panes[i];
                if (!pane.IsHandleCreated)
                {
                    continue;
                }
                if (pane.Handle == hWnd || Native.IsChild(pane.Handle, hWnd))
                {
                    return pane;
                }
            }
            return null;
        }

        public bool PreFilterMessage(ref Message m)
        {
            try
            {
                if (m.Msg == Native.WM_LBUTTONDOWN || m.Msg == Native.WM_RBUTTONDOWN)
                {
                    if (IsWindowOfThisForm(m.HWnd))
                    {
                        Pane pane = FindPaneForWindow(m.HWnd);
                        if (pane != null)
                        {
                            SetActivePane(pane);
                        }
                    }
                    return false;
                }

                bool keyDown = m.Msg == Native.WM_KEYDOWN || m.Msg == Native.WM_SYSKEYDOWN;
                bool keyMessage = m.Msg >= Native.WM_KEYFIRST && m.Msg <= Native.WM_KEYLAST;
                if (keyDown || keyMessage)
                {
                    if (!IsWindowOfThisForm(m.HWnd))
                    {
                        return false;
                    }

                    if (keyDown)
                    {
                        Keys key = (Keys)(int)m.WParam;
                        if (HandleShortcut(key))
                        {
                            return true;
                        }
                    }

                    if (keyMessage)
                    {
                        Pane pane = FindPaneForWindow(m.HWnd);
                        if (pane != null)
                        {
                            Native.MSG msg = Native.MSG.FromMessage(m);
                            if (pane.TranslateViewAccelerator(m.HWnd, ref msg))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private bool HandleShortcut(Keys key)
        {
            Pane pane = _activePane;
            if (pane == null)
            {
                return false;
            }

            Keys modifiers = Control.ModifierKeys;
            bool control = (modifiers & Keys.Control) != 0;
            bool shift = (modifiers & Keys.Shift) != 0;
            bool alt = (modifiers & Keys.Alt) != 0;

            if (control && !alt)
            {
                if (key == Keys.Tab)
                {
                    pane.CycleTab(shift ? -1 : 1);
                    return true;
                }
                if (!shift && key == Keys.T)
                {
                    pane.NewTab();
                    return true;
                }
                if (!shift && key == Keys.W)
                {
                    pane.CloseActiveTab();
                    return true;
                }
                if (shift && key == Keys.T)
                {
                    pane.RestoreClosedTab();
                    return true;
                }
                if (!shift && key == Keys.L)
                {
                    pane.FocusAddressBar();
                    return true;
                }
            }

            if (!control && !alt && key == Keys.F6)
            {
                pane.FocusAddressBar();
                return true;
            }

            if (alt && !control && !shift)
            {
                if (key >= Keys.D1 && key <= Keys.D4)
                {
                    int index = (int)key - (int)Keys.D1;
                    if (index >= 0 && index < 4)
                    {
                        SetActivePane(_panes[index]);
                        _panes[index].FocusView();
                        return true;
                    }
                }
                if (key == Keys.Left)
                {
                    pane.GoBack();
                    return true;
                }
                if (key == Keys.Right)
                {
                    pane.GoForward();
                    return true;
                }
                if (key == Keys.Up)
                {
                    pane.GoUp();
                    return true;
                }
            }

            return false;
        }

        private void RestoreTimerTick(object sender, EventArgs e)
        {
            if (_closing || _shutDown)
            {
                _restoreTimer.Stop();
                return;
            }

            if (_restoreQueue.Count == 0)
            {
                FinishRestore();
                return;
            }

            RestoreJob job = _restoreQueue.Dequeue();
            Pane pane = _panes[job.PaneIndex];
            Tab tab = pane.AddTab(job.Path, false);
            if (tab != null && job.TabIndex == 0)
            {
                pane.SelectTab(0);
            }

            if (_restoreQueue.Count == 0)
            {
                FinishRestore();
            }
        }

        private void FinishRestore()
        {
            if (_restoreComplete || _shutDown)
            {
                _restoreTimer.Stop();
                return;
            }

            _restoreTimer.Stop();

            for (int i = 0; i < 4; i++)
            {
                Pane pane = _panes[i];
                if (pane.TabCount == 0)
                {
                    pane.AddTab(StateStore.GetDefaultPath(i), false);
                }
                if (pane.TabCount > 0)
                {
                    int active = _restoreActive[i];
                    if (active >= pane.TabCount)
                    {
                        active = pane.TabCount - 1;
                    }
                    if (active < 0)
                    {
                        active = 0;
                    }
                    pane.SelectTab(active);
                }
            }

            _restoreComplete = true;
            SetActivePane(_panes[0]);
            _panes[0].FocusView();
        }

        private void SaveState()
        {
            try
            {
                AppState state = new AppState();
                Rectangle bounds = Bounds;
                if (WindowState != FormWindowState.Normal)
                {
                    bounds = RestoreBounds;
                }
                if (bounds.Width >= 400 && bounds.Height >= 300)
                {
                    state.HasWindow = true;
                    state.Window = bounds;
                }
                state.Maximized = WindowState == FormWindowState.Maximized;
                state.SplitX = _splitX;
                state.SplitY = _splitY;

                for (int i = 0; i < 4; i++)
                {
                    state.PaneTabs[i] = _panes[i].GetTabPaths();
                    state.ActiveTab[i] = Math.Max(0, _panes[i].SelectedTabIndex);
                }

                StateStore.Save(state);
            }
            catch (Exception)
            {
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_closing)
            {
                _closing = true;
                _focusTimer.Stop();
                _restoreTimer.Stop();
                if (_restoreComplete)
                {
                    SaveState();
                }
            }

            ShutdownPanes();
            base.OnFormClosing(e);
        }

        private void ShutdownPanes()
        {
            if (_shutDown)
            {
                return;
            }
            _shutDown = true;
            for (int i = 0; i < 4; i++)
            {
                _panes[i].Shutdown();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                _focusTimer.Stop();
                _restoreTimer.Stop();
                _focusTimer.Dispose();
                _restoreTimer.Dispose();
                ShutdownPanes();
            }
            base.Dispose(disposing);
        }
    }

    #endregion

    #region StateStore

    internal sealed class AppState
    {
        public bool HasWindow;
        public Rectangle Window;
        public bool Maximized;
        public double SplitX;
        public double SplitY;
        public List<string>[] PaneTabs;
        public int[] ActiveTab;

        public AppState()
        {
            SplitX = 0.5;
            SplitY = 0.5;
            PaneTabs = new List<string>[4];
            ActiveTab = new int[4];
            for (int i = 0; i < 4; i++)
            {
                PaneTabs[i] = new List<string>();
                ActiveTab[i] = 0;
            }
        }
    }

    internal static class StateStore
    {
        private const string StateFileName = "QuadExplorer.state";
        private const string FallbackFileName = "state.txt";

        internal static AppState Load()
        {
            AppState state = new AppState();
            string text = null;

            string[] candidates = new string[] { GetPrimaryStatePath(), GetFallbackStatePath() };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    if (File.Exists(candidates[i]))
                    {
                        text = File.ReadAllText(candidates[i], Encoding.UTF8);
                        break;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                Parse(state, text);
            }

            for (int i = 0; i < 4; i++)
            {
                if (state.PaneTabs[i].Count == 0)
                {
                    state.PaneTabs[i].Add(GetDefaultPath(i));
                }
                if (state.ActiveTab[i] < 0)
                {
                    state.ActiveTab[i] = 0;
                }
            }

            return state;
        }

        private static void Parse(AppState state, string text)
        {
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();

                if (string.Equals(key, "window", StringComparison.OrdinalIgnoreCase))
                {
                    ParseWindow(state, value);
                }
                else if (string.Equals(key, "split", StringComparison.OrdinalIgnoreCase))
                {
                    ParseSplit(state, value);
                }
                else if (key.StartsWith("pane", StringComparison.OrdinalIgnoreCase))
                {
                    ParsePaneLine(state, key, value);
                }
            }
        }

        private static void ParseWindow(AppState state, string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 5)
            {
                return;
            }

            int x;
            int y;
            int width;
            int height;
            if (!TryParseInt(parts[0], out x) ||
                !TryParseInt(parts[1], out y) ||
                !TryParseInt(parts[2], out width) ||
                !TryParseInt(parts[3], out height))
            {
                return;
            }
            if (width < 400 || height < 300)
            {
                return;
            }

            state.HasWindow = true;
            state.Window = new Rectangle(x, y, width, height);
            state.Maximized = ParseBool(parts[4]);
        }

        private static void ParseSplit(AppState state, string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 2)
            {
                return;
            }

            double x;
            double y;
            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            {
                state.SplitX = ClampRatio(x);
            }
            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                state.SplitY = ClampRatio(y);
            }
        }

        private static void ParsePaneLine(AppState state, string key, string value)
        {
            int dot = key.IndexOf('.');
            if (dot <= 4)
            {
                return;
            }

            string paneText = key.Substring(4, dot - 4);
            int paneIndex;
            if (!TryParseInt(paneText, out paneIndex))
            {
                return;
            }
            if (paneIndex < 0 || paneIndex > 3)
            {
                return;
            }

            string field = key.Substring(dot + 1);
            if (string.Equals(field, "active", StringComparison.OrdinalIgnoreCase))
            {
                int active;
                if (TryParseInt(value, out active))
                {
                    state.ActiveTab[paneIndex] = active;
                }
            }
            else if (string.Equals(field, "tab", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length > 0 && state.PaneTabs[paneIndex].Count < 20)
                {
                    state.PaneTabs[paneIndex].Add(value);
                }
            }
        }

        internal static void Save(AppState state)
        {
            if (state == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            if (state.HasWindow)
            {
                builder.Append("window=");
                builder.Append(state.Window.X);
                builder.Append(',');
                builder.Append(state.Window.Y);
                builder.Append(',');
                builder.Append(state.Window.Width);
                builder.Append(',');
                builder.Append(state.Window.Height);
                builder.Append(',');
                builder.Append(state.Maximized ? "1" : "0");
                builder.AppendLine();
            }

            builder.Append("split=");
            builder.Append(state.SplitX.ToString("0.######", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(state.SplitY.ToString("0.######", CultureInfo.InvariantCulture));
            builder.AppendLine();

            for (int i = 0; i < 4; i++)
            {
                builder.Append("pane");
                builder.Append(i);
                builder.Append(".active=");
                builder.Append(state.ActiveTab[i]);
                builder.AppendLine();

                List<string> tabs = state.PaneTabs[i];
                if (tabs == null)
                {
                    continue;
                }
                for (int j = 0; j < tabs.Count; j++)
                {
                    string path = SanitizePath(tabs[j]);
                    if (path.Length == 0)
                    {
                        continue;
                    }
                    builder.Append("pane");
                    builder.Append(i);
                    builder.Append(".tab=");
                    builder.Append(path);
                    builder.AppendLine();
                }
            }

            string content = builder.ToString();
            string primary = GetPrimaryStatePath();
            try
            {
                File.WriteAllText(primary, content, new UTF8Encoding(false));
                return;
            }
            catch (Exception)
            {
            }

            try
            {
                string fallback = GetFallbackStatePath();
                string directory = Path.GetDirectoryName(fallback);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(fallback, content, new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        internal static string[] NormalizeArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new string[0];
            }

            int count = Math.Min(args.Length, 4);
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
            {
                string value = args[i] == null ? string.Empty : args[i].Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }
                result[i] = value;
            }
            return result;
        }

        internal static string GetDefaultPath(int paneIndex)
        {
            switch (paneIndex)
            {
                case 0:
                    return "shell:Desktop";
                case 1:
                    string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return string.IsNullOrEmpty(documents) ? GetUserProfilePath() : documents;
                case 2:
                    return "shell:Downloads";
                default:
                    return "C:\\";
            }
        }

        internal static string GetUserProfilePath()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
            {
                profile = Environment.GetEnvironmentVariable("USERPROFILE");
            }
            if (string.IsNullOrEmpty(profile))
            {
                profile = "C:\\";
            }
            return profile;
        }

        private static string GetPrimaryStatePath()
        {
            string directory = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }
            return Path.Combine(directory, StateFileName);
        }

        private static string GetFallbackStatePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appData, "QuadExplorer"), FallbackFileName);
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool ParseBool(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            bool parsed;
            if (bool.TryParse(text, out parsed))
            {
                return parsed;
            }
            int number;
            if (TryParseInt(text, out number))
            {
                return number != 0;
            }
            return false;
        }

        private static double ClampRatio(double value)
        {
            if (value < 0.1)
            {
                return 0.1;
            }
            if (value > 0.9)
            {
                return 0.9;
            }
            return value;
        }

        private static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            return path.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    #endregion
}
