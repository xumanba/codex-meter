#!/bin/zsh
set -euo pipefail

destination="${1:?Usage: fetch-third-party-licenses.sh DESTINATION}"
project_dir="${0:A:h:h}"
cache_dir="$project_dir/.build/third-party-licenses"
mkdir -p "$destination" "$cache_dir"

cp "$project_dir/ThirdPartyLicenses/CodexBar-LICENSE.txt" "$destination/"

license_names=(
    "Commander-LICENSE.txt"
    "SweetCookieKit-LICENSE.txt"
    "SwiftCrypto-LICENSE.txt"
    "SwiftCrypto-NOTICE.txt"
    "SwiftLog-LICENSE.txt"
    "SwiftLog-NOTICE.txt"
    "SwiftASN1-LICENSE.txt"
    "SwiftASN1-NOTICE.txt"
)
license_urls=(
    "https://raw.githubusercontent.com/steipete/Commander/v0.2.2/LICENSE"
    "https://raw.githubusercontent.com/steipete/SweetCookieKit/0.4.1/LICENSE"
    "https://raw.githubusercontent.com/apple/swift-crypto/3.15.1/LICENSE.txt"
    "https://raw.githubusercontent.com/apple/swift-crypto/3.15.1/NOTICE.txt"
    "https://raw.githubusercontent.com/apple/swift-log/1.13.2/LICENSE.txt"
    "https://raw.githubusercontent.com/apple/swift-log/1.13.2/NOTICE.txt"
    "https://raw.githubusercontent.com/apple/swift-asn1/1.7.1/LICENSE.txt"
    "https://raw.githubusercontent.com/apple/swift-asn1/1.7.1/NOTICE.txt"
)
license_sha256=(
    "14293556b79940745123d0160c71d27ed0e9fe9b8a848093f3ed78f4853caafe"
    "14293556b79940745123d0160c71d27ed0e9fe9b8a848093f3ed78f4853caafe"
    "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30"
    "b3ddc2ae068e76b3beb71be03c0400f90090f9469aa491bf7b1ac42320af37b8"
    "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30"
    "879b241d49b407215a0ad8e1c6a71c358d7b29591b090cf379d37a5f50cff918"
    "8c6db340475136df3c1201d458fa5755698eace76e510471ecc9d857d6083dac"
    "11dd3b3b783e6ec26098dd38ebc962986ea109b85447e28e62867b83bd0f8c5b"
)

for index in {1..${#license_names[@]}}; do
    name="${license_names[$index]}"
    url="${license_urls[$index]}"
    expected_checksum="${license_sha256[$index]}"
    cache_path="$cache_dir/$name"

    if [[ ! -f "$cache_path" ]]; then
        curl --fail --location --silent --show-error --retry 2 \
            --output "$cache_path.download" "$url"
        mv "$cache_path.download" "$cache_path"
    fi

    actual_checksum="$(shasum -a 256 "$cache_path" | awk '{ print $1 }')"
    if [[ "$actual_checksum" != "$expected_checksum" ]]; then
        echo "Checksum mismatch for third-party notice: $name" >&2
        exit 1
    fi

    cp "$cache_path" "$destination/$name"
done
