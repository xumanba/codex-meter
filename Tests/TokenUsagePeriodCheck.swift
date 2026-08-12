import Foundation

@main
enum TokenUsagePeriodCheck {
    static func main() {
        let resetAt = Date(timeIntervalSince1970: 2_000_000)
        let initialDate = Date(timeIntervalSince1970: 1_500_000)
        let refreshDate = Date(timeIntervalSince1970: 2_100_000)
        let nextResetAt = resetAt.addingTimeInterval(7 * 60 * 60 * 24)

        let initial = TokenUsagePeriod.initial(
            resetAt: resetAt,
            usedPercent: 8,
            now: initialDate
        )
        precondition(initial.start == resetAt.addingTimeInterval(-7 * 60 * 60 * 24))

        let samePeriod = initial.updated(
            resetAt: resetAt,
            usedPercent: 12,
            now: refreshDate
        )
        precondition(samePeriod.start == initial.start)

        let afterUsageDrop = samePeriod.updated(
            resetAt: resetAt,
            usedPercent: 0,
            now: refreshDate
        )
        precondition(afterUsageDrop.start == refreshDate)

        let afterResetChange = afterUsageDrop.updated(
            resetAt: nextResetAt,
            usedPercent: 1,
            now: refreshDate.addingTimeInterval(60)
        )
        precondition(afterResetChange.start == refreshDate.addingTimeInterval(60))

        checkDailyQuotaPercentages()
        checkModelQuotaEstimation()

        print("Token usage period checks passed")
    }

    private static func checkDailyQuotaPercentages() {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        let day1 = Date(timeIntervalSince1970: 10 * 24 * 60 * 60)
        let day2 = day1.addingTimeInterval(24 * 60 * 60)
        let day3 = day2.addingTimeInterval(24 * 60 * 60)
        let samples = [
            QuotaSample(
                capturedAt: day1.addingTimeInterval(2 * 60 * 60),
                resetsAt: nil,
                usedPercent: 8
            ),
            QuotaSample(
                capturedAt: day1.addingTimeInterval(20 * 60 * 60),
                resetsAt: nil,
                usedPercent: 10
            ),
            QuotaSample(
                capturedAt: day2.addingTimeInterval(23 * 60 * 60),
                resetsAt: nil,
                usedPercent: 14
            ),
            QuotaSample(
                capturedAt: day3.addingTimeInterval(2 * 60 * 60),
                resetsAt: nil,
                usedPercent: 17
            )
        ]

        let values = TokenUsageScanner.dailyQuotaPercentages(
            dates: [day1, day2, day3],
            samples: samples,
            periodStart: day1,
            now: day3.addingTimeInterval(3 * 60 * 60),
            calendar: calendar
        )
        precondition(values == [10, 4, 3])

        let refreshSamples = [
            samples[0],
            samples[1],
            QuotaSample(
                capturedAt: day2.addingTimeInterval(12 * 60 * 60),
                resetsAt: nil,
                usedPercent: 0
            ),
            QuotaSample(
                capturedAt: day2.addingTimeInterval(23 * 60 * 60),
                resetsAt: nil,
                usedPercent: 2
            )
        ] + [
            QuotaSample(
                capturedAt: day3.addingTimeInterval(2 * 60 * 60),
                resetsAt: nil,
                usedPercent: 5
            )
        ]
        let afterRefresh = TokenUsageScanner.dailyQuotaPercentages(
            dates: [day1, day2, day3],
            samples: refreshSamples,
            periodStart: day2.addingTimeInterval(12 * 60 * 60),
            now: day3.addingTimeInterval(3 * 60 * 60),
            calendar: calendar
        )
        precondition(afterRefresh == [0, 2, 3])
    }

    private static func checkModelQuotaEstimation() {
        let lunaKey = QuotaBreakdownKey(
            model: "gpt-5.6-luna",
            effort: "max",
            isFast: false
        )
        let autoReviewKey = QuotaBreakdownKey(
            model: "codex-auto-review",
            effort: "max",
            isFast: false
        )
        let fastSolKey = QuotaBreakdownKey(
            model: "gpt-5.6-sol",
            effort: "high",
            isFast: true
        )
        let standardSolKey = QuotaBreakdownKey(
            model: "gpt-5.6-sol",
            effort: "high",
            isFast: false
        )
        let lunaTotals = TokenUsageTotals(
            totalTokens: 100,
            inputTokens: 80,
            cachedInputTokens: 40,
            cacheWriteInputTokens: 0,
            outputTokens: 20,
            reasoningTokens: 10
        )
        let solTotals = TokenUsageTotals(
            totalTokens: 20,
            inputTokens: 16,
            cachedInputTokens: 8,
            cacheWriteInputTokens: 0,
            outputTokens: 4,
            reasoningTokens: 2
        )
        let grouped = [
            lunaKey: lunaTotals,
            autoReviewKey: lunaTotals,
            fastSolKey: solTotals,
            standardSolKey: solTotals
        ]

        let values = TokenUsageScanner.estimatedQuotaPercentages(
            grouped: grouped,
            weeklyUsedPercent: 20
        )!
        let total = values.values.reduce(0, +)

        precondition(abs(total - 20) < 0.000_001)
        precondition(values[fastSolKey]! > values[lunaKey]!)
        precondition(
            abs(values[fastSolKey]! / values[standardSolKey]! - 2.5) < 0.000_001
        )
        precondition(abs(values[lunaKey]! - values[autoReviewKey]!) < 0.000_001)
        precondition(
            TokenUsageScanner.estimatedQuotaPercentages(
                grouped: grouped,
                weeklyUsedPercent: nil
            ) == nil
        )
    }
}
