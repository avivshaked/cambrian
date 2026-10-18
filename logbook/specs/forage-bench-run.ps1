# The foraging bench's first pass (propose-foraging.md, bench item 5 and the numbers for items 3 and 6):
# round 52 seed 1's jointed bodies at 30,000 s, alone in still water, under their own brains and the
# probe's hand strokes, with the limb push at four values. Waits for round 53's build to finish so
# that the machine stays at sixteen threads, then runs the four probes one at a time on four threads.
$ErrorActionPreference = 'Stop'
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-forage"
$out = "$w\logbook\specs\forage-bench"
New-Item -ItemType Directory -Force $out | Out-Null
$build = "$main\scratch\wt-r53\logbook\specs\r53-build\build.log"
$deadline = (Get-Date).AddHours(6)
while (-not (Select-String -Path $build -Pattern '^== done at' -Quiet)) {
    if ((Get-Date) -gt $deadline) { "gave up waiting for the build at $(Get-Date -Format HH:mm:ss)" | Add-Content "$out\bench.log"; exit 1 }
    Start-Sleep -Seconds 60
}
# The owner's rule of 2026-09-29 evening: another session renders on this machine, so a job starts
# only when the machine's % Processor Utility, averaged over a minute, is under 80%.
function Wait-Cpu([double]$limit = 80, [int]$hours = 6) {
    $until = (Get-Date).AddHours($hours)
    while ((Get-Date) -lt $until) {
        $u = (Get-Counter '\Processor Information(_Total)\% Processor Utility' -SampleInterval 5 -MaxSamples 12).CounterSamples.CookedValue | Measure-Object -Average
        if ($u.Average -lt $limit) { return $u.Average }
        Start-Sleep -Seconds 60
    }
    return -1
}
$run = (Get-ChildItem "$main\runs\r52-s1" -Directory | Sort-Object Name | Select-Object -Last 1).FullName
$cfg = (Get-ChildItem "$w\scratch\forage\runs\cfg52" -Directory | Select-Object -Last 1).FullName + '\config.json'
$exe = "$w\artifacts\Evosim.SwimProbe\bin\Release\net8.0\Evosim.SwimProbe.exe"
"bench from $(Get-Date -Format HH:mm:ss): run $run, config $cfg" | Add-Content "$out\bench.log"
foreach ($push in '0', '0.1', '0.3', '1') {
    $cpu = Wait-Cpu
    "cpu $([math]::Round($cpu)) % before push$push at $(Get-Date -Format HH:mm:ss)" | Add-Content "$out\bench.log"
    if ($cpu -lt 0) { "gave up waiting for the cpu" | Add-Content "$out\bench.log"; exit 1 }
    $label = "push$push"
    & $exe --run $run --label $label --out $out --at 30000 --window 2000 --top 20 --random 5 --seconds 120 --threads 4 --config $cfg --push $push *>> "$out\bench.log"
    "$label exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)" | Add-Content "$out\bench.log"
}
"== bench done at $(Get-Date -Format HH:mm:ss)" | Add-Content "$out\bench.log"