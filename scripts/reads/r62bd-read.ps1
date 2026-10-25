# The r62bd screen's read (logbook/specs/r62bd-prereg/predictions.md), into logbook/specs/r62bd-read/, pinned to
# logical 16 to 19 at BelowNormal, which every reader inherits:
#   - r62-read.ps1 -Worlds at 24,000 s on the six screen worlds and the controls r62cs-w500-6201, r62cs-w500-6202,
#     r62ce-6201, r62ce-6202, r62-h6201 and r62-h6202;
#   - travel.py on the same twelve;
#   - bite-cap.py per 3,000 s bin to 24,000 s on the six and on r62cs-w500;
#   - r62bd-guilds.py: the finishing share per 3,000 s bin, the peak population and the guild mix.
# Run after the group has ended, never beside a card group.
param([int64]$Mask = 983040)
$ErrorActionPreference = 'Stop'
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$out = "$b\logbook\specs\r62bd-read"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
$screen = @('r62bd-x2-6201', 'r62bd-x2-6202', 'r62bd-x4-6201', 'r62bd-x4-6202', 'r62bd-x16-6201', 'r62bd-x16-6202')
$scale1 = @('r62cs-w500-6201', 'r62cs-w500-6202')
$controls = $scale1 + @('r62ce-6201', 'r62ce-6202', 'r62-h6201', 'r62-h6202')
$worlds = $screen + $controls
New-Item -ItemType Directory -Force $out | Out-Null

function RunOf([string]$arm) { (Get-ChildItem "$R\runs\$arm" -Directory | Sort-Object Name | Select-Object -Last 1).FullName }

& pwsh -NoProfile -File "$b\scripts\reads\r62-read.ps1" -End 24000 -Out $out -RunsRoot "$R\runs" -Mask $Mask -Worlds ($worlds -join ',')
if ($LASTEXITCODE -ne 0) { Write-Host "r62-read exit $LASTEXITCODE"; exit $LASTEXITCODE }

$runs = foreach ($a in $worlds) { RunOf $a }
& python -I "$b\scripts\reads\travel.py" @runs --end 24000 *> "$out\travel.txt"
Write-Host "travel exit $LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$capRuns = foreach ($a in ($screen + $scale1)) { RunOf $a }
& python -I "$b\scripts\reads\bite-cap.py" @capRuns --every 3000 --end 24000 *> "$out\bite-cap.txt"
Write-Host "bite-cap exit $LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& python -I "$b\scripts\reads\r62bd-guilds.py" @runs --bin 3000 --end 24000 *> "$out\guilds.txt"
Write-Host "guilds exit $LASTEXITCODE"
exit $LASTEXITCODE
