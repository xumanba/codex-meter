import AppKit
import CoreGraphics

enum PanelMode: String {
    case fixed
    case followCodex
}

@MainActor
final class PanelPositioner {
    private weak var panel: NSPanel?
    private let mouseLocation: () -> NSPoint
    private var timer: Timer?
    private var dockEvaluation: DispatchWorkItem?
    private var revealTimer: Timer?
    private var hideTimer: Timer?
    private var isRunning = false
    private var isProgrammaticMove = false
    private var isAnimating = false
    private var dockEdge: DockEdge?
    private var dockScreenNumber: Int?
    private var dockAnchor: CGFloat = 0
    private var isDockRevealed = false

    private let dockingThreshold: CGFloat = 28
    private let visibleStrip: CGFloat = 5
    private let revealInset: CGFloat = 8
    private let revealDelay: TimeInterval = 0.02
    private let hideDelay: TimeInterval = 0.18

    private let dockEdgeKey = "edgeDockEdge"
    private let dockScreenKey = "edgeDockScreenNumber"
    private let dockAnchorKey = "edgeDockAnchor"

    var mode: PanelMode = .fixed {
        didSet {
            cancelPendingPointerActions()
            if mode == .fixed, dockEdge != nil {
                isDockRevealed = false
            }
            restartTimer()
            update()
        }
    }

    init(
        panel: NSPanel,
        mouseLocation: @escaping () -> NSPoint = { NSEvent.mouseLocation }
    ) {
        self.panel = panel
        self.mouseLocation = mouseLocation
        restoreDock()
    }

    func start() {
        isRunning = true
        update()
        restartTimer()
    }

    func stop() {
        isRunning = false
        timer?.invalidate()
        dockEvaluation?.cancel()
        cancelPendingPointerActions()
    }

