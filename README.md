<div align="center">

# ✦ Codex Meter

### A native floating usage meter for macOS, powered by CodexBar

[![macOS 14+](https://img.shields.io/badge/macOS-14%2B-111827?style=flat-square&logo=apple&logoColor=white)](https://www.apple.com/macos/)
[![Swift 6](https://img.shields.io/badge/Swift-6-F05138?style=flat-square&logo=swift&logoColor=white)](https://www.swift.org/)
[![CodexBar](https://img.shields.io/badge/Powered%20by-CodexBar-0A84FF?style=flat-square)](https://github.com/steipete/CodexBar)
[![License: MIT](https://img.shields.io/badge/License-MIT-34C759?style=flat-square)](LICENSE)

Keep your Codex weekly allowance, pacing and reset time visible without leaving
your workspace.

</div>

## Glass themes

<table>
  <tr>
    <th align="center">Dark glass</th>
    <th align="center">Light glass</th>
  </tr>
  <tr>
    <td><img src="assets/dark-glass.jpg" alt="Codex Meter dark glass theme"></td>
    <td><img src="assets/light-glass.jpg" alt="Codex Meter light glass theme"></td>
  </tr>
</table>

Both themes are designed for legibility and use native macOS materials,
typography, rounded geometry and system-inspired colors. Your last theme
selection is restored automatically.

## Highlights

- **Always visible** — floats above normal windows and follows every Space.
- **Remaining allowance** — shows what is left, rather than what has been used.
- **Pace awareness** — displays over-pace percentage, expected depletion time
  and a red pacing marker.
- **Codex Spark support** — automatically shows extra Codex rate windows.
- **Two native glass themes** — switch between dark and light from the menu.
- **Position memory** — drag the card anywhere; its fixed position is restored.
- **Optional Codex following** — show only while Codex is the foreground app.
- **Private by design** — reads only a localhost endpoint and never displays or
  stores account credentials.
- **One-minute refresh** — updates every 60 seconds, with an instant manual
  refresh action.

## Requirements

- macOS 14 Sonoma or newer
- Apple Swift 6 toolchain (Xcode Command Line Tools or Xcode)
- [CodexBar](https://github.com/steipete/CodexBar) with its CLI installed
- Codex configured and authenticated in CodexBar

Install CodexBar first:

```bash
brew install --cask codexbar
```

Open CodexBar once and verify that Codex usage is available:

```bash
codexbar usage --provider codex --source oauth --format json
```

## Quick install

```bash
git clone https://github.com/xumanba/codex-meter.git
cd codex-meter
chmod +x install.sh uninstall.sh build-app.sh
./install.sh
```

The installer:

1. builds an ad-hoc signed native macOS application;
2. installs it as `/Applications/Codex Meter.app`;
3. creates a per-user LaunchAgent;
4. launches the meter in the background without hiding its window.

No administrator password is normally required when your user can write to
`/Applications`.

## Using the meter

Drag the card to place it anywhere. Open the `•••` menu to:

- choose **固定在桌面** to keep it pinned above normal windows;
- choose **跟随 Codex** to show it only when Codex is active;
- switch between **深色玻璃** and **浅色玻璃**;
- refresh immediately;
- quit the application.

The selected theme and fixed position are saved with macOS `UserDefaults`.

## How it works

```text
Codex / OpenAI account
          │
          ▼
      CodexBar CLI
          │  localhost JSON, port 18747
          ▼
  Codex Meter (SwiftUI + AppKit)
          │
          ├── remaining weekly allowance
          ├── pacing and depletion estimate
          ├── additional rate windows
          └── native floating NSPanel
```

Codex Meter starts `codexbar serve` on `127.0.0.1:18747` when needed and polls
the local endpoint every 60 seconds. The server is not exposed to your network.

## Build manually

```bash
chmod +x build-app.sh
./build-app.sh
open ".build/Codex Meter.app"
```

The project intentionally uses Swift Package Manager and AppKit/SwiftUI only.
There are no third-party runtime dependencies beyond CodexBar.

## Uninstall

```bash
./uninstall.sh
```

The application and LaunchAgent are moved to Trash. CodexBar and its settings
are left untouched.

## Relationship to CodexBar

This project is an **independent, unofficial companion** built on the local
interface provided by [steipete/CodexBar](https://github.com/steipete/CodexBar).
CodexBar is copyright Peter Steinberger and is distributed under the
[MIT License](https://github.com/steipete/CodexBar/blob/main/LICENSE).

Codex Meter does not copy or redistribute CodexBar source code, binaries, icons
or branding. It requires users to install CodexBar separately. This repository
is not affiliated with or endorsed by CodexBar, OpenAI or Apple. See
[NOTICE](NOTICE) for attribution details.

## Security and privacy

- All usage requests stay on `127.0.0.1`.
- No passwords, OAuth tokens, cookies or account emails are stored by this app.
- Screenshots in this repository contain usage percentages only.
- The app is ad-hoc signed when built locally. Review the small Swift codebase
  before installation if you require additional assurance.

## License

Codex Meter is available under the [MIT License](LICENSE).

---

<div align="center">
Built for people who want their remaining context visible at a glance.
</div>
