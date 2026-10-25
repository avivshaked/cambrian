# Round 62's dial screens (D201): six cousins of round 61's planted worlds a group, each resumed from its source's
# 30,000 s checkpoint to 36,000 s with the founding trickle off (its rate and pool share to 0), on this checkout's
# build (branch r61-cousin). -Set names the six: two dials, each on seeds 6101, 6102 and 6103, as r62d-<dial>-<seed>.
# A dial is one line of the sources' config.json changed, in rounds/r62d/config-<dial>.json:
#   u    none (the unchanged cousin: the trickle off and nothing else)
#   a1   the consumer cell's upkeep 6 -> 3 W/m3     a3  6 -> 0.75                       (dial A)
#   b1   the mouth's intakeReachMetres 0.5 -> 1, the consumer's upkeep at 6            (dial B)
# Sets: g1 = u, a1 (D201); g2ab = a3, b1 (D203: dial A's strongest step and dial B's first in one group).
# The sources are r61-p6101, r61-p6102 and r61-p6103, each the run under its arm folder that holds 000030000.ckpt; the
# three share one config.json, text for text. One step a call:
#   check   off the card: each of the six resumed on the CPU solver for -CheckSeconds (30 by default), one after
#           another through run-farm.ps1, 8 threads, in this checkout's scratch\r62d-check\runs, each named with a
#           -cpu suffix (r62d-u-6101-cpu) so that its logs never take a card world's name. The script pins
#           itself to -CheckMask (logical 24-31) at BelowNormal, and the farm inherits both; the nvcuda guard keeps
#           and proves it off the card. Each run must be marked a cousin of its source's config hash with exactly
#           its expected changes, and its config.json must hold them. -AllowDirty lets it run from uncommitted changes.
#   group   the card: the six as one group, r62d-<set>, checkpoints every 1,500 s, runs in the main checkout's runs\.
#   half1, half2  the card: the same as two groups of three, r62d-<set>a (the first dial) and r62d-<set>b (the
#           second), for when the six do not fit the card's memory.
# Every card step uses round 61's card settings: group 32, single precision, the snow's transport, the scent's fill, the
# concurrent mode and the spin at 5,000, -Threads threads pinned by -Mask (8 on logical 16-23 by default), the driver
# watch and the heat stop, after a minute's cooling, with -AllowSourceMismatch (the sources ran on round 61's
# commits; the coordinator's gates proved r61-merged's build steps them as the originals did), and every world given
# -WaitForManifestSeconds to write run.json, since a world of thousands takes over a minute to load.
# Before a step: this checkout at -Commit and clean, a plain Release build no older than its sources, the sources'
# checkpoints and configs, each dial's config one line from theirs, the names free, and for a card step the card's memory
# under -CardMemoryLimit and the machine quiet. After a card step: each world ended by its budget, books closed,
# card-run-check's every condition, the three card switches on its engine line, overflow 0, and its manifest naming
# the clean commit, its source at 30,000 s, a cousin of the source's config hash with its expected changes; the
# card's peak memory against -MemoryCeiling. Any failure stops with exit 3. -PreflightOnly runs the checks alone.
# Log: scratch/logs/r62d-<step>.log in this checkout for g1, r62d-<set>-<step>.log for the others, last line
# "r62d <step> ends" or "r62d <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [ValidateSet('check', 'group', 'half1', 'half2')][string]$Step = 'check',
    # Which six: g1 (u, a1) or g2ab (a3, b1), each dial on the three seeds.
    [ValidateSet('g1', 'g2ab')][string]$Set = 'g1',
    # The check step only: run from a tree with uncommitted changes, said in the log. A card step never takes it.
    [switch]$AllowDirty,
    [ValidateRange(1, 600)][int]$CheckSeconds = 30,
    [int]$Until = 36000,
    [int]$Every = 1500,
    [int]$WallMinutes = 360,
    [int]$WaitForManifestSeconds = 180,
    [int]$Threads = 8,
    [long]$Mask = 16711680,
    [long]$CheckMask = 4278190080,
    [int]$CardMemoryLimit = 12000,
    [int]$MemoryCeiling = 20000,
    [string]$AllowBeside = 'Evosim\.NurseryRoom\.dll|Evosim\.Nursery\.dll|D:\\Projects\\code\\|build-server shutdown',
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = Split-Path -Parent $PSScriptRoot
$sourceRuns = "$main\runs"
$runs = if ($Step -eq 'check') { "$tree\scratch\r62d-check\runs" } else { "$main\runs" }
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$beside = $AllowBeside
$at = 30000
New-Item -ItemType Directory -Force $logs | Out-Null
# g1's logs keep their first names (r62d-check.log, r62d-group.log); every other set's carry it (r62d-g2ab-check.log).
$tag = if ($Set -eq 'g1') { $Step } else { "$Set-$Step" }
$log = if ($PreflightOnly) { "$logs\r62d-$tag-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r62d-$tag.log" }
if ($AllowDirty -and $Step -ne 'check') { Write-Host '-AllowDirty is for the check step alone'; exit 1 }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r62d $Step STOPPED: $why"; exit 3 }

