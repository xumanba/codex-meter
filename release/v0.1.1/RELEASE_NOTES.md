# Codex Meter v0.1.1

This is a Windows quality update. The existing macOS v0.1.0 application,
features and installation package are unchanged.

## Windows v0.1.1

- Launching the executable again restores the existing card from the tray or edge shelf.
- “Always on top” can be turned off independently of fixed/follow display mode.
- F5 refreshes immediately; Esc minimizes to the notification area.
- Provider errors are sanitized before display, including errors embedded in JSON responses.
- Network speed is explicitly documented as aggregate system traffic, not Codex-only traffic.
- Quota depletion is labelled as a cumulative-average estimate and withheld during an immature trend.
- The portable ZIP includes optional per-user install and confirm-before-remove uninstall scripts.
- `-StartWithWindows` is opt-in; the portable executable still creates no startup entry.
- Windows build and self-tests run in GitHub Actions.
- Unused Spark windows show the provider's absolute reset date, matching the official usage page even when the timestamp moves forward on refresh.
- Fixed reset countdowns no longer truncate almost a full hour; hover the reset text to see the exact local date and time.

## Packages

- Windows: `Codex-Meter-Windows-portable-v0.1.1.zip`
- macOS: keep using the unchanged `Codex-Meter-macos-universal-0.1.0.zip`

The Windows binary is not Authenticode-signed. The macOS v0.1.0 package remains
ad-hoc signed and not Apple-notarized.
