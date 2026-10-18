# Round 61's launcher (D192): round 60's world (rounds/env-r60.ps1) on one landscape for every world, drawn from its own
# seed (RunConfig.SeedOverrides "landscape"), in two arms on life seeds 6101 and 6102 (D180): planted, with round 59's
# swimmers in the trickle (rounds/env-r61-planted.ps1), and unplanted (rounds/env-r61.ps1). Four worlds, r61-p6101,
# r61-c6101, r61-p6102 and r61-c6102, founded on the graphics card as one group. One step a call, on this checkout's
# Release build:
#   mapcheck  the card: the candidate landscapes -MapSeeds as one group on life seed -MapLifeSeed, founded to 100 s
#             with no checkpoints in this checkout's runs\ as r61-m-<seed>, each from a block written to
#             scratch\r61-maps\ (round 60's world and that landscape seed). Then scripts/reads/r61-maps.py lists each
#             candidate's islands clear of the glass. Nothing here needs the round's landscape to be filled.
#   round     the card: the four as one group, -Seconds (48,000 by default), checkpoints every 1,500 s, a wall of
#             -WallMinutes, the runs in the main checkout's runs\. Only from the commit that adds the round's
#             pre-registration. With -FoundingCheck, the four as one group to 1,500 s as r61-f-p6101 and so on in this
#             checkout's runs\, to be discarded: the card's memory at the group's peak against -MemoryCeiling, the
#             pace, and where each world's crowd stands at 1,500 s (scripts/reads/r60-read.py --end 1500). Over the
#             ceiling the step stops, and the round runs in pairs.
#   b3        the card: r61-p6101 resumed alone through card-run.ps1 from its 45,000 s checkpoint to 48,000 s as
#             r61-p6101b, then read against the whole run from 45,000 s by group-resume-compare --lone. After the round.
#   pair3     the card (D195): a third life seed, r61-p6103 and r61-c6103 on 6103, founded as round does (the same
#             blocks, landscape and checks) as one group r61-3, -Seconds, checkpoints every 1,500 s, main runs\.
#   extend    the card (D195): the round's worlds -Worlds (all four by default) resumed from their 48,000 s
#             checkpoints to -ExtendTo (72,000 by default) as one group, checkpoints every 1,500 s, main runs\, each
#             as <world>x (r61-p6101x and so on). The stitch: an x run's run.json names its source in resumedFrom, and
#             the readers (r59-read.py's World, which r60-read.py and r61-read.py take) read a run with resumedFrom as
#             one world with the run it resumed, 0 to -ExtendTo s. -Worlds 'p6101,c6101' runs a pair, as
#             r61-x-p6101-c6101. The sources' manifests are held to -RoundCommit. -Worlds 'p6103,c6103' runs the
#             third pair alone, as r61-x-p6103-c6103: p6103 resumes r61-p6103r, the stalled world resumed on the giants
#             build, whose manifest is held to -P6103Commit, and c6103 resumes r61-c6103, held to -RoundCommit (the
#             pair's commit). The p6103 line's overflow, the giants' candidates past the card's cap since 45,409 s, is
#             said in the log and does not stop the step, in its source or in its extension.
#   resume    the card: the extended worlds -Worlds (the four by default), each stopped at its own second, resumed
#             from its last checkpoint to -ExtendTo as one group r61-x2, each as <world>x2 (r61-p6101x2 and so on),
#             through card-group's per-world -At and -Until. A source is an x run that stopped (not ended) at a
#             second it holds a checkpoint for, its manifest held to -RoundCommit (the build the extension ran on).
#             The readers join the chain r61-p6101 -> r61-p6101x -> r61-p6101x2 by resumedFrom.
#   lone      the card: -Worlds p6103 alone, -LoneSource (r61-p6103x) stopped by the pace guard, resumed through
#             card-run.ps1 (a group is two worlds or more) from its checkpoint at -ResumeAt (its stop second when 0) to
#             -ExtendTo as -LoneName (r61-p6103x2), then, when -ResumeAt lies before the source's stop, read against
#             the source from there to its stop by group-resume-compare --lone. The
#             p6103 line's giants overflow and its bodies diverge: card-run-check's overflow and diverged conditions,
#             and only those, are said and not stopped on.
# The card is shared with the other project's drawing from 2026-10-14 (the owner's wish, through the coordinator). A card step
# launches with the card's memory under -CardMemoryLimit, 12,000 MiB, and with no batch-mode Unity but the other project's.
# Every card step: group 32, single precision, the snow's transport, the scent's fill, the concurrent mode and the spin
# at 5,000, -Threads threads pinned by -Mask (8 on logical 16-23 by default, as round 60 ran), the driver watch and the
# heat stop (D183), after a minute's cooling.
# -AllowBeside goes to card-group.ps1, card-run-check.ps1 and the wait below: the dotnet.exe processes that may run
# beside a step, by command line. Its default passes the nursery room's server, a nursery run on its own cores, and
# the other project's builds, runner and build-server shutdown; '' passes nothing.
# Before a step: this checkout at -Commit and clean, a plain Release build no older than its sources, the swimmers'
# files against their hashes (rounds/r59-pool-list.py, which must rewrite its list unchanged), for round and b3 the
# landscape set in rounds/env-r61.ps1, the names free, and the card's memory under -CardMemoryLimit. After it: each
# world ended by its budget, books closed, its manifest naming the clean commit and its seed, card-run-check's every
# condition, overflow 0, the three switches on the engine line, and its config.json holding round 60's world, its
# landscape seed, and its arm's pool, text for text. Any failure stops with exit 3. -PreflightOnly runs the checks and
# starts nothing.
# Log: scratch/logs/r61-<step>.log in this checkout (r61-found.log for the founding check), last line "r61 <step>
# ends" or "r61 <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    # b3 and extend: the commit the round ran from, when this checkout has moved on since by commits that leave the build as it was.
    [string]$RoundCommit = '',
    # b3 and extend: where the proof lies that a change to src/Evosim.Farm.Gpu between -RoundCommit and -Commit leaves
    # every bit as it was (the giants fix: gates b, c1 and c2 of 2026-10-17, in HANDOFF). Only that folder may change.
    [string]$ProvenBy = '',
    # b3 and extend: the folders under src that -ProvenBy covers, comma-separated. The card's alone by default; the pace
    # guard's build (farm-guard) changes the farm and the dynamics too, proven by its gate, and resumes as a cousin
    # (sourceMismatch), which the step then accepts.
    [string]$ProvenPaths = 'src/Evosim.Farm.Gpu/',
    # extend -Worlds p6103,c6103: the commit r61-p6103r ran on (the giants fix).
    [string]$P6103Commit = 'b81ca261',
    [ValidateSet('mapcheck', 'round', 'b3', 'pair3', 'extend', 'resume', 'lone')][string]$Step = 'round',
    [switch]$FoundingCheck,
    [string]$MapSeeds = '1,2,3,4,5,6,7,8',
    [long]$MapLifeSeed = 6101,
    [int]$Seconds = 48000,
    [int]$WallMinutes = 900,
    [int]$ExtendTo = 72000,
    # lone: the checkpoint second the world resumes at, at or before its stop; 0 for the stop itself.
    [int]$ResumeAt = 0,
    # lone: the stopped run it resumes and the name it resumes as, each a link of the p6103 line's chain.
    [string]$LoneSource = 'r61-p6103x',
    [string]$LoneName = 'r61-p6103x2',
    [string]$Worlds = 'p6101,c6101,p6102,c6102',
    [int]$Threads = 8,
    [long]$Mask = 16711680,
    [int]$CardMemoryLimit = 12000,
    [int]$MemoryCeiling = 20000,
    [string]$AllowBeside = 'Evosim\.NurseryRoom\.dll|Evosim\.Nursery\.dll|D:\\Projects\\code\\|build-server shutdown',
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
if ($FoundingCheck -and $Step -ne 'round') { Write-Host '-FoundingCheck goes with the round step only'; exit 1 }
# A map check is named by its candidates, so that a second batch is a second group: mapcheck-9-24, r61-m9-24.
$seedSpan = (@($MapSeeds -split ',' | Where-Object { $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })[0, -1]) -join '-'
$tag = if ($FoundingCheck) { 'found' } elseif ($Step -eq 'mapcheck') { "mapcheck-$seedSpan" }
    elseif ($Step -eq 'lone' -and $LoneName -ne 'r61-p6103x2') { "lone-$($LoneName -replace '^r61-', '')" } else { $Step }
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = Split-Path -Parent $PSScriptRoot
$runs = if ($FoundingCheck -or $Step -eq 'mapcheck') { "$tree\runs" } else { "$main\runs" }
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$beside = $AllowBeside
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r61-$tag-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r61-$tag.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r61 $tag STOPPED: $why"; exit 3 }

