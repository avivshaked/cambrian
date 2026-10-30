# r63d's end read, by scratch/wt-r63b's logbook/specs/r63d-prereg/predictions.md (D259): the grazer trait table per
# world (Evosim.Farm --grazer-traits, from scratch/wt-r63b's Release farm, as scratch/r63d-end-traits/traits-<arm>.tsv:
# 3.8 to 6.8 MB each, over the commit hook's ceiling and remade from the run in a minute or two), then
# g2-read.py on each world alone, with the survival floor from 12,000 s, to the second that world ended. All three ended
# at the population ceiling before 72,000 s, so each is read at its own end, as the pre-registration reads a world
# stopped early. One job at a time on logical 16-21 at BelowNormal. Output in logbook/specs/r63d-read/end/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63d-read\end"
$t = "$R\scratch\r63d-end-traits"
New-Item -ItemType Directory -Force $o, $t | Out-Null
$exe = "$R\scratch\wt-r63b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]4128768
$me.PriorityClass = 'BelowNormal'
$ends = [ordered]@{ 'r63d-b-6202' = 67240; 'r63d-b-6203' = 54440; 'r63d-b-6204-s2' = 55260 }
foreach ($a in $ends.Keys) {
    $d = (Get-ChildItem "$R\runs\$a" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
    "== traits $a, $(Get-Date -Format HH:mm:ss)"
    & $exe --grazer-traits $d "$t\traits-$a.tsv" *> "$o\traits-$a.log"
    "exit $LASTEXITCODE, $((Get-Content "$t\traits-$a.tsv" -ErrorAction SilentlyContinue | Measure-Object).Count) lines"
    "== g2-read $a to $($ends[$a]), $(Get-Date -Format HH:mm:ss)"
    & python -I "$R\scripts\reads\bloom\g2-read.py" $d --survive-from 12000 --end $ends[$a] --traits "$t\traits-$a.tsv" *> "$o\g2-read-$a.txt"
    "exit $LASTEXITCODE"
}
"== done at $(Get-Date -Format HH:mm:ss)"
