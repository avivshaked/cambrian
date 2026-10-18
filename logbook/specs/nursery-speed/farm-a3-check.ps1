# A3's farm check (logbook/specs/speed-plan.md). Today's build refuses every round's config written before
# round 56's tunables, so the check reads builds against each other, not against a recording: round 56
# seed 2 resumed from its checkpoint at 5,000 s (1,547 bodies) for 100 s, as a cousin, three times, with a
# digest every 10 steps:
#   a3-t1    nursery-speed's farm (A3) at 1 thread: A3's serial path
#   a3-t8    nursery-speed's farm (A3) at 8 threads: Parallel.For
#   main-t1  main's farm (7887fbc, before A3) at 1 thread: the old Parallel.For path at one thread
# Every run restores the same checkpoint the same way, so a byte-identical A3 makes all three equal.
# scripts/reads/gpu-accept.py compares a3-t1 with a3-t8 and with main-t1: the stats rows (wall clocks left
# out), the digest rows, lineage.jsonl and positions.jsonl. Its exit codes: 0 identical, 1 a difference,
# anything else no verdict. All pinned to the efficiency cores, one at a time. Every wait has a deadline.
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$exe = @{
    a3   = "$main\scratch\wt-speed\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
    main = "$main\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
}
$runs = "$main\scratch\nursery-speed\farm-a3"
function Say([string]$t) { Write-Host "$(Get-Date -Format 'HH:mm:ss') $t" }
function Pin([datetime]$since) {
    $d = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $d) {
        $f = Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $since.AddSeconds(-5) }
        if ($f) { foreach ($p in $f) { $p.ProcessorAffinity = [IntPtr][long]4294901760 }; return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}
function WaitEnd([string]$arm, [datetime]$since) {
    $d = (Get-Date).AddMinutes(30)
    while ((Get-Date) -lt $d) {
        $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m) { $j = Get-Content $m.FullName -Raw | ConvertFrom-Json; if ($j.status -ne 'running') { return $j } }
        elseif ((Get-Date) -gt $since.AddMinutes(2)) { return $null }
        if (-not (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) -and (Get-Date) -gt $since.AddMinutes(1)) {
            Start-Sleep -Seconds 5
            $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Select-Object -Last 1
            if ($m) { return (Get-Content $m.FullName -Raw | ConvertFrom-Json) } else { return $null }
        }
        Start-Sleep -Seconds 10
    }
    return $null
}
foreach ($k in $exe.Keys) { if (-not (Test-Path $exe[$k])) { Say "no farm build at $($exe[$k])"; exit 3 } }
if (Test-Path $runs) { Say "$runs exists; not overwriting"; exit 3 }
New-Item -ItemType Directory -Force $runs | Out-Null
Set-Location $main
foreach ($r in @(@('a3-t1', 'a3', 1), @('a3-t8', 'a3', 8), @('main-t1', 'main', 1))) {
    $arm, $build, $threads = $r
    if (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) { Say 'a farm process is already running; stopping'; exit 3 }
    Say "== $arm ($build build) at $threads thread(s), 5,000 s for 100 s"
    $since = Get-Date
    & "$main\scripts\run-farm.ps1" $arm -ResumeFrom "$main\runs\r56-s2" -At 5000 -Seconds 5100 -Threads $threads `
        -RunsRoot $runs -WallMinutes 25 -AllowSourceMismatch -Exe $exe[$build] -Env @{ EVOSIM_DIGEST_EVERY = 10 } 6>&1 |
        Select-String 'refus|error|resumedFrom|WARNING' | ForEach-Object { Say "  $_" }
    if (-not (Pin $since)) { Say '  no farm process seen to pin' }
    $j = WaitEnd $arm $since
    if ($j) { Say "  $($j.status) $($j.reason), wall $([math]::Round(((Get-Date) - $since).TotalSeconds)) s" } else { Say '  no ending read'; exit 3 }
}
foreach ($other in 'a3-t8', 'main-t1') {
    Say "== compare a3-t1 with $other"
    python "$main\scripts\reads\gpu-accept.py" a3-t1 $other --runs-root $runs 2>&1 | ForEach-Object { Say "  $_" }
    $code = $LASTEXITCODE
    $verdict = if ($code -eq 0) { 'IDENTICAL' } elseif ($code -eq 1) { 'DIFFERS' } else { "NO VERDICT (exit $code)" }
    Say "  verdict a3-t1 against ${other}: $verdict"
}
Say '== done'
