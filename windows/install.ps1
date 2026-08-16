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
        throw "CodexMeter is running from the install directory (PID: $processIds). Exit it from the system tray, then run this installer again."
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
$shortcutPath = Join-Path $startMenuDirectory "CodexMeter.lnk"
$legacyStartMenuShortcut = Join-Path $startMenuDirectory "Codex Meter.lnk"
if ((Test-Path -LiteralPath $legacyStartMenuShortcut) -and
    -not (Test-Path -LiteralPath $shortcutPath)) {
    Move-Item -LiteralPath $legacyStartMenuShortcut -Destination $shortcutPath
}
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedApplication
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = "Codex usage floating meter"
$shortcut.Save()

$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "CodexMeter"
$legacyStartupShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\Codex Meter.lnk"
$existingRunCommand = $null
try {
    $existingRunCommand = (Get-ItemProperty -LiteralPath $runKeyPath -Name $runValueName `
        -ErrorAction Stop).$runValueName
} catch [System.Management.Automation.ItemNotFoundException] {
} catch [System.Management.Automation.PSArgumentException] {
}
$existingRunTarget = $null
if ($existingRunCommand -match '^\s*"([^"]+)"') {
    $existingRunTarget = $Matches[1]
} elseif ($existingRunCommand -match '^\s*(.+?\.exe)(?:\s|$)') {
    $existingRunTarget = $Matches[1]
}
$startupAlreadyEnabled = $false
if ($existingRunTarget) {
    try {
        $startupAlreadyEnabled = [String]::Equals(
            [System.IO.Path]::GetFullPath($existingRunTarget),
            $installedApplication,
            [StringComparison]::OrdinalIgnoreCase)
    } catch {
        $startupAlreadyEnabled = $false
    }
}
$configureStartup = $StartWithWindows -or $startupAlreadyEnabled -or (Test-Path -LiteralPath $legacyStartupShortcut)
if ($configureStartup) {
    New-Item -Path $runKeyPath -Force | Out-Null
    Set-ItemProperty -LiteralPath $runKeyPath -Name $runValueName `
        -Value ('"{0}" --startup' -f $installedApplication)
    if (Test-Path -LiteralPath $legacyStartupShortcut) {
        Remove-Item -LiteralPath $legacyStartupShortcut -Force
    }
}

Write-Host "INSTALL_OK"
Write-Host "Install directory: $installDirectory"
Write-Host "Start menu shortcut: $shortcutPath"
if ($configureStartup) {
    Write-Host "Startup entry: HKCU\Software\Microsoft\Windows\CurrentVersion\Run\$runValueName"
} else {
    Write-Host "No startup entry was created. Use -StartWithWindows to opt in."
}

if ($Launch) {
    Start-Process -FilePath $installedApplication
}
