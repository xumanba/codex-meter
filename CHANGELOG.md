# Changelog

## v0.1.1 (Windows quality update)

- Preserves the macOS v0.1.0 source, application behavior and package unchanged.
- Restores an existing hidden or tray instance when the executable is launched again.
- Adds an independent always-on-top toggle, F5 refresh and Esc tray shortcut.
- Clarifies that the network tile is aggregate system traffic, not Codex-only traffic.
- Labels quota depletion output as a cumulative-average estimate and suppresses it while the trend is immature.
- Sanitizes provider JSON errors before they reach the interface.
- Adds package-ready install/uninstall scripts with opt-in per-user startup.
- Adds Windows build and self-test CI coverage.
- Shows an unused Spark rolling reset as the provider's absolute date, matching the official usage page instead of presenting it as a fixed countdown.
- Rounds long reset countdowns up, shows minutes below one day, and exposes the exact local reset time on hover.

## v0.1.0

First unified Windows and macOS release of Codex Meter.

### Shared features

- Remaining Codex allowance, reset timing, pacing and supported extra windows.
- Native floating glass card with dark and light themes.
- Left/right edge docking with automatic reveal and hide.
- No bundled account credentials.

### Windows

- Native WinForms/DWM client for Windows 10/11 with Per-Monitor V2 DPI support.
- Current aggregate download and upload speed from Windows byte counters.
- Clickable status pill for immediate full-data synchronization.
- Notification-area minimize and restore commands.
- Adaptive refresh, failure backoff, stale-data preservation and sanitized errors.
- External Win-CodexBar CLI integration with hard timeout and cancellation.

### macOS

- Existing SwiftUI/AppKit application and universal Apple silicon/Intel package retained unchanged.
- Bundled checksum-verified CodexBar CLI workflow retained.

### Distribution notes

- Windows network-speed and notification-area features are Windows-only.
- The Windows binary is not Authenticode-signed.
- The macOS application is ad-hoc signed and is not Apple-notarized.
