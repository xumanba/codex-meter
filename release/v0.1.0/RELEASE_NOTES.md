# Codex Meter v0.1.0 — Windows & macOS

Codex Meter v0.1.0 is the first unified stable release for Windows and macOS.
Choose the ZIP for your operating system; the packages are not interchangeable.

## 下载 / Downloads

| System | Download | Installation |
|---|---|---|
| Windows 10/11 | `Codex-Meter-Windows-portable-v0.1.0.zip` | Extract, open `Codex Meter Windows v0.1.0`, and run `CodexMeter.exe`. Install and sign in to Win-CodexBar first. |
| macOS 14+ | `Codex-Meter-macos-universal-0.1.0.zip` | Extract, then move **Codex Meter.app** to Applications. Universal Apple silicon and Intel package. |

## 共同功能 / Shared features

- Codex 剩余额度、重置时间、节奏与附加窗口 / Remaining allowance, reset timing, pacing and supported extra windows
- 浅色和深色原生玻璃主题 / Native light and dark glass themes
- 左右贴边隐藏和触边唤出 / Left/right edge docking, hide and reveal
- 不保存账号凭据 / No stored account credentials

## 平台差异 / Platform differences

- **Windows only:** 实时上传/下载网速；最小化到系统托盘 / live aggregate upload/download speed; notification-area minimize and restore
- **Windows:** requires an existing signed-in [Win-CodexBar](https://github.com/Finesssee/Win-CodexBar) installation
- **macOS:** the existing v0.1.0 application package and bundled CodexBar workflow are unchanged

## 首次运行 / First run

- Windows 程序未进行 Authenticode 签名，可能显示未知发布者提示。
- macOS 程序为 ad-hoc 签名且未经过 Apple 公证；首次运行可按住 Control 点击应用并选择“打开”。
- The Windows binary is not Authenticode-signed. The Mac app is ad-hoc signed and not Apple-notarized.

Verify both ZIP files with `SHA256SUMS-v0.1.0.txt` or their individual `.sha256` files.

Codex Meter is an independent, unofficial application powered by CodexBar. It is not affiliated with or endorsed by CodexBar, OpenAI, Apple, or Microsoft.
