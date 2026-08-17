using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexMeter
{
    internal enum ResetConfidence
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    internal sealed class ResetHistoryEntry
    {
        public long ResetUnixSeconds { get; set; }
        public long DetectedUnixSeconds { get; set; }
        public double BeforeUsedPercent { get; set; }
        public double AfterUsedPercent { get; set; }
        public int Confidence { get; set; }
        public int EvidenceCount { get; set; }
        public string Kind { get; set; }

        [ScriptIgnore]
        public DateTimeOffset ResetAt
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(ResetUnixSeconds); }
        }

        [ScriptIgnore]
        public bool IsEstimated
        {
            get { return !String.Equals(Kind, "observed", StringComparison.OrdinalIgnoreCase); }
        }
    }

    internal sealed class ResetHistoryReport
    {
        public List<ResetHistoryEntry> Entries { get; set; }
        public TimeSpan? AverageInterval { get; set; }
        public TimeSpan? ShortestInterval { get; set; }
        public TimeSpan? LongestInterval { get; set; }
        public int AverageIntervalCount { get; set; }
        public long ImportedSnapshots { get; set; }
        public string Error { get; set; }

        public ResetHistoryReport()
        {
            Entries = new List<ResetHistoryEntry>();
        }
    }

    internal sealed class ResetHistorySample
    {
        public long ObservedUnixSeconds { get; set; }
        public double UsedPercent { get; set; }
        public long ResetUnixSeconds { get; set; }

        [ScriptIgnore]
        public DateTimeOffset ObservedAt
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(ObservedUnixSeconds); }
        }

        [ScriptIgnore]
        public DateTimeOffset ResetAt
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(ResetUnixSeconds); }
        }
    }

    internal sealed class ResetHistoryStore
    {
        private const int DataVersion = 1;
        private const int WeeklyWindowMinutes = 10080;
        private const int MaximumLineLength = 1024 * 1024;
        private static readonly TimeSpan MergeTolerance = TimeSpan.FromMinutes(30);

        private readonly object syncRoot = new object();
        private readonly string sessionsRoot;
        private readonly string archivedSessionsRoot;
        private readonly string historyPath;
        private readonly JavaScriptSerializer serializer;
        private ResetHistoryData data;

        public ResetHistoryStore()
            : this(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex", "sessions"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex", "archived_sessions"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexMeter", "reset-history.json"))
        {
        }

        internal ResetHistoryStore(string sessionsRoot, string archivedSessionsRoot, string historyPath)
        {
            this.sessionsRoot = sessionsRoot;
            this.archivedSessionsRoot = archivedSessionsRoot;
            this.historyPath = historyPath;
            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
            serializer.RecursionLimit = 100;
            data = Load();
        }

        public ResetHistoryReport Read()
        {
            lock (syncRoot)
                return BuildReport(data, null);
        }

        public ResetHistoryReport Observe(UsageWindow window, DateTimeOffset observedAt)
        {
            if (window == null || !window.ResetsAt.HasValue ||
                window.WindowMinutes.GetValueOrDefault(WeeklyWindowMinutes) != WeeklyWindowMinutes)
            {
                return Read();
            }

            ResetHistorySample current = new ResetHistorySample
            {
                ObservedUnixSeconds = observedAt.ToUnixTimeSeconds(),
                UsedPercent = Math.Max(0, Math.Min(100, window.UsedPercent)),
                ResetUnixSeconds = window.ResetsAt.Value.ToUnixTimeSeconds()
            };

            lock (syncRoot)
            {
                ResetHistorySample previous = data.LiveSample;
                ResetHistoryEntry candidate = DetectTransition(previous, current, "live");
                if (candidate != null)
                    MergeEntry(data.Entries, candidate);
                ResetHistoryEntry inferredWindowStart = InferWindowStart(current);
                bool addedWindowStart = MergeInferredEntry(data.Entries, inferredWindowStart);
                data.LiveSample = current;
                bool shouldSave = candidate != null || addedWindowStart || previous == null ||
                    previous.ResetUnixSeconds != current.ResetUnixSeconds ||
                    Math.Abs(previous.UsedPercent - current.UsedPercent) >= 0.01 ||
                    current.ObservedUnixSeconds - previous.ObservedUnixSeconds >= 15 * 60;
                if (shouldSave)
                    Save(data);
                return BuildReport(data, null);
            }
        }

        public ResetHistoryReport ImportLocalHistory()
        {
            string error = null;
            long importedSnapshots = 0;
            List<ResetHistoryEntry> candidates = new List<ResetHistoryEntry>();
            List<ResetHistoryEntry> inferredWindowStarts = new List<ResetHistoryEntry>();

            try
            {
                Dictionary<string, string> files = DiscoverFiles();
                foreach (KeyValuePair<string, string> file in files)
                {
                    ResetLogCheckpoint checkpoint;
                    lock (syncRoot)
                    {
                        ResetLogCheckpoint stored;
                        data.Files.TryGetValue(file.Key, out stored);
                        checkpoint = CloneCheckpoint(stored);
                    }

                    try
                    {
                        importedSnapshots += ScanFile(
                            file.Value, checkpoint, candidates, inferredWindowStarts);
                        ResetHistoryEntry checkpointWindowStart =
                            InferWindowStart(checkpoint.LastSample);
                        if (checkpointWindowStart != null)
                            inferredWindowStarts.Add(checkpointWindowStart);
                        lock (syncRoot)
                            data.Files[file.Key] = checkpoint;
                    }
                    catch (IOException)
                    {
                        // A live rollout may move while it is being archived. The next run retries it.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Inaccessible optional logs do not prevent future live observations.
                    }
                }
            }
            catch (Exception exception)
            {
                error = SafeError(exception);
            }

            lock (syncRoot)
            {
                foreach (ResetHistoryEntry candidate in candidates)
                    MergeEntry(data.Entries, candidate);
                foreach (ResetHistoryEntry inferredWindowStart in inferredWindowStarts)
                    MergeInferredEntry(data.Entries, inferredWindowStart);
                data.ImportedSnapshots += importedSnapshots;
                data.LastImportUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Save(data);
                return BuildReport(data, error);
            }
        }

        internal static ResetHistoryEntry DetectTransition(
            ResetHistorySample previous, ResetHistorySample current, string source)
        {
            if (previous == null || current == null ||
                previous.ResetUnixSeconds <= 0 || current.ResetUnixSeconds <= 0 ||
                current.ObservedUnixSeconds <= previous.ObservedUnixSeconds)
            {
                return null;
            }

            DateTimeOffset previousObserved = previous.ObservedAt;
            DateTimeOffset currentObserved = current.ObservedAt;
            DateTimeOffset previousReset = previous.ResetAt;
            DateTimeOffset currentReset = current.ResetAt;
            TimeSpan observationGap = currentObserved - previousObserved;
            double drop = previous.UsedPercent - current.UsedPercent;
            bool strongDrop = previous.UsedPercent >= 20 && drop >= 20 &&
                current.UsedPercent <= Math.Max(10, previous.UsedPercent * 0.4);
            bool lowUsageReset = previous.UsedPercent >= 5 && current.UsedPercent <= 1 && drop >= 4;

            if ((!strongDrop && !lowUsageReset) || observationGap > TimeSpan.FromDays(14) ||
                currentReset <= previousReset.AddHours(12) ||
                previousObserved > previousReset.AddHours(2) ||
                currentObserved < previousReset.AddMinutes(-5))
            {
                return null;
            }

            bool tightlyObserved = previousReset - previousObserved <= TimeSpan.FromHours(24) &&
                currentObserved - previousReset <= TimeSpan.FromHours(24) &&
                observationGap <= TimeSpan.FromHours(48);
            bool directObservation = String.Equals(source, "live", StringComparison.OrdinalIgnoreCase) &&
                observationGap <= TimeSpan.FromHours(2);
            ResetConfidence confidence;
            if (directObservation || (tightlyObserved && strongDrop))
                confidence = ResetConfidence.High;
            else if (tightlyObserved)
                confidence = ResetConfidence.Medium;
            else
                confidence = ResetConfidence.Low;

            return new ResetHistoryEntry
            {
                ResetUnixSeconds = previous.ResetUnixSeconds,
                DetectedUnixSeconds = current.ObservedUnixSeconds,
                BeforeUsedPercent = previous.UsedPercent,
                AfterUsedPercent = current.UsedPercent,
                Confidence = (int)confidence,
                EvidenceCount = 1,
                Kind = directObservation ? "observed" : "estimated"
            };
        }

        internal static ResetHistoryEntry InferWindowStart(ResetHistorySample sample)
        {
            if (sample == null || sample.ResetUnixSeconds <= 0 ||
                sample.ObservedUnixSeconds <= 0)
            {
                return null;
            }

            DateTimeOffset windowStart = sample.ResetAt.AddMinutes(-WeeklyWindowMinutes);
            DateTimeOffset observedAt = sample.ObservedAt;
            if (observedAt < windowStart.AddMinutes(-5) ||
                observedAt > sample.ResetAt.AddMinutes(5))
            {
                return null;
            }

            return new ResetHistoryEntry
            {
                ResetUnixSeconds = windowStart.ToUnixTimeSeconds(),
                DetectedUnixSeconds = sample.ObservedUnixSeconds,
                BeforeUsedPercent = 0,
                AfterUsedPercent = Math.Max(0, Math.Min(100, sample.UsedPercent)),
                Confidence = (int)ResetConfidence.Medium,
                EvidenceCount = 1,
                Kind = "estimated"
            };
        }

        internal static ResetHistoryReport BuildReportForTests(IEnumerable<ResetHistoryEntry> entries)
        {
            ResetHistoryData testData = new ResetHistoryData();
            testData.Entries = (entries ?? new ResetHistoryEntry[0]).ToList();
            return BuildReport(testData, null);
        }

        private long ScanFile(string path, ResetLogCheckpoint checkpoint,
            ICollection<ResetHistoryEntry> candidates,
            ICollection<ResetHistoryEntry> inferredWindowStarts)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                return 0;
            if (checkpoint.Offset > info.Length)
                checkpoint.Reset();
            if (checkpoint.Offset == info.Length)
                return 0;

            long parsedSnapshots = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (checkpoint.Offset > 0 && checkpoint.Offset <= stream.Length)
                    stream.Seek(checkpoint.Offset, SeekOrigin.Begin);
                else
                    checkpoint.Reset();

                ReadBoundedLines(stream, MaximumLineLength, delegate(string line)
                {
                    ResetHistorySample sample;
                    if (!TryParseRateLimitSample(line, out sample))
                        return;

                    parsedSnapshots++;
                    ResetHistorySample previous = checkpoint.LastSample;
                    if (previous == null || Math.Abs(
                            previous.ResetUnixSeconds - sample.ResetUnixSeconds) >
                        MergeTolerance.TotalSeconds)
                    {
                        ResetHistoryEntry inferredWindowStart = InferWindowStart(sample);
                        if (inferredWindowStart != null)
                            inferredWindowStarts.Add(inferredWindowStart);
                    }
                    ResetHistoryEntry candidate = DetectTransition(
                        previous, sample, "local_log");
                    if (candidate != null)
                        candidates.Add(candidate);
                    checkpoint.LastSample = sample;
                });

                checkpoint.Offset = stream.Length;
            }
            checkpoint.LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
            return parsedSnapshots;
        }

        private static void ReadBoundedLines(
            FileStream stream, int maximumBytes, Action<string> processLine)
        {
            byte[] buffer = new byte[65536];
            using (MemoryStream lineBuffer = new MemoryStream(Math.Min(maximumBytes, 65536)))
            {
                bool oversized = false;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    int offset = 0;
                    while (offset < read)
                    {
                        int newline = Array.IndexOf(buffer, (byte)'\n', offset, read - offset);
                        int end = newline >= 0 ? newline : read;
                        int count = end - offset;
                        if (!oversized && count > 0)
                        {
                            if (lineBuffer.Length + count <= maximumBytes)
                                lineBuffer.Write(buffer, offset, count);
                            else
                            {
                                oversized = true;
                                lineBuffer.SetLength(0);
                            }
                        }

                        if (newline < 0)
                            break;

                        if (!oversized)
                            EmitBufferedLine(lineBuffer, processLine);
                        lineBuffer.SetLength(0);
                        oversized = false;
                        offset = newline + 1;
                    }
                }

                if (!oversized && lineBuffer.Length > 0)
                    EmitBufferedLine(lineBuffer, processLine);
            }
        }

        private static void EmitBufferedLine(MemoryStream buffer, Action<string> processLine)
        {
            int length = Convert.ToInt32(buffer.Length);
            byte[] bytes = buffer.GetBuffer();
            if (length > 0 && bytes[length - 1] == (byte)'\r')
                length--;
            if (length <= 0)
                return;
            processLine(Encoding.UTF8.GetString(bytes, 0, length));
        }

        internal static bool TryParseRateLimitSample(string line, out ResetHistorySample sample)
        {
            sample = null;
            if (String.IsNullOrEmpty(line) ||
                !String.Equals(ExtractJsonString(line, "\"type\"", 0),
                    "event_msg", StringComparison.Ordinal))
            {
                return false;
            }

            int payloadIndex = line.IndexOf("\"payload\"", StringComparison.Ordinal);
            if (payloadIndex < 0 || !String.Equals(
                    ExtractJsonString(line, "\"type\"", payloadIndex),
                    "token_count", StringComparison.Ordinal))
            {
                return false;
            }

            int rateLimitIndex = line.IndexOf("\"rate_limits\"", payloadIndex, StringComparison.Ordinal);
            if (rateLimitIndex < 0 || !String.Equals(
                    ExtractJsonString(line, "\"limit_id\"", rateLimitIndex),
                    "codex", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int primaryIndex = line.IndexOf("\"primary\"", rateLimitIndex, StringComparison.Ordinal);
            if (primaryIndex < 0)
                return false;

            long resetUnixSeconds;
            long windowMinutes;
            double usedPercent;
            if (!TryExtractDouble(line, "\"used_percent\"", primaryIndex, out usedPercent) ||
                !TryExtractLong(line, "\"window_minutes\"", primaryIndex, out windowMinutes) ||
                windowMinutes != WeeklyWindowMinutes ||
                !TryExtractLong(line, "\"resets_at\"", primaryIndex, out resetUnixSeconds) ||
                resetUnixSeconds <= 0)
            {
                return false;
            }

            string timestamp = ExtractJsonString(line, "\"timestamp\"", 0);
            DateTimeOffset observedAt;
            if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out observedAt))
            {
                return false;
            }

            sample = new ResetHistorySample
            {
                ObservedUnixSeconds = observedAt.ToUnixTimeSeconds(),
                UsedPercent = Math.Max(0, Math.Min(100, usedPercent)),
                ResetUnixSeconds = resetUnixSeconds
            };
            return true;
        }

        private Dictionary<string, string> DiscoverFiles()
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddFiles(result, sessionsRoot);
            AddFiles(result, archivedSessionsRoot);
            return result;
        }

        private static void AddFiles(IDictionary<string, string> result, string root)
        {
            if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            foreach (string path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                try
                {
                    string name = Path.GetFileName(path);
                    string existing;
                    if (!result.TryGetValue(name, out existing) ||
                        File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(existing))
                    {
                        result[name] = path;
                    }
                }
                catch
                {
                    // A concurrently moved rollout is retried on the next application start.
                }
            }
        }

        private ResetHistoryData Load()
        {
            if (String.IsNullOrWhiteSpace(historyPath) || !File.Exists(historyPath))
                return new ResetHistoryData();

            try
            {
                ResetHistoryData loaded = serializer.Deserialize<ResetHistoryData>(
                    File.ReadAllText(historyPath, Encoding.UTF8));
                if (loaded == null || loaded.Version != DataVersion)
                    return new ResetHistoryData();
                loaded.Normalize();
                return loaded;
            }
            catch
            {
                return new ResetHistoryData();
            }
        }

        private void Save(ResetHistoryData value)
        {
            if (String.IsNullOrWhiteSpace(historyPath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(historyPath);
                if (!String.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(historyPath, serializer.Serialize(value), new UTF8Encoding(false));
            }
            catch
            {
                // A failed cache write must not interrupt quota synchronization.
            }
        }

        private static ResetHistoryReport BuildReport(ResetHistoryData value, string error)
        {
            ResetHistoryReport report = new ResetHistoryReport();
            report.Entries = (value.Entries ?? new List<ResetHistoryEntry>())
                .Where(item => item != null && item.ResetUnixSeconds > 0)
                .OrderByDescending(item => item.ResetUnixSeconds)
                .Select(CloneEntry)
                .ToList();
            report.ImportedSnapshots = value.ImportedSnapshots;
            report.Error = error;

            List<ResetHistoryEntry> reliable = report.Entries
                .Where(item => item.Confidence >= (int)ResetConfidence.Medium)
                .OrderBy(item => item.ResetUnixSeconds)
                .ToList();
            List<double> intervals = new List<double>();
            for (int index = 1; index < reliable.Count; index++)
            {
                double hours = (reliable[index].ResetAt - reliable[index - 1].ResetAt).TotalHours;
                if (hours >= 24 && hours <= 24 * 14)
                    intervals.Add(hours);
            }

            if (intervals.Count > 0)
            {
                report.AverageInterval = TimeSpan.FromHours(intervals.Average());
                report.ShortestInterval = TimeSpan.FromHours(intervals.Min());
                report.LongestInterval = TimeSpan.FromHours(intervals.Max());
                report.AverageIntervalCount = intervals.Count;
            }
            return report;
        }

        private static void MergeEntry(IList<ResetHistoryEntry> entries, ResetHistoryEntry candidate)
        {
            ResetHistoryEntry existing = entries
                .Where(item => item != null)
                .OrderBy(item => Math.Abs(item.ResetUnixSeconds - candidate.ResetUnixSeconds))
                .FirstOrDefault(item => Math.Abs(item.ResetUnixSeconds - candidate.ResetUnixSeconds) <=
                    MergeTolerance.TotalSeconds);
            if (existing == null)
            {
                entries.Add(CloneEntry(candidate));
                return;
            }

            existing.EvidenceCount = Math.Max(1, existing.EvidenceCount) +
                Math.Max(1, candidate.EvidenceCount);
            existing.BeforeUsedPercent = Math.Max(existing.BeforeUsedPercent, candidate.BeforeUsedPercent);
            existing.AfterUsedPercent = Math.Min(existing.AfterUsedPercent, candidate.AfterUsedPercent);
            existing.DetectedUnixSeconds = Math.Max(existing.DetectedUnixSeconds, candidate.DetectedUnixSeconds);
            if (candidate.Confidence > existing.Confidence)
            {
                existing.Confidence = candidate.Confidence;
                existing.ResetUnixSeconds = candidate.ResetUnixSeconds;
            }
            if (String.Equals(candidate.Kind, "observed", StringComparison.OrdinalIgnoreCase))
                existing.Kind = "observed";
            if (existing.Confidence < (int)ResetConfidence.Medium &&
                existing.EvidenceCount >= 2)
            {
                existing.Confidence = (int)ResetConfidence.Medium;
            }
        }

        private static bool MergeInferredEntry(
            IList<ResetHistoryEntry> entries, ResetHistoryEntry candidate)
        {
            if (candidate == null)
                return false;

            ResetHistoryEntry existing = entries
                .Where(item => item != null)
                .OrderBy(item => Math.Abs(item.ResetUnixSeconds - candidate.ResetUnixSeconds))
                .FirstOrDefault(item => Math.Abs(item.ResetUnixSeconds - candidate.ResetUnixSeconds) <=
                    MergeTolerance.TotalSeconds);
            if (existing == null)
            {
                entries.Add(CloneEntry(candidate));
                return true;
            }

            bool changed = false;
            if (existing.Confidence < candidate.Confidence)
            {
                existing.Confidence = candidate.Confidence;
                existing.ResetUnixSeconds = candidate.ResetUnixSeconds;
                changed = true;
            }
            if (existing.EvidenceCount <= 0)
            {
                existing.EvidenceCount = 1;
                changed = true;
            }
            if (existing.DetectedUnixSeconds <= 0 && candidate.DetectedUnixSeconds > 0)
            {
                existing.DetectedUnixSeconds = candidate.DetectedUnixSeconds;
                changed = true;
            }
            if (candidate.AfterUsedPercent < existing.AfterUsedPercent)
            {
                existing.AfterUsedPercent = candidate.AfterUsedPercent;
                changed = true;
            }
            return changed;
        }

        private static ResetHistoryEntry CloneEntry(ResetHistoryEntry item)
        {
            return new ResetHistoryEntry
            {
                ResetUnixSeconds = item.ResetUnixSeconds,
                DetectedUnixSeconds = item.DetectedUnixSeconds,
                BeforeUsedPercent = item.BeforeUsedPercent,
                AfterUsedPercent = item.AfterUsedPercent,
                Confidence = item.Confidence,
                EvidenceCount = item.EvidenceCount,
                Kind = item.Kind
            };
        }

        private static ResetLogCheckpoint CloneCheckpoint(ResetLogCheckpoint item)
        {
            if (item == null)
                return new ResetLogCheckpoint();
            return new ResetLogCheckpoint
            {
                Offset = item.Offset,
                LastWriteUtcTicks = item.LastWriteUtcTicks,
                LastSample = item.LastSample == null ? null : new ResetHistorySample
                {
                    ObservedUnixSeconds = item.LastSample.ObservedUnixSeconds,
                    UsedPercent = item.LastSample.UsedPercent,
                    ResetUnixSeconds = item.LastSample.ResetUnixSeconds
                }
            };
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
            string number;
            if (!TryExtractNumber(line, key, startIndex, out number))
                return false;
            return Int64.TryParse(number, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractDouble(string line, string key, int startIndex, out double value)
        {
            value = 0;
            string number;
            if (!TryExtractNumber(line, key, startIndex, out number))
                return false;
            return Double.TryParse(number, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractNumber(string line, string key, int startIndex, out string number)
        {
            number = null;
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
            while (end < line.Length && (Char.IsDigit(line[end]) || line[end] == '-' ||
                line[end] == '+' || line[end] == '.' || line[end] == 'e' || line[end] == 'E'))
            {
                end++;
            }
            if (end <= index)
                return false;
            number = line.Substring(index, end - index);
            return true;
        }

        private static string SafeError(Exception exception)
        {
            string message = exception == null ? String.Empty : exception.Message;
            if (String.IsNullOrWhiteSpace(message))
                return "未知错误";
            return message.Length <= 160 ? message : message.Substring(0, 160);
        }

        private sealed class ResetHistoryData
        {
            public int Version { get; set; }
            public Dictionary<string, ResetLogCheckpoint> Files { get; set; }
            public ResetHistorySample LiveSample { get; set; }
            public List<ResetHistoryEntry> Entries { get; set; }
            public long ImportedSnapshots { get; set; }
            public long LastImportUnixSeconds { get; set; }

            public ResetHistoryData()
            {
                Version = DataVersion;
                Files = new Dictionary<string, ResetLogCheckpoint>(StringComparer.OrdinalIgnoreCase);
                Entries = new List<ResetHistoryEntry>();
            }

            public void Normalize()
            {
                if (Files == null)
                    Files = new Dictionary<string, ResetLogCheckpoint>(StringComparer.OrdinalIgnoreCase);
                else
                    Files = new Dictionary<string, ResetLogCheckpoint>(Files, StringComparer.OrdinalIgnoreCase);
                if (Entries == null)
                    Entries = new List<ResetHistoryEntry>();
            }
        }

        private sealed class ResetLogCheckpoint
        {
            public long Offset { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public ResetHistorySample LastSample { get; set; }

            public void Reset()
            {
                Offset = 0;
                LastWriteUtcTicks = 0;
                LastSample = null;
            }
        }
    }
}
