# r63s's G5 read, by logbook/specs/r63s-prereg/predictions.md: g5-read.py per seed at --every 1 (gates 2 and 3, the arm
# "off" being r63c-b's own runs), then g2-read.py over both "on" worlds with the survival floor from 12,000 s (gate 1,
# D256). Reads the files only. Output in logbook/specs/r63s-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63s-read"
New-Item -ItemType Directory -Force $o | Out-Null
Set-Location $R
$on = @()
foreach ($s in '6202', '6203') {
    $d = "runs/r63s-on-$s/2026-10-29-220442-f73b299d"
    $on += $d
    "== g5-read $s, $(Get-Date -Format HH:mm:ss)"
    & python -I scripts/reads/shape/g5-read.py --on $d --off "runs/r63c-b-$s/2026-10-29-123625-716cd9ee" --every 1 *> "$o\g5-$s.txt"
    "exit $LASTEXITCODE"
}
"== g2-read, $(Get-Date -Format HH:mm:ss)"
& python -I scripts/reads/bloom/g2-read.py @on --survive-from 12000 *> "$o\g2-survive.txt"
"exit $LASTEXITCODE"
"== floaters, $(Get-Date -Format HH:mm:ss)"
$off = @('runs/r63c-b-6202/2026-10-29-123625-716cd9ee', 'runs/r63c-b-6203/2026-10-29-123625-716cd9ee')
& python -I scripts/reads/shape/r63s-floaters.py @on @off *> "$o\floaters.txt"
"exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
