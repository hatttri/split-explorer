using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuadExplorer
{
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
}
