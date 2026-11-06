using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SplitExplorer
{
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

        public void Refresh()
        {
            if (_disposed || _browser == null)
            {
                throw new ObjectDisposedException("ExplorerHost");
            }

            Guid iid = ShellIds.IID_IShellView;
            IntPtr viewPointer = IntPtr.Zero;
            object viewObject = null;
            try
            {
                _browser.GetCurrentView(ref iid, out viewPointer);
                if (viewPointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("The current Shell view is unavailable.");
                }

                viewObject = Marshal.GetObjectForIUnknown(viewPointer);
                IShellView view = viewObject as IShellView;
                if (view == null)
                {
                    throw new InvalidCastException("The current view does not support IShellView.");
                }

                int result = view.Refresh();
                if (result < 0)
                {
                    Marshal.ThrowExceptionForHR(result);
                }
            }
            finally
            {
                ComUtil.Release(viewObject);
                if (viewPointer != IntPtr.Zero)
                {
                    Marshal.Release(viewPointer);
                }
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
}
