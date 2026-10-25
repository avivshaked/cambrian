# The r62cs screen's read (logbook/specs/r62cs-prereg/predictions.md): r62-read.ps1 -Worlds and travel.py into
# logbook/specs/r62cs-read/, pinned to logical 16 to 19 at BelowNormal, which every reader inherits. Two passes, since
# the 60 s pair were stopped short of 24,000 s on the coordinator's ruling (2026-10-24), r62cs-w60-6202 at 21,020 s and
# r62cs-w60-6201 at about 22,680 s, and r62-read refuses a world short of its end:
#   - at 24,000 s, the four screen worlds that ran to it and the controls r62ce-6201, r62ce-6202, r62-h6201, r62-h6202;
#   - at 21,000 s, in at21000/, the 60 s pair r62cs-w60-6201 and r62cs-w60-6202 with the same four controls.
# Run after the group has ended, never beside a card group.
param([int64]$Mask = 983040)
$ErrorActionPreference = 'Stop'
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$out = "$b\logbook\specs\r62cs-read"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
$controls = @('r62ce-6201', 'r62ce-6202', 'r62-h6201', 'r62-h6202')
$passes = @(
    @{ end = 24000; dir = $out; worlds = @('r62cs-w500-6201','r62cs-w500-6202', 'r62cs-w1500-6201', 'r62cs-w1500-6202') + $controls },
    @{ end = 21000; dir = "$out\at21000"; worlds = @('r62cs-w60-6201', 'r62cs-w60-6202') + $controls })
foreach ($p in $passes) {
    New-Item -ItemType Directory -Force $p.dir | Out-Null
    & pwsh -NoProfile -File "$b\scripts\reads\r62-read.ps1" -End $p.end -Out $p.dir -RunsRoot "$R\runs" -Mask $Mask -Worlds ($p.worlds -join ',')
    if ($LASTEXITCODE -ne 0) { Write-Host "r62-read to $($p.end) exit $LASTEXITCODE"; exit $LASTEXITCODE }
    $runs = foreach ($a in $p.worlds) { (Get-ChildItem "$R\runs\$a" -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
    & python -I "$b\scripts\reads\travel.py" @runs --end $p.end *> "$($p.dir)\travel.txt"
    Write-Host "travel to $($p.end) exit $LASTEXITCODE"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
