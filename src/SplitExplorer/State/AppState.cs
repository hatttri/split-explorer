using System.Collections.Generic;
using System.Drawing;

namespace SplitExplorer
{
    internal sealed class AppState
    {
        public bool HasWindow;
        public Rectangle Window;
        public bool Maximized;
        public List<string>[] PaneTabs;
        public int[] ActiveTab;

        public AppState()
        {
            PaneTabs = new List<string>[4];
            ActiveTab = new int[4];
            for (int i = 0; i < 4; i++)
            {
                PaneTabs[i] = new List<string>();
                ActiveTab[i] = 0;
            }
        }
    }
}