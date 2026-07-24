import AppKit
import SwiftUI

@MainActor
final class MeterTrackingHostingView: NSHostingView<MeterView> {
    var onMouseEntered: (() -> Void)?
    var onMouseExited: (() -> Void)?
    private var pointerTrackingArea: NSTrackingArea?

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let pointerTrackingArea {
            removeTrackingArea(pointerTrackingArea)
        }

        let area = NSTrackingArea(
            rect: .zero,
            options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
            owner: self,
            userInfo: nil
        )
        addTrackingArea(area)
        pointerTrackingArea = area
    }

    override func mouseEntered(with event: NSEvent) {
        onMouseEntered?()
    }

    override func mouseExited(with event: NSEvent) {
        onMouseExited?()
    }
}

@main
struct CodexBarFloatingMeterApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Settings { EmptyView() }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let client = CodexBarClient()
    private var panel: NSPanel?
    private var positioner: PanelPositioner?
    private var moveObserver: NSObjectProtocol?
    private var mode: PanelMode = .fixed
    private var theme = MeterTheme(
        rawValue: UserDefaults.standard.string(forKey: "meterTheme") ?? ""
    ) ?? .dark

    private let fixedXKey = "fixedPanelX"
    private let fixedYKey = "fixedPanelY"

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)

        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 292, height: 160),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.level = .floating
        panel.appearance = appearance(for: theme)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.isMovableByWindowBackground = true
        panel.hidesOnDeactivate = false

        self.panel = panel
        positioner = PanelPositioner(panel: panel)
        panel.contentView = makeContentView()
        panel.setFrameOrigin(defaultOrigin(for: panel.frame.size))
        panel.orderFrontRegardless()

        moveObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.didMoveNotification,
            object: panel,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                if self.positioner?.panelDidMove() == true {
                    self.saveFixedOrigin()
                }
            }
        }
        positioner?.start()
        client.start()
    }

    func applicationWillTerminate(_ notification: Notification) {
        if let moveObserver {
            NotificationCenter.default.removeObserver(moveObserver)
        }
        positioner?.stop()
        client.stop()
    }

    private func setMode(_ newMode: PanelMode) {
        mode = newMode
        guard let panel else { return }
        if newMode == .fixed {
            panel.setFrameOrigin(defaultOrigin(for: panel.frame.size))
            panel.orderFrontRegardless()
        }
        positioner?.mode = newMode
        panel.contentView = makeContentView()
    }

    private func setTheme(_ newTheme: MeterTheme) {
        theme = newTheme
        UserDefaults.standard.set(newTheme.rawValue, forKey: "meterTheme")
        panel?.appearance = appearance(for: newTheme)
        guard let panel else { return }
        panel.contentView = makeContentView()
    }

    private func makeContentView() -> NSView {
        let view = MeterTrackingHostingView(rootView: MeterView(
            client: client,
            mode: mode,
            theme: theme,
            setMode: { [weak self] mode in self?.setMode(mode) },
            setTheme: { [weak self] theme in self?.setTheme(theme) },
            quit: { NSApp.terminate(nil) }
        ))
        view.onMouseEntered = { [weak self] in
            self?.positioner?.pointerEnteredPanel()
        }
        view.onMouseExited = { [weak self] in
            self?.positioner?.pointerExitedPanel()
        }
        return view
    }

    private func appearance(for theme: MeterTheme) -> NSAppearance? {
        NSAppearance(named: theme == .dark ? .darkAqua : .aqua)
    }

    private func defaultOrigin(for size: NSSize) -> NSPoint {
        let defaults = UserDefaults.standard
        if defaults.object(forKey: fixedXKey) != nil,
           defaults.object(forKey: fixedYKey) != nil {
            let saved = NSPoint(
                x: defaults.double(forKey: fixedXKey),
                y: defaults.double(forKey: fixedYKey)
            )
            let savedFrame = NSRect(origin: saved, size: size)
            if NSScreen.screens.contains(where: { $0.visibleFrame.intersects(savedFrame) }) {
                return saved
            }
        }

        guard let visible = NSScreen.main?.visibleFrame else { return .zero }
        return NSPoint(
            x: visible.maxX - size.width - 24,
            y: visible.maxY - size.height - 54
        )
    }

    private func saveFixedOrigin() {
        guard mode == .fixed, let origin = panel?.frame.origin else { return }
        UserDefaults.standard.set(origin.x, forKey: fixedXKey)
        UserDefaults.standard.set(origin.y, forKey: fixedYKey)
    }
}
