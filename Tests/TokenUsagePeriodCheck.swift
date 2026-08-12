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

        print("Token usage period checks passed")
    }
}
