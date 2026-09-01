using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CodexMeter
{
    internal static class DashboardPresentation
    {
        internal const int DesignWidth = 328;
        internal const int HeaderHeight = 58;
        internal const int MeterHeight = 72;
        internal const int PaceHeight = 30;
        internal const int DailyUsageHeight = 116;
        internal const int ModelHeaderHeight = 26;
        internal const int ModelRowHeight = 36;
        internal const int MaximumModelRows = 4;
        internal const int BottomPadding = 10;

        internal static int ContentHeight(bool hasSnapshot, bool hasWeeklyPace,
            bool detailsExpanded)
        {
            if (!hasSnapshot)
                return 126;

            int compactHeight = HeaderHeight + MeterHeight + BottomPadding +
                (hasWeeklyPace ? PaceHeight : 0);
            if (!detailsExpanded)
                return compactHeight;

            return compactHeight + DailyUsageHeight + ModelHeaderHeight +
                (MaximumModelRows * ModelRowHeight);
        }

        internal static RectangleF PaceForecastBounds(int y)
        {
            return new RectangleF(132, y + 3, DesignWidth - 166, 23);
        }

        internal static double DailyQuotaPercent(
            long dailyTokens, long totalTokens, double usedPercent)
        {
            if (dailyTokens <= 0 || totalTokens <= 0 || usedPercent <= 0)
                return 0;
            return Math.Max(0, usedPercent * dailyTokens / totalTokens);
        }

        internal static List<ModelTokenUsage> VisibleModelRows(
            IEnumerable<ModelTokenUsage> models, int maximumRows)
        {
            List<ModelTokenUsage> sorted = (models ?? new ModelTokenUsage[0])
                .Where(item => item != null && item.Tokens > 0)
                .OrderByDescending(item => item.Tokens)
                .ToList();
            if (maximumRows <= 0)
                return new List<ModelTokenUsage>();
            if (sorted.Count <= maximumRows)
                return sorted;

            List<ModelTokenUsage> visible = sorted.Take(maximumRows - 1).ToList();
            visible.Add(new ModelTokenUsage
            {
                Model = "other",
                Tokens = sorted.Skip(maximumRows - 1).Sum(item => item.Tokens)
            });
            return visible;
        }

        internal static string ModelLabel(ModelTokenUsage usage)
        {
            if (usage == null)
                return String.Empty;
            if (String.Equals(usage.Model, "other", StringComparison.OrdinalIgnoreCase))
                return "其他";

            List<string> parts = new List<string>();
            parts.Add(WeeklyUsageReader.DisplayModelName(usage.Model));
            string mode = DisplayCollaborationMode(usage.CollaborationMode);
            if (!String.IsNullOrEmpty(mode) &&
                !String.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(mode);
            }
            string effort = WeeklyUsageReader.DisplayEffort(usage.Effort);
            if (!String.IsNullOrEmpty(effort))
                parts.Add(effort);
            return String.Join(" · ", parts.ToArray());
        }

        internal static string ChineseWeekday(DayOfWeek day)
        {
            string[] labels = new string[] { "日", "一", "二", "三", "四", "五", "六" };
            return labels[(int)day];
        }

        internal static Color UsageAccent(double percentage, bool darkTheme)
        {
            double clamped = Math.Max(0d, Math.Min(100d, percentage));
            Color low = darkTheme
                ? Color.FromArgb(136, 150, 166)
                : Color.FromArgb(100, 119, 139);
            Color middle = darkTheme
                ? Color.FromArgb(67, 177, 223)
                : Color.FromArgb(39, 157, 210);
            Color high = darkTheme
                ? Color.FromArgb(67, 215, 255)
                : Color.FromArgb(20, 92, 245);

            if (clamped <= 40d)
                return InterpolateColor(low, middle, clamped / 40d);
            return InterpolateColor(middle, high, (clamped - 40d) / 60d);
        }

        private static string DisplayCollaborationMode(string mode)
        {
            if (String.IsNullOrWhiteSpace(mode))
                return String.Empty;
            string[] words = mode.Replace('-', ' ').Replace('_', ' ')
                .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
                words[index] = Char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
            return String.Join(" ", words);
        }

        private static Color InterpolateColor(Color start, Color end, double amount)
        {
            amount = Math.Max(0d, Math.Min(1d, amount));
            return Color.FromArgb(
                Convert.ToInt32(Math.Round(start.R + ((end.R - start.R) * amount))),
                Convert.ToInt32(Math.Round(start.G + ((end.G - start.G) * amount))),
                Convert.ToInt32(Math.Round(start.B + ((end.B - start.B) * amount))));
        }
    }
}
