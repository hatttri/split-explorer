using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SplitExplorer
{
    #region StateStore

    internal static class StateStore
    {
        private const string StateFileName = "SplitExplorer.state";

        internal static AppState Load()
        {
            AppState state = new AppState();
            string text = null;

            try
            {
                string path = GetPrimaryStatePath();
                if (path != null && File.Exists(path))
                {
                    text = File.ReadAllText(path, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }

            if (!string.IsNullOrEmpty(text))
            {
                Parse(state, text);
            }

            for (int i = 0; i < 4; i++)
            {
                if (state.PaneTabs[i].Count == 0)
                {
                    state.PaneTabs[i].Add(GetDefaultPath(i));
                }
                if (state.ActiveTab[i] < 0)
                {
                    state.ActiveTab[i] = 0;
                }
            }

            return state;
        }

        private static void Parse(AppState state, string text)
        {
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();

                if (string.Equals(key, "window", StringComparison.OrdinalIgnoreCase))
                {
                    ParseWindow(state, value);
                }
                else if (key.StartsWith("pane", StringComparison.OrdinalIgnoreCase))
                {
                    ParsePaneLine(state, key, value);
                }
            }
        }

        private static void ParseWindow(AppState state, string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 5)
            {
                return;
            }

            int x;
            int y;
            int width;
            int height;
            if (!TryParseInt(parts[0], out x) ||
                !TryParseInt(parts[1], out y) ||
                !TryParseInt(parts[2], out width) ||
                !TryParseInt(parts[3], out height))
            {
                return;
            }
            if (width < 400 || height < 300)
            {
                return;
            }

            state.HasWindow = true;
            state.Window = new Rectangle(x, y, width, height);
            state.Maximized = ParseBool(parts[4]);
        }

        private static void ParsePaneLine(AppState state, string key, string value)
        {
            int dot = key.IndexOf('.');
            if (dot <= 4)
            {
                return;
            }

            string paneText = key.Substring(4, dot - 4);
            int paneIndex;
            if (!TryParseInt(paneText, out paneIndex))
            {
                return;
            }
            if (paneIndex < 0 || paneIndex > 3)
            {
                return;
            }

            string field = key.Substring(dot + 1);
            if (string.Equals(field, "active", StringComparison.OrdinalIgnoreCase))
            {
                int active;
                if (TryParseInt(value, out active))
                {
                    state.ActiveTab[paneIndex] = active;
                }
            }
            else if (string.Equals(field, "tab", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length > 0 && state.PaneTabs[paneIndex].Count < 20)
                {
                    state.PaneTabs[paneIndex].Add(value);
                }
            }
        }

        internal static void Save(AppState state)
        {
            if (state == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            if (state.HasWindow)
            {
                builder.Append("window=");
                builder.Append(state.Window.X);
                builder.Append(',');
                builder.Append(state.Window.Y);
                builder.Append(',');
                builder.Append(state.Window.Width);
                builder.Append(',');
                builder.Append(state.Window.Height);
                builder.Append(',');
                builder.Append(state.Maximized ? "1" : "0");
                builder.AppendLine();
            }

            for (int i = 0; i < 4; i++)
            {
                builder.Append("pane");
                builder.Append(i);
                builder.Append(".active=");
                builder.Append(state.ActiveTab[i]);
                builder.AppendLine();

                List<string> tabs = state.PaneTabs[i];
                if (tabs == null)
                {
                    continue;
                }
                for (int j = 0; j < tabs.Count; j++)
                {
                    string path = SanitizePath(tabs[j]);
                    if (path.Length == 0)
                    {
                        continue;
                    }
                    builder.Append("pane");
                    builder.Append(i);
                    builder.Append(".tab=");
                    builder.Append(path);
                    builder.AppendLine();
                }
            }

            string content = builder.ToString();
            string statePath = GetPrimaryStatePath();
            if (statePath == null)
            {
                return;
            }

            try
            {
                File.WriteAllText(statePath, content, new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        internal static string[] NormalizeArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new string[0];
            }

            int count = Math.Min(args.Length, 4);
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
            {
                string value = args[i] == null ? string.Empty : args[i].Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }
                result[i] = value;
            }
            return result;
        }

        internal static string GetDefaultPath(int paneIndex)
        {
            switch (paneIndex)
            {
                case 0:
                    return "shell:Desktop";
                case 1:
                    string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return string.IsNullOrEmpty(documents) ? GetUserProfilePath() : documents;
                case 2:
                    return "shell:Downloads";
                default:
                    return "C:\\";
            }
        }

        internal static string GetUserProfilePath()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
            {
                profile = Environment.GetEnvironmentVariable("USERPROFILE");
            }
            if (string.IsNullOrEmpty(profile))
            {
                profile = "C:\\";
            }
            return profile;
        }

        private static string GetPrimaryStatePath()
        {
            string directory = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }
            return Path.Combine(directory, StateFileName);
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool ParseBool(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            bool parsed;
            if (bool.TryParse(text, out parsed))
            {
                return parsed;
            }
            int number;
            if (TryParseInt(text, out number))
            {
                return number != 0;
            }
            return false;
        }

        private static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            return path.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    #endregion
}