#!/bin/zsh
set -euo pipefail

codexbar_version="0.45.2"
output_path="${1:?Usage: fetch-codexbar-cli.sh OUTPUT_PATH [ARCH ...]}"
shift

if (( $# == 0 )); then
    requested_arches=("$(uname -m)")
else
    requested_arches=("$@")
fi

project_dir="${0:A:h:h}"
cache_dir="$project_dir/.build/codexbar-cli/$codexbar_version"
mkdir -p "$cache_dir"

source_binaries=()
for arch in "${requested_arches[@]}"; do
    case "$arch" in
        arm64|x86_64) ;;
        *)
            echo "Unsupported CodexBar CLI architecture: $arch" >&2
            exit 1
            ;;
    esac

    asset_name="CodexBarCLI-v$codexbar_version-macos-$arch.tar.gz"
    asset_url="https://github.com/steipete/CodexBar/releases/download/v$codexbar_version/$asset_name"
    archive_path="$cache_dir/$asset_name"
    checksum_path="$archive_path.sha256"

    if [[ ! -f "$archive_path" ]]; then
        curl --fail --location --silent --show-error --retry 2 \
            --output "$archive_path.download" "$asset_url"
        mv "$archive_path.download" "$archive_path"
    fi
    if [[ ! -f "$checksum_path" ]]; then
        curl --fail --location --silent --show-error --retry 2 \
            --output "$checksum_path.download" "$asset_url.sha256"
        mv "$checksum_path.download" "$checksum_path"
    fi

    expected_checksum="$(awk 'NR == 1 { print $1 }' "$checksum_path")"
    actual_checksum="$(shasum -a 256 "$archive_path" | awk '{ print $1 }')"
    if [[ "$actual_checksum" != "$expected_checksum" ]]; then
        echo "Checksum mismatch for $asset_name" >&2
        exit 1
    fi

    extracted_dir="$cache_dir/$arch"
    mkdir -p "$extracted_dir"
    tar -xzf "$archive_path" -C "$extracted_dir"
    source_binary="$extracted_dir/codexbar"

    if [[ ! -x "$source_binary" ]]; then
        echo "CodexBar CLI binary is missing from $asset_name" >&2
        exit 1
    fi
    if [[ " $(/usr/bin/lipo -archs "$source_binary") " != *" $arch "* ]]; then
        echo "CodexBar CLI architecture check failed for $arch" >&2
        exit 1
    fi

    source_binaries+=("$source_binary")
done

mkdir -p "${output_path:h}"
if (( ${#source_binaries[@]} == 1 )); then
    cp "${source_binaries[1]}" "$output_path"
else
    /usr/bin/lipo -create "${source_binaries[@]}" -output "$output_path"
fi
chmod +x "$output_path"

for arch in "${requested_arches[@]}"; do
    if [[ " $(/usr/bin/lipo -archs "$output_path") " != *" $arch "* ]]; then
        echo "Combined CodexBar CLI is missing architecture: $arch" >&2
        exit 1
    fi
done

"$output_path" --version
