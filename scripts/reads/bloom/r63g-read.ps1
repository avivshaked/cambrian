# r63g's read at 24,000 s, by scratch/wt-r63b's logbook/specs/r63g-prereg/predictions.md: per arm, the grazer trait
# table (Evosim.Farm --grazer-traits from scratch/wt-r63b's Release farm, into scratch/r63g-traits/, too large to
# commit), g2-read.py with the survival floor from 12,000 s (gates 4 and 6, and the grazer traits reported),
# overshoot-read.py against the control r63c-b-6202 (gate 3), and grazer-deaths.py (reported); then r63g-gates.py over
# the control and the three arms (gates 1, 2 and 5). One job at a time on logical 16-21 at BelowNormal. Output in
# logbook/specs/r63g-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63g-read"
$t = "$R\scratch\r63g-traits"
New-Item -ItemType Directory -Force $o, $t | Out-Null
$exe = "$R\scratch\wt-r63b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]4128768
$me.PriorityClass = 'BelowNormal'
$control = (Get-ChildItem "$R\runs\r63c-b-6202" -Directory | Sort-Object Name | Select-Object -First 1).FullName
foreach ($a in 'r63g-a-6202', 'r63g-b-6202', 'r63g-c-6202') {
    $d = (Get-ChildItem "$R\runs\$a" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
    "== $a, $(Get-Date -Format HH:mm:ss)"
    & $exe --grazer-traits $d "$t\traits-$a.tsv" *> "$t\traits-$a.log"
    "traits exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\bloom\g2-read.py" $d --survive-from 12000 --end 24000 --traits "$t\traits-$a.tsv" *> "$o\g2-read-$a.txt"
    "g2 exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\overshoot-read.py" $d $control --end 24000 *> "$o\overshoot-$a.txt"
    "overshoot exit $LASTEXITCODE"
    & python -I "$R\scratch\wt-r63b\scripts\reads\bloom\grazer-deaths.py" "$d\lineage.jsonl" *> "$o\grazer-deaths-$a.txt"
    "deaths exit $LASTEXITCODE"
}
& python -I "$R\scripts\reads\bloom\r63g-gates.py" "$R\runs\r63c-b-6202" "$R\runs\r63g-a-6202" "$R\runs\r63g-b-6202" "$R\runs\r63g-c-6202" --end 24000 *> "$o\leaf-gates.txt"
"gates exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
