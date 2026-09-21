import AppKit

enum DockEdge: String, CaseIterable {
    case left
    case right
}

enum EdgeDockGeometry {
    static func closestEdge(
        to panelFrame: NSRect,
        in visibleFrame: NSRect,
        threshold: CGFloat
    ) -> DockEdge? {
        let distances: [(DockEdge, CGFloat)] = [
            (.left, abs(panelFrame.minX - visibleFrame.minX)),
            (.right, abs(panelFrame.maxX - visibleFrame.maxX)),
        ]

        guard let closest = distances.min(by: { $0.1 < $1.1 }),
              closest.1 <= threshold else {
            return nil
        }
        return closest.0
    }

    static func anchor(for panelFrame: NSRect) -> CGFloat {
        panelFrame.minY
    }

    static func hiddenOrigin(
        edge: DockEdge,
        panelSize: NSSize,
        screenFrame: NSRect,
        visibleFrame: NSRect,
        anchor: CGFloat,
        visibleStrip: CGFloat
    ) -> NSPoint {
        let clamped = clampedAnchor(
            anchor,
            panelSize: panelSize,
            visibleFrame: visibleFrame
        )

        switch edge {
        case .left:
            return NSPoint(x: screenFrame.minX - panelSize.width + visibleStrip, y: clamped)
        case .right:
            return NSPoint(x: screenFrame.maxX - visibleStrip, y: clamped)
        }
    }

    static func revealedOrigin(
        edge: DockEdge,
        panelSize: NSSize,
        visibleFrame: NSRect,
        anchor: CGFloat,
        inset: CGFloat
    ) -> NSPoint {
        let clamped = clampedAnchor(
            anchor,
            panelSize: panelSize,
            visibleFrame: visibleFrame
        )

        switch edge {
        case .left:
            return NSPoint(x: visibleFrame.minX + inset, y: clamped)
        case .right:
            return NSPoint(x: visibleFrame.maxX - panelSize.width - inset, y: clamped)
        }
    }

    private static func clampedAnchor(
        _ anchor: CGFloat,
        panelSize: NSSize,
        visibleFrame: NSRect
    ) -> CGFloat {
        min(
            max(anchor, visibleFrame.minY),
            max(visibleFrame.minY, visibleFrame.maxY - panelSize.height)
        )
    }
}
