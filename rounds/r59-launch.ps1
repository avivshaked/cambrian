# Round 59's launcher (D174, D176, D177, D178; logbook/specs/r59-prereg/draft.txt until its entry lands): three isolated
# worlds founded on the graphics card as one group, each from its own block (card-group.ps1 -Launcher with one block a
# world): seed 1 the control (rounds/env-r59.ps1), seeds 2 and 3 planted (rounds/env-r59-planted.ps1). One step a
# call, on this checkout's Release build:
#   check   off the card: ILGPU's CPU accelerator founds the round's three worlds and the second smoke's (r59-k-c on
#           seed 1, -p2 and -p3 planted on seeds 2 and 3, -x the sixth on seed 2) as one group for 30 s, with
#           checkpoints every 15 s, on 4 threads pinned to logical 16-19, then resumes them at 15 s to 30 s. Each
#           world's resume is read against its whole run by group-resume-compare --lone. A founded card run meets
#           code a resume never reaches (src/Evosim.Farm.Gpu/CLAUDE.md).
#   smoke   the card: D178's two price smokes on seed 2's planted world as one group of two, founded to 4,500 s with a
#           checkpoint at 4,500 s: r59m-third at D177's third of the old mouth prices (env-r59-planted.ps1) and
#           r59m-sixth at a sixth (env-r59-sixth.ps1). The trickle opens when the floor closes at 3,000 s, so the
#           swimmers arrive in the last 1,500 s. Read afterwards by round 59's reader in its smoke mode.
#   round   the card: r59-s1, r59-s2 and r59-s3 as one group, -Seconds (48,000 by default), checkpoints every 1,500 s,
#           a wall of -WallMinutes, the runs in the main checkout's runs\. Only from the commit that adds the round's
#           pre-registration.
#   b3      the card: B3, r59-s2 resumed alone through card-run.ps1 from its 45,000 s checkpoint to 48,000 s as
#           r59-s2b, then read against the whole run from 45,000 s by group-resume-compare --lone. After the round.
# The card is shared with the other project's drawing from 2026-10-14 (the owner's wish, through the coordinator). A card step
# launches with the card's memory under -CardMemoryLimit, 12,000 MiB, room for the other project's drawing beside a group of about
# 5 GB on the card's 24,576, and with no batch-mode Unity but the other project's, which is logged as allowed.
# Every card step: group 32, single precision, the snow's transport, the scent's fill, the concurrent mode and the
# spin at 5,000, 16 threads on logical 16-31, the driver watch and the heat stop, after a minute's cooling.
# -AllowBeside goes to card-group.ps1, card-run-check.ps1 and the wait below: the dotnet.exe processes that may run
# beside a step, by command line. Its default passes the nursery room's server, a nursery run on its own cores, and
# the other project's builds, runner and build-server shutdown; '' passes nothing.
# Before a step: this checkout at -Commit and clean, a plain Release build, the names free, the pool list's genomes
# as rounds/r59-pool-list.py checked them, and the card's memory under -CardMemoryLimit. After it: each world ended by
# its budget, books closed, its manifest naming the clean commit, card-run-check's every condition, overflow 0, the
# three switches on the engine line, and its config.json holding its block's prices, upkeep, pool and smell, with
# its run's pool the list's genomes. Any failure stops with exit 3. -PreflightOnly runs the checks and starts nothing.
# Log: scratch/logs/r59-<step>.log in this checkout, last line "r59 <step> ends" or "r59 <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][ValidateSet('check', 'smoke', 'round', 'b3')][string]$Step,
    [int]$Seconds = 48000,
    [int]$WallMinutes = 360,
    [int]$CardMemoryLimit = 12000,
    [string]$AllowBeside = 'Evosim\.NurseryRoom\.dll|Evosim\.Nursery\.dll|D:\\Projects\\code\\|build-server shutdown',
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = Split-Path -Parent $PSScriptRoot
$runs = if ($Step -in 'round', 'b3') { "$main\runs" } else { "$tree\runs" }
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$beside = $AllowBeside
$card = $Step -ne 'check'
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r59-$Step-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r59-$Step.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r59 $Step STOPPED: $why"; exit 3 }

