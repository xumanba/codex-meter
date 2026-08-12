# Changelog

## v0.2.0 (unified main)

- Brings the complete macOS v0.2.0 source, edge docking and Windows v0.1.1
  implementation onto the `main` branch.
- Adds current-quota daily token usage and separate model, reasoning-effort and
  Fast-mode preference statistics.
- Resets model preference totals when the weekly quota reset is detected and
  recounts from the refresh point instead of carrying totals across periods.
- Uses the CodexMeter app icon in the floating card header.
- Keeps the Chinese README as the primary documentation and links the English
  README.

## v0.1.1 (Windows quality update)

- Preserves the macOS v0.1.0 source, application behavior and package unchanged.
- Restores an existing hidden or tray instance when the executable is launched again.
- Adds an independent always-on-top toggle, F5 refresh and Esc tray shortcut.
- Clarifies that the network tile is aggregate system traffic, not Codex-only traffic.
- Labels quota depletion output as a cumulative-average estimate and suppresses it while the trend is immature.
- Sanitizes provider JSON errors before they reach the interface.
- Adds package-ready install/uninstall scripts with opt-in per-user startup.
- Adds Windows build and self-test CI coverage.
- Uses the same compact `xd xh` reset countdown for weekly and Spark allowances.
- Rounds long reset countdowns up, shows minutes below one day, and exposes the exact local reset time on hover.
- Aligns weekly and Spark reset countdowns to one fixed column and moves the latest update time into the status-pill tooltip.
- Replaces the Windows system tooltip for update time with a rounded in-card prompt that reuses the active allowance-bar palette.
- Treats an old provider timestamp as stale even when the local CLI invocation succeeds, preventing cached data from being shown as live.
- Stops Windows upgrades with a clear instruction when the installed app is still running.
- Extends Windows CI with status/budget hover rendering and portable-package validation.

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
