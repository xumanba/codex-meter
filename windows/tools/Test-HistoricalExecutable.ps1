[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$HistoricalExe,
    [Parameter(Mandatory = $true)][string]$RebuiltExe
)

# Requires PowerShell 7. Does not load or execute either target assembly.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Reflection.Metadata
function Get-NormalizedPeHash([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $bytes = [IO.File]::ReadAllBytes($resolved)
    $stream = [IO.MemoryStream]::new($bytes, $false)
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $mvid = $metadata.GetGuid($metadata.GetModuleDefinition().Mvid)
        $mvidBytes = $mvid.ToByteArray()
    } finally { $pe.Dispose(); $stream.Dispose() }
    $matches = @()
    for ($offset = 0; $offset -le $bytes.Length - 16; $offset++) {
        if ($bytes[$offset] -ne $mvidBytes[0]) { continue }
        $equal = $true
        for ($j = 1; $j -lt 16; $j++) {
            if ($bytes[$offset + $j] -ne $mvidBytes[$j]) { $equal = $false; break }
        }
        if ($equal) { $matches += $offset }
    }
    if ($matches.Count -ne 1) { throw 'Expected exactly one MVID occurrence; cannot safely normalize.' }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ($peOffset -lt 0 -or $peOffset + 12 -gt $bytes.Length -or [BitConverter]::ToUInt32($bytes, $peOffset) -ne 0x4550) {
        throw 'Invalid PE signature.'
    }
    $timestampOffset = $peOffset + 8
    [Array]::Clear($bytes, $timestampOffset, 4)
    [Array]::Clear($bytes, $matches[0], 16)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') }
    finally { $sha.Dispose() }
    [pscustomobject]@{ File = $resolved; Bytes = $bytes.Length; NormalizedSHA256 = $hash; TimestampOffset = $timestampOffset; MvidOffset = $matches[0] }
}
$historical = Get-NormalizedPeHash $HistoricalExe
$rebuilt = Get-NormalizedPeHash $RebuiltExe
if ($historical.Bytes -ne $rebuilt.Bytes -or $historical.NormalizedSHA256 -ne $rebuilt.NormalizedSHA256) {
    throw 'Historical and rebuilt assemblies differ outside the COFF timestamp and MVID.'
}
[pscustomobject]@{ Result = 'HISTORICAL_EXE_MATCH_OK'; Historical = $historical; Rebuilt = $rebuilt } | ConvertTo-Json -Depth 3
