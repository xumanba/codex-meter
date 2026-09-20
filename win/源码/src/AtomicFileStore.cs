using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodexMeter
{
    internal static class AtomicFileStore
    {
        internal static string BackupPath(string path)
        {
            return path + ".bak";
        }

        internal static IEnumerable<string> ExistingReadCandidates(string path)
        {
            if (!String.IsNullOrWhiteSpace(path) && File.Exists(path))
                yield return path;

            string backup = String.IsNullOrWhiteSpace(path) ? null : BackupPath(path);
            if (!String.IsNullOrWhiteSpace(backup) && File.Exists(backup))
                yield return backup;
        }

        internal static void WriteUtf8(string path, string content)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A target path is required.", "path");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + ".tmp";
            string backupPath = BackupPath(fullPath);
            try
            {
                File.WriteAllText(temporaryPath, content ?? String.Empty, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(temporaryPath, fullPath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