# What each kind of world must read back from its config.json, and the genomes its pool must hold.
$kinds = @{
    c       = @{ Block = 'env-r59.ps1'; Price = 0.033; Upkeep = 2; Share = 0.1; Count = 4; List = 'r55-pool-control.tsv' }
    planted = @{ Block = 'env-r59-planted.ps1'; Price = 0.033; Upkeep = 2; Share = 0.6; Count = 14; List = 'r59-pool.tsv' }
    sixth   = @{ Block = 'env-r59-sixth.ps1'; Price = 0.0167; Upkeep = 1; Share = 0.6; Count = 14; List = 'r59-pool.tsv' }
}
$plan = switch ($Step) {
    'check' { @{ Group = 'r59-k'; Names = @('r59-k-c', 'r59-k-p2', 'r59-k-p3', 'r59-k-x'); Kinds = @('c', 'planted', 'planted', 'sixth'); Seeds = '1,2,3,2' } }
    'smoke' { @{ Group = 'r59m'; Names = @('r59m-third', 'r59m-sixth'); Kinds = @('planted', 'sixth'); Seeds = '2,2' } }
    'round' { @{ Group = 'r59'; Names = @('r59-s1', 'r59-s2', 'r59-s3'); Kinds = @('c', 'planted', 'planted'); Seeds = '1,2,3' } }
    'b3'    { @{ Group = 'r59-s2b'; Names = @('r59-s2b'); Kinds = @('planted'); Seeds = '' } }
}
$launchers = @($plan.Kinds | ForEach-Object { "$tree\rounds\$($kinds[$_].Block)" })

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
function RunDir([string]$name) { (Get-ChildItem "$runs\$name" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }

function Tree {
    $head = git -C $tree rev-parse --short HEAD
    Say "  $tree on $(git -C $tree rev-parse --abbrev-ref HEAD) at $head, asked for $Commit"
    if ($head -ne $Commit) { Stop-Step "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain --untracked-files=no
    if ($dirty) { Stop-Step "DIRTY: $($dirty -join '; ')" }
    if (-not (Test-Path $exe)) { Stop-Step "no Release farm at $exe" }
    $cardProbe = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: card probe $cardProbe, O2 $hasO2; $script:hashes"
    if ($cardProbe -or $hasO2) { Stop-Step 'not the plain build' }
    # The build is this commit's: no source file newer than the farm's binary.
    $built = (Get-Item $exe).LastWriteTime
    $newer = @(Get-ChildItem "$tree\src" -Recurse -Include *.cs, *.csproj -File | Where-Object { $_.LastWriteTime -gt $built -and $_.FullName -notmatch '\\(bin|obj)\\' })
    if ($newer) { Stop-Step "the build is older than $($newer.Count) source files, $($newer[0].FullName) among them; rebuild" }
    foreach ($l in $launchers) { if (-not (Test-Path $l)) { Stop-Step "no block at $l" } }
    $pl = python -I "$tree\rounds\r59-pool-list.py" 2>&1
    Say "  pool list: $pl"
    if ($LASTEXITCODE -ne 0) { Stop-Step 'the pool list does not check' }
    if (git -C $tree status --porcelain -- rounds/r59-pool.tsv) { Stop-Step 'rounds/r59-pool-list.py writes a list other than the committed one' }
}

# A group's names, and every file card-group.ps1 would refuse to find there already.
function Free([string]$group, [string[]]$names) {
    $paths = @("$logs\card-run-$group.log", "$logs\card-run-$group-watch.log", "$logs\card-run-$group-gpu.csv")
    foreach ($n in $names) { $paths += @("$runs\$n", "$runs\$n.md", "$logs\$n.args", "$logs\$n.log", "$logs\$n.err", "$logs\$n.out") }
    foreach ($p in $paths) { if (Test-Path $p) { Stop-Step "a name is used: $p exists" } }
    Say "  names free: $group; $($names -join ', ')"
}

function CardIdle {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $util = [int](& nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    Say "  card memory used $mem MiB (limit $CardMemoryLimit), utilization $util%; Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { 'running' } else { 'none' })"
    if ($mem -gt $CardMemoryLimit) { Stop-Step "the card holds $mem MiB at $util% utilization, over $CardMemoryLimit" }
    if ($resolve.Count -gt 0) { Stop-Step 'Resolve is running, and a card step wants a quiet machine' }
    # The other project's Unity shares the card from 2026-10-14; any other batch-mode Unity refuses.
    foreach ($u in @($unity | Where-Object { $_.CommandLine -match '-batchmode' })) {
        if ("$($u.CommandLine)" -match 'D:[\\/]Projects[\\/]code[\\/]') { Say "  allowed beside: the other project's Unity $($u.ProcessId)"; continue }
        Stop-Step "a batch-mode Unity is running: $($u.ProcessId)"
    }
}

function Pause-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Step 'the machine did not cool within 5 minutes of the pause' }
        if ($now -ge $until) {
            $c = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File "$tree\scripts\temp-check.ps1" | Out-Null
            if ($LASTEXITCODE -eq 0 -and $c -lt 60) { Say "  cool: card $c C, heat OK"; return }
        }
        Start-Sleep -Seconds 10
    }
}

