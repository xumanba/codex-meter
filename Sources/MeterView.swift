import SwiftUI

enum MeterTheme: String {
    case dark
    case light
}

struct MeterView: View {
    @ObservedObject var client: CodexBarClient
    let mode: PanelMode
    let theme: MeterTheme
    let setMode: (PanelMode) -> Void
    let setTheme: (MeterTheme) -> Void
    let quit: () -> Void

    var body: some View {
        VStack(spacing: 12) {
            header

            if let snapshot = client.snapshot {
                meter(
                    snapshot.weekly,
                    prominent: true,
                    expectedRemaining: snapshot.pace.map { 100 - $0.expectedUsedPercent }
                )
                if let pace = snapshot.pace {
                    paceRow(pace)
                }
                ForEach(Array(snapshot.extras.enumerated()), id: \.offset) { _, window in
                    meter(window, prominent: false)
                }
            } else {
                loading
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .frame(width: 292)
        .background {
            ZStack {
                RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .fill(.ultraThinMaterial)
                RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .fill(glassTint)
            }
        }
        .overlay {
            RoundedRectangle(cornerRadius: 20, style: .continuous)
                .strokeBorder(glassStroke, lineWidth: 0.75)
        }
        .shadow(color: .black.opacity(theme == .dark ? 0.45 : 0.20), radius: 24, y: 12)
        .environment(\.colorScheme, theme == .dark ? .dark : .light)
    }

    private var header: some View {
        HStack(spacing: 9) {
            Image(systemName: "sparkles")
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(.white)
                .frame(width: 26, height: 26)
                .background(appleBlue.gradient, in: Circle())
                .shadow(color: appleBlue.opacity(0.45), radius: 7)

            VStack(alignment: .leading, spacing: 1) {
                Text("Codex 用量")
                    .font(.system(size: 13.5, weight: .semibold))
                    .foregroundStyle(primaryText)
                Text(client.isConnected ? "数据已更新" : "正在重新连接…")
                    .font(.system(size: 9.5, weight: .medium))
                    .foregroundStyle(secondaryText)
            }

            Spacer()

            HStack(spacing: 5) {
                Circle()
                    .fill(client.isConnected ? Color(red: 0.19, green: 0.82, blue: 0.35) : .orange)
                    .frame(width: 6, height: 6)
                Text(client.isConnected ? "实时" : "离线")
                    .font(.system(size: 9, weight: .semibold))
                    .foregroundStyle(secondaryText)
            }
            .padding(.horizontal, 7)
            .padding(.vertical, 4)
            .background(statusBackground, in: Capsule())

            Menu {
                Button {
                    setMode(.fixed)
                } label: {
                    Label("固定在桌面", systemImage: mode == .fixed ? "checkmark" : "pin")
                }
                Button {
                    setMode(.followCodex)
                } label: {
                    Label("跟随 Codex", systemImage: mode == .followCodex ? "checkmark" : "rectangle.on.rectangle")
                }
                Divider()
                Menu("外观", systemImage: "circle.lefthalf.filled") {
                    Button {
                        setTheme(.dark)
                    } label: {
                        Label("深色玻璃", systemImage: theme == .dark ? "checkmark" : "moon.fill")
                    }
                    Button {
                        setTheme(.light)
                    } label: {
                        Label("浅色玻璃", systemImage: theme == .light ? "checkmark" : "sun.max.fill")
                    }
                }
                Button("立即刷新", systemImage: "arrow.clockwise") {
                    client.refreshNow()
                }
                Button("退出", systemImage: "power", action: quit)
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(secondaryText)
                    .frame(width: 24, height: 20)
                    .contentShape(Rectangle())
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.hidden)
            .fixedSize()
        }
    }

    private func meter(
        _ window: UsageSnapshot.Window,
        prominent: Bool,
        expectedRemaining: Double? = nil
    ) -> some View {
        let remainingPercent = max(0, min(100, 100 - window.usedPercent))

        return VStack(spacing: 7) {
            HStack(alignment: .firstTextBaseline) {
                Text(window.title == "Weekly" ? "每周额度" : window.title)
                    .font(.system(size: prominent ? 11.5 : 10.5, weight: .semibold))
                    .foregroundStyle(prominent ? primaryText : secondaryText)
                Spacer()
                if let reset = window.resetsAt {
                    Text("\(duration(reset.timeIntervalSinceNow)) 后重置")
                        .font(.system(size: 9.5))
                        .foregroundStyle(tertiaryText)
                }
                Text("剩余 \(Int(remainingPercent.rounded()))%")
                    .font(.system(size: prominent ? 15.5 : 11, weight: .bold, design: .rounded))
                    .foregroundStyle(primaryText)
                    .monospacedDigit()
            }

            GeometryReader { geometry in
                ZStack(alignment: .leading) {
                    Capsule().fill(trackColor)
                    Capsule()
                        .fill(barGradient(forRemaining: remainingPercent, prominent: prominent))
                        .frame(width: max(4, geometry.size.width * remainingPercent / 100))
                        .shadow(color: barGlow(forRemaining: remainingPercent), radius: 4)
                    if let expectedRemaining {
                        Rectangle()
                            .fill(Color(red: 1, green: 0.27, blue: 0.23))
                            .frame(width: 3, height: prominent ? 12 : 9)
                            .shadow(color: .red.opacity(0.65), radius: 3)
                            .position(
                                x: geometry.size.width * min(max(expectedRemaining, 0), 100) / 100,
                                y: geometry.size.height / 2
                            )
                    }
                }
            }
            .frame(height: prominent ? 8 : 6)
        }
    }

    private func paceRow(_ pace: UsageSnapshot.Pace) -> some View {
        HStack {
            Text(pace.deltaPercent > 0 ? "超额 \(Int(pace.deltaPercent.rounded()))%" : "节奏正常")
                .foregroundStyle(pace.deltaPercent > 0 ? appleOrange : secondaryText)
            Spacer()
            if pace.willLastToReset {
                Text("预计可用至重置")
            } else if let etaSeconds = pace.etaSeconds {
                Text("预计 \(duration(etaSeconds)) 后耗尽")
            }
        }
        .font(.system(size: 9.5, weight: .semibold))
        .foregroundStyle(secondaryText)
    }

    private func duration(_ seconds: Double) -> String {
        let totalHours = max(0, Int(seconds) / 3600)
        let days = totalHours / 24
        let hours = totalHours % 24
        return days > 0 ? "\(days)d \(hours)h" : "\(hours)h"
    }

    private var loading: some View {
        HStack(spacing: 8) {
            ProgressView().controlSize(.small)
            Text(client.lastError ?? "正在连接 Codex…")
                .font(.system(size: 10.5))
                .foregroundStyle(secondaryText)
                .lineLimit(1)
            Spacer()
        }
        .frame(height: 28)
    }

    private func barGradient(forRemaining percent: Double, prominent: Bool) -> LinearGradient {
        let colors: [Color]
        if percent < 15 {
            colors = [.red, Color(red: 1, green: 0.27, blue: 0.23)]
        } else if percent < 35 {
            colors = [appleOrange, .yellow]
        } else if prominent {
            colors = [Color(red: 0.25, green: 0.79, blue: 1), appleBlue]
        } else {
            colors = [Color(red: 0.38, green: 0.82, blue: 0.94), Color(red: 0.36, green: 0.48, blue: 1)]
        }
        return LinearGradient(colors: colors, startPoint: .leading, endPoint: .trailing)
    }

    private func barGlow(forRemaining percent: Double) -> Color {
        percent < 15 ? .red.opacity(0.45) : percent < 35 ? appleOrange.opacity(0.4) : appleBlue.opacity(0.38)
    }

    private var glassTint: Color {
        theme == .dark
            ? Color(red: 0.035, green: 0.045, blue: 0.065).opacity(0.82)
            : Color(red: 0.94, green: 0.96, blue: 0.99).opacity(0.78)
    }

    private var glassStroke: Color {
        theme == .dark ? .white.opacity(0.14) : .white.opacity(0.82)
    }

    private var primaryText: Color {
        theme == .dark ? .white.opacity(0.96) : .black.opacity(0.84)
    }

    private var secondaryText: Color {
        theme == .dark ? .white.opacity(0.76) : .black.opacity(0.60)
    }

    private var tertiaryText: Color {
        theme == .dark ? .white.opacity(0.62) : .black.opacity(0.48)
    }

    private var statusBackground: Color {
        theme == .dark ? .white.opacity(0.07) : .black.opacity(0.055)
    }

    private var trackColor: Color {
        theme == .dark ? .white.opacity(0.11) : .black.opacity(0.09)
    }

    private let appleBlue = Color(red: 0.04, green: 0.52, blue: 1)
    private let appleOrange = Color(red: 1, green: 0.62, blue: 0.04)
}
