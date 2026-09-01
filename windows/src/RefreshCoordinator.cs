using System;

namespace CodexMeter
{
    internal sealed class RefreshCoordinator
    {
        public bool IsRefreshing { get; private set; }
        public bool ManualRefreshPending { get; private set; }
        public int ConsecutiveFailures { get; private set; }

        internal bool TryBeginAutomaticRefresh()
        {
            if (IsRefreshing)
                return false;
            IsRefreshing = true;
            return true;
        }

        internal bool RequestManualRefresh()
        {
            if (IsRefreshing)
            {
                ManualRefreshPending = true;
                return false;
            }

            IsRefreshing = true;
            return true;
        }

        internal void RegisterSuccess()
        {
            ConsecutiveFailures = 0;
        }

        internal void RegisterFailure()
        {
            ConsecutiveFailures++;
        }

        internal bool FinishAndBeginQueuedRefresh(bool allowQueuedRefresh)
        {
            IsRefreshing = false;
            if (!allowQueuedRefresh || !ManualRefreshPending)
                return false;

            ManualRefreshPending = false;
            IsRefreshing = true;
            return true;
        }

        internal int ApplyFailureBackoff(int baseMilliseconds, int maximumMilliseconds)
        {
            int safeBase = Math.Max(1, baseMilliseconds);
            int safeMaximum = Math.Max(safeBase, maximumMilliseconds);
            if (ConsecutiveFailures <= 0)
                return safeBase;

            int multiplier = 1 << Math.Min(ConsecutiveFailures, 3);
            long delayed = (long)safeBase * multiplier;
            return Convert.ToInt32(Math.Min(safeMaximum, delayed));
        }

        internal void Cancel()
        {
            ManualRefreshPending = false;
            IsRefreshing = false;
        }

        internal static bool ShouldStartManualRefresh(bool refreshRunning)
        {
            return !refreshRunning;
        }

        internal static bool ShouldQueueManualRefresh(bool refreshRunning)
        {
            return refreshRunning;
        }
    }
}
