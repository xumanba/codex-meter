import Foundation
import AppKit
import SwiftUI

enum MeterTheme: String {
    case dark
    case light
}

enum MeterLayout {
    static let width: CGFloat = 344
    static let minimumHeight: CGFloat = 240
    private static let modelRowHeight: CGFloat = 28
    private static let modelRowSpacing: CGFloat = 6
    private static let paceSectionHeight: CGFloat = 29

    static func height(
        for modelCount: Int,
        maximum: CGFloat? = nil,
        hasPace: Bool = false
    ) -> CGFloat {
        let modelHeight: CGFloat
        if modelCount > 0 {
            modelHeight = CGFloat(modelCount) * modelRowHeight
                + CGFloat(max(0, modelCount - 1)) * modelRowSpacing
        } else {
            modelHeight = 28
        }

        let paceHeight = hasPace ? paceSectionHeight : 0
        let intrinsicHeight = 28 + 26 + (3 * 13) + 103 + paceHeight + 108 + 14 + 9 + modelHeight
        guard let maximum else { return intrinsicHeight }
        return min(intrinsicHeight, max(minimumHeight, maximum))
    }
}

struct MeterView: View {
    @ObservedObject var client: CodexBarClient
    let mode: PanelMode
    let theme: MeterTheme
    let panelHeight: CGFloat
    let setMode: (PanelMode) -> Void
    let setTheme: (MeterTheme) -> Void
    let quit: () -> Void

    var body: some View {
        ScrollView(.vertical, showsIndicators: false) {
            VStack(spacing: 13) {
                header

                if let snapshot = client.snapshot {
                    weeklySection(
                        snapshot.weekly,
                        pace: snapshot.pace,
                        weeklyTotals: client.tokenUsage?.weeklyTotals
                    )

                    if let tokenUsage = client.tokenUsage {
                        dailyUsage(tokenUsage)
                        modelUsage(tokenUsage)
                    } else {
                        tokenUsageLoading
                    }
                } else {
                    loading
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 14)
        }
        .frame(width: MeterLayout.width, height: panelHeight)
        .background {
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .fill(.ultraThinMaterial)
                .overlay {
                    RoundedRectangle(cornerRadius: 22, style: .continuous)
                        .fill(glassTint)
                }
                .shadow(color: .black.opacity(theme == .dark ? 0.45 : 0.20), radius: 24, y: 12)
        }
        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .strokeBorder(glassStroke, lineWidth: 0.75)
        }
        .environment(
            \.colorScheme,
            theme == .dark ? .dark : .light
        )
    }

