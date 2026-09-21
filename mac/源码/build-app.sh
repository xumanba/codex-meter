#!/bin/zsh
set -euo pipefail

project_dir="${0:A:h}"
cd "$project_dir"
if [[ -n "${ARCHES:-}" ]]; then
    build_arches=(${=ARCHES})
else
    build_arches=("$(uname -m)")
fi

built_binaries=()
for arch in "${build_arches[@]}"; do
    case "$arch" in
        arm64|x86_64) ;;
        *)
            echo "Unsupported architecture: $arch" >&2
            exit 1
            ;;
    esac

    scratch_path=".build/swift-$arch"
    swift build -c release --arch "$arch" --scratch-path "$scratch_path"
    built_binaries+=("$scratch_path/$arch-apple-macosx/release/CodexBarFloatingMeter")
done

app_dir=".build/CodexMeter.app"
if [[ -e "$app_dir" ]]; then
    /bin/rm -rf "$app_dir"
fi
mkdir -p "$app_dir/Contents/MacOS"
mkdir -p "$app_dir/Contents/Helpers"
mkdir -p "$app_dir/Contents/Resources/ThirdPartyLicenses"

if (( ${#built_binaries[@]} == 1 )); then
    cp "${built_binaries[1]}" "$app_dir/Contents/MacOS/CodexBarFloatingMeter"
else
    /usr/bin/lipo -create "${built_binaries[@]}" \
        -output "$app_dir/Contents/MacOS/CodexBarFloatingMeter"
fi

"$project_dir/Scripts/fetch-codexbar-cli.sh" \
    "$app_dir/Contents/Helpers/codexbar" \
    "${build_arches[@]}"

cp "Info.plist" "$app_dir/Contents/"
cp "$project_dir/assets/CodexMeter.icns" "$app_dir/Contents/Resources/CodexMeter.icns"
"$project_dir/Scripts/fetch-third-party-licenses.sh" \
    "$app_dir/Contents/Resources/ThirdPartyLicenses"

codesign --force --sign - "$app_dir/Contents/Helpers/codexbar"
codesign --force --sign - "$app_dir/Contents/MacOS/CodexBarFloatingMeter"
codesign --force --deep --sign - "$app_dir"
codesign --verify --deep --strict "$app_dir"

echo "$PWD/$app_dir"
