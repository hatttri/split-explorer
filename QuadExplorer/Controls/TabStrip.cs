using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuadExplorer
{
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
}
