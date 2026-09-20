// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "CodexBarFloatingMeter",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "CodexBarFloatingMeter", targets: ["CodexBarFloatingMeter"])
    ],
    targets: [
        .executableTarget(
            name: "CodexBarFloatingMeter",
            path: "Sources"
        )
    ]
)
