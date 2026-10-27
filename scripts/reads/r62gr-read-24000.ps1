# Growth group 1's 24,000 s read (logbook/specs/r62gr-prereg/predictions.md), pinned to logical 16-19 at BelowNormal
# while b0's theatre holds (the coordinator's ruling of 2026-10-26), one reader at a time: the pace screen's readers on
# the four worlds, then per world the overshoot against r62cs-w500-6201, the year-3000 island read and the grazer
# tally, then the gene's drift. The python children inherit the pin.
$ErrorActionPreference = 'Stop'
$root = 'D:\Projects\experiments\evolution-simulator'
$mask = [int64]983040
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$mask
$me.PriorityClass = 'BelowNormal'
$out = Join-Path $root 'logbook\specs\r62gr-read\24000'
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $root 'scratch\growth-identity\read-24000.log'
$worlds = 'r62gr-t60-6201', 'r62gr-t120-6201', 'r62gr-t240-6201', 'r62gr-t120s-6201'
"$(Get-Date -Format s) read begins" | Set-Content $log
& pwsh -NoProfile -File (Join-Path $root 'scripts\reads\r62-read.ps1') -End 24000 -Out $out -Mask $mask -Worlds ($worlds -join ',') *>> $log
"$(Get-Date -Format s) r62-read exit $LASTEXITCODE" | Add-Content $log
$control = (Get-ChildItem (Join-Path $root 'runs\r62cs-w500-6201') -Directory | Sort-Object Name | Select-Object -Last 1).FullName
$runs = $worlds | ForEach-Object { (Get-ChildItem (Join-Path $root "runs\$_") -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
& python -I (Join-Path $root 'scripts\reads\r60-read.py') @runs --end 24000 --year 3000 *> (Join-Path $out 'r60-read-3000.txt')
"$(Get-Date -Format s) r60-read-3000 exit $LASTEXITCODE" | Add-Content $log
& python -I (Join-Path $root 'scripts\reads\scaling-audit-read.py') @runs --end 24000 *> (Join-Path $out 'scaling-audit-1-4.txt')
"$(Get-Date -Format s) scaling-audit exit $LASTEXITCODE" | Add-Content $log
for ($i = 0; $i -lt $worlds.Count; $i++) {
    $w = $worlds[$i]; $r = $runs[$i]
    & python -I (Join-Path $root 'scripts\reads\overshoot-read.py') $r $control --end 24000 *> (Join-Path $out "overshoot-$w.txt")
    "$(Get-Date -Format s) overshoot $w exit $LASTEXITCODE" | Add-Content $log
    & python -I (Join-Path $root 'scripts\reads\grazer-tally.py') $r --end 24000 *> (Join-Path $out "grazer-tally-$w.txt")
    "$(Get-Date -Format s) grazer-tally $w exit $LASTEXITCODE" | Add-Content $log
}
& python -I (Join-Path $root 'scripts\reads\grs-drift-read.py') @runs *> (Join-Path $out 'grs-drift.txt')
"$(Get-Date -Format s) grs-drift exit $LASTEXITCODE" | Add-Content $log
"$(Get-Date -Format s) read ends" | Add-Content $log
