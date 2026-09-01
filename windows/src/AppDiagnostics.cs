using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodexMeter
{
    internal static class AppDiagnostics
    {
        private const long MaximumLogBytes = 1024 * 1024;
        private static readonly object SyncRoot = new object();

        internal static string LogPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexMeter", "codexmeter.log");
            }
        }

        internal static void Record(string component, Exception exception)
        {
            RecordMessage(component, Describe(exception));
        }

        internal static string Describe(Exception exception)
        {
            if (exception == null)
                return "UnknownError: Unknown error";

            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                AggregateException flattened = aggregate.Flatten();
                if (flattened.InnerExceptions.Count > 0)
                    exception = flattened.InnerExceptions[0];
            }

            return exception.GetType().Name + ": " + exception.Message;
        }

        internal static void RecordMessage(string component, string message)
        {
            try
            {
                string safeComponent = Sanitize(component);
                string safeMessage = Sanitize(message);
                string line = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture) +
                    " [" + safeComponent + "] " + safeMessage + Environment.NewLine;

                lock (SyncRoot)
                {
                    string path = LogPath;
                    string directory = Path.GetDirectoryName(path);
                    if (!String.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);
                    RotateIfNeeded(path);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never become a new application failure.
            }
        }

        private static void RotateIfNeeded(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < MaximumLogBytes)
                return;

            string previous = path + ".old";
            if (File.Exists(previous))
                File.Delete(previous);
            File.Move(path, previous);
        }

        private static string Sanitize(string value)
        {
            string safe = CodexBarClient.SanitizeDetail(value ?? String.Empty);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!String.IsNullOrWhiteSpace(profile))
                safe = safe.Replace(profile, "[user]");
            if (!String.IsNullOrWhiteSpace(local))
                safe = safe.Replace(local, "[local-app-data]");
            return safe.Length <= 500 ? safe : safe.Substring(0, 500);
        }
    }
}
