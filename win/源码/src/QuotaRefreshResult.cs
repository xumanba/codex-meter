using System;

namespace CodexMeter
{
    internal sealed class QuotaRefreshResult
    {
        public UsageSnapshot Snapshot { get; set; }
        public ResetHistoryReport ResetHistory { get; set; }
        public DateTimeOffset RefreshedAt { get; set; }
    }
}
