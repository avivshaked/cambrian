# r63h's read at 24,000 s, by scratch/wt-r63b's logbook/specs/r63h-prereg/predictions.md: per arm, the grazer trait
# table (Evosim.Farm --grazer-traits from scratch/wt-r63b's Release farm, into scratch/r63h-traits/, too large to
# commit), g2-read.py with the survival floor from 12,000 s (gates 4 and 6, and the grazer traits reported), gate 6
# again at the arm's own standing cost (0.0091 W, a hundredth of the control's, as information), overshoot-read.py
# against the control r63c-b-6202 (gate 3), grazer-deaths.py and cannibal.py (reported); then r63g-gates.py (gates 1, 2
# and 5) and income-split.py over the control, r63g-b-6202 and the three arms. One job at a time on logical 8-11 at
# BelowNormal. Output in logbook/specs/r63h-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63h-read"
$t = "$R\scratch\r63h-traits"
New-Item -ItemType Directory -Force $o, $t | Out-Null
$exe = "$R\scratch\wt-r63b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]3840
$me.PriorityClass = 'BelowNormal'
$control = (Get-ChildItem "$R\runs\r63c-b-6202" -Directory | Sort-Object Name | Select-Object -First 1).FullName
foreach ($a in 'r63h-d-6202', 'r63h-e-6202', 'r63h-f-6202') {
    $d = (Get-ChildItem "$R\runs\$a" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
    "== $a, $(Get-Date -Format HH:mm:ss)"
    & $exe --grazer-traits $d "$t\traits-$a.tsv" *> "$t\traits-$a.log"
    "traits exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\bloom\g2-read.py" $d --survive-from 12000 --end 24000 --traits "$t\traits-$a.tsv" *> "$o\g2-read-$a.txt"
    "g2 exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\bloom\g2-read.py" $d --survive-from 12000 --end 24000 --traits "$t\traits-$a.tsv" --upkeep-w 0.0091 *> "$o\g2-read-$a-upkeep0.0091.txt"
    "g2 own-upkeep exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\overshoot-read.py" $d $control --end 24000 *> "$o\overshoot-$a.txt"
    "overshoot exit $LASTEXITCODE"
    & python -I "$R\scratch\wt-r63b\scripts\reads\bloom\grazer-deaths.py" "$d\lineage.jsonl" *> "$o\grazer-deaths-$a.txt"
    "deaths exit $LASTEXITCODE"
    & python -I "$R\scratch\wt-r63b\scripts\reads\bloom\cannibal.py" "$d\lineage.jsonl" *> "$o\cannibal-$a.txt"
    "cannibal exit $LASTEXITCODE"
}
$runs = 'r63c-b-6202', 'r63g-b-6202', 'r63h-d-6202', 'r63h-e-6202', 'r63h-f-6202' | ForEach-Object { "$R\runs\$_" }
& python -I "$R\scripts\reads\bloom\r63g-gates.py" @runs --end 24000 *> "$o\leaf-gates.txt"
"gates exit $LASTEXITCODE"
& python -I "$R\scripts\reads\bloom\income-split.py" @runs --from 21000 --end 24000 *> "$o\income-split.txt"
"income exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
