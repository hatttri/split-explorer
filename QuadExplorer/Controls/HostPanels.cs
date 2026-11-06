using System.Drawing;
using System.Windows.Forms;

namespace QuadExplorer
{
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
}
