#!/bin/zsh
set -euo pipefail

project_dir="${0:A:h}"
app_name="CodexMeter.app"
legacy_app_name="Codex Meter.app"
source_app="$project_dir/.build/$app_name"
target_app="/Applications/$app_name"
legacy_app="/Applications/$legacy_app_name"
agent_label="com.local.codexbar-floating-meter"
agent_path="$HOME/Library/LaunchAgents/$agent_label.plist"

"$project_dir/build-app.sh"
/bin/launchctl bootout "gui/$(id -u)/$agent_label" 2>/dev/null || true
/usr/bin/pkill -f "$target_app/Contents/MacOS/CodexBarFloatingMeter" 2>/dev/null || true
/usr/bin/pkill -f "$target_app/Contents/Helpers/codexbar serve --port 18747" 2>/dev/null || true
/usr/bin/pkill -f "$legacy_app/Contents/MacOS/CodexBarFloatingMeter" 2>/dev/null || true
/usr/bin/pkill -f "$legacy_app/Contents/Helpers/codexbar serve --port 18747" 2>/dev/null || true

if [[ -e "$legacy_app" ]]; then
    trash_suffix="$(date +%Y%m%d-%H%M%S)"
    /bin/mv "$legacy_app" "$HOME/.Trash/Codex Meter-$trash_suffix.app"
fi

/usr/bin/ditto "$source_app" "$target_app"

mkdir -p "$HOME/Library/LaunchAgents"
{
    echo '<?xml version="1.0" encoding="UTF-8"?>'
    echo '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">'
    echo '<plist version="1.0"><dict>'
    echo '<key>Label</key><string>com.local.codexbar-floating-meter</string>'
    echo '<key>ProgramArguments</key><array>'
    echo '<string>/usr/bin/open</string><string>-g</string>'
    echo '<string>/Applications/CodexMeter.app</string>'
    echo '</array>'
    echo '<key>RunAtLoad</key><true/>'
    echo '</dict></plist>'
} > "$agent_path"

/usr/bin/plutil -lint "$agent_path"
/bin/launchctl bootstrap "gui/$(id -u)" "$agent_path"

echo "Installed and launched: $target_app"
