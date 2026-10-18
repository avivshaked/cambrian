# Round 60's fed-island screens (D175; proposals/propose-r60-patchy-matter.md §5; logbook/specs/r60-screens-plan.md):
# four arms on round 59's world (rounds/env-r60s-c.ps1, -i1, -i2, -i3), founded on the card in one group a seed, so
# that worlds of different settings share the card's process (card-group.ps1 -Launcher with one block a world).
# One step a call, on this checkout's Release build:
#   check   off the card: ILGPU's CPU accelerator founds the four arms on seed 1 as one group, 30 s with checkpoints
#           every 15 s, on 4 threads pinned to logical 16-19, then resumes the group at 15 s to 30 s. Each world's
#           resume is read against its whole run by group-resume-compare --lone. Meets the founded group's code
#           with every card switch, and the fed islands' influx and burial, before the card does.
#   short   the card: the seed 1 group founded for 60 s with no checkpoints. The group's memory on the card and its
#           pace; above -MemoryCeiling MiB at its peak the step stops, and the screens go in groups of two.
#   s1, s2  the card: the four arms on seed 1 (group r60s-g1), then on seed 2 (r60s-g2), founded, 10,000 s,
#           checkpoints at 5,000 and 10,000 s. Snapshots, field dumps and positions at the farm's own cadence
#           (every 100 s, 100 s and 10 s), which field-map.py and the 10 m-square reads take.
#   solo    the card: r60s-i1 alone on seed 1 for 1,000 s, the wall and memory r60s-big is set against (a world in
#           a group shares its wall). card-run.ps1 founds it, its configHash taken from r60s-i1-s1.
#   big     the card: r60s-big alone on seed 1 (rounds/env-r60s-big.ps1, world r60s-big-s1), the winner r60s-i2 at
#           four times the area, founded by card-run.ps1 (card-group.ps1 wants two worlds or more), 10,000 s,
#           checkpoints at 5,000 and 10,000 s, on s1's settings. Its configHash is taken from r60s-big-found, the same
#           block founded on the CPU for 100 s, at a commit whose src/ is this one's.
#   big2    the card: r60s-big2 alone on seed 1 (rounds/env-r60s-big2.ps1, world r60s-big2-s1, D181), big with its
#           dose scaled to the area: influx 4.8 units/s and 60,000 units of matter. As big in every other way, its
#           configHash taken from r60s-big2-found.
#   big3    the card: r60s-big3 alone on seed 1 (rounds/env-r60s-big3.ps1, world r60s-big3-s1, D185), big2's dose with
#           D185's glass margin of 30 m and D181's founders in the islands (D109's rule, D116's four switches off).
#   big3h   the card: r60s-big3h (rounds/env-r60s-big3h.ps1, world r60s-big3h-s1), big3 at half the dose: 2.4 units/s
#           and 30,000 units. Each takes its configHash from r60s-<step>-found, founded on the CPU for 100 s.
#   big3m   the card: r60s-big3m (rounds/env-r60s-big3m.ps1, world r60s-big3m-s1, D186), big3 at the middle dose:
#           3.6 units/s and 45,000 units, as big3 in every other way.
#   big3c   the card: r60s-big3-s1 run on from its 10,000 s checkpoint to 20,000 s (D186), on the build that ran it
#           (nothing under src/ changed since). First the resume is read on this world: r60s-big3r-s1 resumes the
#           5,000 s checkpoint for -CheckSeconds (5,000 by default, to 10,000 s), and group-resume-compare --lone reads
#           every row file against the unbroken run from 5,000 s; with -CheckSeconds 5000 its 10,000 s checkpoint is
#           set beside the unbroken run's, bytes and payload. Only if the rows are the same does r60s-big3c-s1 resume
#           the 10,000 s checkpoint for 10,000 s, checkpoints at 15,000 and 20,000 s.
#   big3mc  the card: r60s-big3m-s1 run on from its 10,000 s checkpoint to 20,000 s as r60s-big3mc-s1 (D187), on the
#           build that ran it, as big3c's run on; no check run first, big3c having shown the resume exact.
#   big3hc  the card: r60s-big3h-s1 run on the same way as r60s-big3hc-s1 (the owner's ruling, 2026-10-15 evening).
#   desert  the card: D190's two desert screens as one group, seed 1: r60s-d005-s1 and r60s-d02-s1
#           (rounds/env-r60s-d005.ps1 and -d02.ps1), r60s-big3m's world and dose with the desert's matter buried at
#           0.005/s and 0.02/s, founded, 20,000 s, checkpoints every 5,000 s.
#   fix     the card: round 60's build's resume, read on the card. r60s-i2 founded alone on seed 1 by card-run.ps1
#           (world r60s-fix-s1, its configHash taken from r60s-i2-s1; the preflight founds it again on this build and
#           must match), 2,000 s with a checkpoint at 1,500 s; then r60s-fix-r resumes that checkpoint alone at
#           1,500 s to 2,000 s, and group-resume-compare --lone reads it against its whole run from 1,500 s. Round
#           59's B3 resume of a settled card world parted at its first second; this one must read the same.
# -AllowBeside goes to card-group.ps1, card-run-check.ps1 and the wait below for every step but solo: the dotnet.exe
# processes that may run beside a group, by command line. Its default passes the nursery room's server, a nursery
# run on its own cores, and the other project's builds, runner and build-server shutdown. solo runs with nothing beside it, since
# its wall is a timing read, unless -SoloAllowBeside names what may (the coordinator kept the nursery room's server up
# on logical 8-11 for solo on 2026-10-15; it idles between requests).
# The card is shared with the other project's drawing from 2026-10-14 (the owner's wish, through the coordinator), except for solo,
# for which the coordinator holds the other project. Every card step runs after a minute's cooling: group 32, single precision,
# the snow's transport, the scent's fill, the concurrent mode and the spin at 5,000, 8 threads on logical 16-23, the
# driver watch and the heat stop.
# Before it runs: this checkout at -Commit and clean, a plain Release build, the names free, the card's memory under
# -CardMemoryLimit (12,000 MiB, room for the other project's drawing beside a group of about 7 GB on the card's 24,576; solo keeps
# 4,500, the owner's desktop alone, since its wall is the timing baseline), no batch-mode Unity but the other project's, and for
# solo the seed 1 group ended. After it: each world ended by its budget, books closed, its
# manifest naming the clean commit, card-run-check's every condition, overflow 0, and the round's three switches on
# the engine line. Any failure stops with exit 3. -PreflightOnly runs the checks alone and starts nothing.
# Log: scratch/logs/r60s-<step>.log in this checkout, last line "r60s <step> ends" or "r60s <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][ValidateSet('check', 'short', 's1', 's2', 'solo', 'big', 'big2', 'big3', 'big3h', 'big3m', 'big3c', 'big3mc', 'big3hc', 'desert', 'fix')][string]$Step,
    [ValidateRange(500, 5000)][int]$CheckSeconds = 5000,
    [int]$CardMemoryLimit = 12000,
    [int]$MemoryCeiling = 20000,
    [string]$AllowBeside = 'Evosim\.NurseryRoom\.dll|Evosim\.Nursery\.dll|D:\\Projects\\code\\|build-server shutdown',
    [string]$SoloAllowBeside = '',
    [switch]$PreflightOnly)
