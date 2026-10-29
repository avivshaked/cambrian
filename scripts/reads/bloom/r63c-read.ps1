# r63c's G2 read, by logbook/specs/r63c-prereg/predictions.md: the grazer trait table per world (Evosim.Farm
# --grazer-traits, from scratch/wt-r63b's Release farm, written here as traits-<arm>.tsv), then g2-read.py over the four
# worlds with the survival floor from 12,000 s (D256). The controls, r63b's A and r62cf, are read in
# logbook/specs/r63c-prereg/r63b-from12k.txt. Light work beside a card group (D258): logical 16-21 at BelowNormal, one
# job at a time. Output in logbook/specs/r63c-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63c-read"
New-Item -ItemType Directory -Force $o | Out-Null
$exe = "$R\scratch\wt-r63b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]4128768
$me.PriorityClass = 'BelowNormal'
$runs = [System.Collections.Generic.List[string]]::new()
$traits = [System.Collections.Generic.List[string]]::new()
foreach ($a in 'b', 'c') {
    foreach ($s in '6202', '6203') {
        $d = (Get-ChildItem "$R\runs\r63c-$a-$s" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
        $runs.Add($d)
        "== traits r63c-$a-$s, $(Get-Date -Format HH:mm:ss)"
        & $exe --grazer-traits $d "$o\traits-r63c-$a-$s.tsv" *> "$o\traits-$a-$s.log"
        "exit $LASTEXITCODE, $((Get-Content "$o\traits-r63c-$a-$s.tsv" -ErrorAction SilentlyContinue | Measure-Object).Count) lines"
        $traits.Add('--traits'); $traits.Add("r63c-$a-$s=$o\traits-r63c-$a-$s.tsv")
    }
}
"== g2-read, $(Get-Date -Format HH:mm:ss)"
$args2 = @($runs) + @('--survive-from', '12000') + @($traits)
& python -I "$R\scripts\reads\bloom\g2-read.py" @args2 *> "$o\g2-read.txt"
"exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
