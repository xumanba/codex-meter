[CmdletBinding()]
param(
    [ValidateRange(1, 1440)]
    [int]$Minutes = 10,

    [ValidateRange(1, 60)]
    [int]$SampleSeconds = 5
)

$ErrorActionPreference = "Stop"
$process = Get-Process -Name "CodexMeter" -ErrorAction Stop | Select-Object -First 1
$sampleCount = [Math]::Ceiling($Minutes * 60 / $SampleSeconds)
$samples = [System.Collections.Generic.List[object]]::new()

for ($index = 0; $index -lt $sampleCount; $index++) {
    $current = Get-Process -Id $process.Id -ErrorAction Stop
    $samples.Add([pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        WorkingSetMB = [Math]::Round($current.WorkingSet64 / 1MB, 2)
        PrivateMB = [Math]::Round($current.PrivateMemorySize64 / 1MB, 2)
        Handles = $current.HandleCount
        Threads = $current.Threads.Count
        CpuSeconds = [Math]::Round($current.CPU, 3)
    })
    Start-Sleep -Seconds $SampleSeconds
}

$qaDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "qa"
New-Item -ItemType Directory -Path $qaDirectory -Force | Out-Null
$reportPath = Join-Path $qaDirectory ("resource-soak-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".csv")
$samples | Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding UTF8

$first = $samples[0]
$last = $samples[$samples.Count - 1]
[pscustomobject]@{
    ProcessId = $process.Id
    Samples = $samples.Count
    Report = $reportPath
    WorkingSetStartMB = $first.WorkingSetMB
    WorkingSetEndMB = $last.WorkingSetMB
    PrivateStartMB = $first.PrivateMB
    PrivateEndMB = $last.PrivateMB
    HandlesStart = $first.Handles
    HandlesEnd = $last.Handles
    ThreadsStart = $first.Threads
    ThreadsEnd = $last.Threads
}
