using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CodexMeter
{
    internal static class ResetHistoryPresentation
    {
        internal static string AverageText(ResetHistoryReport value)
        {
            if (value == null || !value.AverageInterval.HasValue || value.AverageIntervalCount <= 0)
                return "平均间隔：样本不足";

            return "平均间隔 " + IntervalText(value.AverageInterval) + " · " +
                value.AverageIntervalCount + " 个区间";
        }

        internal static string IntervalText(TimeSpan? value)
        {
            if (!value.HasValue)
                return "样本不足";

            int totalMinutes = Math.Max(0,
                Convert.ToInt32(Math.Round(Math.Abs(value.Value.TotalMinutes))));
            int days = totalMinutes / (24 * 60);
            int hours = (totalMinutes / 60) % 24;
            int minutes = totalMinutes % 60;
            if (days > 0)
                return days + "天" + (hours > 0
                    ? hours + "小时"
                    : (minutes > 0 ? minutes + "分钟" : String.Empty));
            if (hours > 0)
                return hours + "小时" + (minutes > 0 ? minutes + "分钟" : String.Empty);
            return minutes + "分钟";
        }

        internal static string ForecastText(ResetHistoryReport value, TimeSpan? interval,
            string label, DateTimeOffset now)
        {
            ResetHistoryEntry latest = LatestReliableEntry(value);
            if (latest == null || !interval.HasValue)
                return label + "间隔：样本不足";

            DateTimeOffset predicted = latest.ResetAt.Add(interval.Value);
            TimeSpan remaining = predicted - now;
            string timing = remaining >= TimeSpan.Zero
                ? "预计还有 " + IntervalText(remaining)
                : "预计时间已过 " + IntervalText(remaining.Duration()) +
                    "，尚未检测到新重置";
            return "按" + label + "间隔推算\r\n预计重置：" +
                predicted.ToLocalTime().ToString("M月d日 HH:mm") + "\r\n" + timing;
        }

        internal static string ForecastInlineText(ResetHistoryReport value, TimeSpan? interval,
            string label, DateTimeOffset now)
        {
            ResetHistoryEntry latest = LatestReliableEntry(value);
            if (latest == null || !interval.HasValue)
                return label + "间隔：样本不足";

            DateTimeOffset predicted = latest.ResetAt.Add(interval.Value);
            TimeSpan remaining = predicted - now;
            string timing = remaining >= TimeSpan.Zero
                ? "还有 " + IntervalText(remaining)
                : "已过 " + IntervalText(remaining.Duration()) + "，尚未检测到新重置";
            return label + "推算：" + predicted.ToLocalTime().ToString("M月d日 HH:mm") +
                " · " + timing;
        }

        internal static float TimelineX(long timestamp, long oldest, long newest,
            float left, float right)
        {
            if (newest <= oldest)
                return (left + right) / 2f;
            double ratio = (timestamp - oldest) / (double)(newest - oldest);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return left + Convert.ToSingle((right - left) * ratio);
        }

        internal static List<DateTimeOffset> TimelineDays(IList<ResetHistoryEntry> entries)
        {
            List<ResetHistoryEntry> chronological = entries == null
                ? new List<ResetHistoryEntry>()
                : entries.Where(item => item != null)
                    .OrderBy(item => item.ResetUnixSeconds)
                    .ToList();
            if (chronological.Count == 0)
                return new List<DateTimeOffset>();

            DateTime firstDate = chronological[0].ResetAt.ToLocalTime().Date;
            DateTime lastDate = chronological[chronological.Count - 1]
                .ResetAt.ToLocalTime().Date.AddDays(1);
            List<DateTimeOffset> days = new List<DateTimeOffset>();
            for (DateTime date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                DateTime local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
                days.Add(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)));
            }
            return days;
        }

        internal static float TimelineDayCoordinate(DateTimeOffset timestamp,
            IList<DateTimeOffset> days)
        {
            if (days == null || days.Count < 2)
                return 0f;

            long value = timestamp.ToUnixTimeSeconds();
            if (value <= days[0].ToUnixTimeSeconds())
                return 0f;
            int last = days.Count - 1;
            if (value >= days[last].ToUnixTimeSeconds())
                return last;

            for (int index = 0; index < last; index++)
            {
                long start = days[index].ToUnixTimeSeconds();
                long end = days[index + 1].ToUnixTimeSeconds();
                if (value > end)
                    continue;
                double fraction = end <= start
                    ? 0d
                    : (value - start) / (double)(end - start);
                return index + Convert.ToSingle(Math.Max(0d, Math.Min(1d, fraction)));
            }
            return last;
        }

        internal static Color TimelineConfidenceColor(ResetHistoryEntry entry,
            bool darkTheme)
        {
            int confidence = entry == null ? 0 : entry.Confidence;
            if (confidence >= (int)ResetConfidence.High)
                return darkTheme
                    ? Color.FromArgb(67, 222, 160)
                    : Color.FromArgb(18, 158, 103);
            if (confidence >= (int)ResetConfidence.Medium)
                return darkTheme
                    ? Color.FromArgb(74, 199, 255)
                    : Color.FromArgb(20, 132, 218);
            return darkTheme
                ? Color.FromArgb(255, 104, 111)
                : Color.FromArgb(218, 65, 72);
        }

        internal static string EntryStateText(ResetHistoryEntry entry)
        {
            if (entry == null)
                return String.Empty;
            string state;
            if (String.Equals(entry.Source, ResetHistorySource.ProviderWindow,
                    StringComparison.OrdinalIgnoreCase))
                state = "服务窗口";
            else if (String.Equals(entry.Source, ResetHistorySource.LiveTransition,
                    StringComparison.OrdinalIgnoreCase))
                state = "实时检测";
            else if (String.Equals(entry.Source, ResetHistorySource.LocalLogTransition,
                    StringComparison.OrdinalIgnoreCase))
                state = "日志推算";
            else
                state = entry.IsEstimated ? "历史推算" : "历史检测";
            string confidence = entry.Confidence >= (int)ResetConfidence.High
                ? "高" : (entry.Confidence >= (int)ResetConfidence.Medium ? "中" : "低");
            return state + " · " + confidence;
        }

        private static ResetHistoryEntry LatestReliableEntry(ResetHistoryReport value)
        {
            if (value == null || value.Entries == null)
                return null;
            return value.Entries
                .Where(item => item != null &&
                    item.Confidence >= (int)ResetConfidence.Medium)
                .OrderByDescending(item => item.ResetUnixSeconds)
                .FirstOrDefault();
        }
    }
}
