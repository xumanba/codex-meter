#!/bin/zsh
set -euo pipefail

agent_label="com.local.codexbar-floating-meter"
agent_path="$HOME/Library/LaunchAgents/$agent_label.plist"

/bin/launchctl bootout "gui/$(id -u)/$agent_label" 2>/dev/null || true
/usr/bin/pkill -f '/Applications/Codex Meter.app/Contents/MacOS/CodexBarFloatingMeter' 2>/dev/null || true
/usr/bin/pkill -f 'codexbar serve --port 18747' 2>/dev/null || true

trash_suffix="$(date +%Y%m%d-%H%M%S)"
if [[ -e "/Applications/Codex Meter.app" ]]; then
    /bin/mv "/Applications/Codex Meter.app" "$HOME/.Trash/Codex Meter-$trash_suffix.app"
fi
if [[ -e "$agent_path" ]]; then
    /bin/mv "$agent_path" "$HOME/.Trash/$agent_label-$trash_suffix.plist"
fi

echo "Codex Meter was moved to Trash."
