using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal enum NavigationGlyph
    {
        Back,
        Forward,
        Parent,
        Refresh
    }

    internal sealed class NavigationButton : Control
    {
        private static readonly Font IconFont = SystemIconFont.Create(13f);
        private readonly NavigationGlyph _glyph;
        private bool _hovered;
        private bool _pressed;

        internal NavigationButton(NavigationGlyph glyph, string accessibleName)
        {
            _glyph = glyph;
            AccessibleName = accessibleName;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            Size = new Size(30, 30);
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _pressed = true;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(System.EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle face = new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            if (_hovered || _pressed)
            {
                using (GraphicsPath path = RoundedRectangle(face, 5))
                using (SolidBrush brush = new SolidBrush(_pressed ? UiTheme.Border : UiTheme.Hover))
                {
                    g.FillPath(brush, path);
                }
            }

            TextRenderer.DrawText(g, GetGlyph(_glyph).ToString(), IconFont, ClientRectangle,
                Enabled ? UiTheme.Text : UiTheme.DisabledIcon,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private static char GetGlyph(NavigationGlyph glyph)
        {
            switch (glyph)
            {
                case NavigationGlyph.Back: return '\uE72B';
                case NavigationGlyph.Forward: return '\uE72A';
                case NavigationGlyph.Parent: return '\uE74A';
                default: return '\uE72C';
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class SystemIconFont
    {
        internal static Font Create(float size)
        {
            return new Font("Segoe Fluent Icons", size, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
