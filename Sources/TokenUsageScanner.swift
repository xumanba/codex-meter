import Foundation

struct TokenUsageTotals: Equatable, Sendable {
    let totalTokens: Int64
    let inputTokens: Int64
    let cachedInputTokens: Int64
    let cacheWriteInputTokens: Int64
    let outputTokens: Int64
    let reasoningTokens: Int64

    static let zero = TokenUsageTotals(
        totalTokens: 0,
        inputTokens: 0,
        cachedInputTokens: 0,
        cacheWriteInputTokens: 0,
        outputTokens: 0,
        reasoningTokens: 0
    )

    var isZero: Bool {
        totalTokens == 0 && inputTokens == 0 && cachedInputTokens == 0
            && cacheWriteInputTokens == 0
            && outputTokens == 0 && reasoningTokens == 0
    }

    func adding(_ other: TokenUsageTotals) -> TokenUsageTotals {
        TokenUsageTotals(
            totalTokens: totalTokens + other.totalTokens,
            inputTokens: inputTokens + other.inputTokens,
            cachedInputTokens: cachedInputTokens + other.cachedInputTokens,
            cacheWriteInputTokens: cacheWriteInputTokens + other.cacheWriteInputTokens,
            outputTokens: outputTokens + other.outputTokens,
            reasoningTokens: reasoningTokens + other.reasoningTokens
        )
    }

    func delta(from previous: TokenUsageTotals?) -> TokenUsageTotals {
        guard let previous else { return self }

        func component(_ current: Int64, _ old: Int64) -> Int64 {
            current >= old ? current - old : current
        }

        return TokenUsageTotals(
            totalTokens: component(totalTokens, previous.totalTokens),
            inputTokens: component(inputTokens, previous.inputTokens),
            cachedInputTokens: component(cachedInputTokens, previous.cachedInputTokens),
            cacheWriteInputTokens: component(
                cacheWriteInputTokens,
                previous.cacheWriteInputTokens
            ),
            outputTokens: component(outputTokens, previous.outputTokens),
            reasoningTokens: component(reasoningTokens, previous.reasoningTokens)
        )
    }
}

struct TokenUsagePeriod: Codable, Equatable, Sendable {
    let start: Date
    let resetsAt: Date?
    let lastUsedPercent: Double

    static func initial(
        resetAt: Date?,
        usedPercent: Double,
        now: Date
    ) -> TokenUsagePeriod {
        TokenUsagePeriod(
            start: resetAt?.addingTimeInterval(-7 * 60 * 60 * 24) ?? now,
            resetsAt: resetAt,
            lastUsedPercent: usedPercent
        )
    }

    func updated(
        resetAt: Date?,
        usedPercent: Double,
        now: Date
    ) -> TokenUsagePeriod {
        let resetChanged: Bool
        if let previousReset = resetsAt, let currentReset = resetAt {
            resetChanged = abs(previousReset.timeIntervalSince(currentReset)) > 60
        } else {
            resetChanged = false
        }

        let usageDropped = usedPercent + 0.5 < lastUsedPercent
        return TokenUsagePeriod(
            start: resetChanged || usageDropped ? now : start,
            resetsAt: resetAt ?? resetsAt,
            lastUsedPercent: usedPercent
        )
    }
}

struct QuotaSample: Equatable {
    let capturedAt: Date
    let resetsAt: Date?
    let usedPercent: Double
}

struct QuotaBreakdownKey: Hashable {
    let model: String
    let effort: String
    let isFast: Bool
}

struct TokenUsageSnapshot: Equatable, Sendable {
    struct Daily: Equatable, Identifiable, Sendable {
        let date: Date
        let quotaPercent: Double
        let totals: TokenUsageTotals

        var id: Date { date }
    }

    struct Breakdown: Equatable, Identifiable, Sendable {
        let model: String
        let effort: String
        let isFast: Bool
        let totals: TokenUsageTotals
        let estimatedQuotaPercent: Double?

        var id: String {
            "\(model)|\(effort)|\(isFast)"
        }
    }

    let daily: [Daily]
    let weeklyTotals: TokenUsageTotals
    let breakdowns: [Breakdown]
    let scannedAt: Date
}

enum TokenUsageScanner {
    private struct UsageContext {
        var model = "未知模型"
        var effort = "未标注"
        var serviceTier = "default"
        var fastMode = false

        var isFast: Bool {
            let normalized = serviceTier.lowercased()
            return fastMode
                || normalized == "priority"
                || normalized == "fast"
                || normalized == "accelerated"
        }

