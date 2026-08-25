[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "0.1.1"
)

$ErrorActionPreference = "Stop"
$windowsDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $windowsDirectory
$distDirectory = Join-Path $windowsDirectory "dist"
$archiveName = "Codex-Meter-Windows-portable-v$Version.zip"
$archivePath = Join-Path $windowsDirectory $archiveName
$checksumPath = "$archivePath.sha256"
$packageRootName = "CodexMeter Windows v$Version"
$stageDirectory = Join-Path $windowsDirectory "release-stage-v$Version"
$packageDirectory = Join-Path $stageDirectory $packageRootName

if ((Test-Path -LiteralPath $archivePath) -or (Test-Path -LiteralPath $checksumPath)) {
    throw "Release archive or checksum already exists and will not be overwritten: $archivePath"
}
if (Test-Path -LiteralPath $stageDirectory) {
    throw "Release staging directory already exists and will not be overwritten: $stageDirectory"
}

& (Join-Path $windowsDirectory "build.ps1")

$testPath = Join-Path $distDirectory "CodexMeter.Tests.exe"
& $testPath
if ($LASTEXITCODE -ne 0) {
    throw "CodexMeter.Tests.exe failed with exit code $LASTEXITCODE"
}

$applicationPath = Join-Path $distDirectory "CodexMeter.exe"
$application = Get-Item -LiteralPath $applicationPath
$expectedFileVersion = "$Version.0"
if ($application.VersionInfo.FileVersion -ne $expectedFileVersion) {
    throw "Executable version $($application.VersionInfo.FileVersion) does not match $expectedFileVersion"
}

$packageFiles = @{
    "CodexMeter.exe" = $applicationPath
    "CodexMeter.exe.config" = Join-Path $distDirectory "CodexMeter.exe.config"
    "LICENSE.txt" = Join-Path $distDirectory "LICENSE.txt"
    "NOTICE.txt" = Join-Path $distDirectory "NOTICE.txt"
    "README-Windows.txt" = Join-Path $windowsDirectory "README-Windows.txt"
    "install.ps1" = Join-Path $windowsDirectory "install.ps1"
    "uninstall.ps1" = Join-Path $windowsDirectory "uninstall.ps1"
}

New-Item -ItemType Directory -Path $packageDirectory | Out-Null
foreach ($entry in $packageFiles.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value)) {
        throw "Required package file was not found: $($entry.Value)"
    }
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $packageDirectory $entry.Key)
}

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archivePath -CompressionLevel Optimal

$expectedEntries = @(
    "$packageRootName/CodexMeter.exe",
    "$packageRootName/CodexMeter.exe.config",
    "$packageRootName/LICENSE.txt",
    "$packageRootName/NOTICE.txt",
    "$packageRootName/README-Windows.txt",
    "$packageRootName/install.ps1",
    "$packageRootName/uninstall.ps1"
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entryNames = @($archive.Entries |
        Where-Object { -not [String]::IsNullOrEmpty($_.Name) } |
        ForEach-Object { $_.FullName.Replace('\', '/') } |
        Sort-Object)
} finally {
    $archive.Dispose()
}
if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $entryNames) {
    throw "Release archive contents do not match the expected portable package."
}

$archiveHash = Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
$executableHash = Get-FileHash -Algorithm SHA256 -LiteralPath $applicationPath
Set-Content -LiteralPath $checksumPath -Encoding Ascii -Value "$($archiveHash.Hash.ToLowerInvariant())  $archiveName"

Write-Host "PACKAGE_OK"
Write-Host "Archive: $archivePath"
Write-Host "Checksum: $checksumPath"
Write-Host "Package root: $packageRootName"
Write-Host "Archive SHA256: $($archiveHash.Hash)"
Write-Host "Executable SHA256: $($executableHash.Hash)"
