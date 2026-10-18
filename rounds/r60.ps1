# Round 60's launcher (scratch/r60-prereg/0142-draft.md until its entry lands): the fed islands of r60s-big3 (D185) at
# the round's dose and desert burial (rounds/env-r60.ps1, which refuses until both are filled; D190, D191), seeds
# 6001, 6002 and 6003 (D180), founded on the graphics card as one group of three worlds, r60-s6001, r60-s6002 and
# r60-s6003. One step a call, on this checkout's Release build:
#   round   the card: the three as one group, -Seconds (48,000 by default, eight years of 6,000 s), checkpoints every
#           1,500 s, a wall of -WallMinutes, the runs in the main checkout's runs\. Only from the commit that adds the
#           round's pre-registration. With -FoundingCheck, the draft's founding check instead: the three as one group
#           to 1,500 s with a checkpoint at 1,500 s, as r60-f-s6001, r60-f-s6002 and r60-f-s6003 in this checkout's
#           runs\, to be discarded. It is read for the card's memory at the group's peak against -MemoryCeiling, the
#           pace, and each seed's islands and where its crowd stands at 1,500 s (scripts/reads/r60-read.py --end 1500).
#           Over the ceiling the step stops, and the round runs one seed at a time.
#   b3      the card: B3, r60-s6001 resumed alone through card-run.ps1 from its 45,000 s checkpoint to 48,000 s as
#           r60-s6001b, then read against the whole run from 45,000 s by group-resume-compare --lone. After the round.
# The card is shared with the other project's drawing from 2026-10-14 (the owner's wish, through the coordinator). A card step
# launches with the card's memory under -CardMemoryLimit, 12,000 MiB, and with no batch-mode Unity but the other project's.
# Every card step: group 32, single precision, the snow's transport, the scent's fill, the concurrent mode and the spin
# at 5,000, -Threads threads pinned by -Mask (16 on logical 16-31 by default, as round 59's), the driver watch and the
# heat stop (D183: two samples in a row at or over 90 C on the CPU), after a minute's cooling.
# -AllowBeside goes to card-group.ps1, card-run-check.ps1 and the wait below: the dotnet.exe processes that may run
# beside a step, by command line. Its default passes the nursery room's server, a nursery run on its own cores, and
# the other project's builds, runner and build-server shutdown; '' passes nothing.
# Before a step: this checkout at -Commit and clean, a plain Release build no older than its sources, the dose and
# desert burial set in rounds/env-r60.ps1, the names free, and the card's memory under -CardMemoryLimit. After it:
# each world ended by its budget, books closed, its manifest naming the clean commit and its seed, card-run-check's
# every condition, overflow 0, the three switches on the engine line, and its config.json holding r60s-big3's world at
# the block's dose and desert burial, and its run's pool the four stomachs of rounds/r55-pool-control.tsv. Any failure
# stops with exit 3. -PreflightOnly runs the checks and starts nothing.
# Log: scratch/logs/r60-<step>.log in this checkout (r60-found.log for the founding check), last line "r60 <step>
# ends" or "r60 <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [ValidateSet('round', 'b3')][string]$Step = 'round',
    [switch]$FoundingCheck,
    [int]$Seconds = 48000,
    [int]$WallMinutes = 900,
    [int]$Threads = 16,
    [long]$Mask = 4294901760,
    [int]$CardMemoryLimit = 12000,
    [int]$MemoryCeiling = 20000,
    [string]$AllowBeside = 'Evosim\.NurseryRoom\.dll|Evosim\.Nursery\.dll|D:\\Projects\\code\\|build-server shutdown',
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
if ($FoundingCheck -and $Step -ne 'round') { Write-Host '-FoundingCheck goes with the round step only'; exit 1 }
$tag = if ($FoundingCheck) { 'found' } else { $Step }
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = Split-Path -Parent $PSScriptRoot
$runs = if ($FoundingCheck) { "$tree\runs" } else { "$main\runs" }
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$beside = $AllowBeside
$block = "$tree\rounds\env-r60.ps1"
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r60-$tag-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r60-$tag.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r60 $tag STOPPED: $why"; exit 3 }

