using System;
using System.Drawing;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal sealed class AddressBarFrame : Panel
    {
        internal AddressBarFrame()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
            Padding = new Padding(7, 2, 7, 2);
        }

        internal void ApplyColors()
        {
            BackColor = UiTheme.Surface;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (Pen border = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawRectangle(border, bounds);
            }
        }
    }
}