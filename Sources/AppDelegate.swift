import AppKit
import SwiftUI

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

        let view = MeterView(
            client: client,
            mode: mode,
            theme: theme,
            setMode: { [weak self] mode in self?.setMode(mode) },
            setTheme: { [weak self] theme in self?.setTheme(theme) },
            quit: { NSApp.terminate(nil) }
        )
        panel.contentView = NSHostingView(rootView: view)
        panel.setFrameOrigin(defaultOrigin(for: panel.frame.size))
        panel.orderFrontRegardless()

        self.panel = panel
        positioner = PanelPositioner(panel: panel)
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
        panel.contentView = NSHostingView(rootView: MeterView(
            client: client,
            mode: mode,
            theme: theme,
            setMode: { [weak self] mode in self?.setMode(mode) },
            setTheme: { [weak self] theme in self?.setTheme(theme) },
            quit: { NSApp.terminate(nil) }
        ))
    }

    private func setTheme(_ newTheme: MeterTheme) {
        theme = newTheme
        UserDefaults.standard.set(newTheme.rawValue, forKey: "meterTheme")
        panel?.appearance = appearance(for: newTheme)
        guard let panel else { return }
        panel.contentView = NSHostingView(rootView: MeterView(
            client: client,
            mode: mode,
            theme: theme,
            setMode: { [weak self] mode in self?.setMode(mode) },
            setTheme: { [weak self] theme in self?.setTheme(theme) },
            quit: { NSApp.terminate(nil) }
        ))
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
