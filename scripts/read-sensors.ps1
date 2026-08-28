<#
  Read MSI Afterburner's hardware-monitor shared memory (MAHMSharedMemory).

  Afterburner publishes every sensor it polls into a memory-mapped file. The layout is a
  header followed by fixed-size entries; header size and entry size are read FROM the header
  rather than hardcoded, because they have changed between versions and a wrong constant
  yields plausible-looking garbage rather than an error.

  Entry layout (MAHM_SHARED_MEMORY_ENTRY): five MAX_PATH (260-byte) ANSI strings —
  source name, units, localised name, localised units, recommended format — then
  float data, float min, float max, DWORD flags, DWORD gpu, DWORD srcId.

  Requires Afterburner to be RUNNING; the mapping does not exist otherwise.
#>
[CmdletBinding()]
param([string]$Filter)

$ErrorActionPreference = 'Stop'

try {
    $mmf = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting(
        'MAHMSharedMemory',
        [System.IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
}
catch {
    Write-Output "MAHMSharedMemory not available: $($_.Exception.Message)"
    Write-Output "(Afterburner must be running; if it is, it may need to have been started elevated.)"
    return
}

$view = $mmf.CreateViewAccessor(0, 0, [System.IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)

$signature = $view.ReadUInt32(0)
# Accept either byte order. Afterburner's own header defines this as 0x4D41484D; the
# transposed form is what you get reading the four chars the other way round, and both
# appear in the wild depending on which source you copy the constant from. 0xDEAD is
# published deliberately while Afterburner is shutting down.
if ($signature -eq 0xDEAD) {
    Write-Output "Afterburner is shutting down (signature 0xDEAD)"
    return
}
if ($signature -ne 0x4D41484D -and $signature -ne 0x4D48414D) {
    Write-Output ("unexpected signature 0x{0:X8} - not an Afterburner mapping" -f $signature)
    return
}

$headerSize = $view.ReadUInt32(8)
$numEntries = $view.ReadUInt32(12)
$entrySize  = $view.ReadUInt32(16)

$bytes = New-Object byte[] 260
$rows = @()

for ($i = 0; $i -lt $numEntries; $i++) {
    $base = $headerSize + ($i * $entrySize)

    $view.ReadArray([int64]$base, $bytes, 0, 260) | Out-Null
    $name = [System.Text.Encoding]::ASCII.GetString($bytes).TrimEnd([char]0)

    $view.ReadArray([int64]($base + 260), $bytes, 0, 260) | Out-Null
    $units = [System.Text.Encoding]::ASCII.GetString($bytes).TrimEnd([char]0)

    # data sits after the five 260-byte strings
    $value = $view.ReadSingle($base + (260 * 5))
    $max   = $view.ReadSingle($base + (260 * 5) + 8)

    if ([float]::IsNaN($value)) { continue }
    if ($Filter -and $name -notmatch $Filter) { continue }

    $rows += [pscustomobject]@{
        Sensor = $name
        Value  = [math]::Round($value, 1)
        Units  = $units
        Max    = [math]::Round($max, 1)
    }
}

$view.Dispose()
$mmf.Dispose()

$rows | Format-Table -AutoSize
