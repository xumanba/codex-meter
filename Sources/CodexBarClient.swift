import Foundation

@MainActor
final class CodexBarClient: ObservableObject {
    @Published private(set) var snapshot: UsageSnapshot?
    @Published private(set) var isConnected = false
    @Published private(set) var lastError: String?

    private let endpoint = URL(string: "http://127.0.0.1:18747/usage?provider=codex")!
    private var serverProcess: Process?
    private var refreshTask: Task<Void, Never>?

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
        do {
            snapshot = try await fetch()
            isConnected = true
            lastError = nil
        } catch {
            if startServerIfNeeded && serverProcess?.isRunning != true {
                startServer()
                try? await Task.sleep(for: .seconds(1))
                await refresh(startServerIfNeeded: false)
                return
            }
            isConnected = false
            lastError = error.localizedDescription
        }
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
