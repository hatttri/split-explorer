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
        private readonly NavigationButton _backButton;
        private readonly NavigationButton _forwardButton;
        private readonly NavigationButton _upButton;
        private readonly NavigationButton _refreshButton;
        private readonly AddressBarFrame _addressFrame;
        private readonly BreadcrumbBar _breadcrumbBar;
        private readonly TextBox _addressBox;
        private readonly Button _addressModeButton;
        private readonly ViewPanel _viewHost;
        private readonly ToolTip _navigationToolTip;
        private readonly List<Tab> _tabs = new List<Tab>();
        private readonly List<string> _recentlyClosed = new List<string>();
        private readonly Timer _addressFlashTimer;

        private int _selectedIndex = -1;
        private bool _active;
        private bool _shutDown;
        private readonly bool _layoutReady;
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
            BackColor = UiTheme.PaneBackground;
            Font = SystemFonts.MessageBoxFont;

            _tabStrip = new TabStrip(this);
            _tabStrip.Font = SystemFonts.MessageBoxFont;

            _backButton = CreateNavButton(NavigationGlyph.Back, "戻る");
            _forwardButton = CreateNavButton(NavigationGlyph.Forward, "進む");
            _upButton = CreateNavButton(NavigationGlyph.Parent, "親フォルダー");
            _refreshButton = CreateNavButton(NavigationGlyph.Refresh, "最新の状態へ更新");
            _refreshButton.Click += delegate { RefreshActiveTab(); };
            _navigationToolTip = new ToolTip();
            _navigationToolTip.SetToolTip(_backButton, "戻る");
            _navigationToolTip.SetToolTip(_forwardButton, "進む");
            _navigationToolTip.SetToolTip(_upButton, "親フォルダー");
            _navigationToolTip.SetToolTip(_refreshButton, "最新の状態へ更新");

            _addressFrame = new AddressBarFrame();
            _breadcrumbBar = new BreadcrumbBar();
            _addressBox = new TextBox();
            _addressModeButton = new Button();
            _addressBox.BorderStyle = BorderStyle.None;
            _addressBox.Font = SystemFonts.MessageBoxFont;
            _addressBox.BackColor = UiTheme.Surface;
            _addressBox.ForeColor = UiTheme.Text;
            _addressBox.Visible = false;
            _breadcrumbBar.Visible = true;
            _addressModeButton.Text = "\uE711";
            _addressModeButton.Font = SystemIconFont.Create(10f);
            _addressModeButton.ForeColor = UiTheme.MutedText;
            _addressModeButton.BackColor = UiTheme.Surface;
            _addressModeButton.FlatStyle = FlatStyle.Flat;
            _addressModeButton.FlatAppearance.BorderSize = 0;
            _addressModeButton.TabStop = true;
            _addressModeButton.AccessibleName = "パンくず表示に戻る";
            _addressModeButton.Visible = false;
            _navigationToolTip.SetToolTip(_addressModeButton, "入力を破棄して階層表示に戻る");
            _addressFrame.Controls.Add(_breadcrumbBar);
            _addressFrame.Controls.Add(_addressBox);
            _addressFrame.Controls.Add(_addressModeButton);
            _addressBox.Enter += delegate { _addressFrame.Invalidate(); };
            _addressBox.Leave += delegate
            {
                _addressFrame.Invalidate();
                if (!_addressModeButton.Focused)
                {
                    ShowBreadcrumbBar();
                }
            };
            _breadcrumbBar.SegmentClicked += NavigateToBreadcrumb;
            _breadcrumbBar.EditRequested += FocusAddressBar;
            _addressModeButton.Click += delegate { CancelAddressEditing(); };

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
            Controls.Add(_refreshButton);
            Controls.Add(_addressFrame);

            _addressFlashTimer = new Timer();
            _addressFlashTimer.Interval = 1200;
            _addressFlashTimer.Tick += delegate
            {
                _addressFlashTimer.Stop();
                _addressBox.BackColor = UiTheme.Surface;
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

        private NavigationButton CreateNavButton(NavigationGlyph glyph, string accessibleName)
        {
            return new NavigationButton(glyph, accessibleName);
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
            e.Graphics.Clear(UiTheme.PaneBackground);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int thickness = 1;
            Color color = UiTheme.Border;
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
            int navWidth = Math.Max(1, tabWidth - border * 2);
            int navSlotWidth = Math.Max(1, navWidth / 4);
            int buttonSize = Math.Min(navHeight, navSlotWidth);
            int innerWidth = ClientSize.Width - tabWidth - border * 2;
            if (innerWidth < 1)
            {
                innerWidth = 1;
            }

            int sidebarX = border;
            NavigationButton[] navigationButtons = { _backButton, _forwardButton, _upButton, _refreshButton };
            for (int i = 0; i < navigationButtons.Length; i++)
            {
                int slotLeft = sidebarX + i * navWidth / navigationButtons.Length;
                int slotRight = sidebarX + (i + 1) * navWidth / navigationButtons.Length;
                int buttonX = slotLeft + (slotRight - slotLeft - buttonSize) / 2;
                navigationButtons[i].SetBounds(buttonX, border, buttonSize, navHeight);
            }
            int tabY = border + navHeight + Scale(2);
            _tabStrip.SetBounds(sidebarX, tabY, tabWidth - border, Math.Max(1, ClientSize.Height - tabY - border));

            int addressX = border + tabWidth;
            int addressWidth = ClientSize.Width - addressX - border;
            int addressHeight = _addressBox.PreferredHeight;
            int addressFrameHeight = Math.Max(addressHeight, navHeight - Scale(2));
            int addressY = border + Math.Max(0, (navHeight - addressFrameHeight) / 2);
            _addressFrame.SetBounds(addressX, addressY, addressWidth, addressFrameHeight);
            Rectangle addressContent = new Rectangle(_addressFrame.Padding.Left, Math.Max(0, (addressFrameHeight - addressHeight) / 2),
                Math.Max(1, addressWidth - _addressFrame.Padding.Horizontal - Scale(25)), addressHeight);
            _addressBox.Bounds = addressContent;
            _breadcrumbBar.SetBounds(_addressFrame.Padding.Left, 0,
                Math.Max(1, addressWidth - _addressFrame.Padding.Horizontal), addressFrameHeight);
            _addressModeButton.SetBounds(addressWidth - _addressFrame.Padding.Right - Scale(23),
                Math.Max(0, (addressFrameHeight - Scale(23)) / 2), Scale(23), Scale(23));

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

        internal void ApplyColors()
        {
            BackColor = UiTheme.PaneBackground;
            ForeColor = UiTheme.Text;
            _addressBox.BackColor = UiTheme.Surface;
            _addressBox.ForeColor = UiTheme.Text;
            _addressFrame.ApplyColors();
            _tabStrip.ApplyColors();
            _viewHost.Invalidate(true);
            Invalidate(true);
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
                FocusAddressBar();
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
                FocusAddressBar();
            }
        }

        internal void FocusAddressBar()
        {
            ActivatePane();
            Tab tab = ActiveTab;
            _addressBox.Text = tab == null ? string.Empty : tab.CurrentAddress;
            _breadcrumbBar.Visible = false;
            _addressBox.Visible = true;
            _addressModeButton.Visible = true;
            _addressBox.BringToFront();
            _addressBox.Focus();
            _addressBox.SelectAll();
        }

        private void ShowBreadcrumbBar()
        {
            Tab tab = ActiveTab;
            _breadcrumbBar.SetPath(tab == null ? string.Empty : tab.CurrentAddress);
            _addressBox.Visible = false;
            _addressModeButton.Visible = false;
            _breadcrumbBar.Visible = true;
            _breadcrumbBar.BringToFront();
        }

        private void CancelAddressEditing()
        {
            Tab tab = ActiveTab;
            _addressBox.Text = tab == null ? string.Empty : tab.CurrentAddress;
            _addressBox.BackColor = UiTheme.Surface;
            _addressFlashTimer.Stop();
            ShowBreadcrumbBar();
        }

        private void NavigateToBreadcrumb(string path)
        {
            ActivatePane();
            Tab tab = ActiveTab;
            if (tab != null && !string.IsNullOrEmpty(path))
            {
                tab.NavigateTo(path);
                UpdateNavButtons();
            }
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
                _breadcrumbBar.SetPath(tab.CurrentAddress);
            }
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            Tab tab = ActiveTab;
            _backButton.Enabled = tab != null && tab.CanGoBack;
            _forwardButton.Enabled = tab != null && tab.CanGoForward;
            _upButton.Enabled = tab != null && tab.CanGoUp;
            _refreshButton.Enabled = tab != null;
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

        internal void RefreshActiveTab()
        {
            Tab tab = ActiveTab;
            if (tab != null)
            {
                tab.Refresh();
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
                _breadcrumbBar.SetPath(tab.CurrentAddress);
                if (!_addressBox.Focused)
                {
                    _addressBox.Text = tab.CurrentAddress;
                }
                _addressBox.BackColor = UiTheme.Surface;
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
                _breadcrumbBar.SetPath(tab.CurrentAddress);
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
                ShowBreadcrumbBar();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                CancelAddressEditing();
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
                _navigationToolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion
}