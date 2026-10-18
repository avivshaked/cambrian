<#
.SYNOPSIS
  One look at the machine's heat: the graphics card's temperature and slowdown reasons, and the
  processor's limiting (its core temperature is not readable here without a third-party tool).
  Prints one line and a verdict, and with -Log appends a row to scratch/temps/temps.tsv. Exits.

.DESCRIPTION
  The owner asked on 2026-10-01 for the temperatures to be watched and the work stopped if the
  machine gets too hot (logbook/specs/machine-and-tools.md, "Heat"). Verdicts:
    OK    nothing of note
    WARN  GPU at or above 80 C, or the processor limited on this reading
    HOT   GPU at or above 85 C, or the GPU slowing for heat, or the processor limited with
          firmware limiting logged in the last 15 minutes
  Exit code: 0 OK, 1 WARN, 2 HOT, 3 could not read.
#>
param([switch]$Log)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$verdict = 'OK'
$notes = @()

function Raise($level, $note) {
    $rank = @{ OK = 0; WARN = 1; HOT = 2 }
    if ($rank[$level] -gt $rank[$script:verdict]) { $script:verdict = $level }
    $script:notes += $note
}

# The graphics card.
$gpuTemp = $null; $gpuPower = $null; $gpuUtil = $null; $reasons = $null
try {
    $row = (& nvidia-smi --query-gpu=temperature.gpu,power.draw,utilization.gpu,clocks_event_reasons.active --format=csv,noheader,nounits 2>$null | Select-Object -First 1)
    $f = $row -split ',\s*'
    $gpuTemp = [int]$f[0]; $gpuPower = [double]$f[1]; $gpuUtil = [int]$f[2]
    $reasons = [Convert]::ToUInt64(($f[3] -replace '^0x', ''), 16)
} catch { Raise 'WARN' 'GPU unreadable' }

if ($gpuTemp -ne $null) {
    if ($gpuTemp -ge 85) { Raise 'HOT' "GPU $gpuTemp C" }
    elseif ($gpuTemp -ge 80) { Raise 'WARN' "GPU $gpuTemp C" }
    # NVIDIA's clock event reasons: 0x20 software thermal slowdown, 0x40 hardware thermal
    # slowdown, 0x8 hardware slowdown (heat or power brake).
    if ($reasons -band 0x60) { Raise 'HOT' ('GPU thermal slowdown 0x{0:X}' -f $reasons) }
    elseif ($reasons -band 0x8) { Raise 'WARN' ('GPU hardware slowdown 0x{0:X}' -f $reasons) }
}

# The processor: limiting and boost, and firmware limiting logged by Windows.
$c = (Get-Counter '\Processor Information(_Total)\Performance Limit Flags', '\Processor Information(_Total)\% Processor Performance', '\Processor Information(_Total)\% Processor Utility').CounterSamples
$limit = [int]($c | Where-Object { $_.Path -like '*limit flags' }).CookedValue
$perf = [math]::Round(($c | Where-Object { $_.Path -like '*% processor performance' }).CookedValue, 0)
$util = [math]::Round(($c | Where-Object { $_.Path -like '*% processor utility' }).CookedValue, 1)
$firmware = 0
try {
    $firmware = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-Kernel-Processor-Power'; Id = 37; StartTime = (Get-Date).AddMinutes(-15) } -ErrorAction Stop).Count
} catch { $firmware = 0 }
if ($limit -ne 0 -and $firmware -gt 0) { Raise 'HOT' "CPU limited (flags $limit, firmware events $firmware)" }
elseif ($limit -ne 0) { Raise 'WARN' "CPU limited (flags $limit)" }
elseif ($firmware -gt 0) { Raise 'WARN' "CPU firmware limiting logged ($firmware)" }

# The CPU's own temperature, when MSI Afterburner runs: its monitoring shared memory (MAHM), opened
# read-only. Nothing is written to it and no setting of Afterburner's is touched.
$cpuTemp = $null
try {
    Add-Type -AssemblyName System.IO.MemoryMappedFiles -ErrorAction SilentlyContinue
    $mm = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('MAHMSharedMemory', [System.IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
    $va = $mm.CreateViewAccessor(0, 0, [System.IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
    try {
        if ($va.ReadUInt32(0) -eq 0x4D41484D) {   # 'MAHM'
            $headerSize = $va.ReadUInt32(8); $entries = $va.ReadUInt32(12); $entrySize = $va.ReadUInt32(16)
            $hottest = $null
            for ($i = 0; $i -lt $entries; $i++) {
                $at = $headerSize + $i * $entrySize
                $bytes = New-Object byte[] 260
                [void]$va.ReadArray($at, $bytes, 0, 260)
                $name = [Text.Encoding]::ASCII.GetString($bytes).Split([char]0)[0]
                if ($name -match '(?i)^CPU\d* ?temperature$|^CPU temperature') {
                    $v = $va.ReadSingle($at + 5 * 260)
                    if ($v -gt 0 -and $v -lt 150 -and ($hottest -eq $null -or $v -gt $hottest)) { $hottest = $v }
                }
            }
            if ($hottest -ne $null) { $cpuTemp = [math]::Round($hottest, 0) }
        }
    } finally { $va.Dispose(); $mm.Dispose() }
} catch { }
if ($cpuTemp -ne $null) {
    if ($cpuTemp -ge 95) { Raise 'HOT' "CPU $cpuTemp C" }
    elseif ($cpuTemp -ge 90) { Raise 'WARN' "CPU $cpuTemp C" }
}

$zone = $null
try { $zone = [math]::Round(((Get-Counter '\Thermal Zone Information(*)\Temperature').CounterSamples | Select-Object -First 1).CookedValue - 273.15, 1) } catch { }

$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$line = "$stamp  $verdict  gpu ${gpuTemp}C ${gpuPower}W util ${gpuUtil}% reasons 0x{0:X}  cpu $(if ($cpuTemp -ne $null) { "${cpuTemp}C" } else { 'temp n/a' }) utility ${util}% perf ${perf}% limit $limit firmware $firmware  board zone ${zone}C" -f $reasons
if ($notes.Count) { $line += "  [" + ($notes -join '; ') + "]" }
$line

if ($Log) {
    $dir = Join-Path $root 'scratch\temps'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $tsv = Join-Path $dir 'temps.tsv'
    if (-not (Test-Path $tsv)) { "time`tverdict`tgpuC`tgpuW`tgpuUtil`tgpuReasons`tcpuUtility`tcpuPerf`tcpuLimit`tfirmware`tzoneC`tcpuC" | Set-Content -Path $tsv -Encoding utf8 }
    ("{0}`t{1}`t{2}`t{3}`t{4}`t0x{5:X}`t{6}`t{7}`t{8}`t{9}`t{10}`t{11}" -f $stamp, $verdict, $gpuTemp, $gpuPower, $gpuUtil, $reasons, $util, $perf, $limit, $firmware, $zone, $cpuTemp) | Add-Content -Path $tsv -Encoding utf8
}

exit @{ OK = 0; WARN = 1; HOT = 2 }[$verdict]
