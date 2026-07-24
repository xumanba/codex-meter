import AppKit
import CoreGraphics

enum PanelMode: String {
    case fixed
    case followCodex
}

@MainActor
final class PanelPositioner {
    private weak var panel: NSPanel?
    private var timer: Timer?

    var mode: PanelMode = .fixed {
        didSet { update() }
    }

    init(panel: NSPanel) {
        self.panel = panel
    }

    func start() {
        update()
        timer = Timer.scheduledTimer(withTimeInterval: 0.75, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.update() }
        }
    }

    func stop() {
        timer?.invalidate()
    }

    func update() {
        guard let panel else { return }
        if mode == .fixed {
            if !NSScreen.screens.contains(where: { $0.visibleFrame.intersects(panel.frame) }),
               let visible = NSScreen.main?.visibleFrame {
                panel.setFrameOrigin(NSPoint(
                    x: visible.maxX - panel.frame.width - 24,
                    y: visible.maxY - panel.frame.height - 24
                ))
            }
            panel.alphaValue = 1
            panel.orderFrontRegardless()
            return
        }

        guard
            let codexWindow = Self.codexWindowFrame(),
            let screen = NSScreen.screens.first(where: { $0.frame.intersects(codexWindow) })
        else {
            panel.alphaValue = 0
            return
        }

        let visible = screen.visibleFrame
        let size = panel.frame.size
        let preferredX = codexWindow.maxX + 12
        let x = preferredX + size.width <= visible.maxX
            ? preferredX
            : codexWindow.maxX - size.width - 18
        let y = min(codexWindow.maxY - size.height - 62, visible.maxY - size.height - 12)

        panel.setFrameOrigin(NSPoint(
            x: max(visible.minX + 12, x),
            y: max(visible.minY + 12, y)
        ))
        panel.alphaValue = NSWorkspace.shared.frontmostApplication?.bundleIdentifier == "com.openai.codex" ? 1 : 0
    }

    private static func codexWindowFrame() -> CGRect? {
        let codexPIDs = Set(
            NSRunningApplication
                .runningApplications(withBundleIdentifier: "com.openai.codex")
                .map(\.processIdentifier)
        )

        guard let windows = CGWindowListCopyWindowInfo(
            [.optionOnScreenOnly, .excludeDesktopElements],
            kCGNullWindowID
        ) as? [[String: Any]] else {
            return nil
        }

        guard let info = windows.first(where: {
            guard let pid = $0[kCGWindowOwnerPID as String] as? Int else { return false }
            return codexPIDs.contains(pid_t(pid))
                && ($0[kCGWindowLayer as String] as? Int) == 0
        }),
        let bounds = info[kCGWindowBounds as String] as? [String: CGFloat],
        let x = bounds["X"],
        let top = bounds["Y"],
        let width = bounds["Width"],
        let height = bounds["Height"],
        let screenHeight = NSScreen.screens.first?.frame.height
        else {
            return nil
        }

        return CGRect(
            x: x,
            y: screenHeight - top - height,
            width: width,
            height: height
        )
    }
}
