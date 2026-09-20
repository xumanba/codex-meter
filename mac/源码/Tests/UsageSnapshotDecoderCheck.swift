import Foundation

@main
enum UsageSnapshotDecoderCheck {
    static func main() throws {
        try checkWeeklyUsage()
        checkMissingCodexLogin()
        print("Usage snapshot decoder checks passed")
    }

    private static func checkWeeklyUsage() throws {
        let data = Data(
            """
            [{
              "usage": {
                "secondary": {
                  "resetsAt": null,
                  "usedPercent": 42
                },
                "extraRateWindows": [],
                "updatedAt": null
              },
              "pace": {
                "secondary": {
                  "deltaPercent": 2.5,
                  "expectedUsedPercent": 39.5,
                  "etaSeconds": 3600,
                  "willLastToReset": true
                }
              }
            }]
            """.utf8
        )

        let snapshot = try UsageSnapshotDecoder.decode(data)

        precondition(snapshot.weekly.usedPercent == 42)
        precondition(snapshot.extras.isEmpty)
        precondition(snapshot.pace?.deltaPercent == 2.5)
        precondition(snapshot.pace?.expectedUsedPercent == 39.5)
        precondition(snapshot.pace?.etaSeconds == 3600)
        precondition(snapshot.pace?.willLastToReset == true)
    }

    private static func checkMissingCodexLogin() {
        let data = Data(
            """
            [{
              "provider": "codex",
              "source": "auto",
              "error": {
                "code": 1,
                "message": "Codex connection failed: codex account authentication required to read rate limits",
                "kind": "provider"
              }
            }]
            """.utf8
        )

        do {
            _ = try UsageSnapshotDecoder.decode(data)
            preconditionFailure("Expected a missing-login error")
        } catch {
            precondition(
                error.localizedDescription == "未检测到 Codex 登录，请先登录 Codex 后重试"
            )
        }
    }
}