$beside = if ($Step -eq 'solo') { $SoloAllowBeside } else { $AllowBeside }
if ($Step -eq 'solo') { $CardMemoryLimit = [math]::Min($CardMemoryLimit, 4500) }
$otherProjectPath = 'D:[\\/]Projects[\\/]code[\\/]'
$ErrorActionPreference = 'Continue'
$tree = Split-Path -Parent $PSScriptRoot
$runs = "$tree\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$bigSteps = @('big', 'big2', 'big3', 'big3h', 'big3m')
$armKeys = if ($Step -in $bigSteps) { @($Step) } elseif ($Step -eq 'big3c') { @('big3') } elseif ($Step -eq 'big3mc') { @('big3m') } elseif ($Step -eq 'big3hc') { @('big3h') } elseif ($Step -eq 'fix') { @('i2') } elseif ($Step -eq 'desert') { @('d005', 'd02') } else { @('c', 'i1', 'i2', 'i3') }
$launchers = @($armKeys | ForEach-Object { "$tree\rounds\env-r60s-$_.ps1" })
$card = $Step -ne 'check'
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r60s-$Step-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r60s-$Step.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r60s $Step STOPPED: $why"; exit 3 }

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
    foreach ($l in $launchers) { if (-not (Test-Path $l)) { Stop-Step "no block at $l" } }
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
    # The other project's Unity shares the card from 2026-10-14, but not with solo; any other batch-mode Unity refuses.
    foreach ($u in @($unity | Where-Object { $_.CommandLine -match '-batchmode' })) {
        if ($Step -ne 'solo' -and "$($u.CommandLine)" -match $otherProjectPath) { Say "  allowed beside: the other project's Unity $($u.ProcessId)"; continue }
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
function Manifest([string]$name, [string]$want = $Commit) {
    $dir = RunDir $name
    if (-not $dir) { Stop-Step "no run under $runs\$name" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    Say "  ${name}: status $($m.status) ($($m.reason)), seed $($m.seed), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$want*") { Stop-Step "${name}'s manifest does not name a clean $want" }
    $g = $m.gpu
    $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
    if ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    return $m
}

# Each world's config holds its arm's settings: the matter's placement and doses, read back from config.json.
function Settings([string]$name, [string]$arm) {
    $text = Get-Content "$(RunDir $name)\config.json" -Raw
    $want = @{ c = 'Surface'; i1 = 'Islands'; i2 = 'Islands'; i3 = 'Islands'; big = 'Islands'; big2 = 'Islands'; big3 = 'Islands'; big3h = 'Islands'; big3m = 'Islands'; d005 = 'Islands'; d02 = 'Islands' }[$arm]
    $at = if ($text -match '"matterInfluxAt"\s*:\s*"(\w+)"') { $Matches[1] } else { '?' }
    $pick = { param($k) if ($text -match ('"' + $k + '"\s*:\s*([-0-9.eE]+)')) { $Matches[1] } else { '?' } }
    Say "  ${name}: matterInfluxAt $at, influx $(& $pick 'matterInfluxPerSecond'), burial $(& $pick 'matterBurialPerSecond'), sink $(& $pick 'matterSinkMetresPerSecond'), leafScentRange $(& $pick 'leafScentRangeMetres')"
    if ($arm -ne 'c' -and $at -ne $want) { Stop-Step "${name}'s matter is placed '$at', not '$want'" }
    if ($arm -in ($bigSteps + @('d005', 'd02'))) {
        $got = @{}
        foreach ($k in 'worldAreaSquareMetres', 'matterIslandWavelengthMetres', 'matterIslandCover', 'reefMaxCount', 'matterSinkMetresPerSecond', 'matterInfluxPerSecond', 'matterBudgetUnits') { $got[$k] = [double](& $pick $k) }
        Say "  ${name}: area $($got.worldAreaSquareMetres), island wavelength $($got.matterIslandWavelengthMetres), cover $($got.matterIslandCover), reefs $($got.reefMaxCount), sink $($got.matterSinkMetresPerSecond), influx $($got.matterInfluxPerSecond), budget $($got.matterBudgetUnits)"
        $want4 = @{ worldAreaSquareMetres = 88000; matterIslandWavelengthMetres = 90; matterIslandCover = 0.05; reefMaxCount = 256; matterSinkMetresPerSecond = 0.02
            matterInfluxPerSecond = @{ big = 1.2; big2 = 4.8; big3 = 4.8; big3h = 2.4; big3m = 3.6; d005 = 3.6; d02 = 3.6 }[$arm]; matterBudgetUnits = @{ big = 15000; big2 = 60000; big3 = 60000; big3h = 30000; big3m = 45000; d005 = 45000; d02 = 45000 }[$arm] }
        foreach ($k in $want4.Keys) { if ([math]::Abs($got[$k] - $want4[$k]) -gt 1e-6) { Stop-Step "${name}'s $k is $($got[$k]), not $($want4[$k])" } }
        if ($arm -in 'big3', 'big3h', 'big3m', 'd005', 'd02') {
            # D185's margin and D181's founders: D109's rule on, D116's four switches off.
            $margin = [double](& $pick 'matterIslandGlassMarginMetres')
            $flags = @{}
            foreach ($k in 'foundersFollowMatter', 'foundersFollowFood', 'foundersFollowFoodDepth', 'foundersFollowIncomeDepth', 'foundersFollowCorpses') {
                $flags[$k] = if ($text -match ('"' + $k + '"\s*:\s*(true|false)')) { $Matches[1] } else { '?' }
            }
            Say "  ${name}: glass margin $margin m; founders: follow matter $($flags.foundersFollowMatter), food $($flags.foundersFollowFood), food depth $($flags.foundersFollowFoodDepth), income depth $($flags.foundersFollowIncomeDepth), corpses $($flags.foundersFollowCorpses)"
            if ([math]::Abs($margin - 30) -gt 1e-6) { Stop-Step "${name}'s glass margin is $margin m, not 30" }
            if ($flags.foundersFollowMatter -ne 'true') { Stop-Step "${name}'s founders do not follow the matter" }
            foreach ($k in 'foundersFollowFood', 'foundersFollowFoodDepth', 'foundersFollowIncomeDepth', 'foundersFollowCorpses') {
                if ($flags[$k] -ne 'false') { Stop-Step "${name}'s $k is $($flags[$k]), not false" }
            }
        }
        if ($arm -in 'd005', 'd02') {
            # D190: the islands bury at 0.001/s, the desert at its screen's rate.
            $burial = [double](& $pick 'matterBurialPerSecond')
            $desert = [double](& $pick 'matterBurialDesertPerSecond')
            $wantDesert = @{ d005 = 0.005; d02 = 0.02 }[$arm]
            Say "  ${name}: burial $burial/s on the islands, $desert/s in the desert"
            if ([math]::Abs($burial - 0.001) -gt 1e-9) { Stop-Step "${name}'s island burial is $burial, not 0.001" }
            if ([math]::Abs($desert - $wantDesert) -gt 1e-9) { Stop-Step "${name}'s desert burial is $desert, not $wantDesert" }
        }
    }
}

# One founded or resumed group through card-group.ps1, then each world held to its conditions.
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

function Invoke-CardGroup([string]$group, [string[]]$names, [string]$seeds, [int]$seconds, [int]$every, [string]$device, [int]$threads, [long]$mask, [int]$wall, [int]$at, [string]$from) {
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
    $lines | Where-Object { $_ -match 'times real time|card r60s|farm exited|STOPPED|warning|books over' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card group ends$' })) { Stop-Step "$group did not end" }
    for ($k = 0; $k -lt $names.Count; $k++) {
        $n = $names[$k]
        if (-not ($lines | Where-Object { $_ -match "books over .* of ${n}: .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${n}'s books did not close" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $n -LogArm $group -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${n}: $_" }
        if ($code -ne 0) { Stop-Step "$n failed a condition of card-run-check" }
        $engine = Get-Content "$logs\$n.log" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${n}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${n}'s engine line lacks a switch" }
        $null = Manifest $n
        if ($at -eq 0) { Settings $n $armKeys[$k] }
    }
}

# One world resumed alone on the card by card-run.ps1 (-At, -Seconds, -ResumeFrom), held to big's conditions.
function Resume-Run([string]$arm, [int]$at, [int]$seconds, [string]$from, [int]$every, [int]$wall) {
    Pause-Cool; Wait-Quiet; CardIdle
    if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
    $since = (Get-Date).AddSeconds(-30)
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm, '-At', "$at", '-Seconds', "$seconds",
        '-ResumeFrom', $from, '-Group', '32', '-WallMinutes', "$wall", '-Mask', '16711680', '-Threads', '8', '-CheckpointEvery', "$every",
        '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
    if ($beside) { $a += @('-AllowBeside', $beside) }
    $env:EVOSIM_GPU_CONCURRENT = '1'
    $env:EVOSIM_GPU_SCENT = '1'
    Say "== ${arm}: card-run $($a[3..($a.Count - 1)] -join ' ')"
    Invoke-Pwsh $a "card-run-$arm"
    Wait-Watch "$logs\card-run-$arm.log"
    $lines = Get-Content "$logs\card-run-$arm.log"
    $lines | Where-Object { $_ -match 'resum|ended \(|times real time|card: |books over|STOPPED' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$arm did not end" }
    $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
    $code = $LASTEXITCODE
    $out | ForEach-Object { Say "  check ${arm}: $_" }
    if ($code -ne 0) { Stop-Step "$arm failed a condition of card-run-check" }
    $m = Manifest $arm
    Say "  ${arm}: resumedFrom $($m.resumedFrom.arm)/$($m.resumedFrom.run) at $($m.resumedFrom.seconds) s, sourceMismatch $($m.resumedFrom.sourceMismatch)"
    if ([math]::Abs([double]$m.resumedFrom.seconds - $at) -gt 1e-6) { Stop-Step "$arm did not resume at $at s" }
    if ($m.resumedFrom.sourceMismatch -ne $false) { Stop-Step "$arm resumed on another build than the one that wrote its checkpoint" }
}

# Two checkpoints of one second: their files' digests, and their payloads' after gunzip, since the header carries the
# stored length and a gzip member may carry what the world does not.
function Compare-Checkpoint([string]$a, [string]$b) {
    $ha = (Get-FileHash $a -Algorithm SHA256).Hash; $hb = (Get-FileHash $b -Algorithm SHA256).Hash
    Say "  checkpoint bytes: $(Split-Path -Leaf $a) $((Get-Item $a).Length) and $((Get-Item $b).Length) bytes, $(if ($ha -eq $hb) { 'the same file' } else { 'different files' })"
    python -I "$tree\scripts\reads\ckpt-compare.py" $a $b 2>&1 | ForEach-Object { Say "  checkpoint payload: $_" }
}

# The environment the scripts inherit: no EVOSIM_ setting at all; each world's settings are its block's.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
Say "r60s $Step begins: runs in $runs"
Tree
$plan = switch ($Step) {
    'check' { @{ Group = 'r60s-k'; Names = @($armKeys | ForEach-Object { "r60s-k-$_" }); Seeds = '1,1,1,1' } }
    'short' { @{ Group = 'r60s-q'; Names = @($armKeys | ForEach-Object { "r60s-q-$_" }); Seeds = '1,1,1,1' } }
    's1'    { @{ Group = 'r60s-g1'; Names = @($armKeys | ForEach-Object { "r60s-$_-s1" }); Seeds = '1,1,1,1' } }
    's2'    { @{ Group = 'r60s-g2'; Names = @($armKeys | ForEach-Object { "r60s-$_-s2" }); Seeds = '2,2,2,2' } }
    'solo'  { @{ Group = 'r60s-i1-solo'; Names = @('r60s-i1-solo'); Seeds = '1' } }
    'big'   { @{ Group = 'r60s-big-s1'; Names = @('r60s-big-s1'); Seeds = '1' } }
    'big2'  { @{ Group = 'r60s-big2-s1'; Names = @('r60s-big2-s1'); Seeds = '1' } }
    'big3'  { @{ Group = 'r60s-big3-s1'; Names = @('r60s-big3-s1'); Seeds = '1' } }
    'big3h' { @{ Group = 'r60s-big3h-s1'; Names = @('r60s-big3h-s1'); Seeds = '1' } }
    'big3m' { @{ Group = 'r60s-big3m-s1'; Names = @('r60s-big3m-s1'); Seeds = '1' } }
    'big3c' { @{ Group = 'r60s-big3r-s1'; Names = @('r60s-big3r-s1'); Seeds = '1' } }
    'big3mc' { @{ Group = 'r60s-big3mc-s1'; Names = @('r60s-big3mc-s1'); Seeds = '1' } }
    'big3hc' { @{ Group = 'r60s-big3hc-s1'; Names = @('r60s-big3hc-s1'); Seeds = '1' } }
    'desert' { @{ Group = 'r60s-desert'; Names = @('r60s-d005-s1', 'r60s-d02-s1'); Seeds = '1,1' } }
    'fix'   { @{ Group = 'r60s-fix-s1'; Names = @('r60s-fix-s1'); Seeds = '1' } }
}
if ($Step -in 'big3c', 'big3mc', 'big3hc') {
    if ($Step -eq 'big3c') { Free 'r60s-big3c-s1' @('r60s-big3c-s1') }
    # The run on is the same world only on the build that ran it: the world's commit an ancestor, nothing under src/
    # since, and its checkpoints present (big3c's check resumes 5,000 s as well as 10,000 s).
    $srcName = @{ big3c = 'r60s-big3-s1'; big3mc = 'r60s-big3m-s1'; big3hc = 'r60s-big3h-s1' }[$Step]
    $script:src = RunDir $srcName
    if (-not $script:src) { Stop-Step "no run under $runs\$srcName" }
    $m0 = Get-Content (Join-Path $script:src 'run.json') -Raw | ConvertFrom-Json
    $c0 = "$($m0.source.gitCommit)"
    git -C $tree merge-base --is-ancestor $c0 $Commit
    if ($LASTEXITCODE -ne 0) { Stop-Step "${srcName}'s commit $c0 is not an ancestor of $Commit" }
    git -C $tree diff --quiet $c0 $Commit -- src
    if ($LASTEXITCODE -ne 0) { Stop-Step "src/ changed between ${srcName}'s commit $c0 and $Commit" }
    $null = Manifest $srcName $c0
    $need = if ($Step -eq 'big3c') { @(5000, 10000) } else { @(10000) }
    foreach ($s in $need) { if (-not (Test-Path ("$script:src\checkpoints\{0:D9}.ckpt" -f $s))) { Stop-Step "$srcName has no checkpoint at $s s" } }
    Say "  $srcName at $script:src (commit $c0): checkpoints at $(($need | ForEach-Object { '{0:N0}' -f $_ }) -join ' and ') s$(if ($Step -eq 'big3c') { "; the check resumes 5,000 s for $CheckSeconds s" })"
}
if ($Step -eq 'fix') {
    Free 'r60s-fix-r' @('r60s-fix-r')
    # The build changed under src/ since s1, so the hash is only the expectation; card-run's preflight founds the
    # world on this build and refuses to start the card unless it matches.
    $script:soloHash = "$((Manifest 'r60s-i2-s1' ((Get-Content (Join-Path (RunDir 'r60s-i2-s1') 'run.json') -Raw | ConvertFrom-Json).source.gitCommit)).configHash)"
    Say "  r60s-fix-s1 takes r60s-i2-s1's configHash $script:soloHash"
}
Free $plan.Group $plan.Names
if ($Step -eq 'check') { Free 'r60s-kr' @($armKeys | ForEach-Object { "r60s-kr-$_" }) }
if ($Step -eq 'solo') {
    # s1 may have run at an earlier commit of this branch, so long as nothing under src/ has changed since.
    $m0 = Get-Content (Join-Path (RunDir 'r60s-i1-s1') 'run.json') -Raw | ConvertFrom-Json
    $c0 = "$($m0.source.gitCommit)"
    git -C $tree merge-base --is-ancestor $c0 $Commit
    if ($LASTEXITCODE -ne 0) { Stop-Step "r60s-i1-s1's commit $c0 is not an ancestor of $Commit" }
    git -C $tree diff --quiet $c0 $Commit -- src
    if ($LASTEXITCODE -ne 0) { Stop-Step "src/ changed between r60s-i1-s1's commit $c0 and $Commit" }
    $src = Manifest 'r60s-i1-s1' $c0
    $script:soloHash = $src.configHash
    Say "  r60s-i1-solo takes r60s-i1-s1's configHash $script:soloHash"
}
if ($Step -in $bigSteps) {
    $m0 = Get-Content (Join-Path (RunDir "r60s-$Step-found") 'run.json') -Raw | ConvertFrom-Json
    $c0 = "$($m0.source.gitCommit)"
    if ($m0.status -ne 'ended' -or $m0.source.gitDirty -ne $false) { Stop-Step "r60s-$Step-found is not a clean ended run" }
    git -C $tree merge-base --is-ancestor $c0 $Commit
    if ($LASTEXITCODE -ne 0) { Stop-Step "r60s-$Step-found's commit $c0 is not an ancestor of $Commit" }
    git -C $tree diff --quiet $c0 $Commit -- src "rounds/env-r60s-$Step.ps1" rounds/env-r60s-big.ps1 rounds/env-r60s-big2.ps1 rounds/env-r60s-big3.ps1 rounds/env-r60s-i2.ps1 rounds/env-r60s-i1.ps1 rounds/env-r60s.ps1 rounds/env-r57.ps1
    if ($LASTEXITCODE -ne 0) { Stop-Step "src/ or the $Step block changed between r60s-$Step-found's commit $c0 and $Commit" }
    $script:soloHash = "$($m0.configHash)"
    Say "  r60s-$Step-s1 takes r60s-$Step-found's configHash $script:soloHash (founded on the CPU at $c0)"
}
if ($card) { CardIdle }
if ($PreflightOnly) { Say "r60s $Step preflight ends: nothing started"; exit 0 }

switch ($Step) {
    'check' {
        Invoke-CardGroup 'r60s-k' $plan.Names $plan.Seeds 30 15 'cpu' 4 983040 120 0 ''
        $from = ($plan.Names | ForEach-Object { RunDir $_ }) -join ','
        Invoke-CardGroup 'r60s-kr' @($armKeys | ForEach-Object { "r60s-kr-$_" }) '' 15 15 'cpu' 4 983040 120 15 $from
        $pairs = @()
        foreach ($k in $armKeys) { $pairs += @((RunDir "r60s-k-$k"), (RunDir "r60s-kr-$k")) }
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 15 @pairs 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'a world resumed at 15 s is not its whole run' }
    }
    'short' {
        Invoke-CardGroup $plan.Group $plan.Names $plan.Seeds 60 0 'cuda' 8 16711680 20 0 ''
        $csv = "$logs\card-run-$($plan.Group)-gpu.csv"
        $peak = (Get-Content $csv | Where-Object { $_ -match '^\d\d:\d\d:\d\d, \d' } | ForEach-Object { [int](($_ -split ',\s*')[4] -replace ' MiB', '') } | Measure-Object -Maximum).Maximum
        # The card is shared: the peak is the card's whole, the other project's drawing and the desktop with the group.
        Say "  the card's memory at the group's peak: $peak MiB (ceiling $MemoryCeiling), $script:launchMemory at launch, so about $($peak - $script:launchMemory) MiB the group's if nothing else moved"
        if ($peak -gt $MemoryCeiling) { Stop-Step "four worlds held $peak MiB of the card; run the screens in groups of two" }
    }
    's1' { Invoke-CardGroup $plan.Group $plan.Names $plan.Seeds 10000 5000 'cuda' 8 16711680 240 0 '' }
    's2' { Invoke-CardGroup $plan.Group $plan.Names $plan.Seeds 10000 5000 'cuda' 8 16711680 240 0 '' }
    'desert' { Invoke-CardGroup $plan.Group $plan.Names $plan.Seeds 20000 5000 'cuda' 8 16711680 600 0 '' }
    { $_ -in $bigSteps } {
        $w = "r60s-$Step-s1"
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $w, '-At', '0', '-Seconds', '10000',
            '-Launcher', "$tree\rounds\env-r60s-$Step.ps1", '-Seed', '1', '-ConfigHash', $script:soloHash, '-Group', '32', '-WallMinutes', '240',
            '-Mask', '16711680', '-Threads', '8', '-CheckpointEvery', '5000',
            '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== ${w}: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a "card-run-$w"
        Wait-Watch "$logs\card-run-$w.log"
        $lines = Get-Content "$logs\card-run-$w.log"
        $lines | Where-Object { $_ -match 'preflight|config hash|ended \(|times real time|card: |books over|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$w did not end" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $w -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${w}: $_" }
        if ($code -ne 0) { Stop-Step "$w failed a condition of card-run-check" }
        $null = Manifest $w
        Settings $w $Step
    }
    'big3c' {
        # The check: big3's 5,000 s checkpoint resumed alone on the card, every row file read against the unbroken run.
        Resume-Run 'r60s-big3r-s1' 5000 $CheckSeconds $script:src 5000 120
        $check = RunDir 'r60s-big3r-s1'
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 5000 $script:src $check 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'r60s-big3-s1 resumed on the card at 5,000 s is not its unbroken run' }
        if ($CheckSeconds -eq 5000) {
            Compare-Checkpoint "$script:src\checkpoints\000010000.ckpt" "$check\checkpoints\000010000.ckpt"
            if ($LASTEXITCODE -ne 0) { Stop-Step "the resumed run's 10,000 s state is not the unbroken run's" }
        }
        # The run on: the unbroken run's own 10,000 s checkpoint, to 20,000 s.
        Resume-Run 'r60s-big3c-s1' 10000 10000 $script:src 5000 240
    }
    { $_ -in 'big3mc', 'big3hc' } {
        # D187 and the owner's ruling of the evening: big3m's or big3h's own 10,000 s checkpoint run on to 20,000 s,
        # as big3's was (its resume shown exact by big3c).
        Resume-Run "r60s-$Step-s1" 10000 10000 $script:src 5000 240
    }
    'fix' {
        # One card-run, founded (-At 0) or resumed, held to the same conditions as big's.
        function Fix-Run([string]$arm, [string[]]$how) {
            Pause-Cool; Wait-Quiet; CardIdle
            if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
            $since = (Get-Date).AddSeconds(-30)
            [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm) + $how + @('-Group', '32',
                '-WallMinutes', '30', '-Mask', '16711680', '-Threads', '8', '-CheckpointEvery', '1500',
                '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
            if ($beside) { $a += @('-AllowBeside', $beside) }
            $env:EVOSIM_GPU_CONCURRENT = '1'
            $env:EVOSIM_GPU_SCENT = '1'
            Say "== ${arm}: card-run $($a[3..($a.Count - 1)] -join ' ')"
            Invoke-Pwsh $a "card-run-$arm"
            Wait-Watch "$logs\card-run-$arm.log"
            $lines = Get-Content "$logs\card-run-$arm.log"
            $lines | Where-Object { $_ -match 'preflight|config hash|ended \(|times real time|card: |books over|STOPPED' } | ForEach-Object { Say "  $_" }
            if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$arm did not end" }
            $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
            $code = $LASTEXITCODE
            $out | ForEach-Object { Say "  check ${arm}: $_" }
            if ($code -ne 0) { Stop-Step "$arm failed a condition of card-run-check" }
            $null = Manifest $arm
        }
        Fix-Run 'r60s-fix-s1' @('-At', '0', '-Seconds', '2000', '-Launcher', "$tree\rounds\env-r60s-i2.ps1", '-Seed', '1', '-ConfigHash', $script:soloHash)
        Settings 'r60s-fix-s1' 'i2'
        $whole = RunDir 'r60s-fix-s1'
        Fix-Run 'r60s-fix-r' @('-At', '1500', '-Seconds', '500', '-ResumeFrom', $whole)
        python -I "$tree\scripts\reads\group-resume-compare.py" --lone 1500 $whole (RunDir 'r60s-fix-r') 2>&1 | ForEach-Object { Say "  resume compare: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-Step 'r60s-fix-s1 resumed on the card at 1,500 s is not its whole run' }
    }
    'solo' {
        Pause-Cool; Wait-Quiet; CardIdle
        if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
        $since = (Get-Date).AddSeconds(-30)
        [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', 'r60s-i1-solo', '-At', '0', '-Seconds', '1000',
            '-Launcher', "$tree\rounds\env-r60s-i1.ps1", '-Seed', '1', '-ConfigHash', $script:soloHash, '-Group', '32', '-WallMinutes', '30',
            '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
        if ($beside) { $a += @('-AllowBeside', $beside) }
        # card-run sets no card switch of its own but the transport and the spin; the farm inherits these two.
        $env:EVOSIM_GPU_CONCURRENT = '1'
        $env:EVOSIM_GPU_SCENT = '1'
        Say "== r60s-i1-solo: card-run $($a[3..($a.Count - 1)] -join ' ')"
        Invoke-Pwsh $a 'card-run-r60s-i1-solo'
        Wait-Watch "$logs\card-run-r60s-i1-solo.log"
        $lines = Get-Content "$logs\card-run-r60s-i1-solo.log"
        $lines | Where-Object { $_ -match 'preflight|config hash|ended \(|times real time|card: |books over|STOPPED' } | ForEach-Object { Say "  $_" }
        if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step 'r60s-i1-solo did not end' }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm 'r60s-i1-solo' -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check r60s-i1-solo: $_" }
        if ($code -ne 0) { Stop-Step 'r60s-i1-solo failed a condition of card-run-check' }
        $null = Manifest 'r60s-i1-solo'
    }
}
if ($Step -ne 'check') {
    # @() keeps a one-name list a list: unrolled to a string, the splat below handed python its letters.
    $names = @(if ($Step -eq 'solo') { 'r60s-i1-solo' } elseif ($Step -eq 'fix') { 'r60s-fix-s1', 'r60s-fix-r' } elseif ($Step -eq 'big3c') { 'r60s-big3r-s1', 'r60s-big3c-s1' } else { $plan.Names })  # big's plan names r60s-big-s1
    python -I "$tree\scripts\reads\pace-calc.py" $runs @names 2>&1 | ForEach-Object { Say "  pace: $_" }
}
Say "r60s $Step ends"
