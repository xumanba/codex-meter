import Foundation

@MainActor
final class CodexBarClient: ObservableObject {
    @Published private(set) var snapshot: UsageSnapshot?
    @Published private(set) var tokenUsage: TokenUsageSnapshot?
    @Published private(set) var isConnected = false
    @Published private(set) var lastError: String?

    private let endpoint = URL(string: "http://127.0.0.1:18747/usage?provider=codex")!
    private var serverProcess: Process?
    private var refreshTask: Task<Void, Never>?
    private let tokenUsagePeriodKey = "tokenUsagePeriod"
    private var tokenUsagePeriod: TokenUsagePeriod?

    init() {
        if let data = UserDefaults.standard.data(forKey: tokenUsagePeriodKey) {
            tokenUsagePeriod = try? JSONDecoder().decode(TokenUsagePeriod.self, from: data)
        }
    }

    func start() {
        refreshTask?.cancel()
        refreshTask = Task {
            await refresh(startServerIfNeeded: true)
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(60))
                await refresh(startServerIfNeeded: true)
            }
        }
    }

    func refreshNow() {
        Task { await refresh(startServerIfNeeded: true) }
    }

    func stop() {
        refreshTask?.cancel()
        if serverProcess?.isRunning == true {
            serverProcess?.terminate()
        }
    }

    private func refresh(startServerIfNeeded: Bool) async {
        let refreshDate = Date()
        do {
            let usageSnapshot = try await fetch()
            let periodStart = updateTokenUsagePeriod(
                for: usageSnapshot.weekly,
                at: refreshDate
            )
            snapshot = usageSnapshot
            tokenUsage = await scanTokenUsage(
                at: refreshDate,
                weekly: usageSnapshot.weekly,
                periodStart: periodStart
            )
            isConnected = true
            lastError = nil
        } catch {
            if startServerIfNeeded && serverProcess?.isRunning != true {
                startServer()
                try? await Task.sleep(for: .seconds(1))
                await refresh(startServerIfNeeded: false)
                return
            }
            tokenUsage = await scanTokenUsage(
                at: refreshDate,
                weekly: snapshot?.weekly,
                periodStart: tokenUsagePeriod?.start
            )
            isConnected = false
            lastError = error.localizedDescription
        }
    }

    private func scanTokenUsage(
        at date: Date,
        weekly: UsageSnapshot.Window?,
        periodStart: Date?
    ) async -> TokenUsageSnapshot {
        let weeklyUsedPercent = weekly?.usedPercent
        let weeklyResetsAt = weekly?.resetsAt
        return await Task.detached(priority: .utility) {
            TokenUsageScanner.scan(
                now: date,
                weeklyUsedPercent: weeklyUsedPercent,
                weeklyResetsAt: weeklyResetsAt,
                quotaPeriodStart: periodStart
            )
        }.value
    }

    private func updateTokenUsagePeriod(
        for weekly: UsageSnapshot.Window,
        at date: Date
    ) -> Date {
        let nextPeriod: TokenUsagePeriod
        if let tokenUsagePeriod {
            nextPeriod = tokenUsagePeriod.updated(
                resetAt: weekly.resetsAt,
                usedPercent: weekly.usedPercent,
                now: date
            )
        } else {
            nextPeriod = TokenUsagePeriod.initial(
                resetAt: weekly.resetsAt,
                usedPercent: weekly.usedPercent,
                now: date
            )
        }

        tokenUsagePeriod = nextPeriod
        if let data = try? JSONEncoder().encode(nextPeriod) {
            UserDefaults.standard.set(data, forKey: tokenUsagePeriodKey)
        }
        return nextPeriod.start
    }

    private func fetch() async throws -> UsageSnapshot {
        var request = URLRequest(url: endpoint)
        request.timeoutInterval = 20
        let (data, response) = try await URLSession.shared.data(for: request)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }
        return try UsageSnapshotDecoder.decode(data)
    }

    private func startServer() {
        guard let executableURL = codexBarExecutableURL() else {
            lastError = "CodexBar helper is unavailable"
            return
        }

        let process = Process()
        process.executableURL = executableURL
        process.arguments = [
            "serve",
            "--port", "18747",
            "--refresh-interval", "60",
            "--request-timeout", "30"
        ]
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice

        do {
            try process.run()
            serverProcess = process
        } catch {
            lastError = "Could not start CodexBar helper"
        }
    }

    private func codexBarExecutableURL() -> URL? {
        let bundledExecutable = Bundle.main.bundleURL
            .appendingPathComponent("Contents/Helpers/codexbar")

        return [
            bundledExecutable.path,
            "/opt/homebrew/bin/codexbar",
            "/usr/local/bin/codexbar"
        ]
        .first(where: { FileManager.default.isExecutableFile(atPath: $0) })
        .map(URL.init(fileURLWithPath:))
    }
}
