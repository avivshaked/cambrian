# r64's step 2 read at 48,000 s, by scratch/wt-phase1's logbook/specs/r64-prereg/step2.md. Per world (r64s2-d-6202 and
# r64s2-a-6202, each resumed at 24,000 s): the grazer trait table of the resumed run (Evosim.Farm --grazer-traits from
# scratch/wt-phase1's Release farm, into scratch/r64s2-traits/), g2-read.py over the windows from 24,000 to 48,000 s,
# grazer-deaths.py, cannibal.py and lineage-income.py on the step's own lineage, and window-leaves.py for the leaves by
# window. Then income-split.py over 45,000-48,000 s. One job at a time on logical 8-11 at BelowNormal. Output in
# logbook/specs/r64s2-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r64s2-read"
$t = "$R\scratch\r64s2-traits"
New-Item -ItemType Directory -Force $o, $t | Out-Null
$exe = "$R\scratch\wt-phase1\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]3840
$me.PriorityClass = 'BelowNormal'
$dirs = @()
foreach ($a in 'r64s2-d-6202', 'r64s2-a-6202') {
    $d = (Get-ChildItem "$R\runs\$a" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
    $dirs += $d
    "== $a, $(Get-Date -Format HH:mm:ss)"
    & $exe --grazer-traits $d "$t\traits-$a.tsv" *> "$t\traits-$a.log"
    "traits exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\bloom\g2-read.py" $d --from 24000 --survive-from 24000 --end 48000 --traits "$t\traits-$a.tsv" *> "$o\g2-read-$a.txt"
    "g2 exit $LASTEXITCODE"
    & python -I "$R\scratch\wt-r63b\scripts\reads\bloom\grazer-deaths.py" "$d\lineage.jsonl" *> "$o\grazer-deaths-$a.txt"
    "deaths exit $LASTEXITCODE"
    & python -I "$R\scratch\wt-r63b\scripts\reads\bloom\cannibal.py" "$d\lineage.jsonl" *> "$o\cannibal-$a.txt"
    "cannibal exit $LASTEXITCODE"
    & python -I "$R\scripts\reads\bloom\lineage-income.py" "$d\lineage.jsonl" --end 48000 --min 30 *> "$o\lineage-income-$a.txt"
    "lineage exit $LASTEXITCODE"
}
& python -I "$R\scripts\reads\bloom\window-leaves.py" @dirs --from 24000 --end 48000 *> "$o\window-leaves.txt"
"leaves exit $LASTEXITCODE"
& python -I "$R\scripts\reads\bloom\income-split.py" @dirs --from 45000 --end 48000 *> "$o\income-split.txt"
"income exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
