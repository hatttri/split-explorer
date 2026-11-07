using System;
using System.Windows.Forms;

namespace SplitExplorer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Native.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.OleRequired();

            AppState state = StateStore.Load();
            string[] initial = StateStore.NormalizeArguments(args);
            MainForm form = new MainForm(state, initial);
            Application.AddMessageFilter(form);
            Application.Run(form);
        }
    }
}