    private var header: some View {
        HStack(spacing: 9) {
            headerLogo

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

    @ViewBuilder
    private var headerLogo: some View {
        if let iconURL = Bundle.main.url(forResource: "CodexMeter", withExtension: "icns"),
           let appIcon = NSImage(contentsOf: iconURL) {
            Image(nsImage: appIcon)
                .resizable()
                .interpolation(.high)
                .scaledToFit()
                .clipShape(RoundedRectangle(cornerRadius: 7, style: .continuous))
                .frame(width: 30, height: 30)
                .shadow(color: appleBlue.opacity(0.35), radius: 7)
        } else {
            Image(systemName: "sparkles")
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(.white)
                .frame(width: 26, height: 26)
                .background(appleBlue.gradient, in: Circle())
                .shadow(color: appleBlue.opacity(0.45), radius: 7)
        }
    }

    private func weeklySection(
        _ window: UsageSnapshot.Window,
        pace: UsageSnapshot.Pace?,
        weeklyTotals: TokenUsageTotals?
    ) -> some View {
        let usedPercent = max(0, min(100, window.usedPercent))
        let remainingPercent = max(0, 100 - usedPercent)

        return VStack(alignment: .leading, spacing: 9) {
            HStack(alignment: .firstTextBaseline) {
                Text("每周额度")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(primaryText)
                Spacer()
                if let reset = window.resetsAt {
                    Text("\(duration(reset.timeIntervalSinceNow)) 后重置")
                        .font(.system(size: 9.5))
                        .foregroundStyle(tertiaryText)
                }
            }

            HStack(alignment: .lastTextBaseline) {
                Text("剩余")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(secondaryText)
                Text(formatWholePercent(remainingPercent))
                    .font(.system(size: 29, weight: .bold, design: .rounded))
                    .foregroundStyle(remainingColor(for: remainingPercent))
                    .monospacedDigit()
                Spacer()
                Text(weeklyTotals.map { "本周 \(tokenAmount($0.totalTokens)) token" } ?? "本周 —")
                    .font(.system(size: 10.5, weight: .medium))
                    .foregroundStyle(secondaryText)
                    .monospacedDigit()
            }

            quotaProgressBar(
                remainingPercent: remainingPercent,
                expectedRemaining: pace.map {
                    max(0, min(100, 100 - $0.expectedUsedPercent))
                },
                markerColor: pace.map { paceMarkerColor(for: $0) }
            )

            if let pace {
                paceRow(pace, usedPercent: usedPercent)
                    .padding(.top, 4)
            }
        }
        .padding(13)
        .background(weeklySurface, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(weeklyStroke, lineWidth: 0.65)
        }
    }

    private func quotaProgressBar(
        remainingPercent: Double,
        expectedRemaining: Double? = nil,
        markerColor: Color? = nil
    ) -> some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(trackColor)

                Capsule()
                    .fill(remainingGradient(for: remainingPercent))
                    .frame(width: geometry.size.width * remainingPercent / 100)
                    .shadow(color: remainingColor(for: remainingPercent).opacity(0.45), radius: 5)

                if let expectedRemaining, let markerColor {
                    Capsule()
                        .fill(markerColor)
                        .frame(width: 4, height: 20)
                        .offset(
                            x: geometry.size.width * expectedRemaining / 100 - 2,
                            y: 0
                        )
                        .shadow(color: markerColor.opacity(0.45), radius: 3)
                }
            }
        }
        .frame(height: 12)
    }

    private func paceRow(
        _ pace: UsageSnapshot.Pace,
        usedPercent: Double
    ) -> some View {
        HStack(spacing: 5) {
            HStack(spacing: 4) {
                Circle()
                    .fill(paceColor(for: pace))
                    .frame(width: 5, height: 5)
                Text(paceLabel(for: pace))
                    .foregroundStyle(paceColor(for: pace))
            }

            Spacer(minLength: 3)

            Text("已用 \(formatPercent(usedPercent)) · 应 \(formatPercent(pace.expectedUsedPercent))")
                .foregroundStyle(secondaryText)
                .monospacedDigit()

            Text("·")
                .foregroundStyle(tertiaryText)

            Text(paceForecast(for: pace))
                .foregroundStyle(tertiaryText)
        }
        .font(.system(size: 9, weight: .semibold))
        .lineLimit(1)
        .minimumScaleFactor(0.72)
    }

    private func paceLabel(for pace: UsageSnapshot.Pace) -> String {
        if pace.deltaPercent > 0.5 {
            return "节奏偏快"
        }
        if pace.deltaPercent < -0.5 {
            return "节奏偏慢"
        }
        return "节奏正常"
    }

    private func paceColor(for pace: UsageSnapshot.Pace) -> Color {
        if pace.deltaPercent > 0.5 {
            return Color(red: 1, green: 0.62, blue: 0.22)
        }
        if pace.deltaPercent < -0.5 {
            return appleBlue
        }
        return Color(red: 0.25, green: 0.82, blue: 0.42)
    }

    private func paceMarkerColor(for _: UsageSnapshot.Pace) -> Color {
        .white
    }

    private func paceForecast(for pace: UsageSnapshot.Pace) -> String {
        if pace.willLastToReset {
            return "可用至重置"
        }
        if let etaSeconds = pace.etaSeconds {
            return "约 \(duration(etaSeconds)) 后耗尽"
        }
        return "暂无预测"
    }

    private func dailyUsage(_ usage: TokenUsageSnapshot) -> some View {
        let tokenMaximum = max(1, usage.daily.map(\.totals.totalTokens).max() ?? 0)

        return VStack(alignment: .leading, spacing: 9) {
            Text("近7天每日")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(primaryText)

            HStack(alignment: .bottom, spacing: 6) {
                ForEach(usage.daily) { day in
                    dailyBar(
                        day,
                        tokenMaximum: tokenMaximum
                    )
                }
            }
            .frame(height: 84)
        }
    }

    private func dailyBar(
        _ day: TokenUsageSnapshot.Daily,
        tokenMaximum: Int64
    ) -> some View {
        let normalized = Double(max(0, day.totals.totalTokens)) / Double(tokenMaximum)

        return VStack(spacing: 4) {
            Text(tokenAmount(day.totals.totalTokens))
                .font(.system(size: 9.5, weight: .bold, design: .rounded))
                .foregroundStyle(primaryText)
                .monospacedDigit()
                .frame(height: 12)

            ZStack(alignment: .bottom) {
                Capsule()
                    .fill(trackColor)
                Capsule()
                    .fill(dailyBarGradient)
                    .frame(height: max(4, 54 * min(1, normalized)))
            }
            .frame(height: 54)

            Text(dayLabel(day.date))
                .font(.system(size: 9.5, weight: .medium))
                .foregroundStyle(secondaryText)
        }
        .frame(maxWidth: .infinity)
    }

    private func modelUsage(_ usage: TokenUsageSnapshot) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("模型偏好")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(primaryText)

            if usage.breakdowns.isEmpty {
                Text("当前周额度窗口暂无 token 记录")
                    .font(.system(size: 10))
                    .foregroundStyle(tertiaryText)
            } else {
                ForEach(usage.breakdowns) { breakdown in
                    modelRow(breakdown)
                }
            }
        }
    }

    private func modelRow(
        _ breakdown: TokenUsageSnapshot.Breakdown
    ) -> some View {
        let accent = modelAccentColor(breakdown.model)

        return HStack(alignment: .firstTextBaseline, spacing: 7) {
            Text(modelDisplayName(breakdown.model))
                .font(.system(size: 12.5, weight: .semibold))
                .foregroundStyle(accent)
                .lineLimit(1)
                .minimumScaleFactor(0.72)
            Text("· \(effortDisplayName(breakdown.effort))")
                .font(.system(size: 11.5, weight: .medium))
                .foregroundStyle(secondaryText)
                .lineLimit(1)
                .minimumScaleFactor(0.72)
            if breakdown.isFast {
                Text("· Fast")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(.orange)
            }
            Spacer(minLength: 3)
            Text(tokenAmount(breakdown.totals.totalTokens))
                .font(.system(size: 14.5, weight: .bold, design: .rounded))
                .foregroundStyle(accent)
                .monospacedDigit()
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 5)
        .background(statusBackground, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private func modelAccentColor(_ model: String) -> Color {
        switch model.lowercased() {
        case "gpt-5.6-sol":
            return Color(red: 0.30, green: 0.78, blue: 1.0)
        case "gpt-5.6-terra":
            return Color(red: 0.32, green: 0.86, blue: 0.56)
        case "gpt-5.6-luna":
            return Color(red: 0.76, green: 0.54, blue: 1.0)
        case "codex-auto-review":
            return Color(red: 1.0, green: 0.62, blue: 0.28)
        default:
            return appleBlue
        }
    }

    private func modelDisplayName(_ model: String) -> String {
        switch model.lowercased() {
        case "gpt-5.6-sol":
            return "5.6 Sol"
        case "gpt-5.6-terra":
            return "5.6 Terra"
        case "gpt-5.6-luna":
            return "5.6 Luna"
        case "codex-auto-review":
            return "5.6 Luna · Auto Review"
        default:
            return model
        }
    }

    private func effortDisplayName(_ effort: String) -> String {
        switch effort.lowercased() {
        case "none":
            return "None"
        case "low":
            return "Low"
        case "medium":
            return "Medium"
        case "high":
            return "High"
        case "xhigh":
            return "xHigh"
        case "max":
            return "Max"
        case "ultra":
            return "Ultra"
        default:
            return effort
        }
    }

    private var tokenUsageLoading: some View {
        HStack(spacing: 8) {
            ProgressView().controlSize(.small)
            Text("正在扫描本地 token 记录…")
                .font(.system(size: 10.5))
                .foregroundStyle(secondaryText)
            Spacer()
        }
        .frame(height: 28)
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

    private func duration(_ seconds: Double) -> String {
        let totalHours = max(0, Int(seconds) / 3600)
        let days = totalHours / 24
        let hours = totalHours % 24
        return days > 0 ? "\(days)d \(hours)h" : "\(hours)h"
    }

    private func dayLabel(_ date: Date) -> String {
        if Calendar.current.isDateInToday(date) {
            return "今"
        }
        let weekday = Calendar.current.component(.weekday, from: date)
        return ["日", "一", "二", "三", "四", "五", "六"][max(1, min(7, weekday)) - 1]
    }

    private func formatPercent(_ value: Double?) -> String {
        guard let value else { return "—" }
        return String(format: "%.1f%%", max(0, value))
    }

    private func formatWholePercent(_ value: Double?) -> String {
        guard let value else { return "—" }
        return String(format: "%.0f%%", max(0, value))
    }

    private func tokenAmount(_ tokens: Int64) -> String {
        let value = Double(max(0, tokens))
        if value >= 1_000_000_000 {
            return String(format: "%.2fB", value / 1_000_000_000)
        }
        if value >= 1_000_000 {
            return String(format: "%.1fM", value / 1_000_000)
        }
        if value >= 1_000 {
            return String(format: "%.1fK", value / 1_000)
        }
        return "\(Int64(value.rounded()))"
    }

    private func remainingColor(for percent: Double) -> Color {
        if percent >= 50 {
            return Color(red: 0.25, green: 0.82, blue: 0.42)
        }
        if percent >= 20 {
            return Color(red: 1, green: 0.68, blue: 0.12)
        }
        return Color(red: 1, green: 0.28, blue: 0.24)
    }

    private func remainingGradient(for percent: Double) -> LinearGradient {
        let color = remainingColor(for: percent)
        return LinearGradient(
            colors: [color.opacity(0.72), color],
            startPoint: .leading,
            endPoint: .trailing
        )
    }

    private var dailyBarGradient: LinearGradient {
        LinearGradient(
            colors: [Color(red: 0.38, green: 0.82, blue: 0.94), Color(red: 0.36, green: 0.48, blue: 1)],
            startPoint: .leading,
            endPoint: .trailing
        )
    }

    private var glassTint: Color {
        theme == .dark
            ? Color(red: 0.035, green: 0.045, blue: 0.065).opacity(0.84)
            : Color(red: 0.94, green: 0.96, blue: 0.99).opacity(0.80)
    }

    private var glassStroke: Color {
        theme == .dark ? .white.opacity(0.14) : .white.opacity(0.82)
    }

    private var weeklySurface: Color {
        theme == .dark ? Color.white.opacity(0.065) : Color.black.opacity(0.045)
    }

    private var weeklyStroke: Color {
        theme == .dark ? .white.opacity(0.12) : .black.opacity(0.08)
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
}
