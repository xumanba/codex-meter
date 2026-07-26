#!/bin/zsh
set -euo pipefail

project_dir="${0:A:h:h}"
release_dir="$project_dir/.build/release"
app_path="$project_dir/.build/Codex Meter.app"
archive_name="Codex-Meter-macos-universal-0.1.0.zip"
archive_path="$release_dir/$archive_name"

cd "$project_dir"
ARCHES="arm64 x86_64" "$project_dir/build-app.sh"

mkdir -p "$release_dir"
timestamp="$(date +%s)"
if [[ -e "$archive_path" ]]; then
    mv "$archive_path" "$archive_path.previous-$timestamp"
fi
if [[ -e "$archive_path.sha256" ]]; then
    mv "$archive_path.sha256" "$archive_path.sha256.previous-$timestamp"
fi

/usr/bin/ditto -c -k --sequesterRsrc --keepParent "$app_path" "$archive_path"

(
    cd "$release_dir"
    shasum -a 256 "$archive_name" > "$archive_name.sha256"
)

verify_dir="$(mktemp -d)"
trap '/bin/rm -rf "$verify_dir"' EXIT
/usr/bin/ditto -x -k "$archive_path" "$verify_dir"

verified_app="$verify_dir/Codex Meter.app"
codesign --verify --deep --strict "$verified_app"

for binary in \
    "$verified_app/Contents/MacOS/CodexBarFloatingMeter" \
    "$verified_app/Contents/Helpers/codexbar"; do
    architectures=" $(/usr/bin/lipo -archs "$binary") "
    [[ "$architectures" == *" arm64 "* ]]
    [[ "$architectures" == *" x86_64 "* ]]
done

test -f \
    "$verified_app/Contents/Resources/ThirdPartyLicenses/CodexBar-LICENSE.txt"
license_count="$(
    find "$verified_app/Contents/Resources/ThirdPartyLicenses" \
        -type f | wc -l | tr -d ' '
)"
[[ "$license_count" == "9" ]]

echo "$archive_path"
echo "$archive_path.sha256"
