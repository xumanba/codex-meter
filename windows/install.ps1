[CmdletBinding()]
param(
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$distDirectory = Join-Path $PSScriptRoot "dist"
$applicationSource = Join-Path $distDirectory "CodexMeter.exe"

if (-not (Test-Path -LiteralPath $applicationSource)) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\CodexMeter"
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
$installFiles = @(
    "CodexMeter.exe",
    "CodexMeter.exe.config",
    "LICENSE.txt",
    "NOTICE.txt",
    "CodexBar-LICENSE.txt"
)
foreach ($fileName in $installFiles) {
    $sourcePath = Join-Path $distDirectory $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required install file was not built: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination $installDirectory -Force
}

$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDirectory "Codex Meter.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory "CodexMeter.exe"
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = "Codex usage floating meter"
$shortcut.Save()

Write-Host "INSTALL_OK"
Write-Host "Install directory: $installDirectory"
Write-Host "Start menu shortcut: $shortcutPath"
Write-Host "No startup entry was created."

if ($Launch) {
    Start-Process -FilePath (Join-Path $installDirectory "CodexMeter.exe")
}
