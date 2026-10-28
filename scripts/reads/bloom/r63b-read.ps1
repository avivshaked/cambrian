# r63b's G2 read: the grazer trait table per world (Evosim.Farm --grazer-traits, from scratch/wt-r63b's Release farm,
# written here as traits-<arm>.tsv, since the command never writes into a run), then g2-read.py over all eight.
# Arm A has no bloom, so it takes its islands from arm B of the same seed. Pinned to logical 22-27 at BelowNormal, one
# job at a time. Output in logbook/specs/r63b-read/.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\logbook\specs\r63b-read"
$exe = "$R\scratch\wt-r63b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]264241152
$me.PriorityClass = 'BelowNormal'
$runs = [System.Collections.Generic.List[string]]::new()
$traits = [System.Collections.Generic.List[string]]::new()
foreach ($a in 'a', 'b', 'c', 'd') {
    foreach ($s in '6202', '6203') {
        $d = (Get-ChildItem "$R\runs\r63b-$a-$s" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
        $runs.Add($d)
        "== traits r63b-$a-$s, $(Get-Date -Format HH:mm:ss)"
        & $exe --grazer-traits $d "$o\traits-r63b-$a-$s.tsv" *> "$o\traits-$a-$s.log"
        "exit $LASTEXITCODE, $((Get-Content "$o\traits-r63b-$a-$s.tsv" -ErrorAction SilentlyContinue | Measure-Object).Count) lines"
        $traits.Add('--traits'); $traits.Add("r63b-$a-$s=$o\traits-r63b-$a-$s.tsv")
    }
}
"== g2-read, $(Get-Date -Format HH:mm:ss)"
$args2 = @($runs) + @('--islands-from', "r63b-a-6202=$($runs[2])", '--islands-from', "r63b-a-6203=$($runs[3])") + @($traits)
& python -I "$R\scripts\reads\bloom\g2-read.py" @args2 *> "$o\g2-read.txt"
"exit $LASTEXITCODE"
"== done at $(Get-Date -Format HH:mm:ss)"
