using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodexMeter
{
    internal sealed class AppSettings
    {
        public int? Left { get; set; }
        public int? Top { get; set; }
        public string Theme { get; set; }
        public string Mode { get; set; }
        public string DockEdge { get; set; }
        public int? DockTop { get; set; }
        public bool EdgeAutoHide { get; set; }
        public string DockScreen { get; set; }
        public int UiVersion { get; set; }

        public AppSettings()
        {
            Theme = "light";
            Mode = "fixed";
            EdgeAutoHide = true;
            UiVersion = 0;
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string settingsPath;
        private readonly object syncRoot = new object();

        public SettingsStore()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexMeter");
            settingsPath = Path.Combine(folder, "settings.ini");
        }

        public string SettingsPath
        {
            get { return settingsPath; }
        }

        public AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            if (!File.Exists(settingsPath))
            {
                settings.UiVersion = 2;
                return settings;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    int separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    Apply(settings, key, value);
                }
            }
            catch
            {
                // Corrupt settings should never prevent the meter from starting.
            }

            if (settings.UiVersion < 2)
            {
                settings.Theme = "light";
                settings.UiVersion = 2;
            }

            return settings;
        }

        public void Save(AppSettings settings)
        {
            lock (syncRoot)
            {
                string folder = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                List<string> lines = new List<string>();
                lines.Add("# Codex Meter for Windows settings (no credentials are stored here)");
                Add(lines, "left", settings.Left);
                Add(lines, "top", settings.Top);
                lines.Add("theme=" + (settings.Theme ?? "dark"));
                lines.Add("mode=" + (settings.Mode ?? "fixed"));
                lines.Add("edge_auto_hide=" + (settings.EdgeAutoHide ? "true" : "false"));
                lines.Add("ui_version=" + settings.UiVersion.ToString(CultureInfo.InvariantCulture));
                if (!String.IsNullOrEmpty(settings.DockEdge))
                    lines.Add("dock_edge=" + settings.DockEdge);
                if (!String.IsNullOrEmpty(settings.DockScreen))
                    lines.Add("dock_screen=" + settings.DockScreen);
                Add(lines, "dock_top", settings.DockTop);

                File.WriteAllLines(settingsPath, lines.ToArray(), new UTF8Encoding(false));
            }
        }

        private static void Apply(AppSettings settings, string key, string value)
        {
            int parsed;
            if (String.Equals(key, "left", StringComparison.OrdinalIgnoreCase) && TryInt(value, out parsed))
                settings.Left = parsed;
            else if (String.Equals(key, "top", StringComparison.OrdinalIgnoreCase) && TryInt(value, out parsed))
                settings.Top = parsed;
            else if (String.Equals(key, "dock_top", StringComparison.OrdinalIgnoreCase) && TryInt(value, out parsed))
                settings.DockTop = parsed;
            else if (String.Equals(key, "theme", StringComparison.OrdinalIgnoreCase))
                settings.Theme = String.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
            else if (String.Equals(key, "mode", StringComparison.OrdinalIgnoreCase))
                settings.Mode = String.Equals(value, "follow", StringComparison.OrdinalIgnoreCase) ? "follow" : "fixed";
            else if (String.Equals(key, "dock_edge", StringComparison.OrdinalIgnoreCase))
                settings.DockEdge = value == "left" || value == "right" ? value : null;
            else if (String.Equals(key, "edge_auto_hide", StringComparison.OrdinalIgnoreCase))
                settings.EdgeAutoHide = !String.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            else if (String.Equals(key, "dock_screen", StringComparison.OrdinalIgnoreCase))
                settings.DockScreen = value;
            else if (String.Equals(key, "ui_version", StringComparison.OrdinalIgnoreCase) && TryInt(value, out parsed))
                settings.UiVersion = parsed;
        }

        private static bool TryInt(string value, out int result)
        {
            return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static void Add(ICollection<string> lines, string key, int? value)
        {
            if (value.HasValue)
                lines.Add(key + "=" + value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
