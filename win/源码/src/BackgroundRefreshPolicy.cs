using System;

namespace CodexMeter
{
    internal static class BackgroundRefreshPolicy
    {
        internal static bool ShouldRun(bool running, DateTimeOffset? lastAttempt,
            DateTimeOffset now, TimeSpan minimumInterval)
        {
            if (running)
                return false;
            if (!lastAttempt.HasValue)
                return true;
            return now - lastAttempt.Value >= minimumInterval;
        }
    }
}