    func update() {
        guard let panel else { return }
        if mode == .fixed {
            if let edge = dockEdge, let screen = activeDockScreen() {
                panel.orderFrontRegardless()
                if !isAnimating {
                    moveDockedPanel(
                        edge: edge,
                        screen: screen,
                        revealed: isDockRevealed,
                        animated: false
                    )
                }
                return
            }

            if !NSScreen.screens.contains(where: { $0.visibleFrame.intersects(panel.frame) }),
               let visible = NSScreen.main?.visibleFrame {
                setOrigin(NSPoint(
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

        setOrigin(NSPoint(
            x: max(visible.minX + 12, x),
            y: max(visible.minY + 12, y)
        ))
        panel.alphaValue = NSWorkspace.shared.frontmostApplication?.bundleIdentifier == "com.openai.codex" ? 1 : 0
    }

    func panelDidMove() -> Bool {
        guard mode == .fixed else { return false }
        guard !isProgrammaticMove, !isAnimating else { return false }

        if dockEdge != nil {
            clearDock()
        }
        scheduleDockEvaluation()
        return true
    }

    func pointerEnteredPanel() {
        guard mode == .fixed, dockEdge != nil else { return }
        hideTimer?.invalidate()
        hideTimer = nil
        guard !isDockRevealed else { return }

        revealTimer?.invalidate()
        let timer = Timer(timeInterval: revealDelay, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.revealDockedPanelIfNeeded()
            }
        }
        RunLoop.main.add(timer, forMode: .common)
        revealTimer = timer
    }

    func pointerExitedPanel() {
        revealTimer?.invalidate()
        revealTimer = nil
        guard mode == .fixed, dockEdge != nil, isDockRevealed else { return }

        hideTimer?.invalidate()
        let timer = Timer(timeInterval: hideDelay, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.hideDockedPanelIfNeeded()
            }
        }
        RunLoop.main.add(timer, forMode: .common)
        hideTimer = timer
    }

    private func restartTimer() {
        timer?.invalidate()
        guard isRunning else { return }

        let timer = Timer(timeInterval: 0.75, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.update() }
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    private func scheduleDockEvaluation(after delay: TimeInterval = 0.22) {
        dockEvaluation?.cancel()
        let work = DispatchWorkItem { [weak self] in
            self?.evaluateDocking()
        }
        dockEvaluation = work
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: work)
    }

    private func evaluateDocking() {
        guard mode == .fixed, dockEdge == nil, let panel else { return }
        if NSEvent.pressedMouseButtons != 0 {
            scheduleDockEvaluation(after: 0.10)
            return
        }

        guard let screen = screenContaining(mouseLocation())
                ?? screenWithLargestIntersection(panel.frame),
              let edge = EdgeDockGeometry.closestEdge(
                to: panel.frame,
                in: screen.visibleFrame,
                threshold: dockingThreshold
              ) else {
            return
        }

        dockEdge = edge
        dockScreenNumber = screenNumber(for: screen)
        dockAnchor = EdgeDockGeometry.anchor(for: panel.frame)
        isDockRevealed = false
        persistDock()
        restartTimer()
        moveDockedPanel(edge: edge, screen: screen, revealed: false, animated: true)
    }

    private func moveDockedPanel(
        edge: DockEdge,
        screen: NSScreen,
        revealed: Bool,
        animated: Bool
    ) {
        guard let panel else { return }

        let origin = revealed
            ? EdgeDockGeometry.revealedOrigin(
                edge: edge,
                panelSize: panel.frame.size,
                visibleFrame: screen.visibleFrame,
                anchor: dockAnchor,
                inset: revealInset
            )
            : EdgeDockGeometry.hiddenOrigin(
                edge: edge,
                panelSize: panel.frame.size,
                screenFrame: screen.frame,
                visibleFrame: screen.visibleFrame,
                anchor: dockAnchor,
                visibleStrip: visibleStrip
            )

        guard animated else {
            panel.alphaValue = 1
            setOrigin(origin)
            return
        }

        isAnimating = true
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.12
            context.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel.animator().setFrame(
                NSRect(origin: origin, size: panel.frame.size),
                display: true
            )
        } completionHandler: { [weak self] in
            Task { @MainActor in self?.isAnimating = false }
        }
    }

    private func clearDock() {
        dockEvaluation?.cancel()
        dockEdge = nil
        dockScreenNumber = nil
        isDockRevealed = false
        cancelPendingPointerActions()

        let defaults = UserDefaults.standard
        defaults.removeObject(forKey: dockEdgeKey)
        defaults.removeObject(forKey: dockScreenKey)
        defaults.removeObject(forKey: dockAnchorKey)
        restartTimer()
    }

    private func revealDockedPanelIfNeeded() {
        revealTimer = nil
        guard mode == .fixed,
              !isDockRevealed,
              let edge = dockEdge,
              let screen = activeDockScreen() else {
            return
        }
        isDockRevealed = true
        moveDockedPanel(edge: edge, screen: screen, revealed: true, animated: true)
    }

    private func hideDockedPanelIfNeeded() {
        hideTimer = nil
        guard mode == .fixed,
              isDockRevealed,
              let panel,
              !panel.frame.insetBy(dx: -20, dy: -20).contains(mouseLocation()),
              let edge = dockEdge,
              let screen = activeDockScreen() else {
            return
        }
        isDockRevealed = false
        moveDockedPanel(edge: edge, screen: screen, revealed: false, animated: true)
    }

    private func cancelPendingPointerActions() {
        revealTimer?.invalidate()
        revealTimer = nil
        hideTimer?.invalidate()
        hideTimer = nil
    }

    private func restoreDock() {
        let defaults = UserDefaults.standard
        guard let rawEdge = defaults.string(forKey: dockEdgeKey),
              let edge = DockEdge(rawValue: rawEdge),
              defaults.object(forKey: dockAnchorKey) != nil else {
            return
        }

        dockEdge = edge
        dockAnchor = defaults.double(forKey: dockAnchorKey)
        if defaults.object(forKey: dockScreenKey) != nil {
            dockScreenNumber = defaults.integer(forKey: dockScreenKey)
        }
    }

    private func persistDock() {
        guard let dockEdge else { return }
        let defaults = UserDefaults.standard
        defaults.set(dockEdge.rawValue, forKey: dockEdgeKey)
        defaults.set(dockAnchor, forKey: dockAnchorKey)
        if let dockScreenNumber {
            defaults.set(dockScreenNumber, forKey: dockScreenKey)
        }
    }

    private func activeDockScreen() -> NSScreen? {
        if let dockScreenNumber,
           let screen = NSScreen.screens.first(where: {
               screenNumber(for: $0) == dockScreenNumber
           }) {
            return screen
        }

        guard let fallback = NSScreen.main ?? NSScreen.screens.first else { return nil }
        dockScreenNumber = screenNumber(for: fallback)
        if dockEdge != nil, let panel {
            dockAnchor = EdgeDockGeometry.anchor(for: panel.frame)
        }
        persistDock()
        return fallback
    }

    private func setOrigin(_ origin: NSPoint) {
        guard let panel, panel.frame.origin != origin else { return }
        isProgrammaticMove = true
        panel.setFrameOrigin(origin)
        isProgrammaticMove = false
    }

    private func screenContaining(_ point: NSPoint) -> NSScreen? {
        NSScreen.screens.first(where: {
            NSMouseInRect(point, $0.frame, false)
        })
    }

    private func screenWithLargestIntersection(_ frame: NSRect) -> NSScreen? {
        NSScreen.screens.max(by: {
            intersectionArea($0.visibleFrame, frame) < intersectionArea($1.visibleFrame, frame)
        })
    }

    private func intersectionArea(_ lhs: NSRect, _ rhs: NSRect) -> CGFloat {
        let intersection = lhs.intersection(rhs)
        return intersection.isNull ? 0 : intersection.width * intersection.height
    }

    private func screenNumber(for screen: NSScreen) -> Int? {
        (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.intValue
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
