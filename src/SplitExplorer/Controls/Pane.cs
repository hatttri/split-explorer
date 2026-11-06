using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SplitExplorer
{
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
            int tabWidth = Math.Max(Scale(96), (int)Math.Round(ClientSize.Width * 0.15));
            int navHeight = Scale(30);
            int buttonWidth = Math.Max(Scale(24), (tabWidth - border * 2 - Scale(4)) / 3);
            int buttonGap = Scale(2);
            int inset = Scale(1);

            int innerWidth = ClientSize.Width - tabWidth - border * 2;
            if (innerWidth < 1)
            {
                innerWidth = 1;
            }

            int sidebarX = border;
            _backButton.SetBounds(sidebarX, border, buttonWidth, navHeight);
            _forwardButton.SetBounds(sidebarX + buttonWidth + buttonGap, border, buttonWidth, navHeight);
            _upButton.SetBounds(sidebarX + (buttonWidth + buttonGap) * 2, border, buttonWidth, navHeight);
            int tabY = border + navHeight + buttonGap;
            _tabStrip.SetBounds(sidebarX, tabY, tabWidth - border, Math.Max(1, ClientSize.Height - tabY - border));

            int addressX = border + tabWidth;
            int addressWidth = ClientSize.Width - addressX - border;
            int addressHeight = _addressBox.PreferredHeight;
            int addressY = border + Math.Max(0, (navHeight - addressHeight) / 2);
            _addressBox.SetBounds(addressX, addressY, addressWidth, addressHeight);

            int viewY = border + navHeight + border;
            int viewHeight = ClientSize.Height - viewY - border;
            if (viewHeight < 1)
            {
                viewHeight = 1;
            }
            _viewHost.SetBounds(addressX, viewY, innerWidth, viewHeight);

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
}
