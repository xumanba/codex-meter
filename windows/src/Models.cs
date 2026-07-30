using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexMeter
{
    internal sealed class UsageWindow
    {
        public string Title { get; set; }
        public double UsedPercent { get; set; }
        public DateTimeOffset? ResetsAt { get; set; }
        public int? WindowMinutes { get; set; }
        public string ResetDescription { get; set; }

        public double RemainingPercent
        {
            get { return Math.Max(0, Math.Min(100, 100 - UsedPercent)); }
        }
    }

    internal sealed class PaceInfo
    {
        public double DeltaPercent { get; set; }
        public double ExpectedUsedPercent { get; set; }
        public double? EtaSeconds { get; set; }
        public bool WillLastToReset { get; set; }
        public bool IsTrendStable { get; set; }
    }

    internal sealed class UsageSnapshot
    {
        public UsageWindow Session { get; set; }
        public UsageWindow Weekly { get; set; }
        public List<UsageWindow> Extras { get; set; }
        public PaceInfo WeeklyPace { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string LoginMethod { get; set; }

        public UsageSnapshot()
        {
            Extras = new List<UsageWindow>();
        }
    }

    internal static class UsageSnapshotDecoder
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static UsageSnapshot Decode(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("CodexBar 返回了空数据。");

            object root = Serializer.DeserializeObject(json);
            IDictionary<string, object> payload = FirstPayload(root);
            IDictionary<string, object> providerError = AsDictionary(Get(payload, "error"));
            if (providerError != null)
            {
                string message = AsString(Get(providerError, "message"));
                if (!String.IsNullOrEmpty(message) &&
                    message.IndexOf("authentication required", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException("未检测到 Codex 登录，请先在 Codex 中登录后重试。");
                }

                string safeMessage = CodexBarClient.SanitizeDetail(message);
                throw new InvalidOperationException(String.IsNullOrEmpty(safeMessage)
                    ? "CodexBar 返回了提供商错误。"
                    : safeMessage);
            }

            IDictionary<string, object> usage = AsDictionary(Get(payload, "usage"));
            if (usage == null)
                throw new InvalidOperationException("CodexBar 响应中缺少 usage 数据。");

            UsageSnapshot snapshot = new UsageSnapshot();
            UsageWindow primary = DecodeWindow(AsDictionary(Get(usage, "primary")), "会话额度");
            UsageWindow secondary = DecodeWindow(AsDictionary(Get(usage, "secondary")), "每周额度");

            // Standard Codex payloads use primary=session and secondary=weekly.
            // Some Pro Lite payloads instead put the only real seven-day window in
            // primary and return a metadata-free secondary placeholder. Prefer the
            // window metadata over a positional label so the card matches Codex.
            if (IsSevenDayWindow(primary) && !HasWindowMetadata(secondary))
            {
                primary.Title = "每周额度";
                snapshot.Weekly = primary;
                snapshot.Session = null;
            }
            else
            {
                snapshot.Session = primary;
                snapshot.Weekly = secondary ?? primary;
            }
            snapshot.UpdatedAt = ParseDate(AsString(GetEither(usage, "updated_at", "updatedAt")));
            snapshot.LoginMethod = AsString(GetEither(usage, "login_method", "loginMethod"));

            object extraObject = GetEither(usage, "extra_rate_windows", "extraRateWindows");
            foreach (object item in AsEnumerable(extraObject))
            {
                IDictionary<string, object> extra = AsDictionary(item);
                if (extra == null)
                    continue;

                string rawTitle = AsString(Get(extra, "title"));
                string title = CleanExtraTitle(rawTitle);
                UsageWindow window = DecodeWindow(AsDictionary(Get(extra, "window")), title);
                if (window != null)
                    snapshot.Extras.Add(window);
            }

            snapshot.WeeklyPace = PaceCalculator.Calculate(snapshot.Weekly, DateTimeOffset.Now);
            return snapshot;
        }

        private static UsageWindow DecodeWindow(IDictionary<string, object> value, string title)
        {
            if (value == null)
                return null;

            object usedObject = GetEither(value, "used_percent", "usedPercent");
            if (usedObject == null)
                return null;

            UsageWindow window = new UsageWindow();
            window.Title = title;
            window.UsedPercent = Clamp(AsDouble(usedObject), 0, 100);
            window.ResetsAt = ParseDate(AsString(GetEither(value, "resets_at", "resetsAt")));
            window.WindowMinutes = AsNullableInt(GetEither(value, "window_minutes", "windowMinutes"));
            window.ResetDescription = AsString(GetEither(value, "reset_description", "resetDescription"));
            return window;
        }

        private static IDictionary<string, object> FirstPayload(object root)
        {
            IDictionary<string, object> direct = AsDictionary(root);
            if (direct != null)
                return direct;

            foreach (object item in AsEnumerable(root))
            {
                IDictionary<string, object> payload = AsDictionary(item);
                if (payload != null)
                    return payload;
            }

            throw new InvalidOperationException("CodexBar 用量响应为空。");
        }

        private static IEnumerable AsEnumerable(object value)
        {
            if (value == null || value is string)
                return new object[0];

            IEnumerable enumerable = value as IEnumerable;
            return enumerable ?? new object[0];
        }

        private static IDictionary<string, object> AsDictionary(object value)
        {
            return value as IDictionary<string, object>;
        }

        private static object Get(IDictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
                return null;

            object value;
            return dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static object GetEither(IDictionary<string, object> dictionary, string first, string second)
        {
            object value = Get(dictionary, first);
            return value ?? Get(dictionary, second);
        }

        private static string AsString(object value)
        {
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static double AsDouble(object value)
        {
            if (value == null)
                return 0;

            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static int? AsNullableInt(object value)
        {
            if (value == null)
                return null;

            int result;
            if (Int32.TryParse(AsString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return result;

            double number;
            if (Double.TryParse(AsString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return Convert.ToInt32(number);

            return null;
        }

        private static DateTimeOffset? ParseDate(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return null;

            // DateTimeOffset on .NET Framework accepts at most seven fractional digits.
            string normalized = Regex.Replace(value, @"(\.\d{7})\d+(?=Z|[+-]\d\d:\d\d$)", "$1");
            DateTimeOffset result;
            if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }

            return null;
        }

        private static string CleanExtraTitle(string rawTitle)
        {
            if (String.IsNullOrWhiteSpace(rawTitle))
                return "附加额度";

            string title = rawTitle.Replace("Codex ", "").Replace(" Weekly", "").Trim();
            return String.IsNullOrWhiteSpace(title) ? "附加额度" : title;
        }

        private static bool HasWindowMetadata(UsageWindow window)
        {
            return window != null && (window.WindowMinutes.HasValue || window.ResetsAt.HasValue ||
                !String.IsNullOrWhiteSpace(window.ResetDescription));
        }

        private static bool IsSevenDayWindow(UsageWindow window)
        {
            return window != null && window.WindowMinutes.HasValue && window.WindowMinutes.Value >= 7 * 24 * 60;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal static class PaceCalculator
    {
        private static readonly double DailyAllowanceSeconds = TimeSpan.FromDays(1).TotalSeconds;
        private static readonly double StableTrendSeconds = TimeSpan.FromHours(6).TotalSeconds;

        public static PaceInfo Calculate(UsageWindow window, DateTimeOffset now)
        {
            if (window == null || !window.ResetsAt.HasValue || !window.WindowMinutes.HasValue || window.WindowMinutes.Value <= 0)
                return null;

            double totalSeconds = TimeSpan.FromMinutes(window.WindowMinutes.Value).TotalSeconds;
            double secondsToReset = Math.Max(0, (window.ResetsAt.Value - now).TotalSeconds);
            double elapsedSeconds = Math.Max(0, Math.Min(totalSeconds, totalSeconds - secondsToReset));

            // The warning marker represents the allowance available during the
            // current 24-hour block, measured from the actual quota reset time.
            // This gives a fresh seven-day window one seventh of its budget
            // immediately, then advances the marker once per completed day.
            double allowanceBlockSeconds = Math.Min(DailyAllowanceSeconds, totalSeconds);
            double availableBlocks = Math.Floor(elapsedSeconds / allowanceBlockSeconds) + 1;
            double allowedSeconds = Math.Min(totalSeconds, availableBlocks * allowanceBlockSeconds);
            double expectedUsed = Math.Max(0, Math.Min(100, allowedSeconds / totalSeconds * 100));

            double? eta = null;
            bool willLast = true;
            if (window.UsedPercent > 0.001 && elapsedSeconds > 1)
            {
                double usedPerSecond = window.UsedPercent / elapsedSeconds;
                eta = Math.Max(0, 100 - window.UsedPercent) / usedPerSecond;
                willLast = eta.Value >= secondsToReset;
            }

            PaceInfo pace = new PaceInfo();
            pace.DeltaPercent = window.UsedPercent - expectedUsed;
            pace.ExpectedUsedPercent = expectedUsed;
            pace.EtaSeconds = eta;
            pace.WillLastToReset = willLast;
            pace.IsTrendStable = elapsedSeconds >= Math.Min(StableTrendSeconds, totalSeconds);
            return pace;
        }
    }
}