$seedList = @(6001, 6002, 6003)
$plan = if ($FoundingCheck) { @{ Group = 'r60-f'; Names = @($seedList | ForEach-Object { "r60-f-s$_" }); Seconds = 1500; Every = 1500; Wall = 60 } }
elseif ($Step -eq 'round') { @{ Group = 'r60'; Names = @($seedList | ForEach-Object { "r60-s$_" }); Seconds = $Seconds; Every = 1500; Wall = $WallMinutes } }
else { @{ Group = 'r60-s6001b'; Names = @('r60-s6001b') } }

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
    # The dose: the block refuses until the pre-registration fills it.
    try { $script:world = . $block } catch { Stop-Step "$block refuses: $($_.Exception.Message)" }
    $script:dose = [double]$script:world.EVOSIM_MATTER_INFLUX
    $script:budget = [double]$script:world.EVOSIM_MATTER_BUDGET
    $script:desert = [double]$script:world.EVOSIM_MATTER_BURIAL_DESERT
    Say "  dose: influx $script:dose units/s, budget $script:budget units; desert burial $script:desert/s (below 0: the islands' rate); from $block"
}

# A group's names, and every file card-group.ps1 or card-run.ps1 would refuse to find there already.
function Free([string]$group, [string[]]$names) {
    $paths = @("$logs\card-run-$group.log", "$logs\card-run-$group-watch.log", "$logs\card-run-$group-gpu.csv")
    foreach ($n in $names) { $paths += @("$runs\$n", "$runs\$n.md", "$logs\$n.args", "$logs\$n.log", "$logs\$n.err", "$logs\$n.out", "$runs\$n-preflight") }
    foreach ($p in $paths) { if (Test-Path $p) { Stop-Step "a name is used: $p exists" } }
    Say "  names free: $group; $($names -join ', ')"
}

