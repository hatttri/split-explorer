using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal sealed class BreadcrumbBar : Control
    {
        private static readonly Font SeparatorFont = SystemIconFont.Create(9f);
        private sealed class Segment
        {
            internal string Text;
            internal string Path;
            internal Rectangle Bounds;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private int _hoveredSegment = -1;

        internal event Action<string> SegmentClicked;
        internal event Action EditRequested;

        internal BreadcrumbBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
            Cursor = Cursors.Default;
        }

        internal void SetPath(string path)
        {
            _segments.Clear();
            _hoveredSegment = -1;
            if (string.IsNullOrWhiteSpace(path))
            {
                Invalidate();
                return;
            }

            try
            {
                string root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                {
                    _segments.Add(new Segment { Text = path, Path = path });
                }
                else
                {
                    _segments.Add(new Segment { Text = root, Path = root });
                    string remainder = path.Substring(root.Length);
                    string current = root;
                    string[] parts = remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        current = Path.Combine(current, part);
                        _segments.Add(new Segment { Text = part, Path = current });
                    }
                }
            }
            catch (ArgumentException)
            {
                _segments.Clear();
                _segments.Add(new Segment { Text = path, Path = path });
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(UiTheme.Surface);
            int x = 3;
            int separatorWidth = Scale(16);
            int horizontalPadding = Scale(5);
            int height = ClientSize.Height;
            _segments.ForEach(delegate(Segment segment) { segment.Bounds = Rectangle.Empty; });

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];
                Size textSize = TextRenderer.MeasureText(e.Graphics, segment.Text, Font,
                    Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                int width = Math.Max(1, textSize.Width + horizontalPadding * 2);
                segment.Bounds = new Rectangle(x, 0, width, height);

                if (i == _hoveredSegment)
                {
                    using (SolidBrush brush = new SolidBrush(UiTheme.Hover))
                    using (System.Drawing.Drawing2D.GraphicsPath path = RoundedRectangle(segment.Bounds, Scale(4)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }

                TextRenderer.DrawText(e.Graphics, segment.Text, Font,
                    new Rectangle(x + horizontalPadding, 0, Math.Max(1, width - horizontalPadding * 2), height),
                    UiTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                x += width;

                if (i < _segments.Count - 1)
                {
                    TextRenderer.DrawText(e.Graphics, "\uE76C", SeparatorFont,
                        new Rectangle(x, 0, separatorWidth, height), UiTheme.MutedText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    x += separatorWidth;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hovered = HitTest(e.Location);
            if (hovered != _hoveredSegment)
            {
                _hoveredSegment = hovered;
                Cursor = hovered >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredSegment >= 0)
            {
                _hoveredSegment = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = HitTest(e.Location);
            if (index >= 0)
            {
                Action<string> handler = SegmentClicked;
                if (handler != null)
                {
                    handler(_segments[index].Path);
                }
            }
            else
            {
                Action handler = EditRequested;
                if (handler != null)
                {
                    handler();
                }
            }
        }

        private int HitTest(Point point)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].Bounds.Contains(point))
                {
                    return i;
                }
            }
            return -1;
        }

        private int Scale(int pixels)
        {
            float scale = IsHandleCreated ? Native.GetScale(this) : 1f;
            return Math.Max(1, (int)Math.Round(pixels * scale));
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