function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            ($_.Name -match '^(Evosim\.Farm|Evosim\.Nursery\w*|testhost|dotnet)\.exe$' -or
             ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run|card-group|card-pair|driver-watch')) -and
            -not ($beside -and $_.Name -eq 'dotnet.exe' -and "$($_.CommandLine)" -match $beside) })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Step "busy 90 s before the run: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"
}

# A finished world's manifest, held to the step's conditions.
function Manifest([string]$name) {
    $dir = RunDir $name
    if (-not $dir) { Stop-Step "no run under $runs\$name" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    Say "  ${name}: status $($m.status) ($($m.reason)), seed $($m.seed), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Step "${name}'s manifest does not name a clean $Commit" }
    $g = $m.gpu
    $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
    if ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    return $m
}

# Each world's config holds its kind's settings, and its run's pool the list's genomes, text for text.
function Settings([string]$name, [string]$kind) {
    $k = $kinds[$kind]
    $dir = RunDir $name
    $text = Get-Content "$dir\config.json" -Raw
    $pick = { param($key) if ($text -match ('"' + $key + '"\s*:\s*("?[-0-9A-Za-z.eE]+"?)')) { $Matches[1].Trim('"') } else { '?' } }
    $consumer = if ($text -match '"id":\s*"consumer",\s*"upkeepWattsPerCubicMetre":\s*([-0-9.eE]+)') { [double]$Matches[1] } else { -1 }
    $got = [ordered]@{
        attack = [double](& $pick 'attackWattsPerUnit'); intake = [double](& $pick 'intakeWattsPerUnit'); consumer = $consumer
        share = [double](& $pick 'foundingTricklePoolShare'); floor = [double](& $pick 'foundingFloorPoolShare'); count = [int](& $pick 'foundingTricklePoolCount')
        scent = (& $pick 'leafScent'); range = [double](& $pick 'leafScentRangeMetres'); drag = (& $pick 'turningDrag'); spin = [double](& $pick 'spinDragCoefficient'); touch = (& $pick 'senseBedTouch')
    }
    Say "  ${name} ($kind): $(($got.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', ')"
    $want = [ordered]@{ attack = $k.Price; intake = $k.Price; consumer = [double]$k.Upkeep; share = $k.Share; floor = 0.0; count = $k.Count
        scent = 'Direction'; range = 6.0; drag = 'Spin'; spin = 0.5; touch = 'true' }
    foreach ($key in $want.Keys) { if ("$($got[$key])" -ne "$($want[$key])") { Stop-Step "${name}'s $key is $($got[$key]), not $($want[$key])" } }
    # The run's pool, genome for genome against the list's files in the list's order. The farm writes each genome
    # through GenomeJson.Write, which drops a source file's top-level id, so the two are compared parsed, id aside.
    $rows = @(Get-Content "$tree\rounds\$($k.List)" | Select-Object -Skip 1 | Where-Object { $_ -ne '' })
    $pool = @(Get-ChildItem "$dir\pool\*.json" | Sort-Object Name)
    if ($pool.Count -ne $rows.Count) { Stop-Step "${name}'s pool holds $($pool.Count) genomes, the list $($rows.Count)" }
    $canon = { param($path) $j = Get-Content $path -Raw | ConvertFrom-Json; $j.PSObject.Properties.Remove('id'); $j | ConvertTo-Json -Depth 100 -Compress }
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $src = Join-Path "$tree\rounds" (($rows[$i] -split "`t")[0])
        if ((& $canon $src) -ne (& $canon $pool[$i].FullName)) { Stop-Step "${name}'s pool genome $($pool[$i].Name) is not $src" }
    }
    $origins = @(Get-Content "$dir\pool\index.tsv" | Select-Object -Skip 1 | ForEach-Object { ($_ -split "`t")[2] } | Group-Object | ForEach-Object { "$($_.Count) $($_.Name)" })
    Say "  ${name}: its pool is the list's $($rows.Count) genomes, text for text ($($origins -join ', '))"
}

