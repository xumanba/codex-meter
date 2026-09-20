using System;

namespace CodexMeter
{
    internal sealed class DashboardState
    {
        internal DashboardState(ResetHistoryReport initialResetHistory)
        {
            ResetHistory = initialResetHistory;
            NetworkSpeed = new NetworkSpeedSnapshot(0, 0);
        }

        internal UsageSnapshot Snapshot { get; private set; }
        internal WeeklyTokenReport WeeklyUsage { get; private set; }
        internal ResetHistoryReport ResetHistory { get; private set; }
        internal NetworkSpeedSnapshot NetworkSpeed { get; private set; }
        internal string LastError { get; private set; }
        internal bool IsConnected { get; private set; }
        internal DateTimeOffset? LastSuccessfulRefreshAt { get; private set; }

        internal void ApplyQuotaSuccess(QuotaRefreshResult result)
        {
            if (result == null)
                throw new ArgumentNullException("result");

            Snapshot = result.Snapshot;
            ResetHistory = result.ResetHistory;
            LastSuccessfulRefreshAt = result.RefreshedAt;
            LastError = null;
            IsConnected = true;
        }

        internal void ApplyQuotaFailure(string error)
        {
            LastError = error;
            IsConnected = false;
        }

        internal void ApplyWeeklyUsage(WeeklyTokenReport report)
        {
            WeeklyUsage = report;
        }

        internal void ApplyResetHistory(ResetHistoryReport report)
        {
            if (report != null)
                ResetHistory = report;
        }

        internal void ApplyNetworkSpeed(NetworkSpeedSnapshot speed)
        {
            NetworkSpeed = speed;
        }
    }
}