        mutating func update(from payload: [String: Any]) {
            if let value = firstString(
                in: payload,
                paths: [
                    ["model"],
                    ["model_id"],
                    ["model_slug"],
                    ["state", "model"],
                    ["thread_settings", "model"],
                    ["collaboration_mode", "settings", "model"]
                ]
            ) {
                model = value
            }

            if let value = firstString(
                in: payload,
                paths: [
                    ["effort"],
                    ["reasoning_effort"],
                    ["thread_settings", "reasoning_effort"],
                    ["thread_settings", "effort"],
                    ["state", "reasoning_effort"],
                    ["collaboration_mode", "settings", "reasoning_effort"]
                ]
            ) {
                effort = value
            }

            if let value = firstString(
                in: payload,
                paths: [
                    ["service_tier"],
                    ["thread_settings", "service_tier"],
                    ["state", "service_tier"],
                    ["speed"],
                    ["thread_settings", "speed"],
                    ["state", "speed"]
                ]
            ) {
                serviceTier = value
            }

            if let value = firstBool(
                in: payload,
                paths: [
                    ["fast"],
                    ["fast_mode"],
                    ["thread_settings", "fast"],
                    ["thread_settings", "fast_mode"],
                    ["state", "fast"],
                    ["state", "fast_mode"]
                ]
            ) {
                fastMode = value
            }
        }

        private func firstString(
            in payload: [String: Any],
            paths: [[String]]
        ) -> String? {
            for path in paths {
                guard let value = value(at: path, in: payload) as? String,
                      !value.isEmpty else {
                    continue
                }
                return value
            }
            return nil
        }

        private func firstBool(
            in payload: [String: Any],
            paths: [[String]]
        ) -> Bool? {
            for path in paths {
                if let value = value(at: path, in: payload) as? Bool {
                    return value
                }
                if let value = value(at: path, in: payload) as? NSNumber {
                    return value.boolValue
                }
            }
            return nil
        }
    }

    private struct UsageEvent {
        let date: Date
        let totals: TokenUsageTotals
        let model: String
        let effort: String
        let isFast: Bool
        let quotaUsedPercent: Double?
        let quotaResetsAt: Date?
    }

    private struct QuotaHistory: Decodable {
        let accounts: [String: [QuotaWindow]]
    }

    private struct QuotaWindow: Decodable {
        let entries: [QuotaEntry]
        let windowMinutes: Int
    }

    private struct QuotaEntry: Decodable {
        let capturedAt: String
        let resetsAt: String?
        let usedPercent: Double
    }

    private struct LocalQuotaHistory: Codable {
        var samples: [LocalQuotaEntry]
    }

    private struct LocalQuotaEntry: Codable {
        let capturedAt: Date
        let resetsAt: Date?
        let usedPercent: Double
    }

    static func scan(
        now: Date,
        weeklyUsedPercent: Double?,
        weeklyResetsAt: Date?,
        quotaPeriodStart: Date? = nil
    ) -> TokenUsageSnapshot {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: now)
        let firstDay = calendar.date(byAdding: .day, value: -6, to: today) ?? now
        let quotaStart = quotaPeriodStart
            ?? weeklyResetsAt?.addingTimeInterval(-7 * 60 * 60 * 24)
            ?? firstDay
        let eventCutoff = max(firstDay, quotaStart)
        let events = scanFiles(cutoff: eventCutoff)

        var quotaSamples = loadQuotaSamples(currentReset: weeklyResetsAt)
        if let weeklyUsedPercent {
            let currentSample = QuotaSample(
                capturedAt: now,
                resetsAt: weeklyResetsAt,
                usedPercent: weeklyUsedPercent
            )
            persistQuotaSample(currentSample, now: now)
            quotaSamples = mergedSamples(quotaSamples + [currentSample])
        }
        let dailyDates = (0..<7).compactMap {
            calendar.date(byAdding: .day, value: $0, to: firstDay)
        }
        let dailyIndex = Dictionary(uniqueKeysWithValues: dailyDates.enumerated().map {
            (calendar.startOfDay(for: $0.element), $0.offset)
        })

        var dailyTotals = Array(repeating: TokenUsageTotals.zero, count: dailyDates.count)
        var weeklyTotals = TokenUsageTotals.zero
        var grouped: [QuotaBreakdownKey: TokenUsageTotals] = [:]

