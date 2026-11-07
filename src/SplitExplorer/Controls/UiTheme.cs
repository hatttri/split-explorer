using System.Drawing;

namespace SplitExplorer
{
    internal static class UiTheme
    {
        internal static Color PaneBackground { get { return Color.FromArgb(247, 247, 247); } }
        internal static Color SidebarBackground { get { return Color.FromArgb(243, 243, 243); } }
        internal static Color Surface { get { return Color.White; } }
        internal static Color Hover { get { return Color.FromArgb(232, 232, 232); } }
        internal static Color Selected { get { return Color.White; } }
        internal static Color Border { get { return Color.FromArgb(214, 214, 214); } }
        internal static Color Text { get { return Color.FromArgb(32, 32, 32); } }
        internal static Color MutedText { get { return Color.FromArgb(96, 96, 96); } }
        internal static Color DisabledIcon { get { return Color.FromArgb(174, 174, 174); } }
        internal static Color Accent { get { return Color.FromArgb(0, 103, 192); } }
    }
}