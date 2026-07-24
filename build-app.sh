#!/bin/zsh
set -euo pipefail

cd "${0:A:h}"
swift build -c release

app_dir=".build/Codex Meter.app"
mkdir -p "$app_dir/Contents/MacOS"
cp ".build/release/CodexBarFloatingMeter" "$app_dir/Contents/MacOS/"
cp "Info.plist" "$app_dir/Contents/"
codesign --force --sign - "$app_dir"

echo "$PWD/$app_dir"