        for event in events where event.date >= quotaStart
            && event.date <= now
            && belongsToQuotaWindow(event.quotaResetsAt, weeklyReset: weeklyResetsAt) {
            let day = calendar.startOfDay(for: event.date)
            if let index = dailyIndex[day] {
                dailyTotals[index] = dailyTotals[index].adding(event.totals)
            }

            weeklyTotals = weeklyTotals.adding(event.totals)
            let key = QuotaBreakdownKey(
                model: event.model,
                effort: event.effort,
                isFast: event.isFast
            )
            grouped[key] = (grouped[key] ?? .zero).adding(event.totals)
        }

        let eventQuotaSamples = events.compactMap { event -> QuotaSample? in
            guard let usedPercent = event.quotaUsedPercent,
                  belongsToQuotaWindow(event.quotaResetsAt, weeklyReset: weeklyResetsAt) else {
                return nil
            }
            return QuotaSample(
                capturedAt: event.date,
                resetsAt: event.quotaResetsAt,
                usedPercent: usedPercent
            )
        }
        let dailyQuotaPercents = dailyQuotaPercentages(
            dates: dailyDates,
            samples: mergedSamples(quotaSamples + eventQuotaSamples),
            periodStart: quotaStart,
            now: now,
            calendar: calendar
        )
        let currentWeeklyUsedPercent = weeklyUsedPercent
            ?? mergedSamples(quotaSamples + eventQuotaSamples)
                .filter {
                    $0.capturedAt <= now
                        && belongsToQuotaWindow($0.resetsAt, weeklyReset: weeklyResetsAt)
                }
                .last?
                .usedPercent
        let estimatedQuotaPercentByBreakdown = estimatedQuotaPercentages(
            grouped: grouped,
            weeklyUsedPercent: currentWeeklyUsedPercent
        )

        let daily = dailyDates.enumerated().map { index, date in
            return TokenUsageSnapshot.Daily(
                date: date,
                quotaPercent: dailyQuotaPercents[index],
                totals: dailyTotals[index]
            )
        }

        let breakdowns = grouped.map { key, totals in
            TokenUsageSnapshot.Breakdown(
                model: key.model,
                effort: key.effort,
                isFast: key.isFast,
                totals: totals,
                estimatedQuotaPercent: estimatedQuotaPercentByBreakdown?[key]
            )
        }
        .sorted {
            if $0.totals.totalTokens != $1.totals.totalTokens {
                return $0.totals.totalTokens > $1.totals.totalTokens
            }
            return $0.id < $1.id
        }