# What each arm must read back from its config.json, and the genomes its pool must hold.
$kinds = @{
    c = @{ Block = 'env-r61.ps1'; Share = 0.1; Count = 4; List = 'r55-pool-control.tsv' }
    p = @{ Block = 'env-r61-planted.ps1'; Share = 0.6; Count = 14; List = 'r59-pool.tsv' }
}
$mapList = @($MapSeeds -split ',' | Where-Object { $_.Trim() -ne '' } | ForEach-Object { [long]$_.Trim() })
$plan = if ($Step -eq 'mapcheck') {
    @{ Group = "r61-m$seedSpan"; Names = @($mapList | ForEach-Object { "r61-m-$_" }); Kinds = @($mapList | ForEach-Object { 'c' }); Seeds = @($mapList | ForEach-Object { $MapLifeSeed }); Seconds = 100; Every = 0; Wall = 60 }
} elseif ($FoundingCheck) {
    @{ Group = 'r61-f'; Names = @('r61-f-p6101', 'r61-f-c6101', 'r61-f-p6102', 'r61-f-c6102'); Kinds = @('p', 'c', 'p', 'c'); Seeds = @(6101, 6101, 6102, 6102); Seconds = 1500; Every = 1500; Wall = 60 }
} elseif ($Step -eq 'round') {
    @{ Group = 'r61'; Names = @('r61-p6101', 'r61-c6101', 'r61-p6102', 'r61-c6102'); Kinds = @('p', 'c', 'p', 'c'); Seeds = @(6101, 6101, 6102, 6102); Seconds = $Seconds; Every = 1500; Wall = $WallMinutes }
} elseif ($Step -eq 'pair3') {
    @{ Group = 'r61-3'; Names = @('r61-p6103', 'r61-c6103'); Kinds = @('p', 'c'); Seeds = @(6103, 6103); Seconds = $Seconds; Every = 1500; Wall = $WallMinutes }
} elseif ($Step -eq 'extend') {
    $ws = @($Worlds -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    foreach ($x in $ws) { if ($x -notmatch '^[pc]610[123]$') { Write-Host "-Worlds: '$x' is not one of p6101, c6101, p6102, c6102, p6103, c6103"; exit 1 } }
    if ($ws.Count -lt 2 -or @($ws | Sort-Object -Unique).Count -ne $ws.Count) { Write-Host '-Worlds names two worlds or more, each once'; exit 1 }
    # The third pair ran from its own commit, and p6103 from its stall's resume: it extends alone, as a pair.
    $third = @($ws | Where-Object { $_ -match '6103$' })
    if ($third.Count -gt 0 -and ($third.Count -ne 2 -or $ws.Count -ne 2)) { Write-Host "-Worlds: p6103 and c6103 extend together and alone"; exit 1 }
    if ($ExtendTo -le 48000 -or $ExtendTo % 1500 -ne 0) { Write-Host '-ExtendTo is a checkpoint second past 48,000'; exit 1 }
    @{ Group = $(if ($ws.Count -eq 4) { 'r61-x' } else { 'r61-x-' + ($ws -join '-') }); Names = @($ws | ForEach-Object { "r61-${_}x" })
       Sources = @($ws | ForEach-Object { if ($_ -eq 'p6103') { 'r61-p6103r' } else { "r61-$_" } }); Kinds = @($ws | ForEach-Object { $_.Substring(0, 1) })
       OverflowKnown = @('r61-p6103r', 'r61-p6103x')
       Seeds = @($ws | ForEach-Object { [long]$_.Substring(1) }); At = 48000; Seconds = $ExtendTo - 48000; Every = 1500; Wall = $WallMinutes }
} elseif ($Step -eq 'resume') {
    $ws = @($Worlds -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    foreach ($x in $ws) { if ($x -notmatch '^[pc]610[12]$') { Write-Host "-Worlds: '$x' is not one of p6101, c6101, p6102, c6102"; exit 1 } }
    if ($ws.Count -lt 2 -or @($ws | Sort-Object -Unique).Count -ne $ws.Count) { Write-Host '-Worlds names two worlds or more, each once'; exit 1 }
    if ($ExtendTo % 1500 -ne 0) { Write-Host '-ExtendTo is a checkpoint second'; exit 1 }
    # At, a second a world, is read from each source's manifest before the group starts.
    @{ Group = $(if ($ws.Count -eq 4) { 'r61-x2' } else { 'r61-x2-' + ($ws -join '-') }); Names = @($ws | ForEach-Object { "r61-${_}x2" })
       Sources = @($ws | ForEach-Object { "r61-${_}x" }); Kinds = @($ws | ForEach-Object { $_.Substring(0, 1) })
       Seeds = @($ws | ForEach-Object { [long]$_.Substring(1) }); At = @(); Until = $ExtendTo; Every = 1500; Wall = $WallMinutes }
} elseif ($Step -eq 'lone') {
    if ($Worlds -ne 'p6103') { Write-Host "-Worlds: the lone step takes p6103 alone"; exit 1 }
    if ($ExtendTo % 1500 -ne 0) { Write-Host '-ExtendTo is a checkpoint second'; exit 1 }
    if ($LoneSource -notmatch '^r61-p6103x\d*$' -or $LoneName -notmatch '^r61-p6103x\d+$' -or $LoneSource -eq $LoneName) {
        Write-Host "-LoneSource and -LoneName: two links of the chain r61-p6103x, r61-p6103x2, r61-p6103x3 and on"; exit 1
    }
    @{ Group = $LoneName; Names = @($LoneName); Sources = @($LoneSource); Kinds = @('p'); Seeds = @(6103)
       OverflowKnown = @(@('r61-p6103x', 'r61-p6103x2', $LoneSource, $LoneName) | Sort-Object -Unique); Every = 1500; Wall = $WallMinutes }
} else {
    @{ Group = 'r61-p6101b'; Names = @('r61-p6101b'); Kinds = @('p'); Seeds = @(6101) }
}

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
    # Round 60's world, whose dose and desert burial every arm keeps.
    try { $w60 = . "$tree\rounds\env-r60.ps1" } catch { Stop-Step "rounds/env-r60.ps1 refuses: $($_.Exception.Message)" }
    $script:dose = [double]$w60.EVOSIM_MATTER_INFLUX
    $script:budget = [double]$w60.EVOSIM_MATTER_BUDGET
    $script:desert = [double]$w60.EVOSIM_MATTER_BURIAL_DESERT
    Say "  round 60's world: influx $script:dose units/s, budget $script:budget units, desert burial $script:desert/s"
    # The landscape: the round's, or each candidate's.
    if ($Step -eq 'mapcheck') {
        $script:landscapes = @($mapList | ForEach-Object { "landscape=$_" })
    } else {
        try { $w61 = . "$tree\rounds\env-r61.ps1" } catch { Stop-Step "rounds/env-r61.ps1 refuses: $($_.Exception.Message)" }
        $script:landscapes = @($plan.Names | ForEach-Object { "$($w61.EVOSIM_SEED_OVERRIDES)" })
        Say "  the round's landscape: $($w61.EVOSIM_SEED_OVERRIDES)"
    }
    # The swimmers' files against their hashes; the list must come back as committed.
    $pl = python -I "$tree\rounds\r59-pool-list.py" 2>&1
    Say "  pool list: $pl"
    if ($LASTEXITCODE -ne 0) { Stop-Step 'the pool list does not check' }
    # By content, line by line: in a checkout that converts line endings, git's status calls the rewritten list
    # changed when its bytes are the committed ones (2026-10-17).
    $committed = @(git -C $tree show HEAD:rounds/r59-pool.tsv)
    $written = @(Get-Content "$tree\rounds\r59-pool.tsv")
    if ($LASTEXITCODE -ne 0 -or $committed.Count -eq 0 -or (($committed -join "`n") -cne ($written -join "`n"))) { Stop-Step 'rounds/r59-pool-list.py writes a list other than the committed one' }
}

# The blocks the group founds from: one a world. The map check's are written here, one a candidate.
function Blocks {
    if ($Step -ne 'mapcheck') { return @($plan.Kinds | ForEach-Object { "$tree\rounds\$($kinds[$_].Block)" }) }
    $dir = "$tree\scratch\r61-maps"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $out = @()
    foreach ($m in $mapList) {
        $file = "$dir\env-r61-map-$m.ps1"
        $text = "# Round 61's map check, written by rounds/r61.ps1: round 60's world on landscape seed $m.`r`n" +
            "`$s = . '$tree\rounds\env-r60.ps1'`r`n`$s.EVOSIM_SEED_OVERRIDES = 'landscape=$m'`r`n`$s`r`n"
        [IO.File]::WriteAllText($file, $text, [Text.UTF8Encoding]::new($true))
        $out += $file
    }
    return $out
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
function Manifest([string]$name, [long]$seed = 0, [string]$want = $Commit) {
    # A world whose overflow is known (the p6103 line's giants) has it said and not stopped on.
    $known = $plan.OverflowKnown -contains $name
    $dir = RunDir $name
    if (-not $dir) { Stop-Step "no run under $runs\$name" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    Say "  ${name}: status $($m.status) ($($m.reason)), seed $($m.seed), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$want*") { Stop-Step "${name}'s manifest does not name a clean $want" }
    if ($seed -gt 0 -and [long]$m.seed -ne $seed) { Stop-Step "${name}'s seed is $($m.seed), not $seed" }
    $g = $m.gpu
    $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
    if ($over -ne '0/0/0/0' -and $known) { Say "  ${name}: overflow $over, the giants' known overflow; said, not stopped on" }
    elseif ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    return $m
}

# Each world's config holds round 60's world on its landscape seed, and its run's pool its arm's list, text for text.
function Settings([string]$name, [string]$kind, [string]$landscape) {
    $k = $kinds[$kind]
    $dir = RunDir $name
    $text = Get-Content "$dir\config.json" -Raw
    $pick = { param($key) if ($text -match ('"' + $key + '"\s*:\s*("?[-0-9A-Za-z.eE]+"?)')) { $Matches[1].Trim('"') } else { '?' } }
    $consumer = if ($text -match '"id":\s*"consumer",\s*"upkeepWattsPerCubicMetre":\s*([-0-9.eE]+)') { [double]$Matches[1] } else { -1 }
    $over = if ($text -match '"seedOverrides"\s*:\s*\[\s*([^\]]*)\]') { ($Matches[1] -replace '[\s"]', '') } else { '?' }
    $num = [ordered]@{ worldAreaSquareMetres = 88000; matterIslandWavelengthMetres = 90; matterIslandCover = 0.05; reefMaxCount = 256
        matterSinkMetresPerSecond = 0.02; matterIslandGlassMarginMetres = 30; matterInfluxPerSecond = $script:dose; matterBudgetUnits = $script:budget
        matterBurialPerSecond = 0.001; matterBurialDesertPerSecond = $script:desert
        attackWattsPerUnit = 0.1; intakeWattsPerUnit = 0.1; foundingTricklePoolShare = $k.Share; foundingFloorPoolShare = 0; foundingTricklePoolCount = $k.Count
        leafScentRangeMetres = 6; spinDragCoefficient = 0.5 }
    $word = [ordered]@{ worldShape = 'Tank'; matterInfluxAt = 'Islands'; leafScent = 'Direction'; turningDrag = 'Spin'; senseBedTouch = 'true'
        foundersFollowMatter = 'true'; foundersFollowFood = 'false'; foundersFollowFoodDepth = 'false'; foundersFollowIncomeDepth = 'false'; foundersFollowCorpses = 'false' }
    $got = [ordered]@{}
    foreach ($key in $num.Keys) { $got[$key] = & $pick $key }
    foreach ($key in $word.Keys) { $got[$key] = & $pick $key }
    Say "  ${name} ($kind): $(($got.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', '), consumer upkeep $consumer, seedOverrides $over"
    foreach ($key in $num.Keys) {
        $v = 0.0
        if (-not [double]::TryParse("$($got[$key])", [ref]$v) -or [math]::Abs($v - [double]$num[$key]) -gt 1e-6) { Stop-Step "${name}'s $key is $($got[$key]), not $($num[$key])" }
    }
    foreach ($key in $word.Keys) { if ("$($got[$key])" -ne "$($word[$key])") { Stop-Step "${name}'s $key is $($got[$key]), not $($word[$key])" } }
    if ([math]::Abs($consumer - 6) -gt 1e-6) { Stop-Step "${name}'s consumer upkeep is $consumer, not 6" }
    if ($over -ne $landscape) { Stop-Step "${name}'s seedOverrides are '$over', not '$landscape'" }
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
    Say "  ${name}: its pool is $($k.List)'s $($rows.Count) genomes, text for text"
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
function Invoke-CardGroup([string[]]$blocks) {
    Pause-Cool; Wait-Quiet; CardIdle
    if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
    $since = (Get-Date).AddSeconds(-30)
    $group = $plan.Group
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-group.ps1", '-Name', $group, '-Arms', ($plan.Names -join ','),
        $(if ($plan.Until) { '-Until' } else { '-Seconds' }), $(if ($plan.Until) { "$($plan.Until)" } else { "$($plan.Seconds)" }), '-Group', '32', '-WallMinutes', "$($plan.Wall)", '-Mask', "$Mask", '-Threads', "$Threads",
        '-Transport', '-Scent', '-Concurrent', '-SpinLimit', 'default', '-RunsRoot', $runs, '-Exe', $exe, '-Device', 'cuda',
        '-SampleCpu', '-SampleCores', '-HeatStop', '-LogOutside')
    # A founded group names its blocks and seeds; a resumed one its sources and the second, and reads both from them.
    if ($plan.Sources) { $a += @('-At', (@($plan.At) -join ','), '-ResumeFrom', ($script:sourceDirs -join ',')) }
    else { $a += @('-Launcher', ($blocks -join ','), '-Seeds', ($plan.Seeds -join ',')) }
    if ($plan.Every -gt 0) { $a += @('-CheckpointEvery', "$($plan.Every)") }
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
        $m = Manifest $n $plan.Seeds[$i]
        if ($plan.Sources) {
            Say "  ${n}: resumedFrom $($m.resumedFrom.arm)/$($m.resumedFrom.run) at $($m.resumedFrom.seconds) s, sourceMismatch $($m.resumedFrom.sourceMismatch)"
            $atI = @($plan.At)[[math]::Min($i, @($plan.At).Count - 1)]
            if ($m.resumedFrom.arm -ne $plan.Sources[$i] -or [double]$m.resumedFrom.seconds -ne [double]$atI) { Stop-Step "$n did not resume $($plan.Sources[$i]) at $atI s" }
            if ($m.resumedFrom.sourceMismatch -ne $false -and $script:cousinProven) { Say "  ${n}: a cousin of its source by the build's hashes, proven bit-identical by $ProvenBy" }
            elseif ($m.resumedFrom.sourceMismatch -ne $false) { Stop-Step "$n resumed on another build than the one that wrote its checkpoint" }
        }
        Settings $n $plan.Kinds[$i] $script:landscapes[$i]
    }
}

# The environment the scripts inherit: no EVOSIM_ setting at all; each world's settings are its block's.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
Say "r61 $tag begins: runs in $runs, $Threads threads on mask $Mask, allowed beside: $(if ($beside) { $beside } else { 'nothing' })"
Tree
Free $plan.Group $plan.Names
$round = if ($RoundCommit) { $RoundCommit } else { $Commit }
if ($Step -in 'b3', 'extend', 'resume', 'lone' -and $round -ne $Commit) {
    # Only commits that leave the build alone may lie between: nothing under src, and the binaries no older than the sources.
    $moved = @(git -C $tree diff --name-only $round $Commit -- src)
    $provenIn = @($ProvenPaths -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    $unproven = @($moved | Where-Object { $f = $_; -not ($ProvenBy -and @($provenIn | Where-Object { $f -like "$_*" }).Count -gt 0) })
    # Proven changes outside the card's folder change the farm's or the dynamics' hash, so the resume is a cousin.
    $script:cousinProven = [bool]($ProvenBy -and @($moved | Where-Object { $_ -notlike 'src/Evosim.Farm.Gpu/*' }).Count -gt 0)
    if ($unproven) { Stop-Step "$round to $Commit changes the source: $($unproven -join ', ')" }
    if ($moved) { Say "  $round to $Commit changes the card's source, proven to leave every bit as it was by: $ProvenBy; $($moved -join ', ')" }
    Say "  the round ran from $round; $Commit adds $((git -C $tree diff --name-only $round $Commit) -join ', ')"
}
if ($Step -eq 'extend') {
    # Each source ended by its budget at 48,000 s on the round's commit, with its 48,000 s checkpoint.
    $script:sourceDirs = @()
    for ($i = 0; $i -lt $plan.Sources.Count; $i++) {
        $src = $plan.Sources[$i]
        $null = Manifest $src $plan.Seeds[$i] $(if ($src -eq 'r61-p6103r') { $P6103Commit } else { $round })
        $d = RunDir $src
        if (-not (Test-Path "$d\checkpoints\000048000.ckpt")) { Stop-Step "$src has no 48,000 s checkpoint at $d" }
        $script:sourceDirs += $d
        Say "  $($plan.Names[$i]) resumes $d at 48,000 s to $ExtendTo s"
    }
}
if ($Step -eq 'resume') {
    # Each source stopped (by stop-arm) on the extension's commit, at a second it holds a checkpoint for; it resumes there.
    $script:sourceDirs = @()
    for ($i = 0; $i -lt $plan.Sources.Count; $i++) {
        $src = $plan.Sources[$i]
        $d = RunDir $src
        if (-not $d) { Stop-Step "no run under $runs\$src" }
        $m = Get-Content "$d\run.json" -Raw | ConvertFrom-Json
        $g = $m.gpu
        $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
        Say "  ${src}: status $($m.status) ($($m.reason)) at $($m.simulatedSeconds) s, seed $($m.seed), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty), overflow $over"
        if ($m.status -ne 'stopped') { Stop-Step "$src did not stop; a resume step takes a world stopped at a checkpoint" }
        if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$round*") { Stop-Step "${src}'s manifest does not name a clean $round" }
        if ([long]$m.seed -ne $plan.Seeds[$i]) { Stop-Step "${src}'s seed is $($m.seed), not $($plan.Seeds[$i])" }
        if ($over -ne '0/0/0/0') { Stop-Step "${src}'s engine dropped contacts (overflow $over)" }
        $at = [int][math]::Round([double]$m.simulatedSeconds)
        if (-not (Test-Path ("$d\checkpoints\{0:D9}.ckpt" -f $at))) { Stop-Step "$src has no checkpoint at its stop, $at s, in $d" }
        if ($at -ge $ExtendTo) { Stop-Step "$src stopped at $at s, not before -ExtendTo $ExtendTo s" }
        $plan.At += $at
        $script:sourceDirs += $d
        Say "  $($plan.Names[$i]) resumes $d at $at s to $ExtendTo s"
    }
}
if ($Step -eq 'b3') {
    $whole = Manifest 'r61-p6101' 6101 $round
    $script:b3From = RunDir 'r61-p6101'
    if (-not (Test-Path "$script:b3From\checkpoints\000045000.ckpt")) { Stop-Step "r61-p6101 has no 45,000 s checkpoint at $script:b3From" }
    Say "  r61-p6101b resumes $script:b3From (configHash $($whole.configHash)) at 45,000 s"
}
if ($Step -eq 'lone') {
    # The source stopped (by the pace guard or stop-arm) on the extension's commit; it resumes at a checkpoint at or before its stop.
    $src = $plan.Sources[0]
    $d = RunDir $src
    if (-not $d) { Stop-Step "no run under $runs\$src" }
    $m = Get-Content "$d\run.json" -Raw | ConvertFrom-Json
    $g = $m.gpu
    $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
    Say "  ${src}: status $($m.status) ($($m.reason)) at $($m.simulatedSeconds) s, seed $($m.seed), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty), overflow $over, divergedTotal $($m.divergedTotal)"
    if ($m.status -ne 'stopped') { Stop-Step "$src did not stop; the lone step takes a world stopped at a checkpoint" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$round*") { Stop-Step "${src}'s manifest does not name a clean $round" }
    if ([long]$m.seed -ne $plan.Seeds[0]) { Stop-Step "${src}'s seed is $($m.seed), not $($plan.Seeds[0])" }
    if ($over -ne '0/0/0/0') { Say "  ${src}: overflow $over, the giants' known overflow; said, not stopped on" }
    $stop = [int][math]::Round([double]$m.simulatedSeconds)
    $at = if ($ResumeAt -gt 0) { $ResumeAt } else { $stop }
    if ($at -gt $stop) { Stop-Step "-ResumeAt $at s is past ${src}'s stop at $stop s" }
    if (-not (Test-Path ("$d\checkpoints\{0:D9}.ckpt" -f $at))) { Stop-Step "$src has no checkpoint at $at s in $d" }
    if ($at -ge $ExtendTo) { Stop-Step "$src resumes at $at s, not before -ExtendTo $ExtendTo s" }
    Settings $src $plan.Kinds[0] $script:landscapes[0]
    $script:loneFrom = $d
    $script:loneAt = $at
    $script:loneStop = $stop
    Say "  $($plan.Names[0]) resumes $d at $at s to $ExtendTo s; ${src}'s rows from $at to $stop s are its to match"
}
CardIdle
if ($PreflightOnly) { Say "r61 $tag preflight ends: nothing started"; exit 0 }

switch ($Step) {
    'mapcheck' {
        $blocks = Blocks
        Invoke-CardGroup $blocks
        $dirs = @($plan.Names | ForEach-Object { RunDir $_ })
        python -I "$tree\scripts\reads\r61-maps.py" @dirs 2>&1 | ForEach-Object { Say "  maps: $_" }
    }
    'pair3' { Invoke-CardGroup (Blocks) }
    'extend' { Invoke-CardGroup @() }
    'resume' { Invoke-CardGroup @() }
    'round' {
        Invoke-CardGroup (Blocks)
        if ($FoundingCheck) {
            $csv = "$logs\card-run-$($plan.Group)-gpu.csv"
            $peak = (Get-Content $csv | Where-Object { $_ -match '^\d\d:\d\d:\d\d, \d' } | ForEach-Object { [int](($_ -split ',\s*')[4] -replace ' MiB', '') } | Measure-Object -Maximum).Maximum
            # The card is shared: the peak is the card's whole, the other project's drawing and the desktop with the group.
            Say "  the card's memory at the group's peak: $peak MiB (ceiling $MemoryCeiling), $script:launchMemory at launch, so about $($peak - $script:launchMemory) MiB the group's if nothing else moved"
            $dirs = @($plan.Names | ForEach-Object { RunDir $_ })
            python -I "$tree\scripts\reads\r60-read.py" --end 1500 @dirs 2>&1 | ForEach-Object { Say "  read: $_" }
            if ($peak -gt $MemoryCeiling) { Stop-Step "the four held $peak MiB of the card, over $MemoryCeiling; run the round in pairs" }
        }
    }
    'b3' {
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', 'r61-p6101b', '-At', '45000', '-Seconds', '3000',
            '-ResumeFrom', $script:b3From, '-Group', '32', '-WallMinutes', '120', '-Mask', "$Mask", '-Threads', "$Threads",
            '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== r61-p6101b: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a 'card-run-r61-p6101b'
        Wait-Watch "$logs\card-run-r61-p6101b.log"
        $lines = Get-Content "$logs\card-run-r61-p6101b.log"
        $lines | Where-Object { $_ -match 'resum|ended \(|times real time|card: |books over|allowed beside|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step 'r61-p6101b did not end' }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm 'r61-p6101b' -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check r61-p6101b: $_" }
        if ($code -ne 0) { Stop-Step 'r61-p6101b failed a condition of card-run-check' }
        $m = Manifest 'r61-p6101b' 6101
        Say "  r61-p6101b: resumedFrom $($m.resumedFrom.arm)/$($m.resumedFrom.run) at $($m.resumedFrom.seconds) s, sourceMismatch $($m.resumedFrom.sourceMismatch)"
        if ($m.resumedFrom.sourceMismatch -ne $false) { Stop-Step 'r61-p6101b resumed on another build than the one that wrote its checkpoint' }
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 45000 $script:b3From (RunDir 'r61-p6101b') 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'r61-p6101 resumed at 45,000 s is not its whole run' }
    }
    'lone' {
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        $n = $plan.Names[0]
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $n, '-At', "$script:loneAt", '-Seconds', "$($ExtendTo - $script:loneAt)",
            '-ResumeFrom', $script:loneFrom, '-Group', '32', '-WallMinutes', "$($plan.Wall)", '-Mask', "$Mask", '-Threads', "$Threads",
            '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside',
            '-CheckpointEvery', "$($plan.Every)")
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== ${n}: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a "card-run-$n"
        Wait-Watch "$logs\card-run-$n.log"
        $lines = Get-Content "$logs\card-run-$n.log"
        $lines | Where-Object { $_ -match 'resum|ended \(|times real time|card: |books over|allowed beside|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$n did not end" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $n -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${n}: $_" }
        if ($code -ne 0) {
            # The p6103 line's giants: overflow and divergence are known and said; any other failed condition stops.
            $verdict = [regex]::Match((@($out | ForEach-Object { "$_" }) -join "`n"), 'VERDICT: FAILED on ([^\n]*)')
            $failed = @(if ($verdict.Success) { $verdict.Groups[1].Value -split ',\s*' | Where-Object { $_ } })
            $other = @($failed | Where-Object { $_ -notin 'overflow 0', 'diverged 0' })
            if ($failed.Count -eq 0 -or $other.Count -gt 0) { Stop-Step "$n failed a condition of card-run-check" }
            Say "  ${n}: card-run-check failed on $($failed -join ' and ') alone, the p6103 line's known giants; said, not stopped on"
        }
        # card-run writes the farm's output to <arm>.out, where card-group writes <arm>.log.
        $engine = Get-Content "$logs\$n.out" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${n}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${n}'s engine line lacks a switch" }
        $m = Manifest $n $plan.Seeds[0]
        Say "  ${n}: resumedFrom $($m.resumedFrom.arm)/$($m.resumedFrom.run) at $($m.resumedFrom.seconds) s, sourceMismatch $($m.resumedFrom.sourceMismatch), divergedTotal $($m.divergedTotal)"
        if ($m.resumedFrom.arm -ne $plan.Sources[0] -or [double]$m.resumedFrom.seconds -ne [double]$script:loneAt) { Stop-Step "$n did not resume $($plan.Sources[0]) at $script:loneAt s" }
        if ($m.resumedFrom.sourceMismatch -ne $false) { Stop-Step "$n resumed on another build than the one that wrote its checkpoint" }
        Settings $n $plan.Kinds[0] $script:landscapes[0]
        if ($script:loneAt -lt $script:loneStop) {
            python -I "$tree\scripts\reads\group-resume-compare.py" --lone "--until=$script:loneStop" $script:loneAt $script:loneFrom (RunDir $n) 2>&1 | ForEach-Object { Say "  resume compare: $_" }
            if ($LASTEXITCODE -ne 0) { Stop-Step "$n from $script:loneAt s is not $($plan.Sources[0]) to its stop at $script:loneStop s" }
        }
    }
}
python -I "$tree\scripts\reads\pace-calc.py" $runs @($plan.Names) 2>&1 | ForEach-Object { Say "  pace: $_" }
Say "r61 $tag ends"
