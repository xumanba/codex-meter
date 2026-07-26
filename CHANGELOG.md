# Changelog

## v1.0.0

Codex Meter now supports Windows 10/11 and macOS 14+ from one repository.

### Windows

- Native WinForms/DWM floating card with dark and light glass themes.
- Weekly allowance, reset time, pacing and Codex Spark visualization.
- Clickable status pill for immediate full-data synchronization.
- Adaptive refresh, failure backoff, stale-data preservation and sanitized errors.
- Left/right edge auto-hide with Per-Monitor V2 DPI support.
- Notification-area mode with explicit minimize and restore commands.
- Hard CLI timeout and cancellation without storing account credentials.
- Reproducible portable ZIP packaging with tests and SHA-256 output.

### macOS

- Version metadata and universal release package name updated to v1.0.0.
- Existing SwiftUI/AppKit meter, edge shelf and bundled CodexBar workflow retained.

### Distribution notes

- The Windows binary is not Authenticode-signed.
- The macOS application is ad-hoc signed and is not Apple-notarized.