function CardIdle {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $util = [int](& nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    $script:launchMemory = $mem
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
function Manifest([string]$name, [long]$seed = 0) {
    $dir = RunDir $name
    if (-not $dir) { Stop-Step "no run under $runs\$name" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    Say "  ${name}: status $($m.status) ($($m.reason)), seed $($m.seed), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Step "${name}'s manifest does not name a clean $Commit" }
    if ($seed -gt 0 -and [long]$m.seed -ne $seed) { Stop-Step "${name}'s seed is $($m.seed), not $seed" }
    $g = $m.gpu
    $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
    if ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    return $m
}

# Each world's config holds r60s-big3's world at the block's dose: round 59's control's smell, drag and touch on round
# 57's prices and upkeep as rounds/env-r60s.ps1 leaves them, and its run's pool the control list's four stomachs.
function Settings([string]$name) {
    $dir = RunDir $name
    $text = Get-Content "$dir\config.json" -Raw
    $pick = { param($key) if ($text -match ('"' + $key + '"\s*:\s*("?[-0-9A-Za-z.eE]+"?)')) { $Matches[1].Trim('"') } else { '?' } }
    $consumer = if ($text -match '"id":\s*"consumer",\s*"upkeepWattsPerCubicMetre":\s*([-0-9.eE]+)') { [double]$Matches[1] } else { -1 }
    $num = [ordered]@{ worldAreaSquareMetres = 88000; matterIslandWavelengthMetres = 90; matterIslandCover = 0.05; reefMaxCount = 256
        matterSinkMetresPerSecond = 0.02; matterIslandGlassMarginMetres = 30; matterInfluxPerSecond = $script:dose; matterBudgetUnits = $script:budget
        matterBurialPerSecond = 0.001; matterBurialDesertPerSecond = $script:desert
        attackWattsPerUnit = 0.1; intakeWattsPerUnit = 0.1; foundingTricklePoolShare = 0.1; foundingFloorPoolShare = 0; foundingTricklePoolCount = 4
        leafScentRangeMetres = 6; spinDragCoefficient = 0.5 }
    $word = [ordered]@{ worldShape = 'Tank'; matterInfluxAt = 'Islands'; leafScent = 'Direction'; turningDrag = 'Spin'; senseBedTouch = 'true'
        foundersFollowMatter = 'true'; foundersFollowFood = 'false'; foundersFollowFoodDepth = 'false'; foundersFollowIncomeDepth = 'false'; foundersFollowCorpses = 'false' }
    $got = [ordered]@{}
    foreach ($k in $num.Keys) { $got[$k] = & $pick $k }
    foreach ($k in $word.Keys) { $got[$k] = & $pick $k }
    Say "  ${name}: $(($got.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', '), consumer upkeep $consumer"
    foreach ($k in $num.Keys) {
        $v = 0.0
        if (-not [double]::TryParse("$($got[$k])", [ref]$v) -or [math]::Abs($v - [double]$num[$k]) -gt 1e-6) { Stop-Step "${name}'s $k is $($got[$k]), not $($num[$k])" }
    }
    foreach ($k in $word.Keys) { if ("$($got[$k])" -ne "$($word[$k])") { Stop-Step "${name}'s $k is $($got[$k]), not $($word[$k])" } }
    if ([math]::Abs($consumer - 6) -gt 1e-6) { Stop-Step "${name}'s consumer upkeep is $consumer, not 6" }
    # The run's pool, genome for genome against the list's files in the list's order. The farm writes each genome
    # through GenomeJson.Write, which drops a source file's top-level id, so the two are compared parsed, id aside.
    $rows = @(Get-Content "$tree\rounds\r55-pool-control.tsv" | Select-Object -Skip 1 | Where-Object { $_ -ne '' })
    $pool = @(Get-ChildItem "$dir\pool\*.json" | Sort-Object Name)
    if ($pool.Count -ne $rows.Count) { Stop-Step "${name}'s pool holds $($pool.Count) genomes, the list $($rows.Count)" }
    $canon = { param($path) $j = Get-Content $path -Raw | ConvertFrom-Json; $j.PSObject.Properties.Remove('id'); $j | ConvertTo-Json -Depth 100 -Compress }
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $src = Join-Path "$tree\rounds" (($rows[$i] -split "`t")[0])
        if ((& $canon $src) -ne (& $canon $pool[$i].FullName)) { Stop-Step "${name}'s pool genome $($pool[$i].Name) is not $src" }
    }
    Say "  ${name}: its pool is the control list's $($rows.Count) genomes, text for text"
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
# the true end and nothing left running, waits for it first (round 59's check of 2026-10-14 read it 18 s too early).
function Wait-Watch([string]$runLog) {
    $line = Get-Content $runLog -ErrorAction SilentlyContinue | Where-Object { $_ -match 'watch pid (\d+):' } | Select-Object -First 1
    if (-not $line) { return }
    $id = [int]([regex]::Match($line, 'watch pid (\d+):').Groups[1].Value)
    $d = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $d -and (Get-Process -Id $id -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 2 }
    Say "  watch pid ${id}: $(if (Get-Process -Id $id -ErrorAction SilentlyContinue) { 'still running after 3 minutes' } else { 'ended' })"
}

# The founded group through card-group.ps1, then each world held to its conditions.
function Invoke-CardGroup {
    Pause-Cool; Wait-Quiet; CardIdle
    if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
    $since = (Get-Date).AddSeconds(-30)
    $group = $plan.Group
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-group.ps1", '-Name', $group, '-Arms', ($plan.Names -join ','),
        '-Seconds', "$($plan.Seconds)", '-Group', '32', '-WallMinutes', "$($plan.Wall)", '-Mask', "$Mask", '-Threads', "$Threads",
        '-Transport', '-Scent', '-Concurrent', '-SpinLimit', 'default', '-RunsRoot', $runs, '-Exe', $exe, '-Device', 'cuda',
        '-SampleCpu', '-SampleCores', '-HeatStop', '-LogOutside', '-CheckpointEvery', "$($plan.Every)",
        '-Launcher', $block, '-Seeds', ($seedList -join ','))
    if ($beside) { $a += @('-AllowBeside', $beside) }
    Say "== ${group}: card-group $($a[3..($a.Count - 1)] -join ' ')"
    Invoke-Pwsh $a "card-group-$group"
    Wait-Watch "$logs\card-run-$group.log"
    $lines = Get-Content "$logs\card-run-$group.log"
    $lines | Where-Object { $_ -match 'times real time|farm exited|STOPPED|warning|books over|allowed beside' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card group ends$' })) { Stop-Step "$group did not end" }
    for ($i = 0; $i -lt $plan.Names.Count; $i++) {
        $n = $plan.Names[$i]
        if (-not ($lines | Where-Object { $_ -match "books over .* of ${n}: .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${n}'s books did not close" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $n -LogArm $group -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${n}: $_" }
        if ($code -ne 0) { Stop-Step "$n failed a condition of card-run-check" }
        $engine = Get-Content "$logs\$n.log" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${n}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${n}'s engine line lacks a switch" }
        $null = Manifest $n $seedList[$i]
        Settings $n
    }
}

# The environment the scripts inherit: no EVOSIM_ setting at all; each world's settings are its block's.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
Say "r60 $tag begins: runs in $runs, $Threads threads on mask $Mask, allowed beside: $(if ($beside) { $beside } else { 'nothing' })"
Tree
Free $plan.Group $plan.Names
if ($Step -eq 'b3') {
    $whole = Manifest 'r60-s6001' 6001
    $script:b3From = RunDir 'r60-s6001'
    if (-not (Test-Path "$script:b3From\checkpoints\000045000.ckpt")) { Stop-Step "r60-s6001 has no 45,000 s checkpoint at $script:b3From" }
    Say "  r60-s6001b resumes $script:b3From (configHash $($whole.configHash)) at 45,000 s"
}
CardIdle
if ($PreflightOnly) { Say "r60 $tag preflight ends: nothing started"; exit 0 }

switch ($Step) {
    'round' {
        Invoke-CardGroup
        if ($FoundingCheck) {
            $csv = "$logs\card-run-$($plan.Group)-gpu.csv"
            $peak = (Get-Content $csv | Where-Object { $_ -match '^\d\d:\d\d:\d\d, \d' } | ForEach-Object { [int](($_ -split ',\s*')[4] -replace ' MiB', '') } | Measure-Object -Maximum).Maximum
            # The card is shared: the peak is the card's whole, the other project's drawing and the desktop with the group.
            Say "  the card's memory at the group's peak: $peak MiB (ceiling $MemoryCeiling), $script:launchMemory at launch, so about $($peak - $script:launchMemory) MiB the group's if nothing else moved"
            $dirs = @($plan.Names | ForEach-Object { RunDir $_ })
            python -I "$tree\scripts\reads\r60-read.py" --end 1500 @dirs 2>&1 | ForEach-Object { Say "  read: $_" }
            if ($peak -gt $MemoryCeiling) { Stop-Step "the three held $peak MiB of the card, over $MemoryCeiling; run the round one seed at a time" }
        }
    }
    'b3' {
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', 'r60-s6001b', '-At', '45000', '-Seconds', '3000',
            '-ResumeFrom', $script:b3From, '-Group', '32', '-WallMinutes', '120', '-Mask', "$Mask", '-Threads', "$Threads",
            '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== r60-s6001b: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a 'card-run-r60-s6001b'
        Wait-Watch "$logs\card-run-r60-s6001b.log"
        $lines = Get-Content "$logs\card-run-r60-s6001b.log"
        $lines | Where-Object { $_ -match 'resum|ended \(|times real time|card: |books over|allowed beside|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step 'r60-s6001b did not end' }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm 'r60-s6001b' -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check r60-s6001b: $_" }
        if ($code -ne 0) { Stop-Step 'r60-s6001b failed a condition of card-run-check' }
        $m = Manifest 'r60-s6001b' 6001
        Say "  r60-s6001b: resumedFrom $($m.resumedFrom.arm)/$($m.resumedFrom.run) at $($m.resumedFrom.seconds) s, sourceMismatch $($m.resumedFrom.sourceMismatch)"
        if ($m.resumedFrom.sourceMismatch -ne $false) { Stop-Step 'r60-s6001b resumed on another build than the one that wrote its checkpoint' }
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 45000 $script:b3From (RunDir 'r60-s6001b') 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'r60-s6001 resumed at 45,000 s is not its whole run' }
    }
}
python -I "$tree\scripts\reads\pace-calc.py" $runs @($plan.Names) 2>&1 | ForEach-Object { Say "  pace: $_" }
Say "r60 $tag ends"
