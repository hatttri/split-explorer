using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal sealed class MainForm : Form, IMessageFilter
    {
        private sealed class RestoreJob
        {
            public int PaneIndex;
            public int TabIndex;
            public string Path;
        }

        private readonly Pane[] _panes = new Pane[4];
        private readonly Timer _focusTimer;
        private readonly Timer _restoreTimer;
        private readonly Queue<RestoreJob> _restoreQueue = new Queue<RestoreJob>();
        private readonly int[] _restoreActive = new int[4];
        private readonly AppState _state;
        private readonly string[] _initialArgs;

        private Pane _activePane;
        private bool _layingOut;
        private readonly bool _uiReady;
        private bool _restoreComplete;
        private bool _closing;
        private bool _shutDown;

        internal MainForm(AppState state, string[] initialArgs)
        {
            _state = state == null ? new AppState() : state;
            _initialArgs = initialArgs == null ? new string[0] : initialArgs;

            Text = "SplitExplorer";
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            DoubleBuffered = true;
            MinimumSize = new Size(800, 500);

            ApplyInitialBounds(_state);

            for (int i = 0; i < 4; i++)
            {
                Pane pane = new Pane(this, i);
                _panes[i] = pane;
                Controls.Add(pane);
            }

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
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(1600, 900);
            }

            WindowState = FormWindowState.Maximized;
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
            ApplyColors();
            LayoutAll();
        }

        private void ApplyColors()
        {
            BackColor = UiTheme.PaneBackground;
            ForeColor = UiTheme.Text;
            for (int i = 0; i < _panes.Length; i++)
            {
                if (_panes[i] != null)
                {
                    _panes[i].ApplyColors();
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

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
                if (width < 2)
                {
                    width = 2;
                }
                if (height < 2)
                {
                    height = 2;
                }

                int left = width / 2;
                int top = height / 2;
                int right = width - left;
                int bottom = height - top;

                _panes[0].SetBounds(0, 0, left, top);
                _panes[1].SetBounds(left, 0, right, top);
                _panes[2].SetBounds(0, top, left, bottom);
                _panes[3].SetBounds(left, top, right, bottom);
            }
            finally
            {
                ResumeLayout(false);
                _layingOut = false;
            }
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

                bool keyMessage = m.Msg >= Native.WM_KEYFIRST && m.Msg <= Native.WM_KEYLAST;
                if (keyMessage)
                {
                    if (!IsWindowOfThisForm(m.HWnd))
                    {
                        return false;
                    }

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
            catch (Exception)
            {
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
}