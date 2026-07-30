[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$windowsDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $windowsDirectory
$sourceDirectory = Join-Path $windowsDirectory "src"
$outputDirectory = Join-Path $windowsDirectory "dist"
$iconPath = Join-Path $windowsDirectory "assets\CodexMeter.ico"
$compilerCandidates = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "The .NET Framework C# compiler csc.exe was not found."
}
if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "The Windows application icon was not found: $iconPath"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath $sourceDirectory -Filter "*.cs" |
    Where-Object { $_.Name -ne "CodexMeterForm.cs" } |
    ForEach-Object { $_.FullName }

$commonArguments = @(
    "/nologo",
    "/optimize+",
    "/platform:anycpu",
    "/warn:4",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Web.Extensions.dll"
)

$applicationPath = Join-Path $outputDirectory "CodexMeter.exe"
& $compiler $commonArguments "/target:winexe" "/main:CodexMeter.Program" "/win32manifest:$windowsDirectory\app.manifest" "/win32icon:$iconPath" "/out:$applicationPath" $sourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "CodexMeter.exe build failed with exit code $LASTEXITCODE"
}

$hangingTestSource = Join-Path $windowsDirectory "tests\HangingCli.cs"
$hangingTestPath = Join-Path $outputDirectory "CodexMeter.HangingTest.exe"
& $compiler $commonArguments "/target:exe" "/main:HangingCli" "/out:$hangingTestPath" $hangingTestSource
if ($LASTEXITCODE -ne 0) {
    throw "CodexMeter.HangingTest.exe build failed with exit code $LASTEXITCODE"
}

$testPath = Join-Path $outputDirectory "CodexMeter.Tests.exe"
& $compiler $commonArguments "/target:exe" "/main:CodexMeter.TestProgram" "/out:$testPath" $sourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "CodexMeter.Tests.exe build failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $windowsDirectory "CodexMeter.exe.config") -Destination (Join-Path $outputDirectory "CodexMeter.exe.config") -Force
Copy-Item -LiteralPath (Join-Path $repositoryDirectory "LICENSE") -Destination (Join-Path $outputDirectory "LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repositoryDirectory "NOTICE") -Destination (Join-Path $outputDirectory "NOTICE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repositoryDirectory "ThirdPartyLicenses\CodexBar-LICENSE.txt") -Destination (Join-Path $outputDirectory "CodexBar-LICENSE.txt") -Force

Write-Host "BUILD_OK"
Write-Host "Application: $applicationPath"
Write-Host "Tests: $testPath"
