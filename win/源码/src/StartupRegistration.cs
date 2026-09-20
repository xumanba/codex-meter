using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexMeter
{
    internal static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CodexMeter";

        internal static bool IsEnabled()
        {
            string executablePath = Path.GetFullPath(Application.ExecutablePath);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                string command = key == null
                    ? null
                    : key.GetValue(RunValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (CommandTargetsExecutable(command, executablePath))
                    return true;
            }

            // v0.1.1 installers used a Startup-folder shortcut. Recognize it so
            // upgrades preserve the user's existing choice and the menu can turn it off.
            return File.Exists(LegacyShortcutPath());
        }

        internal static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                        throw new InvalidOperationException("无法打开当前用户的 Windows 启动项。");
                    key.SetValue(RunValueName, BuildCommand(Application.ExecutablePath), RegistryValueKind.String);
                }
                return;
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                    key.DeleteValue(RunValueName, false);
            }

            string shortcutPath = LegacyShortcutPath();
            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);
        }

        internal static string BuildCommand(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path is required.", "executablePath");
            string fullPath = Path.GetFullPath(executablePath.Trim().Trim('"'));
            return "\"" + fullPath + "\" --startup";
        }

        internal static bool CommandTargetsExecutable(string command, string executablePath)
        {
            if (String.IsNullOrWhiteSpace(command) || String.IsNullOrWhiteSpace(executablePath))
                return false;

            string candidate = ExtractExecutablePath(Environment.ExpandEnvironmentVariables(command));
            if (String.IsNullOrWhiteSpace(candidate))
                return false;

            try
            {
                return String.Equals(
                    Path.GetFullPath(candidate),
                    Path.GetFullPath(executablePath.Trim().Trim('"')),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractExecutablePath(string command)
        {
            string value = command.Trim();
            if (value.Length == 0)
                return null;

            if (value[0] == '"')
            {
                int closingQuote = value.IndexOf('"', 1);
                return closingQuote > 1 ? value.Substring(1, closingQuote - 1) : null;
            }

            int executableEnd = value.IndexOf(".exe ", StringComparison.OrdinalIgnoreCase);
            return executableEnd >= 0 ? value.Substring(0, executableEnd + 4) : value;
        }

        private static string LegacyShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "Codex Meter.lnk");
        }
    }
}
