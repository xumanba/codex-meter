[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = "Stop"
$installDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "Programs\CodexMeter")).TrimEnd('\')
$expectedDirectory = [System.IO.Path]::GetFullPath(
    "$env:LOCALAPPDATA\Programs\CodexMeter").TrimEnd('\')
if (-not [String]::Equals($installDirectory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to uninstall from an unexpected directory: $installDirectory"
}

$running = @(Get-Process -Name "CodexMeter" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "Codex Meter is still running. Exit it from the tray menu, then run this script again."
}

$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Codex Meter.lnk"
$startupShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\Codex Meter.lnk"
$settingsDirectory = Join-Path $env:LOCALAPPDATA "CodexMeter"

$currentDirectory = [System.IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')
if ([String]::Equals($currentDirectory, $installDirectory, [StringComparison]::OrdinalIgnoreCase) -or
    $currentDirectory.StartsWith($installDirectory + "\", [StringComparison]::OrdinalIgnoreCase)) {
    Set-Location -LiteralPath (Split-Path -Parent $installDirectory)
}

foreach ($shortcut in @($startMenuShortcut, $startupShortcut)) {
    if ((Test-Path -LiteralPath $shortcut) -and
        $PSCmdlet.ShouldProcess($shortcut, "Remove Codex Meter shortcut")) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}

if ((Test-Path -LiteralPath $installDirectory) -and
    $PSCmdlet.ShouldProcess($installDirectory, "Remove Codex Meter application files")) {
    Remove-Item -LiteralPath $installDirectory -Recurse -Force
}

if ($RemoveSettings -and (Test-Path -LiteralPath $settingsDirectory) -and
    $PSCmdlet.ShouldProcess($settingsDirectory, "Remove Codex Meter interface settings")) {
    Remove-Item -LiteralPath $settingsDirectory -Recurse -Force
}

Write-Host "UNINSTALL_OK"
if (-not $RemoveSettings) {
    Write-Host "Settings were kept at: $settingsDirectory"
}
