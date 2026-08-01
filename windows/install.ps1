[CmdletBinding()]
param(
    [switch]$Launch,
    [switch]$StartWithWindows
)

$ErrorActionPreference = "Stop"
$distDirectory = Join-Path $PSScriptRoot "dist"
$portableApplication = Join-Path $PSScriptRoot "CodexMeter.exe"
$builtApplication = Join-Path $distDirectory "CodexMeter.exe"

if (Test-Path -LiteralPath $portableApplication) {
    $sourceDirectory = $PSScriptRoot
} elseif (-not (Test-Path -LiteralPath $builtApplication)) {
    & (Join-Path $PSScriptRoot "build.ps1")
    $sourceDirectory = $distDirectory
} else {
    $sourceDirectory = $distDirectory
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\CodexMeter"
$installedApplication = Join-Path $installDirectory "CodexMeter.exe"

if (Test-Path -LiteralPath $installedApplication) {
    $installedFullPath = [System.IO.Path]::GetFullPath($installedApplication)
    $runningInstalledProcesses = @(Get-Process -Name "CodexMeter" -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                $processPath = $_.Path
                $processPath -and [String]::Equals(
                    [System.IO.Path]::GetFullPath($processPath),
                    $installedFullPath,
                    [StringComparison]::OrdinalIgnoreCase)
            } catch {
                $false
            }
        })
    if ($runningInstalledProcesses.Count -gt 0) {
        $processIds = ($runningInstalledProcesses | ForEach-Object { $_.Id }) -join ", "
        throw "Codex Meter is running from the install directory (PID: $processIds). Exit it from the system tray, then run this installer again."
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

function Copy-InstallFile {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$FileName
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Required install file was not found: $SourcePath"
    }

    $destinationPath = Join-Path $installDirectory $FileName
    $sourceFullPath = [System.IO.Path]::GetFullPath($SourcePath)
    $destinationFullPath = [System.IO.Path]::GetFullPath($destinationPath)
    if (-not [String]::Equals($sourceFullPath, $destinationFullPath,
        [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $SourcePath -Destination $destinationPath -Force
    }
}

$applicationFiles = @(
    "CodexMeter.exe",
    "CodexMeter.exe.config",
    "LICENSE.txt",
    "NOTICE.txt",
    "CodexBar-LICENSE.txt"
)
foreach ($fileName in $applicationFiles) {
    $sourcePath = Join-Path $sourceDirectory $fileName
    Copy-InstallFile -SourcePath $sourcePath -FileName $fileName
}

$supportFiles = @("README-Windows.txt", "install.ps1", "uninstall.ps1")
foreach ($fileName in $supportFiles) {
    $sourcePath = Join-Path $PSScriptRoot $fileName
    Copy-InstallFile -SourcePath $sourcePath -FileName $fileName
}

$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$shortcutPath = Join-Path $startMenuDirectory "Codex Meter.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedApplication
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = "Codex usage floating meter"
$shortcut.Save()

$startupShortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\Codex Meter.lnk"
if ($StartWithWindows) {
    $startupDirectory = Split-Path -Parent $startupShortcutPath
    New-Item -ItemType Directory -Path $startupDirectory -Force | Out-Null
    $startupShortcut = $shell.CreateShortcut($startupShortcutPath)
    $startupShortcut.TargetPath = Join-Path $installDirectory "CodexMeter.exe"
    $startupShortcut.WorkingDirectory = $installDirectory
    $startupShortcut.Description = "Start Codex Meter with Windows"
    $startupShortcut.Save()
}

Write-Host "INSTALL_OK"
Write-Host "Install directory: $installDirectory"
Write-Host "Start menu shortcut: $shortcutPath"
if ($StartWithWindows) {
    Write-Host "Startup shortcut: $startupShortcutPath"
} else {
    Write-Host "No startup entry was created. Use -StartWithWindows to opt in."
}

if ($Launch) {
    Start-Process -FilePath $installedApplication
}
