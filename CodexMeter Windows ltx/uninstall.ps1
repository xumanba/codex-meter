[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = "Stop"
$installDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "Programs\CodexMeter")).TrimEnd('\')
$installedApplication = Join-Path $installDirectory "CodexMeter.exe"
$expectedDirectory = [System.IO.Path]::GetFullPath(
    "$env:LOCALAPPDATA\Programs\CodexMeter").TrimEnd('\')
if (-not [String]::Equals($installDirectory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to uninstall from an unexpected directory: $installDirectory"
}

$running = @(Get-Process -Name "CodexMeter" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "CodexMeter is still running. Exit it from the tray menu, then run this script again."
}

$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CodexMeter.lnk"
$legacyStartMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Codex Meter.lnk"
$startupShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\Codex Meter.lnk"
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "CodexMeter"
$settingsDirectory = Join-Path $env:LOCALAPPDATA "CodexMeter"

$currentDirectory = [System.IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')
if ([String]::Equals($currentDirectory, $installDirectory, [StringComparison]::OrdinalIgnoreCase) -or
    $currentDirectory.StartsWith($installDirectory + "\", [StringComparison]::OrdinalIgnoreCase)) {
    Set-Location -LiteralPath (Split-Path -Parent $installDirectory)
}

foreach ($shortcut in @($startMenuShortcut, $legacyStartMenuShortcut, $startupShortcut)) {
    if ((Test-Path -LiteralPath $shortcut) -and
        $PSCmdlet.ShouldProcess($shortcut, "Remove CodexMeter shortcut")) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}

$runValue = $null
try {
    $runValue = (Get-ItemProperty -LiteralPath $runKeyPath -Name $runValueName `
        -ErrorAction Stop).$runValueName
} catch [System.Management.Automation.ItemNotFoundException] {
} catch [System.Management.Automation.PSArgumentException] {
}
$runTarget = $null
if ($runValue -match '^\s*"([^"]+)"') {
    $runTarget = $Matches[1]
} elseif ($runValue -match '^\s*(.+?\.exe)(?:\s|$)') {
    $runTarget = $Matches[1]
}
if ($runTarget -and [String]::Equals(
    [System.IO.Path]::GetFullPath($runTarget),
    $installedApplication,
    [StringComparison]::OrdinalIgnoreCase) -and
    $PSCmdlet.ShouldProcess("HKCU\Software\Microsoft\Windows\CurrentVersion\Run\$runValueName",
        "Remove CodexMeter startup entry")) {
    Remove-ItemProperty -LiteralPath $runKeyPath -Name $runValueName
}

if ((Test-Path -LiteralPath $installDirectory) -and
    $PSCmdlet.ShouldProcess($installDirectory, "Remove CodexMeter application files")) {
    Remove-Item -LiteralPath $installDirectory -Recurse -Force
}

if ($RemoveSettings -and (Test-Path -LiteralPath $settingsDirectory) -and
    $PSCmdlet.ShouldProcess($settingsDirectory, "Remove CodexMeter interface settings")) {
    Remove-Item -LiteralPath $settingsDirectory -Recurse -Force
}

Write-Host "UNINSTALL_OK"
if (-not $RemoveSettings) {
    Write-Host "Settings were kept at: $settingsDirectory"
}