# A card script run to its end, waited on as a process, not read through a pipe: its driver watch inherits the
# pipe's handle, and a watch the script failed to stop held it open to the watch's deadline (2026-10-14).
# Start-Process joins its arguments with spaces, so one holding a space is quoted here.
function Invoke-Pwsh([string[]]$a, [string]$tag) {
    $q = @($a | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $c = Start-Process pwsh -ArgumentList $q -WindowStyle Hidden -PassThru -RedirectStandardOutput "$logs\$tag.console" -RedirectStandardError "$logs\$tag.console.err"
    $c.WaitForExit()
}

# The driver watch polls every 20 s and ends on its own after the run, so card-run-check, which wants it ended on
# the true end and nothing left running, waits for it first (the check of 2026-10-14 read it 18 s too early).
function Wait-Watch([string]$runLog) {
    $line = Get-Content $runLog -ErrorAction SilentlyContinue | Where-Object { $_ -match 'watch pid (\d+):' } | Select-Object -First 1
    if (-not $line) { return }
    $id = [int]([regex]::Match($line, 'watch pid (\d+):').Groups[1].Value)
    $d = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $d -and (Get-Process -Id $id -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 2 }
    Say "  watch pid ${id}: $(if (Get-Process -Id $id -ErrorAction SilentlyContinue) { 'still running after 3 minutes' } else { 'ended' })"
}

# One founded or resumed group through card-group.ps1, then each world held to its conditions.
function Invoke-CardGroup([string]$group, [string[]]$names, [string[]]$kindList, [string]$seeds, [int]$seconds, [int]$every, [string]$device, [int]$threads, [long]$mask, [int]$wall, [int]$at, [string]$from) {
    if ($device -eq 'cuda') { Pause-Cool; Wait-Quiet; CardIdle }
    if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
    $since = (Get-Date).AddSeconds(-30)
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-group.ps1", '-Name', $group, '-Arms', ($names -join ','),
        '-Seconds', "$seconds", '-Group', '32', '-WallMinutes', "$wall", '-Mask', "$mask", '-Threads', "$threads",
        '-Transport', '-Scent', '-Concurrent', '-SpinLimit', 'default', '-RunsRoot', $runs, '-Exe', $exe, '-Device', $device,
        '-SampleCpu', '-SampleCores', '-HeatStop', '-LogOutside')
    if ($every -gt 0) { $a += @('-CheckpointEvery', "$every") }
    if ($beside) { $a += @('-AllowBeside', $beside) }
    if ($at -eq 0) { $a += @('-Launcher', ($launchers -join ','), '-Seeds', $seeds) } else { $a += @('-At', "$at", '-ResumeFrom', $from) }
    Say "== ${group}: card-group $($a[3..($a.Count - 1)] -join ' ')"
    Invoke-Pwsh $a "card-group-$group"
    Wait-Watch "$logs\card-run-$group.log"
    $lines = Get-Content "$logs\card-run-$group.log"
    $lines | Where-Object { $_ -match 'times real time|farm exited|STOPPED|warning|books over|allowed beside' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card group ends$' })) { Stop-Step "$group did not end" }
    for ($i = 0; $i -lt $names.Count; $i++) {
        $n = $names[$i]
        if (-not ($lines | Where-Object { $_ -match "books over .* of ${n}: .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${n}'s books did not close" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $n -LogArm $group -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${n}: $_" }
        if ($code -ne 0) { Stop-Step "$n failed a condition of card-run-check" }
        $engine = Get-Content "$logs\$n.log" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${n}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${n}'s engine line lacks a switch" }
        $null = Manifest $n
        if ($at -eq 0) { Settings $n $kindList[$i] }
    }
}

# The environment the scripts inherit: no EVOSIM_ setting at all; each world's settings are its block's.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
Say "r59 $Step begins: runs in $runs, allowed beside: $(if ($beside) { $beside } else { 'nothing' })"
Tree
Free $plan.Group $plan.Names
$resumed = @($plan.Names | ForEach-Object { $_ -replace '^r59-k-', 'r59-kr-' })
if ($Step -eq 'check') { Free 'r59-kr' $resumed }
if ($Step -eq 'b3') {
    $whole = Manifest 'r59-s2'
    $script:b3From = RunDir 'r59-s2'
    if (-not (Test-Path "$script:b3From\checkpoints")) { Stop-Step "r59-s2 has no checkpoints folder at $script:b3From" }
    Say "  r59-s2b resumes $script:b3From (configHash $($whole.configHash)) at 45,000 s"
}
if ($card) { CardIdle }
if ($PreflightOnly) { Say "r59 $Step preflight ends: nothing started"; exit 0 }

switch ($Step) {
    'check' {
        Invoke-CardGroup $plan.Group $plan.Names $plan.Kinds $plan.Seeds 30 15 'cpu' 4 983040 120 0 ''
        $from = ($plan.Names | ForEach-Object { RunDir $_ }) -join ','
        Invoke-CardGroup 'r59-kr' $resumed $plan.Kinds '' 15 15 'cpu' 4 983040 120 15 $from
        $pairs = @()
        for ($i = 0; $i -lt $plan.Names.Count; $i++) { $pairs += @((RunDir $plan.Names[$i]), (RunDir $resumed[$i])) }
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 15 @pairs 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'a world resumed at 15 s is not its whole run' }
    }
    'smoke' { Invoke-CardGroup $plan.Group $plan.Names $plan.Kinds $plan.Seeds 4500 4500 'cuda' 16 4294901760 60 0 '' }
    'round' { Invoke-CardGroup $plan.Group $plan.Names $plan.Kinds $plan.Seeds $Seconds 1500 'cuda' 16 4294901760 $WallMinutes 0 '' }
    'b3' {
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', 'r59-s2b', '-At', '45000', '-Seconds', '3000',
            '-ResumeFrom', $script:b3From, '-Group', '32', '-WallMinutes', '40', '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop',
            '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== r59-s2b: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a 'card-run-r59-s2b'
        Wait-Watch "$logs\card-run-r59-s2b.log"
        $lines = Get-Content "$logs\card-run-r59-s2b.log"
        $lines | Where-Object { $_ -match 'ended \(|times real time|card: |books over|allowed beside|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step 'r59-s2b did not end' }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm 'r59-s2b' -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check r59-s2b: $_" }
        if ($code -ne 0) { Stop-Step 'r59-s2b failed a condition of card-run-check' }
        $null = Manifest 'r59-s2b'
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 45000 $script:b3From (RunDir 'r59-s2b') 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'r59-s2 resumed at 45,000 s is not its whole run' }
    }
}
if ($Step -ne 'check') {
    python -I "$tree\scripts\reads\pace-calc.py" $runs @($plan.Names) 2>&1 | ForEach-Object { Say "  pace: $_" }
}
Say "r59 $Step ends"
