# A screen's 24,000 s read, pinned to a mask at BelowNormal while the theatre holds, one reader at a time: the pace
# screen's readers on the worlds (r62-read.ps1), the year-3000 island read and the scaling audit on all of them, then
# per world the overshoot against r62cs-w500 on the world's own seed (the name's last four digits) and the grazer tally,
# and, with -Gene, the growth reserve's drift. The python children inherit the pin. Used for growth group 1
# (r62gr-read-24000.ps1 is its first form), pace group 2 and growth group 2.
# Call: pwsh -NoProfile -File scripts/reads/screen-read-24000.ps1 -Worlds 'a-6201,b-6202' -Out logbook\specs\x\24000 [-Gene]
param(
    [Parameter(Mandatory)] [string]$Worlds,
    [Parameter(Mandatory)] [string]$Out,
    [int64]$Mask = 983040,
    [int]$End = 24000,
    [switch]$Gene)
$ErrorActionPreference = 'Stop'
$root = 'D:\Projects\experiments\evolution-simulator'
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
if (-not [System.IO.Path]::IsPathRooted($Out)) { $Out = Join-Path $root $Out }
New-Item -ItemType Directory -Force $Out | Out-Null
$log = Join-Path $Out 'screen-read.log'
$list = @($Worlds -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
function Latest($name) { (Get-ChildItem (Join-Path $root "runs\$name") -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
"$(Get-Date -Format s) read begins: $($list -join ', ') to $End s" | Set-Content $log
& pwsh -NoProfile -File (Join-Path $root 'scripts\reads\r62-read.ps1') -End $End -Out $Out -Mask $Mask -Worlds ($list -join ',') *>> $log
"$(Get-Date -Format s) r62-read exit $LASTEXITCODE" | Add-Content $log
$runs = @($list | ForEach-Object { Latest $_ })
& python -I (Join-Path $root 'scripts\reads\r60-read.py') @runs --end $End --year 3000 *> (Join-Path $Out 'r60-read-3000.txt')
"$(Get-Date -Format s) r60-read-3000 exit $LASTEXITCODE" | Add-Content $log
& python -I (Join-Path $root 'scripts\reads\scaling-audit-read.py') @runs --end $End *> (Join-Path $Out 'scaling-audit.txt')
"$(Get-Date -Format s) scaling-audit exit $LASTEXITCODE" | Add-Content $log
for ($i = 0; $i -lt $list.Count; $i++) {
    $w = $list[$i]; $run = $runs[$i]
    $seed = $w.Substring($w.Length - 4)
    $control = Latest "r62cs-w500-$seed"
    & python -I (Join-Path $root 'scripts\reads\overshoot-read.py') $run $control --end $End *> (Join-Path $Out "overshoot-$w.txt")
    "$(Get-Date -Format s) overshoot $w against r62cs-w500-$seed exit $LASTEXITCODE" | Add-Content $log
    & python -I (Join-Path $root 'scripts\reads\grazer-tally.py') $run --end $End *> (Join-Path $Out "grazer-tally-$w.txt")
    "$(Get-Date -Format s) grazer-tally $w exit $LASTEXITCODE" | Add-Content $log
}
if ($Gene) {
    & python -I (Join-Path $root 'scripts\reads\grs-drift-read.py') @runs *> (Join-Path $Out 'grs-drift.txt')
    "$(Get-Date -Format s) grs-drift exit $LASTEXITCODE" | Add-Content $log
}
"$(Get-Date -Format s) read ends" | Add-Content $log
