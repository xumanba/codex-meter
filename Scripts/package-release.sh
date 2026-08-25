#!/bin/zsh
set -euo pipefail

project_dir="${0:A:h:h}"
release_dir="$project_dir/.build/release"
app_path="$project_dir/.build/CodexMeter.app"
archive_name="CodexMeter-macos-universal-0.2.0.zip"
archive_path="$release_dir/$archive_name"
dmg_name="CodexMeter-macos-universal-0.2.0.dmg"
dmg_path="$release_dir/$dmg_name"

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
if [[ -e "$dmg_path" ]]; then
    mv "$dmg_path" "$dmg_path.previous-$timestamp"
fi
if [[ -e "$dmg_path.sha256" ]]; then
    mv "$dmg_path.sha256" "$dmg_path.sha256.previous-$timestamp"
fi

/usr/bin/ditto -c -k --sequesterRsrc --keepParent "$app_path" "$archive_path"

dmg_staging_dir="$(mktemp -d)"
verify_dir="$(mktemp -d)"
dmg_mount_dir="$(mktemp -d)"
cleanup() {
    /usr/bin/hdiutil detach "$dmg_mount_dir" >/dev/null 2>&1 || true
    /bin/rm -rf "$dmg_staging_dir" "$verify_dir" "$dmg_mount_dir"
}
trap cleanup EXIT

/usr/bin/ditto "$app_path" "$dmg_staging_dir/CodexMeter.app"
/bin/ln -s /Applications "$dmg_staging_dir/Applications"
/usr/bin/hdiutil create \
    -quiet \
    -volname "CodexMeter 0.2.0" \
    -srcfolder "$dmg_staging_dir" \
    -ov \
    -format UDZO \
    "$dmg_path"

(
    cd "$release_dir"
    shasum -a 256 "$archive_name" > "$archive_name.sha256"
    shasum -a 256 "$dmg_name" > "$dmg_name.sha256"
)

/usr/bin/ditto -x -k "$archive_path" "$verify_dir"

verified_app="$verify_dir/CodexMeter.app"
codesign --verify --deep --strict "$verified_app"

test -f "$verified_app/Contents/Resources/CodexMeter.icns"

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

/usr/bin/hdiutil attach \
    -nobrowse \
    -readonly \
    -mountpoint "$dmg_mount_dir" \
    "$dmg_path" >/dev/null
test -d "$dmg_mount_dir/CodexMeter.app"
test -L "$dmg_mount_dir/Applications"
codesign --verify --deep --strict "$dmg_mount_dir/CodexMeter.app"
/usr/bin/hdiutil detach "$dmg_mount_dir" >/dev/null

echo "$archive_path"
echo "$archive_path.sha256"
echo "$dmg_path"
echo "$dmg_path.sha256"
