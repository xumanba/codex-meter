using System;

namespace CodexMeter
{
    internal enum DashboardStatusKind
    {
        Syncing,
        Stale,
        Live,
        Offline
    }

    internal static class DashboardStatusPolicy
    {
        internal static bool IsSnapshotStale(
            UsageSnapshot currentSnapshot,
            bool connected,
            string error,
            DateTimeOffset? successfulRefreshAt,
            int refreshMilliseconds,
            DateTimeOffset now)
        {
            if (currentSnapshot == null)
                return false;
            if (!connected || !String.IsNullOrWhiteSpace(error) || !successfulRefreshAt.HasValue)
                return true;

            DateTimeOffset effectiveFreshness = successfulRefreshAt.Value;
            if (currentSnapshot.UpdatedAt.HasValue &&
                currentSnapshot.UpdatedAt.Value <= now.AddMinutes(5) &&
                currentSnapshot.UpdatedAt.Value < effectiveFreshness)
            {
                effectiveFreshness = currentSnapshot.UpdatedAt.Value;
            }

            double staleAfterMilliseconds = Math.Max(180000,
                Math.Max(1000, refreshMilliseconds) * 2.5);
            return (now - effectiveFreshness).TotalMilliseconds > staleAfterMilliseconds;
        }

        internal static DashboardStatusKind Determine(
            bool synchronizing, bool stale, bool connected)
        {
            if (synchronizing)
                return DashboardStatusKind.Syncing;
            if (stale)
                return DashboardStatusKind.Stale;
            if (connected)
                return DashboardStatusKind.Live;
            return DashboardStatusKind.Offline;
        }

        internal static string Label(DashboardStatusKind status)
        {
            switch (status)
            {
                case DashboardStatusKind.Stale:
                    return "过期";
                case DashboardStatusKind.Offline:
                    return "离线";
                default:
                    return "实时";
            }
        }
    }
}
