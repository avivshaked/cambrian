# r64's read at 24,000 s, by scratch/wt-phase1's logbook/specs/r64-prereg/predictions.md (D264's phase 1). Per arm: the
# grazer trait table (Evosim.Farm --grazer-traits from scratch/wt-phase1's Release farm, into scratch/r64-traits/, too
# large to commit), g2-read.py with the survival floor from 12,000 s, and again at r63h's standing cost of 0.0091 W as
# information, overshoot-read.py against r63c-b-6202, grazer-deaths.py, cannibal.py, and lineage-income.py (the death
# rows' income by lineage). Then r63g-gates.py and income-split.py over the controls r63c-b-6202 and r63h-d-6202 and the
# five arms. One job at a time on logical 8-11 at BelowNormal. Output in logbook/specs/r64-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r64-read"
$t = "$R\scratch\r64-traits"
New-Item -ItemType Directory -Force $o, $t | Out-Null
$exe = "$R\scratch\wt-phase1\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]3840
$me.PriorityClass = 'BelowNormal'
$control = (Get-ChildItem "$R\runs\r63c-b-6202" -Directory | Sort-Object Name | Select-Object -First 1).FullName
foreach ($a in 'r64-a-6202', 'r64-b-6202', 'r64-c-6202', 'r64-d-6202', 'r64-e-6202') {
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
    & python -I "$R\scripts\reads\bloom\lineage-income.py" "$d\lineage.jsonl" --end 24000 --min 30 *> "$o\lineage-income-$a.txt"
    "lineage exit $LASTEXITCODE"
}
$runs = 'r63c-b-6202', 'r63h-d-6202', 'r64-a-6202', 'r64-b-6202', 'r64-c-6202', 'r64-d-6202', 'r64-e-6202' | ForEach-Object { "$R\runs\$_" }
& python -I "$R\scripts\reads\bloom\r63g-gates.py" @runs --end 24000 *> "$o\leaf-gates.txt"
"gates exit $LASTEXITCODE"
& python -I "$R\scripts\reads\bloom\income-split.py" @runs --from 21000 --end 24000 *> "$o\income-split.txt"
"income exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
