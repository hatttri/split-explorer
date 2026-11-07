using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal sealed class TabStrip : Control
    {
        private static readonly Font IconFont = SystemIconFont.Create(12f);
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
            BackColor = UiTheme.SidebarBackground;
            TabStop = false;
        }

        internal void ApplyColors()
        {
            BackColor = UiTheme.SidebarBackground;
            Invalidate();
        }

        public void InvalidateLayout()
        {
            int rowHeight = Scale(38);
            int tabArea = Math.Max(0, Height - Scale(34));
            int selected = _pane.SelectedTabIndex;
            if (selected >= 0 && tabArea > 0)
            {
                int top = selected * rowHeight;
                int bottom = top + rowHeight;
                if (top < _scrollOffset) _scrollOffset = top;
                else if (bottom > _scrollOffset + tabArea) _scrollOffset = bottom - tabArea;
            }
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

        private int _scrollOffset;

        private void EnsureLayout()
        {
            int count = _pane.TabCount;
            if (!_layoutDirty && _tabBounds.Count == count) return;
            _tabBounds.Clear();
            _closeBounds.Clear();
            int rowHeight = Scale(38);
            int plusHeight = Scale(34);
            int tabArea = Math.Max(0, Height - plusHeight);
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, Math.Max(0, count * rowHeight - tabArea)));
            _plusBounds = new Rectangle(0, Height - plusHeight, Width, plusHeight);
            int closeWidth = Scale(18);
            for (int i = 0; i < count; i++)
            {
                Rectangle tabRect = new Rectangle(0, i * rowHeight - _scrollOffset, Width, rowHeight);
                _tabBounds.Add(tabRect);
                _closeBounds.Add(new Rectangle(tabRect.Right - closeWidth - Scale(5), tabRect.Top + Scale(4), closeWidth, Math.Max(1, rowHeight - Scale(8))));
            }
            _layoutDirty = false;
        }
        private void HitTest(Point point, out int tabIndex, out bool onClose, out bool onPlus)
        {
            EnsureLayout();
            tabIndex = -1;
            onClose = false;
            onPlus = _plusBounds.Contains(point);
            if (onPlus)
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
            g.Clear(UiTheme.SidebarBackground);

            for (int i = 0; i < _tabBounds.Count; i++)
            {
                Rectangle bounds = _tabBounds[i];
                Rectangle rect = new Rectangle(bounds.X + Scale(3), bounds.Y + Scale(2),
                    Math.Max(1, bounds.Width - Scale(6)), Math.Max(1, bounds.Height - Scale(4)));
                bool selected = i == _pane.SelectedTabIndex;
                bool hovered = i == _hoverTab;

                Color backColor = selected ? UiTheme.Selected : (hovered ? UiTheme.Hover : UiTheme.SidebarBackground);

                if (selected || hovered)
                {
                    using (GraphicsPath row = RoundedRectangle(rect, Scale(5)))
                    using (SolidBrush brush = new SolidBrush(backColor))
                    {
                        g.FillPath(brush, row);
                    }
                }

                if (selected)
                {
                    using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                    {
                        g.FillRectangle(accent, rect.Left, rect.Top + Scale(7), Scale(2), Math.Max(1, rect.Height - Scale(14)));
                    }
                }

                Rectangle closeRect = _closeBounds[i];
                Rectangle textRect = new Rectangle(
                    rect.Left + Scale(10),
                    rect.Top,
                    Math.Max(1, closeRect.Left - rect.Left - Scale(13)),
                    rect.Height);
                TextRenderer.DrawText(
                    g,
                    _pane.GetTabTitle(i),
                    Font,
                    textRect,
                    selected ? UiTheme.Text : UiTheme.MutedText,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine);

                bool closeHovered = hovered && _hoverClose;
                if (closeHovered)
                {
                    using (GraphicsPath closeFace = RoundedRectangle(closeRect, Scale(4)))
                    using (SolidBrush brush = new SolidBrush(UiTheme.Hover))
                    {
                        g.FillPath(brush, closeFace);
                    }
                }

                if (selected || hovered)
                {
                    Color closeColor = closeHovered ? UiTheme.Text : UiTheme.MutedText;
                    DrawSystemGlyph(g, '\uE8BB', closeRect, closeColor);
                }
            }

            bool plusHovered = _hoverPlus;
            if (plusHovered)
            {
                using (SolidBrush brush = new SolidBrush(UiTheme.Hover))
                {
                    g.FillRectangle(brush, _plusBounds);
                }
            }

            int plusCenterX = _plusBounds.Left + Scale(22);
            int plusCenterY = _plusBounds.Top + _plusBounds.Height / 2;
            DrawSystemGlyph(g, '\uE710', new Rectangle(plusCenterX - Scale(9), plusCenterY - Scale(9), Scale(18), Scale(18)),
                plusHovered ? UiTheme.Text : UiTheme.MutedText);
            TextRenderer.DrawText(g, "新しいタブ", Font,
                new Rectangle(_plusBounds.Left + Scale(38), _plusBounds.Top, Math.Max(1, Width - Scale(42)), _plusBounds.Height),
                UiTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            using (Pen bottom = new Pen(UiTheme.Border))
            {
                g.DrawLine(bottom, 0, Height - 1, Width, Height - 1);
            }
        }

        private static void DrawSystemGlyph(Graphics graphics, char glyph, Rectangle bounds, Color color)
        {
            TextRenderer.DrawText(graphics, glyph.ToString(), IconFont, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scrollOffset = Math.Max(0, _scrollOffset - Math.Sign(e.Delta) * Scale(76));
            _layoutDirty = true;
            Invalidate();
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
}