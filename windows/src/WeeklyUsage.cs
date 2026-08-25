using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexMeter
{
    internal sealed class DailyTokenUsage
    {
        public DateTime Day { get; set; }
        public long Tokens { get; set; }
    }

    internal sealed class ModelTokenUsage
    {
        public string Model { get; set; }
        public string CollaborationMode { get; set; }
        public string Effort { get; set; }
        public long Tokens { get; set; }
    }

    internal sealed class WeeklyTokenReport
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public long TotalTokens { get; set; }
        public long UnattributedTokens { get; set; }
        public List<DailyTokenUsage> Days { get; set; }
        public List<ModelTokenUsage> Models { get; set; }
        public ModelTokenUsage FavoriteModel { get; set; }
        public string Error { get; set; }

        public WeeklyTokenReport()
        {
            Days = new List<DailyTokenUsage>();
            Models = new List<ModelTokenUsage>();
        }
    }

    internal sealed class WeeklyUsageReader
    {
        private const int CacheVersion = 1;
        private const string UnknownModel = "unknown";
        private const char BucketSeparator = '\u001f';

        private readonly string sessionsRoot;
        private readonly string archivedSessionsRoot;
        private readonly string cachePath;
        private readonly JavaScriptSerializer serializer;

        public WeeklyUsageReader()
            : this(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "archived_sessions"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexMeter", "weekly-usage-cache.json"))
        {
        }

        internal WeeklyUsageReader(string sessionsRoot, string archivedSessionsRoot, string cachePath)
        {
            this.sessionsRoot = sessionsRoot;
            this.archivedSessionsRoot = archivedSessionsRoot;
            this.cachePath = cachePath;
            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
            serializer.RecursionLimit = 100;
        }

        public WeeklyTokenReport Read(DateTimeOffset now)
        {
            try
            {
                DateTime firstDay = now.LocalDateTime.Date.AddDays(-6);
                UsageCache cache = LoadCache();
                Dictionary<string, string> files = DiscoverCandidateFiles(firstDay);

                foreach (KeyValuePair<string, string> candidate in files)
                {
                    FileCheckpoint checkpoint;
                    if (!cache.Files.TryGetValue(candidate.Key, out checkpoint) || checkpoint == null)
                    {
                        checkpoint = new FileCheckpoint();
                        cache.Files[candidate.Key] = checkpoint;
                    }

                    checkpoint.FileName = candidate.Key;
                    TrimBuckets(checkpoint, firstDay);
                    UpdateCheckpoint(candidate.Value, checkpoint, firstDay);
                }

                foreach (FileCheckpoint checkpoint in cache.Files.Values)
                    TrimBuckets(checkpoint, firstDay);

                RemoveExpiredEmptyCheckpoints(cache, files);
                SaveCache(cache);
                return BuildReport(cache, now, firstDay);
            }
            catch (Exception exception)
            {
                return EmptyReport(now, "无法读取本机近 7 天用量：" + SafeError(exception));
            }
        }

        internal static string FormatTokenCount(long tokens)
        {
            double value = Math.Max(0, tokens);
            if (value >= 1000000000d)
                return (value / 1000000000d).ToString("0.0", CultureInfo.InvariantCulture) + "B";
            if (value >= 1000000d)
                return (value / 1000000d).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000d)
                return (value / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
        }

        internal static string DisplayModelName(string model)
        {
            string value = String.IsNullOrWhiteSpace(model) ? UnknownModel : model.Trim();
            if (String.Equals(value, UnknownModel, StringComparison.OrdinalIgnoreCase))
                return "未标注模型";
            if (String.Equals(value, "codex-auto-review", StringComparison.OrdinalIgnoreCase))
                return "Auto Review";

            if (value.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(4);
            value = value.Replace('-', ' ');

            string[] words = value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
            {
                string word = words[index];
                if (word.Length > 0 && !Char.IsDigit(word[0]))
                    words[index] = Char.ToUpperInvariant(word[0]) + word.Substring(1);
            }
            return String.Join(" ", words);
        }

        internal static string DisplayEffort(string effort)
        {
            string value = (effort ?? String.Empty).Trim().ToLowerInvariant();
            if (value == "xhigh")
                return "XHigh";
            if (value == "ultra")
                return "Ultra";
            if (value == "high")
                return "High";
            if (value == "medium")
                return "Medium";
            if (value == "low")
                return "Low";
            if (value == "max")
                return "Max";
            return value;
        }

        private UsageCache LoadCache()
        {
            if (String.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
                return new UsageCache();

            try
            {
                UsageCache cache = serializer.Deserialize<UsageCache>(File.ReadAllText(cachePath, Encoding.UTF8));
                if (cache == null || cache.Version != CacheVersion)
                    return new UsageCache();
                if (cache.Files == null)
                    cache.Files = new Dictionary<string, FileCheckpoint>(StringComparer.OrdinalIgnoreCase);
                else
                    cache.Files = new Dictionary<string, FileCheckpoint>(cache.Files, StringComparer.OrdinalIgnoreCase);
                return cache;
            }
            catch
            {
                return new UsageCache();
            }
        }

        private void SaveCache(UsageCache cache)
        {
            if (String.IsNullOrWhiteSpace(cachePath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(cachePath);
                if (!String.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(cachePath, serializer.Serialize(cache), new UTF8Encoding(false));
            }
            catch
            {
                // The report remains usable for this run; only the next run may need a full scan.
            }
        }

        private Dictionary<string, string> DiscoverCandidateFiles(DateTime firstDay)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddCandidateFiles(result, sessionsRoot, firstDay);
            AddCandidateFiles(result, archivedSessionsRoot, firstDay);
            return result;
        }

        private static void AddCandidateFiles(Dictionary<string, string> result, string root, DateTime firstDay)
        {
            if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            try
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo info = new FileInfo(path);
                        if (info.LastWriteTime < firstDay)
                            continue;

                        string fileName = info.Name;
                        string existing;
                        if (!result.TryGetValue(fileName, out existing) ||
                            File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(existing))
                        {
                            result[fileName] = path;
                        }
                    }
                    catch
                    {
                        // A concurrently moved rollout is safe to retry on the next refresh.
                    }
                }
            }
            catch
            {
                // Missing or inaccessible optional roots simply contribute no files.
            }
        }

        private static void UpdateCheckpoint(string path, FileCheckpoint checkpoint, DateTime firstDay)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                return;

            if (info.Length < checkpoint.Offset)
                checkpoint.Reset();
            if (info.Length == checkpoint.Offset)
            {
                checkpoint.Length = info.Length;
                checkpoint.LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                return;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (checkpoint.Offset > 0 && checkpoint.Offset <= stream.Length)
                    stream.Seek(checkpoint.Offset, SeekOrigin.Begin);
                else
                    checkpoint.Reset();

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 65536, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        ProcessLine(line, checkpoint, firstDay);
                }

                checkpoint.Offset = stream.Length;
                checkpoint.Length = stream.Length;
            }
            checkpoint.LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
        }

        private static void ProcessLine(string line, FileCheckpoint checkpoint, DateTime firstDay)
        {
            if (String.IsNullOrEmpty(line))
                return;

            string recordType = ExtractJsonString(line, "\"type\"", 0);
            if (String.Equals(recordType, "turn_context", StringComparison.Ordinal))
            {
                int contextMarker = Math.Max(0,
                    line.IndexOf("\"payload\"", StringComparison.Ordinal));
                string model = ExtractJsonString(line, "\"model\"", contextMarker);
                string effort = ExtractJsonString(line, "\"effort\"", contextMarker);
                int collaborationIndex = line.IndexOf("\"collaboration_mode\"", contextMarker,
                    StringComparison.Ordinal);
                string mode = collaborationIndex < 0
                    ? String.Empty
                    : ExtractJsonString(line, "\"mode\"", collaborationIndex);

                checkpoint.Model = String.IsNullOrWhiteSpace(model) ? UnknownModel : model;
                checkpoint.Effort = effort ?? String.Empty;
                checkpoint.CollaborationMode = mode ?? String.Empty;
                return;
            }

            if (!String.Equals(recordType, "event_msg", StringComparison.Ordinal))
                return;
            int payloadIndex = line.IndexOf("\"payload\"", StringComparison.Ordinal);
            if (payloadIndex < 0 || !String.Equals(
                    ExtractJsonString(line, "\"type\"", payloadIndex),
                    "token_count", StringComparison.Ordinal))
                return;

            int lastUsageIndex = line.IndexOf("\"last_token_usage\"", StringComparison.Ordinal);
            if (lastUsageIndex < 0)
                return;

            long tokens;
            if (!TryExtractLong(line, "\"total_tokens\"", lastUsageIndex, out tokens) || tokens <= 0)
                return;

            string timestamp = ExtractJsonString(line, "\"timestamp\"", 0);
            DateTimeOffset parsed;
            if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return;

            DateTime localDay = parsed.ToLocalTime().Date;
            if (localDay < firstDay)
                return;

            string modelValue = String.IsNullOrWhiteSpace(checkpoint.Model) ? UnknownModel : checkpoint.Model;
            string key = BucketKey(localDay, modelValue, checkpoint.CollaborationMode, checkpoint.Effort);
            long current;
            checkpoint.Buckets.TryGetValue(key, out current);
            checkpoint.Buckets[key] = SafeAdd(current, tokens);
        }

        private static string ExtractJsonString(string line, string key, int startIndex)
        {
            int keyIndex = line.IndexOf(key, Math.Max(0, startIndex), StringComparison.Ordinal);
            if (keyIndex < 0)
                return null;
            int colon = line.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return null;
            int quote = line.IndexOf('"', colon + 1);
            if (quote < 0)
                return null;

            StringBuilder value = new StringBuilder();
            bool escaped = false;
            for (int index = quote + 1; index < line.Length; index++)
            {
                char character = line[index];
                if (escaped)
                {
                    if (character == 'n')
                        value.Append('\n');
                    else if (character == 'r')
                        value.Append('\r');
                    else if (character == 't')
                        value.Append('\t');
                    else
                        value.Append(character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    return value.ToString();
                }
                else
                {
                    value.Append(character);
                }
            }
            return null;
        }

        private static bool TryExtractLong(string line, string key, int startIndex, out long value)
        {
            value = 0;
            int keyIndex = line.IndexOf(key, Math.Max(0, startIndex), StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;
            int colon = line.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return false;

            int index = colon + 1;
            while (index < line.Length && Char.IsWhiteSpace(line[index]))
                index++;
            int end = index;
            while (end < line.Length && Char.IsDigit(line[end]))
                end++;
            return end > index && Int64.TryParse(line.Substring(index, end - index),
                NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static void TrimBuckets(FileCheckpoint checkpoint, DateTime firstDay)
        {
            if (checkpoint.Buckets == null)
                checkpoint.Buckets = new Dictionary<string, long>(StringComparer.Ordinal);

            string firstKey = firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (string key in checkpoint.Buckets.Keys.ToArray())
            {
                if (String.CompareOrdinal(key.Substring(0, Math.Min(10, key.Length)), firstKey) < 0)
                    checkpoint.Buckets.Remove(key);
            }
        }

        private static void RemoveExpiredEmptyCheckpoints(UsageCache cache, Dictionary<string, string> files)
        {
            foreach (string key in cache.Files.Keys.ToArray())
            {
                FileCheckpoint checkpoint = cache.Files[key];
                bool empty = checkpoint == null || checkpoint.Buckets == null || checkpoint.Buckets.Count == 0;
                if (empty && !files.ContainsKey(key))
                    cache.Files.Remove(key);
            }
        }

        private static WeeklyTokenReport BuildReport(UsageCache cache, DateTimeOffset now, DateTime firstDay)
        {
            Dictionary<DateTime, long> daily = new Dictionary<DateTime, long>();
            Dictionary<string, ModelTokenUsage> models =
                new Dictionary<string, ModelTokenUsage>(StringComparer.Ordinal);

            for (int offset = 0; offset < 7; offset++)
                daily[firstDay.AddDays(offset)] = 0;

            foreach (FileCheckpoint checkpoint in cache.Files.Values)
            {
                if (checkpoint == null || checkpoint.Buckets == null)
                    continue;

                foreach (KeyValuePair<string, long> bucket in checkpoint.Buckets)
                {
                    string[] parts = bucket.Key.Split(BucketSeparator);
                    DateTime day;
                    if (parts.Length != 4 || !DateTime.TryParseExact(parts[0], "yyyy-MM-dd",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out day) ||
                        day < firstDay || day > firstDay.AddDays(6))
                    {
                        continue;
                    }

                    daily[day] = SafeAdd(daily[day], bucket.Value);
                    string modelKey = parts[1] + BucketSeparator + parts[2] + BucketSeparator + parts[3];
                    ModelTokenUsage model;
                    if (!models.TryGetValue(modelKey, out model))
                    {
                        model = new ModelTokenUsage
                        {
                            Model = parts[1],
                            CollaborationMode = parts[2],
                            Effort = parts[3]
                        };
                        models[modelKey] = model;
                    }
                    model.Tokens = SafeAdd(model.Tokens, bucket.Value);
                }
            }

            WeeklyTokenReport report = new WeeklyTokenReport();
            report.GeneratedAt = now;
            foreach (KeyValuePair<DateTime, long> day in daily.OrderBy(item => item.Key))
            {
                report.Days.Add(new DailyTokenUsage { Day = day.Key, Tokens = day.Value });
                report.TotalTokens = SafeAdd(report.TotalTokens, day.Value);
            }
            report.Models = models.Values.OrderByDescending(item => item.Tokens).ToList();
            report.FavoriteModel = report.Models
                .Where(item => item != null && item.Tokens > 0 &&
                    !String.Equals(item.Model, UnknownModel, StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(item.Model, "other", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Tokens)
                .FirstOrDefault();
            report.UnattributedTokens = report.Models
                .Where(item => String.Equals(item.Model, UnknownModel, StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Tokens);
            return report;
        }

        private static WeeklyTokenReport EmptyReport(DateTimeOffset now, string error)
        {
            WeeklyTokenReport report = new WeeklyTokenReport();
            report.GeneratedAt = now;
            report.Error = error;
            DateTime firstDay = now.LocalDateTime.Date.AddDays(-6);
            for (int offset = 0; offset < 7; offset++)
                report.Days.Add(new DailyTokenUsage { Day = firstDay.AddDays(offset), Tokens = 0 });
            return report;
        }

        private static string BucketKey(DateTime day, string model, string mode, string effort)
        {
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + BucketSeparator +
                (model ?? UnknownModel) + BucketSeparator + (mode ?? String.Empty) + BucketSeparator +
                (effort ?? String.Empty);
        }

        private static long SafeAdd(long left, long right)
        {
            if (right > 0 && left > Int64.MaxValue - right)
                return Int64.MaxValue;
            return left + right;
        }

        private static string SafeError(Exception exception)
        {
            string message = exception == null ? String.Empty : exception.Message;
            if (String.IsNullOrWhiteSpace(message))
                return "未知错误";
            return message.Length <= 160 ? message : message.Substring(0, 160);
        }

        private sealed class UsageCache
        {
            public int Version { get; set; }
            public Dictionary<string, FileCheckpoint> Files { get; set; }

            public UsageCache()
            {
                Version = CacheVersion;
                Files = new Dictionary<string, FileCheckpoint>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private sealed class FileCheckpoint
        {
            public string FileName { get; set; }
            public long Offset { get; set; }
            public long Length { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public string Model { get; set; }
            public string CollaborationMode { get; set; }
            public string Effort { get; set; }
            public Dictionary<string, long> Buckets { get; set; }

            public FileCheckpoint()
            {
                Reset();
            }

            public void Reset()
            {
                Offset = 0;
                Length = 0;
                LastWriteUtcTicks = 0;
                Model = UnknownModel;
                CollaborationMode = String.Empty;
                Effort = String.Empty;
                Buckets = new Dictionary<string, long>(StringComparer.Ordinal);
            }
        }
    }
}