        return TokenUsageSnapshot(
            daily: daily,
            weeklyTotals: weeklyTotals,
            breakdowns: breakdowns,
            scannedAt: now
        )
    }

    static func dailyQuotaPercentages(
        dates: [Date],
        samples: [QuotaSample],
        periodStart: Date,
        now: Date,
        calendar: Calendar
    ) -> [Double] {
        let eligibleSamples = samples
            .filter { $0.capturedAt >= periodStart && $0.capturedAt <= now }
            .sorted { $0.capturedAt < $1.capturedAt }

        return dates.map { dayStart in
            let nextDay = calendar.date(byAdding: .day, value: 1, to: dayStart)
                ?? dayStart.addingTimeInterval(24 * 60 * 60)
            guard let endSample = eligibleSamples.last(where: {
                $0.capturedAt < nextDay
            }) else {
                return 0
            }

            let startPercent: Double
            if periodStart > dayStart {
                startPercent = eligibleSamples.last(where: {
                    $0.capturedAt <= periodStart
                })?.usedPercent ?? 0
            } else {
                startPercent = eligibleSamples.last(where: {
                    $0.capturedAt < dayStart
                })?.usedPercent ?? 0
            }
            return max(0, min(100, endSample.usedPercent - startPercent))
        }
    }

    static func estimatedQuotaPercentages(
        grouped: [QuotaBreakdownKey: TokenUsageTotals],
        weeklyUsedPercent: Double?
    ) -> [QuotaBreakdownKey: Double]? {
        guard let weeklyUsedPercent else { return nil }

        let weightedCosts = grouped.reduce(into: [QuotaBreakdownKey: Double]()) { result, entry in
            result[entry.key] = estimatedQuotaCost(for: entry.key, totals: entry.value)
        }
        let totalWeightedCost = weightedCosts.values.reduce(0, +)
        let clampedWeeklyPercent = max(0, min(100, weeklyUsedPercent))

        guard totalWeightedCost > 0 else {
            return grouped.mapValues { _ in 0 }
        }

        return weightedCosts.mapValues { cost in
            clampedWeeklyPercent * cost / totalWeightedCost
        }
    }

    private struct QuotaCostWeights {
        let input: Double
        let cachedInput: Double
        let cacheWriteInput: Double
        let output: Double

        func cost(for totals: TokenUsageTotals) -> Double {
            let uncachedInput = max(
                0,
                totals.inputTokens - totals.cachedInputTokens
            )
            return Double(uncachedInput) * input
                + Double(max(0, totals.cachedInputTokens)) * cachedInput
                + Double(max(0, totals.cacheWriteInputTokens)) * cacheWriteInput
                + Double(max(0, totals.outputTokens)) * output
        }
    }

    private static func estimatedQuotaCost(
        for key: QuotaBreakdownKey,
        totals: TokenUsageTotals
    ) -> Double {
        let baseCost: Double
        if let weights = quotaCostWeights(for: key.model) {
            baseCost = weights.cost(for: totals)
        } else {
            baseCost = Double(max(0, totals.totalTokens))
                + Double(max(0, totals.cacheWriteInputTokens))
        }

        return key.isFast ? baseCost * 2.5 : baseCost
    }

    private static func quotaCostWeights(for model: String) -> QuotaCostWeights? {
        switch model.lowercased() {
        case "gpt-5.6-sol":
            return QuotaCostWeights(
                input: 5.0,
                cachedInput: 0.5,
                cacheWriteInput: 6.25,
                output: 30.0
            )
        case "gpt-5.6-terra":
            return QuotaCostWeights(
                input: 2.0,
                cachedInput: 0.2,
                cacheWriteInput: 2.5,
                output: 12.0
            )
        case "gpt-5.6-luna", "codex-auto-review":
            return QuotaCostWeights(
                input: 0.2,
                cachedInput: 0.02,
                cacheWriteInput: 0.25,
                output: 1.2
            )
        default:
            return nil
        }
    }

    private static func scanFiles(cutoff: Date) -> [UsageEvent] {
        let fileManager = FileManager.default
        let codexDirectory = fileManager.homeDirectoryForCurrentUser
            .appendingPathComponent(".codex")
        let roots = [
            codexDirectory.appendingPathComponent("sessions"),
            codexDirectory.appendingPathComponent("archived_sessions")
        ]

        var events: [UsageEvent] = []
        var seenRollouts = Set<String>()

        for root in roots {
            guard let enumerator = fileManager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isRegularFileKey, .contentModificationDateKey],
                options: [.skipsHiddenFiles]
            ) else {
                continue
            }

            for case let url as URL in enumerator {
                guard url.pathExtension == "jsonl" else { continue }
                let values = try? url.resourceValues(forKeys: [.isRegularFileKey, .contentModificationDateKey])
                guard values?.isRegularFile == true else { continue }
                if let modifiedAt = values?.contentModificationDate, modifiedAt < cutoff {
                    continue
                }

                let rolloutID = url.deletingPathExtension().lastPathComponent
                guard seenRollouts.insert(rolloutID).inserted else { continue }
                events.append(contentsOf: scanFile(url, cutoff: cutoff))
            }
        }

        return events
    }

    private static func scanFile(_ url: URL, cutoff: Date) -> [UsageEvent] {
        guard let data = try? Data(contentsOf: url, options: .mappedIfSafe) else {
            return []
        }

        let text = String(decoding: data, as: UTF8.self)
        let fractionalFormatter = ISO8601DateFormatter()
        fractionalFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let standardFormatter = ISO8601DateFormatter()
        standardFormatter.formatOptions = [.withInternetDateTime]

        var context = UsageContext()
        var previousTotal: TokenUsageTotals?
        var previousLast: TokenUsageTotals?
        var events: [UsageEvent] = []

        for line in text.split(whereSeparator: \.isNewline) {
            guard let object = try? JSONSerialization.jsonObject(with: Data(line.utf8)),
                  let dictionary = object as? [String: Any],
                  let payload = dictionary["payload"] as? [String: Any] else {
                continue
            }

            context.update(from: payload)
            let quota = quotaUsage(in: payload)
            let dateString = (dictionary["timestamp"] as? String)
                ?? (payload["timestamp"] as? String)
            guard let dateString,
                  let date = parseDate(
                    dateString,
                    fractionalFormatter: fractionalFormatter,
                    standardFormatter: standardFormatter
                  ) else {
                continue
            }

            if let total = tokenUsage(at: ["info", "total_token_usage"], in: payload) {
                let delta = total.delta(from: previousTotal)
                previousTotal = total
                previousLast = nil
                if delta.totalTokens > 0 && date >= cutoff {
                    events.append(
                        UsageEvent(
                            date: date,
                            totals: delta,
                            model: context.model,
                            effort: context.effort,
                            isFast: context.isFast,
                            quotaUsedPercent: quota?.usedPercent,
                            quotaResetsAt: quota?.resetsAt
                        )
                    )
                }
                continue
            }

            guard let last = tokenUsage(at: ["info", "last_token_usage"], in: payload) else {
                continue
            }
            if last != previousLast, last.totalTokens > 0, date >= cutoff {
                events.append(
                    UsageEvent(
                        date: date,
                        totals: last,
                        model: context.model,
                        effort: context.effort,
                        isFast: context.isFast,
                        quotaUsedPercent: quota?.usedPercent,
                        quotaResetsAt: quota?.resetsAt
                    )
                )
            }
            previousLast = last
        }

        return events
    }

    private static func tokenUsage(
        at path: [String],
        in payload: [String: Any]
    ) -> TokenUsageTotals? {
        guard let dictionary = value(at: path, in: payload) as? [String: Any] else {
            return nil
        }

        guard let totalTokens = integer(dictionary["total_tokens"]) else {
            return nil
        }

        return TokenUsageTotals(
            totalTokens: totalTokens,
            inputTokens: integer(dictionary["input_tokens"]) ?? 0,
            cachedInputTokens: integer(dictionary["cached_input_tokens"]) ?? 0,
            cacheWriteInputTokens: integer(dictionary["cache_write_input_tokens"]) ?? 0,
            outputTokens: integer(dictionary["output_tokens"]) ?? 0,
            reasoningTokens: integer(dictionary["reasoning_output_tokens"])
                ?? integer(dictionary["reasoning_tokens"])
                ?? 0
        )
    }

    private static func integer(_ value: Any?) -> Int64? {
        if let number = value as? NSNumber {
            return number.int64Value
        }
        if let string = value as? String {
            return Int64(string)
        }
        return nil
    }

    private static func double(_ value: Any?) -> Double? {
        if let number = value as? NSNumber {
            return number.doubleValue
        }
        if let string = value as? String {
            return Double(string)
        }
        return nil
    }

    private static func quotaUsage(
        in payload: [String: Any]
    ) -> (usedPercent: Double, resetsAt: Date?)? {
        guard let rateLimits = payload["rate_limits"] as? [String: Any],
              let primary = rateLimits["primary"] as? [String: Any],
              let usedPercent = double(primary["used_percent"]) else {
            return nil
        }

        let resetsAt = double(primary["resets_at"])
            .map(Date.init(timeIntervalSince1970:))
        return (usedPercent, resetsAt)
    }

    private static func value(at path: [String], in object: [String: Any]) -> Any? {
        var current: Any = object
        for key in path {
            guard let dictionary = current as? [String: Any],
                  let next = dictionary[key] else {
                return nil
            }
            current = next
        }
        return current
    }

    private static func parseDate(
        _ string: String,
        fractionalFormatter: ISO8601DateFormatter,
        standardFormatter: ISO8601DateFormatter
    ) -> Date? {
        fractionalFormatter.date(from: string) ?? standardFormatter.date(from: string)
    }

    private static func loadQuotaSamples(currentReset: Date?) -> [QuotaSample] {
        let url = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/com.steipete.codexbar/history/codex.json")

        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let standardFormatter = ISO8601DateFormatter()
        standardFormatter.formatOptions = [.withInternetDateTime]

        var legacySamples: [QuotaSample] = []
        if let data = try? Data(contentsOf: url),
           let history = try? JSONDecoder().decode(QuotaHistory.self, from: data) {
            let sequences = history.accounts.values
                .flatMap { $0 }
                .filter { $0.windowMinutes == 10080 }
                .map { window in
                    window.entries.compactMap { entry -> QuotaSample? in
                        guard let capturedAt = formatter.date(from: entry.capturedAt)
                                ?? standardFormatter.date(from: entry.capturedAt) else {
                            return nil
                        }
                        let resetsAt = entry.resetsAt.flatMap {
                            formatter.date(from: $0) ?? standardFormatter.date(from: $0)
                        }
                        return QuotaSample(
                            capturedAt: capturedAt,
                            resetsAt: resetsAt,
                            usedPercent: entry.usedPercent
                        )
                    }
                }
                .filter { !$0.isEmpty }

            if let currentReset {
                let matching = sequences.min { lhs, rhs in
                    resetDistance(lhs, to: currentReset) < resetDistance(rhs, to: currentReset)
                }
                if let matching, resetDistance(matching, to: currentReset) <= 10 * 60 {
                    legacySamples = matching
                }
            } else {
                legacySamples = sequences
                    .max { latestSample(in: $0).capturedAt < latestSample(in: $1).capturedAt } ?? []
            }
        }

        let localSamples = loadLocalQuotaSamples(currentReset: currentReset)
        return mergedSamples(legacySamples + localSamples)
    }

    private static func loadLocalQuotaSamples(currentReset: Date?) -> [QuotaSample] {
        let url = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Codex Meter/quota-history.json")
        guard let data = try? Data(contentsOf: url) else { return [] }

        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        guard let history = try? decoder.decode(LocalQuotaHistory.self, from: data) else {
            return []
        }

        let samples = history.samples.map {
            QuotaSample(
                capturedAt: $0.capturedAt,
                resetsAt: $0.resetsAt,
                usedPercent: $0.usedPercent
            )
        }
        guard let currentReset else { return samples }
        return samples.filter { sample in
            guard let reset = sample.resetsAt else { return false }
            return abs(reset.timeIntervalSince(currentReset)) <= 10 * 60
        }
    }

    private static func persistQuotaSample(_ sample: QuotaSample, now: Date) {
        let directory = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Codex Meter", isDirectory: true)
        let url = directory.appendingPathComponent("quota-history.json")
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let existing = (try? Data(contentsOf: url))
            .flatMap { try? decoder.decode(LocalQuotaHistory.self, from: $0) }
            ?? LocalQuotaHistory(samples: [])

        var samples = existing.samples
        samples.append(
            LocalQuotaEntry(
                capturedAt: sample.capturedAt,
                resetsAt: sample.resetsAt,
                usedPercent: sample.usedPercent
            )
        )
        let cutoff = now.addingTimeInterval(-30 * 24 * 60 * 60)
        samples = samples
            .filter { $0.capturedAt >= cutoff }
            .sorted { $0.capturedAt < $1.capturedAt }
        if samples.count > 2_000 {
            samples.removeFirst(samples.count - 2_000)
        }

        do {
            try FileManager.default.createDirectory(
                at: directory,
                withIntermediateDirectories: true
            )
            let encoder = JSONEncoder()
            encoder.dateEncodingStrategy = .iso8601
            let data = try encoder.encode(LocalQuotaHistory(samples: samples))
            try data.write(to: url, options: .atomic)
        } catch {
            // Quota history is an enhancement; the current live percentage remains usable.
        }
    }

    private static func mergedSamples(_ samples: [QuotaSample]) -> [QuotaSample] {
        var byTimestamp: [TimeInterval: QuotaSample] = [:]
        for sample in samples {
            byTimestamp[sample.capturedAt.timeIntervalSince1970] = sample
        }
        return byTimestamp.values.sorted { $0.capturedAt < $1.capturedAt }
    }

    private static func latestSample(in samples: [QuotaSample]) -> QuotaSample {
        samples.max { $0.capturedAt < $1.capturedAt } ?? QuotaSample(
            capturedAt: .distantPast,
            resetsAt: nil,
            usedPercent: 0
        )
    }

    private static func resetDistance(_ samples: [QuotaSample], to reset: Date) -> TimeInterval {
        guard let sampleReset = latestSample(in: samples).resetsAt else {
            return .greatestFiniteMagnitude
        }
        return abs(sampleReset.timeIntervalSince(reset))
    }

    private static func matchesReset(
        _ eventReset: Date?,
        _ weeklyReset: Date?
    ) -> Bool {
        switch (eventReset, weeklyReset) {
        case let (.some(eventReset), .some(weeklyReset)):
            return abs(eventReset.timeIntervalSince(weeklyReset)) <= 10 * 60
        case (.none, .none):
            return true
        default:
            return false
        }
    }

    private static func belongsToQuotaWindow(
        _ eventReset: Date?,
        weeklyReset: Date?
    ) -> Bool {
        guard weeklyReset != nil else { return true }
        return matchesReset(eventReset, weeklyReset)
    }

}
