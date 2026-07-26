[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$windowsDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $windowsDirectory
$distDirectory = Join-Path $windowsDirectory "dist"
$archiveName = "CodexMeter-Windows-portable-v$Version.zip"
$archivePath = Join-Path $windowsDirectory $archiveName
$checksumPath = "$archivePath.sha256"

if ((Test-Path -LiteralPath $archivePath) -or (Test-Path -LiteralPath $checksumPath)) {
    throw "Release archive or checksum already exists and will not be overwritten: $archivePath"
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

$packageFiles = @(
    $applicationPath,
    (Join-Path $distDirectory "CodexMeter.exe.config"),
    (Join-Path $distDirectory "LICENSE.txt"),
    (Join-Path $distDirectory "NOTICE.txt"),
    (Join-Path $distDirectory "CodexBar-LICENSE.txt")
)

Compress-Archive -LiteralPath $packageFiles -DestinationPath $archivePath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
} finally {
    $archive.Dispose()
}

$expectedEntries = @(
    "CodexBar-LICENSE.txt",
    "CodexMeter.exe",
    "CodexMeter.exe.config",
    "LICENSE.txt",
    "NOTICE.txt"
)
if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $entryNames) {
    throw "Release archive contents do not match the expected portable package."
}

$archiveHash = Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
$executableHash = Get-FileHash -Algorithm SHA256 -LiteralPath $applicationPath
Set-Content -LiteralPath $checksumPath -Encoding Ascii -Value "$($archiveHash.Hash.ToLowerInvariant())  $archiveName"

Write-Host "PACKAGE_OK"
Write-Host "Archive: $archivePath"
Write-Host "Checksum: $checksumPath"
Write-Host "Archive SHA256: $($archiveHash.Hash)"
Write-Host "Executable SHA256: $($executableHash.Hash)"
