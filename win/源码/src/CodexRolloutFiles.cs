using System;
using System.Collections.Generic;
using System.IO;

namespace CodexMeter
{
    internal static class CodexRolloutFiles
    {
        internal static Dictionary<string, string> DiscoverLatestByName(
            IEnumerable<string> roots, Func<FileInfo, bool> include)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (roots == null)
                return result;

            foreach (string root in roots)
                AddRoot(result, root, include);
            return result;
        }

        private static void AddRoot(IDictionary<string, string> result,
            string root, Func<FileInfo, bool> include)
        {
            if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            try
            {
                foreach (string path in Directory.EnumerateFiles(
                    root, "*.jsonl", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo info = new FileInfo(path);
                        if (include != null && !include(info))
                            continue;

                        string existing;
                        if (!result.TryGetValue(info.Name, out existing) ||
                            info.LastWriteTimeUtc > File.GetLastWriteTimeUtc(existing))
                        {
                            result[info.Name] = path;
                        }
                    }
                    catch (IOException)
                    {
                        // A live rollout can move to the archive during enumeration.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Inaccessible optional logs do not block other sessions.
                    }
                }
            }
            catch (Exception exception)
            {
                AppDiagnostics.Record("rollout-discovery", exception);
            }
        }
    }
}
