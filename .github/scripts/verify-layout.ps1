[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$required = @(
    'README.md', 'LICENSE', '.gitignore', '.github/workflows/windows.yml',
    '.github/workflows/layout.yml', '.github/docs/REPOSITORY-LAYOUT.md',
    'mac/README.md', 'mac/源码/README.md', 'mac/源码/Package.swift',
    'mac/源码/build-app.sh', 'mac/源码/install.sh', 'mac/源码/uninstall.sh',
    'mac/源码/Scripts/package-release.sh', 'mac/源码/assets/CodexMeter.icns',
    'mac/源码/NOTICE', 'win/README.md', 'win/源码/README.md',
    'win/源码/build.ps1', 'win/源码/package-release.ps1',
    'win/源码/src/Program.cs', 'win/源码/assets/CodexMeter.ico',
    'win/源码/vendor/codexbar-cli.exe', 'win/源码/NOTICE'
)
foreach ($version in @('v0.1.0', 'v0.1.1', 'v0.1.3', 'v0.1.4')) {
    $required += "win/版本/$version/README.md"
}
foreach ($version in @('v0.1.0', 'v0.2.0')) {
    $required += "mac/版本/$version/README.md"
}
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $path))) {
        throw "Missing required path: $path"
    }
}

$files = @(& git -C $repositoryRoot -c core.quotepath=false ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Unable to list repository files.' }
$files = @($files | Sort-Object -Unique)
$allowedRoot = @('README.md', 'LICENSE', '.gitignore', '.github', 'mac', 'win')
$linkCount = 0
$scriptCount = 0
foreach ($path in $files) {
    if ($allowedRoot -notcontains $path.Split('/')[0]) {
        throw "Unexpected tracked top-level path: $path"
    }
    if ($path -match '^(mac|win)/版本/.*\.(zip|dmg|exe)$') {
        throw "Release binaries belong in GitHub Releases, not version indexes: $path"
    }
    $fullPath = Join-Path $repositoryRoot $path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Indexed file missing on disk: $path"
    }
    if ($path.EndsWith('.ps1')) {
        $tokens = $null
        $parseErrors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($fullPath, [ref]$tokens, [ref]$parseErrors)
        if ($parseErrors.Count -gt 0) { throw "PowerShell syntax error in ${path}: $parseErrors" }
        $scriptCount++
    }
    if (-not $path.EndsWith('.md')) { continue }
    $content = [IO.File]::ReadAllText($fullPath)
    $content = [regex]::Replace($content, '(?ms)^```[^\r\n]*\r?\n.*?^```[^\r\n]*', '')
    $pattern = '\]\((?<target>[^\s)]+)(?:\s+"[^"]*")?\)|\bsrc="(?<target>[^"]+)"'
    foreach ($match in [regex]::Matches($content, $pattern)) {
        $target = $match.Groups['target'].Value.Trim('<', '>')
        if ($target -match '^(?:[a-z][a-z0-9+.-]*:|#|//)') { continue }
        $target = [Uri]::UnescapeDataString(($target -split '[#?]', 2)[0])
        if (-not $target) { continue }
        $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $fullPath) $target))
        $rootPrefix = $repositoryRoot + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Link escapes repository in ${path}: $target"
        }
        if (-not (Test-Path -LiteralPath $resolved)) { throw "Broken link in ${path}: $target" }
        $linkCount++
    }
}

$licenseHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Algorithm SHA256).Hash
foreach ($platform in @('mac', 'win')) {
    $copy = Join-Path $repositoryRoot "$platform/源码/LICENSE"
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $licenseHash) {
        throw "Platform LICENSE differs from repository LICENSE: $platform"
    }
}
foreach ($name in @('CodexBar-LICENSE.txt', 'Win-CodexBar-LICENSE.txt')) {
    $macHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot "mac/源码/ThirdPartyLicenses/$name") -Algorithm SHA256).Hash
    $winHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot "win/源码/ThirdPartyLicenses/$name") -Algorithm SHA256).Hash
    if ($macHash -ne $winHash) { throw "Third-party license mismatch: $name" }
}
Write-Host "LAYOUT_CHECK_OK: $($files.Count) files, $linkCount local links, $scriptCount PowerShell scripts"
