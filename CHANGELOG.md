# Changelog

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