# The six worlds: name, source arm, and whether it carries dial A step 1.
# Each dial a world can carry: the field it moves, from what to what, in rounds/r62d/config-<dial>.json. u is none.
$dials = @{
    u  = $null
    a1 = @{ Field = 'consumer'; Was = '6'; Now = '3' }
    a3 = @{ Field = 'consumer'; Was = '6'; Now = '0.75' }
    b1 = @{ Field = 'reach'; Was = '0.5'; Now = '1' }
}
# How each field is found in a config's text, how it is written, and how CousinConfig names its change.
$fields = @{
    consumer = @{ Pattern = '("id": "consumer",\n\s*"upkeepWattsPerCubicMetre": )'; Read = '"id":\s*"consumer",\s*"upkeepWattsPerCubicMetre":\s*([-0-9.eE]+)'; Change = 'cellTypes.consumer.upkeepWattsPerCubicMetre' }
    reach    = @{ Pattern = '("intakeReachMetres": )'; Read = '"intakeReachMetres"\s*:\s*([-0-9.eE]+)'; Change = 'mouth.intakeReachMetres' }
}
$pair = @{ g1 = @('u', 'a1'); g2ab = @('a3', 'b1') }[$Set]
$all = @(foreach ($d in $pair) { foreach ($seed in 6101, 6102, 6103) { @{ Name = "r62d-$d-$seed"; Source = "r61-p$seed"; Seed = $seed; Dial = $d } } })
$worlds = switch ($Step) { 'half1' { @($all[0..2]) } 'half2' { @($all[3..5]) } default { $all } }
# The check's worlds carry a suffix, so that their logs never take a card world's names.
# Beyond g1 the suffix names the set too, since two sets may share a dial (r62d-a3-6101-g2ab-cpu).
$suffix = if ($Set -eq 'g1') { '-cpu' } else { "-$Set-cpu" }
if ($Step -eq 'check') { $worlds = @($worlds | ForEach-Object { $c = $_.Clone(); $c.Name = "$($_.Name)$suffix"; $c }) }
$group = switch ($Step) { 'half1' { "r62d-$($Set)a" } 'half2' { "r62d-$($Set)b" } default { "r62d-$Set" } }
function ConfigOf($w) { if ($dials[$w.Dial]) { "$tree\rounds\r62d\config-$($w.Dial).json" } else { '' } }
# What every cousin's resumedFrom.changes must hold, as CousinConfig writes them.
$trickleChanges = @('population.foundingTricklePerSecond: 0.033333335 -> 0', 'population.foundingTricklePoolShare: 0.6 -> 0')
function Expected($w) {
    $d = $dials[$w.Dial]
    if ($d) { @($trickleChanges) + @("$($fields[$d.Field].Change): $($d.Was) -> $($d.Now)") } else { @($trickleChanges) }
}

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
function RunDir([string]$root, [string]$name) { (Get-ChildItem "$root\$name" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }
function Lines([string]$path) { @([IO.File]::ReadAllText($path) -split "\r?\n") }

function Tree {
    $head = git -C $tree rev-parse --short HEAD
    Say "  $tree on $(git -C $tree rev-parse --abbrev-ref HEAD) at $head, asked for $Commit"
    if ($head -ne $Commit) { Stop-Step "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain --untracked-files=no
    if ($dirty -and $AllowDirty) { Say "  DIRTY, allowed for this check (-AllowDirty): $($dirty -join '; ')" }
    elseif ($dirty) { Stop-Step "DIRTY: $($dirty -join '; ')" }
    if (-not (Test-Path $exe)) { Stop-Step "no Release farm at $exe" }
    $cardProbe = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: card probe $cardProbe, O2 $hasO2; $script:hashes"
    if ($cardProbe -or $hasO2) { Stop-Step 'not the plain build' }
    $built = (Get-Item $exe).LastWriteTime
    $newer = @(Get-ChildItem "$tree\src" -Recurse -Include *.cs, *.csproj -File | Where-Object { $_.LastWriteTime -gt $built -and $_.FullName -notmatch '\\(bin|obj)\\' })
    if ($newer) { Stop-Step "the build is older than $($newer.Count) source files, $($newer[0].FullName) among them; rebuild" }
}

# Each source's run holding the 30,000 s checkpoint; the three configs one text; the a1 config that text with one line changed.
function Sources {
    $script:sourceDirs = @{}
    $base = $null
    foreach ($arm in @($worlds | ForEach-Object { $_.Source } | Sort-Object -Unique)) {
        $d = @(Get-ChildItem "$sourceRuns\$arm" -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path "$($_.FullName)\checkpoints\000030000.ckpt" })
        if ($d.Count -ne 1) { Stop-Step "$arm has $($d.Count) runs holding a 30,000 s checkpoint, not one" }
        $dir = $d[0].FullName
        $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
        Say "  ${arm}: $dir, configHash $($m.configHash), seed $($m.seed), source.gitCommit $($m.source.gitCommit), status $($m.status)"
        $text = (Lines "$dir\config.json") -join "`n"
        if ($null -eq $base) { $base = $text; $script:sourceHash = $m.configHash }
        elseif ($text -cne $base) { Stop-Step "${arm}'s config.json is not the other sources' text" }
        if ($m.configHash -ne $script:sourceHash) { Stop-Step "${arm}'s configHash $($m.configHash) is not $script:sourceHash" }
        $script:sourceDirs[$arm] = $dir
    }
    Say "  sources' configHash $script:sourceHash"
    foreach ($dial in $pair) {
        $d = $dials[$dial]
        if (-not $d) { continue }
        $path = "$tree\rounds\r62d\config-$dial.json"
        if (-not (Test-Path $path)) { Stop-Step "no $dial config at $path" }
        $f = $fields[$d.Field]
        $find = $f.Pattern + [regex]::Escape($d.Was) + ','
        if (([regex]::Matches($base, $find)).Count -ne 1) { Stop-Step "the sources' config does not hold its $($d.Field) at $($d.Was) once" }
        $want = [regex]::Replace($base, $find, '${1}' + $d.Now + ',')
        if (((Lines $path) -join "`n") -cne $want) { Stop-Step "$path is not the sources' config with $($f.Change) $($d.Was) -> $($d.Now) and nothing else" }
        Say "  ${dial}: $path is their text with $($f.Change) $($d.Was) -> $($d.Now)"
    }
}

function Free([string]$g, [string[]]$names) {
    $paths = @("$logs\card-run-$g.log", "$logs\card-run-$g-watch.log", "$logs\card-run-$g-gpu.csv")
    foreach ($n in $names) { $paths += @("$runs\$n", "$runs\$n.md", "$logs\$n.args", "$logs\$n.log", "$logs\$n.err", "$logs\$n.out") }
    foreach ($p in $paths) { if (Test-Path $p) { Stop-Step "a name is used: $p exists" } }
    Say "  names free: $(if ($Step -ne 'check') { "$g; " })$($names -join ', ')"
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

# A finished world held to its cousin's conditions: its source at 30,000 s, the source's config hash, its changes,
# and its config.json holding them.
function Cousin($w, [string]$root, [bool]$card) {
    $dir = RunDir $root $w.Name
    if (-not $dir) { Stop-Step "no run under $root\$($w.Name)" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    $rf = $m.resumedFrom
    Say "  $($w.Name): status $($m.status) ($($m.reason)), seed $($m.seed), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    Say "  $($w.Name): resumedFrom $($rf.arm)/$($rf.run) at $($rf.seconds) s, cousin $($rf.cousin), sourceConfigHash $($rf.sourceConfigHash), sourceMismatch $($rf.sourceMismatch); changes: $(@($rf.changes) -join '; ')"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$($w.Name) did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Step "$($w.Name)'s manifest does not name a clean $Commit" }
    if ([long]$m.seed -ne $w.Seed) { Stop-Step "$($w.Name)'s seed is $($m.seed), not $($w.Seed)" }
    if ($rf.arm -ne $w.Source -or [double]$rf.seconds -ne [double]$at) { Stop-Step "$($w.Name) did not resume $($w.Source) at $at s" }
    if ($rf.cousin -ne $true -or $rf.sourceConfigHash -ne $script:sourceHash -or $m.configHash -eq $script:sourceHash) { Stop-Step "$($w.Name) is not marked a cousin of $script:sourceHash" }
    $want = @(Expected $w | Sort-Object)
    $got = @(@($rf.changes) | Sort-Object)
    if (($want -join "`n") -cne ($got -join "`n")) { Stop-Step "$($w.Name)'s changes are '$($got -join '; ')', not '$($want -join '; ')'" }
    $text = Get-Content "$dir\config.json" -Raw
    $pick = { param($key) if ($text -match ('"' + $key + '"\s*:\s*("?[-0-9A-Za-z.eE]+"?)')) { $Matches[1].Trim('"') } else { '?' } }
    $rate = & $pick 'foundingTricklePerSecond'
    $share = & $pick 'foundingTricklePoolShare'
    $read = @{}
    foreach ($k in $fields.Keys) { $read[$k] = if ($text -match $fields[$k].Read) { [double]$Matches[1] } else { -1 } }
    Say "  $($w.Name): config.json trickle $rate, pool share $share, consumer upkeep $($read.consumer), intake reach $($read.reach)"
    if ([double]$rate -ne 0 -or [double]$share -ne 0) { Stop-Step "$($w.Name)'s config.json keeps the trickle on" }
    $d = $dials[$w.Dial]
    foreach ($k in $fields.Keys) {
        $base = @{ consumer = 6; reach = 0.5 }[$k]
        $want = if ($d -and $d.Field -eq $k) { [double]$d.Now } else { $base }
        if ($read[$k] -ne $want) { Stop-Step "$($w.Name)'s $k is $($read[$k]), not $want" }
    }
    if ($card) {
        $g = $m.gpu
        $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
        if ($over -ne '0/0/0/0') { Stop-Step "$($w.Name)'s engine dropped contacts (overflow $over); the run is unreadable" }
    }
}

# A card script run to its end, waited on as a process (rounds/r61.ps1's reasons).
function Invoke-Pwsh([string[]]$a, [string]$tag) {
    $q = @($a | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $c = Start-Process pwsh -ArgumentList $q -WindowStyle Hidden -PassThru -RedirectStandardOutput "$logs\$tag.console" -RedirectStandardError "$logs\$tag.console.err"
    $c.WaitForExit()
}

function Wait-Watch([string]$runLog) {
    $line = Get-Content $runLog -ErrorAction SilentlyContinue | Where-Object { $_ -match 'watch pid (\d+):' } | Select-Object -First 1
    if (-not $line) { return }
    $id = [int]([regex]::Match($line, 'watch pid (\d+):').Groups[1].Value)
    $d = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $d -and (Get-Process -Id $id -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 2 }
    Say "  watch pid ${id}: $(if (Get-Process -Id $id -ErrorAction SilentlyContinue) { 'still running after 3 minutes' } else { 'ended' })"
}

function Invoke-Check {
    $me = [System.Diagnostics.Process]::GetCurrentProcess()
    $me.ProcessorAffinity = [IntPtr]$CheckMask
    $me.PriorityClass = 'BelowNormal'
    . "$tree\scripts\card\nvcuda-guard.ps1"
    Enter-OffCard
    if ($env:CUDA_VISIBLE_DEVICES -ne '-1') { Stop-Step 'the guard did not hide the card' }
    Say "  check: pinned to $($me.ProcessorAffinity) at $($me.PriorityClass), $CheckSeconds s a world on the CPU solver"
    New-Item -ItemType Directory -Force $runs | Out-Null
    foreach ($w in $worlds) {
        $p = @{ Arm = $w.Name; Seconds = $at + $CheckSeconds; Threads = 8; RunsRoot = $runs; ResumeFrom = "$($script:sourceDirs[$w.Source])\checkpoints\000030000.ckpt"
                At = $at; AllowSourceMismatch = $true; TrickleOff = $true; WallMinutes = 30; WaitForManifestSeconds = $WaitForManifestSeconds; Exe = $exe; Env = @{ EVOSIM_ENGINE = 'cpu' } }
        if (ConfigOf $w) { $p.ResumeConfig = ConfigOf $w }
        $said = @(& "$tree\scripts\run-farm.ps1" @p *>&1 | ForEach-Object { "$_" })
        $said | ForEach-Object { Say "    $($w.Name)> $_" }
        if ($said -match 'exited \(code|refused|Exception') { Stop-Step "$($w.Name) did not start; read $logs\$($w.Name).err" }
        $deadline = (Get-Date).AddMinutes(35)
        $status = 'running'
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 10
            Test-OffCardTree $deadline
            if ($script:OffCardHits.Count -gt 0) { & "$tree\scripts\stop-arm.ps1" $w.Name -RunsRoot $runs -Reason manual-other; Stop-Step "$($w.Name): $(Resolve-OffCard)" }
            $d = RunDir $runs $w.Name
            if (-not $d -or -not (Test-Path "$d\run.json")) { continue }
            $status = (Get-Content "$d\run.json" -Raw | ConvertFrom-Json).status
            if ($status -ne 'running') { break }
        }
        if ($status -eq 'running') { & "$tree\scripts\stop-arm.ps1" $w.Name -RunsRoot $runs -Reason manual-other; Stop-Step "$($w.Name) ran past its deadline" }
        Cousin $w $runs $false
    }
    Say "  $(Resolve-OffCard)"
}

function Invoke-Group {
    Pause-Cool; Wait-Quiet; CardIdle
    if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
    $since = (Get-Date).AddSeconds(-30)
    $names = @($worlds | ForEach-Object { $_.Name })
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-group.ps1", '-Name', $group, '-Arms', ($names -join ','),
        '-At', "$at", '-Until', "$Until", '-ResumeFrom', (@($worlds | ForEach-Object { $script:sourceDirs[$_.Source] }) -join ','),
        '-ResumeConfig', (@($worlds | ForEach-Object { if (ConfigOf $_) { ConfigOf $_ } else { 'none' } }) -join ','), '-TrickleOff',
        '-WaitForManifestSeconds', "$WaitForManifestSeconds", '-CheckpointEvery', "$Every",
        '-Group', '32', '-WallMinutes', "$WallMinutes", '-Mask', "$Mask", '-Threads', "$Threads",
        '-Transport', '-Scent', '-Concurrent', '-SpinLimit', 'default', '-RunsRoot', $runs, '-Exe', $exe, '-Device', 'cuda',
        '-SampleCpu', '-SampleCores', '-HeatStop', '-LogOutside')
    if ($beside) { $a += @('-AllowBeside', $beside) }
    Say "== ${group}: card-group $($a[3..($a.Count - 1)] -join ' ')"
    Invoke-Pwsh $a "card-group-$group"
    Wait-Watch "$logs\card-run-$group.log"
    $lines = Get-Content "$logs\card-run-$group.log"
    $lines | Where-Object { $_ -match 'times real time|farm exited|STOPPED|warning|books over|allowed beside' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card group ends$' })) { Stop-Step "$group did not end" }
    $csv = "$logs\card-run-$group-gpu.csv"
    $peak = (Get-Content $csv | Where-Object { $_ -match '^\d\d:\d\d:\d\d, \d' } | ForEach-Object { [int](($_ -split ',\s*')[4] -replace ' MiB', '') } | Measure-Object -Maximum).Maximum
    Say "  the card's memory at the group's peak: $peak MiB (ceiling $MemoryCeiling), $script:launchMemory at launch, so about $($peak - $script:launchMemory) MiB the group's if nothing else moved"
    foreach ($w in $worlds) {
        $n = $w.Name
        $given = @(Get-Content "$logs\$n.args")
        if (-not ($given -match '^EVOSIM_RESUME_TRICKLE_OFF=1$') -or [bool]($given -match '^EVOSIM_RESUME_CONFIG=') -ne [bool](ConfigOf $w)) { Stop-Step "${n}'s settings do not carry its cousin's switches" }
        if (-not ($lines | Where-Object { $_ -match "books over .* of ${n}: .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${n}'s books did not close" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $n -LogArm $group -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs -AllowBeside $beside *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${n}: $_" }
        if ($code -ne 0) { Stop-Step "$n failed a condition of card-run-check" }
        $engine = Get-Content "$logs\$n.log" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${n}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${n}'s engine line lacks a switch" }
        Cousin $w $runs $true
    }
    if ($peak -gt $MemoryCeiling) { Stop-Step "the group held $peak MiB of the card, over $MemoryCeiling" }
}

# The environment the scripts inherit: no EVOSIM_ setting at all.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
Say "r62d $Step begins: runs in $runs, $(@($worlds | ForEach-Object { $_.Name }) -join ', '), from $at s to $(if ($Step -eq 'check') { $at + $CheckSeconds } else { $Until }) s, allowed beside: $(if ($beside) { $beside } else { 'nothing' })"
Tree
Sources
Free $group @($worlds | ForEach-Object { $_.Name })
if ($Step -ne 'check') { CardIdle }
if ($PreflightOnly) { Say "r62d $Step preflight ends: nothing started"; exit 0 }
if ($Step -eq 'check') { Invoke-Check } else { Invoke-Group }
Say "r62d $Step ends"
