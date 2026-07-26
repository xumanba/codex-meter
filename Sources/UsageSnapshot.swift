import Foundation

struct UsageSnapshot: Equatable {
    struct Window: Equatable {
        let title: String
        let usedPercent: Double
        let resetsAt: Date?
    }

    struct Pace: Equatable {
        let deltaPercent: Double
        let expectedUsedPercent: Double
        let etaSeconds: Double?
        let willLastToReset: Bool
    }

    let weekly: Window
    let extras: [Window]
    let pace: Pace?
    let updatedAt: Date?
}

enum UsageSnapshotError: LocalizedError, Equatable {
    case provider(String)

    var errorDescription: String? {
        switch self {
        case let .provider(message):
            if message.localizedCaseInsensitiveContains("authentication required") {
                return "未检测到 Codex 登录，请先登录 Codex 后重试"
            }
            return message
        }
    }
}

enum UsageSnapshotDecoder {
    private struct Payload: Decodable {
        struct Usage: Decodable {
            struct Window: Decodable {
                let resetsAt: Date?
                let usedPercent: Double
            }

            struct ExtraWindow: Decodable {
                let title: String
                let window: Window
            }

            let secondary: Window?
            let extraRateWindows: [ExtraWindow]?
            let updatedAt: Date?
        }

        struct Pace: Decodable {
            struct Secondary: Decodable {
                let deltaPercent: Double
                let expectedUsedPercent: Double
                let etaSeconds: Double?
                let willLastToReset: Bool
            }

            let secondary: Secondary?
        }

        struct ProviderError: Decodable {
            let message: String
        }

        let usage: Usage?
        let pace: Pace?
        let error: ProviderError?
    }

    static func decode(_ data: Data) throws -> UsageSnapshot {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let payloads = try decoder.decode([Payload].self, from: data)

        guard let payload = payloads.first else {
            throw DecodingError.valueNotFound(
                Payload.self,
                .init(codingPath: [], debugDescription: "Codex usage response is empty")
            )
        }
        if let providerError = payload.error {
            throw UsageSnapshotError.provider(providerError.message)
        }
        guard let usage = payload.usage, let weekly = usage.secondary else {
            throw DecodingError.valueNotFound(
                Payload.Usage.Window.self,
                .init(codingPath: [], debugDescription: "Codex weekly usage is missing")
            )
        }

        return UsageSnapshot(
            weekly: .init(
                title: "Weekly",
                usedPercent: weekly.usedPercent,
                resetsAt: weekly.resetsAt
            ),
            extras: (usage.extraRateWindows ?? []).map {
                .init(
                    title: $0.title
                        .replacingOccurrences(of: "Codex ", with: "")
                        .replacingOccurrences(of: " Weekly", with: ""),
                    usedPercent: $0.window.usedPercent,
                    resetsAt: $0.window.resetsAt
                )
            },
            pace: payload.pace?.secondary.map {
                .init(
                    deltaPercent: $0.deltaPercent,
                    expectedUsedPercent: $0.expectedUsedPercent,
                    etaSeconds: $0.etaSeconds,
                    willLastToReset: $0.willLastToReset
                )
            },
            updatedAt: usage.updatedAt
        )
    }
}
