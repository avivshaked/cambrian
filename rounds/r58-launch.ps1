# Round 58's launch (logbook/0138, D159): six worlds of round 57's world on the graphics card, single precision, from
# rounds\env-r57.ps1 with seeds 1-3, run by scripts\card\card-group.ps1 one group at a time:
#   join  r58-join: r58-s1j, r58-s2j, r58-s3j, founded, trading 3 genomes every 3,000 s in a ring (seed 1 to 2 to 3 to 1)
#   iso   r58-iso:  r58-s1i, r58-s2i, r58-s3i, founded, the same seeds and settings with no exchange (the twins)
#   b2    r58-b2:   r58-s1jb, r58-s2jb, r58-s3jb, r58-join resumed from its 45,000 s checkpoint to 48,000 s (B2)
# Every group runs 48,000 s (b2: 3,000 s), checkpoints every 1,500 s, group 32, the snow's transport, the scent's
# fill and the concurrent mode on the card, the spin at 5,000, a wall of -WallMinutes (b2: 30), -HeatStop, the
# driver watch, and the processor and core samples; the runs go to the main checkout's runs\.
#
# Before anything runs: this tree is r58-runner at -Commit, clean (any change anywhere refuses: DIRTY), the commit
# holds logbook/0138, its src, scripts (scripts/reads aside) and unity are 02c448e6's (the build every screen and check ran), and the farm's
# binaries are check58's pins of 02c448e6. Then, before each group: the binaries unchanged, a minute's cooling to a
# card under 60 C with temp-check OK, a quiet machine (no farm, test host or dotnet: the room server must be down),
# the card's memory under 4,500 MiB (8,000 beside Resolve) and no batch-mode Unity.
# After each group, for every world: an ending by budget (or a note if the wall cut it, which stops the chain),
# books closed, card-run-check's every condition, the engine line's three switches, the card under 80 C and the CPU
# under 85 C. A driver event stops the group through card-group's watch, and the chain with it.
# After b2: group-resume-compare.py from 45,000 s, joined against resumed. The twins' row-for-row read to the first
# exchange and the round's reader run afterwards, off the card.
# -Steps picks which groups to run, in this order ('join,iso,b2' by default), so a chain stopped between groups is
# restarted from the next. -PreflightOnly runs the checks alone and touches nothing on the card.
# Log: scratch/logs/r58-launch.log in the main checkout, last line "r58 launch ends" or "r58 launch STOPPED: <why>".
param([Parameter(Mandatory)][string]$Commit, [string]$Steps = 'join,iso,b2', [int]$WallMinutes = 360, [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = Split-Path -Parent $PSScriptRoot
$runs = "$main\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$built = '02c448e6'
$pins = 'Evosim.Farm.exe dacb75e884ba1331, Evosim.Farm.dll 9265ac97886d6c3c, Evosim.Farm.Gpu.dll 678925814379c4c4, Evosim.Core.dll 20c86289650094f5, Evosim.Dynamics.dll 57de011f59186d43'
$log = if ($PreflightOnly) { "$main\scratch\logs\r58-launch-preflight-$(Get-Date -Format yyyyMMdd-HHmmss).log" } else { "$main\scratch\logs\r58-launch.log" }
if (Test-Path $log) { Add-Content $log "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') r58 launch resumes with -Steps $Steps" }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Launch([string]$why) { Say "r58 launch STOPPED: $why"; exit 3 }
$groups = [ordered]@{
    join = @{ Name = 'r58-join'; Arms = @('r58-s1j', 'r58-s2j', 'r58-s3j') }
    iso  = @{ Name = 'r58-iso'; Arms = @('r58-s1i', 'r58-s2i', 'r58-s3i') }
    b2   = @{ Name = 'r58-b2'; Arms = @('r58-s1jb', 'r58-s2jb', 'r58-s3jb') }
}
$wanted = @($Steps -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($wanted.Count -eq 0 -or @($wanted | Where-Object { $_ -notin $groups.Keys }).Count -gt 0) { Stop-Launch "-Steps '$Steps' names a step other than join, iso and b2" }

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}

# The tree, the commit and the build.
function Tree {
    $head = git -C $tree rev-parse --short HEAD
    $branch = git -C $tree rev-parse --abbrev-ref HEAD
    Say "  $tree on $branch at $head, asked for $Commit"
    if ($branch -ne 'r58-runner') { Stop-Launch "$tree is on $branch, not r58-runner" }
    if ($head -ne $Commit) { Stop-Launch "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain
    if ($dirty) { Stop-Launch "DIRTY: $($dirty -join '; ')" }
    if (-not (git -C $tree ls-tree --name-only HEAD logbook/ | Where-Object { $_ -match '^logbook/0138' })) { Stop-Launch "$Commit holds no logbook/0138" }
    # The round's reads (scripts/reads/) come with the entry and run nothing on the card; everything else is the build's.
    $code = @('src', 'scripts', 'unity', ':(exclude)scripts/reads')
    git -C $tree diff --quiet $built HEAD -- @code
    if ($LASTEXITCODE -ne 0) { Stop-Launch "src, scripts or unity differ from $built, the build the screens ran: $((git -C $tree diff --name-only $built HEAD -- @code) -join ', ')" }
    Say "  clean; logbook/0138 present; src, scripts (but scripts/reads) and unity are ${built}'s"
    if (-not (Test-Path $exe)) { Stop-Launch "$tree has no farm build" }
    $hasCycle = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $scentOrder = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-scent-order'
    $scent = Has "$bin\Evosim.Farm.dll" 'EVOSIM_GPU_SCENT'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: cycle probe $hasCycle, scent order probe $scentOrder, O2 $hasO2, binds EVOSIM_GPU_SCENT $scent; $script:hashes"
    if ($hasCycle -or $scentOrder -or $hasO2) { Stop-Launch 'not a plain build' }
    if (-not $scent) { Stop-Launch 'the build does not bind EVOSIM_GPU_SCENT' }
    if ($script:hashes -ne $pins) { Stop-Launch "the binaries are not check58's pins of ${built}: $pins" }
    Say "  binaries match check58's pins of $built"
}

function Free([string]$step) {
    $g = $groups[$step]
    foreach ($arm in @($g.Name) + $g.Arms) {
        foreach ($p in "$logs\card-run-$arm.log", "$logs\card-run-$arm-watch.log", "$runs\$arm", "$runs\$arm.md", "$logs\$arm.out", "$logs\$arm.err",
                       "$logs\$arm.log", "$logs\$arm.args", "$logs\card-run-$arm-gpu.csv", "$logs\card-group-$arm.out") {
            if (Test-Path $p) { Stop-Launch "the name $arm is used: $p exists" }
        }
    }
    Say "  names free for ${step}: $(@($g.Name) + $g.Arms -join ', ')"
}

function CardIdle([bool]$refuse) {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    $limit = if ($resolve.Count -gt 0) { 8000 } else { 4500 }
    Say "  card memory used $mem MiB (limit $limit); Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { ($resolve | ForEach-Object { $_.ProcessId }) -join ' ' } else { 'none' })"
    if ($refuse -and $mem -gt $limit) { Stop-Launch "the card holds $mem MiB, over ${limit}: something else is on it" }
    if ($refuse -and ($unity | Where-Object { $_.CommandLine -match '-batchmode' })) { Stop-Launch 'a batch-mode Unity is running' }
}

function Pause-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Launch 'the machine did not cool within 5 minutes of the pause' }
        if ($now -ge $until) {
            $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File "$main\scripts\temp-check.ps1" | Out-Null
            if ($LASTEXITCODE -eq 0 -and $card -lt 60) { Say "  cool: card $card C, heat OK"; return }
        }
        Start-Sleep -Seconds 10
    }
}

function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
            ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|driver-watch') })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Launch "busy 90 s before the group: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId) $($_.CommandLine)" }) -join ', ')"
}

function Before([string]$step) {
    if ((Hashes) -ne $script:hashes) { Stop-Launch "the binaries changed since the pre-flight: $(Hashes)" }
    if (git -C $tree status --porcelain) { Stop-Launch 'DIRTY since the pre-flight' }
    Pause-Cool
    Wait-Quiet
    CardIdle $true
}

function RunDir([string]$arm) { (Get-ChildItem "$runs\$arm" -Directory | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }

function CpuPeak([string]$name) {
    $peak = -1
    foreach ($t in (Get-Content "$logs\card-run-$name.log" | Where-Object { $_ -match 'heat: ' })) {
        if ($t -match 'cpu (\d+)C') { $peak = [Math]::Max($peak, [int]$Matches[1]) }
    }
    return $peak
}

# One group through card-group.ps1; every world must end by its budget with its books closed and every check held.
function Invoke-Group([string]$step, [string[]]$extra, [int]$wall) {
    $g = $groups[$step]
    $since = (Get-Date).AddSeconds(-30)
    Say "== ${step}: $($g.Name), worlds $($g.Arms -join ', '), $($extra -join ' '), wall $wall min"
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-group.ps1", '-Name', $g.Name, '-Arms', ($g.Arms -join ','),
        '-CheckpointEvery', '1500', '-Group', '32', '-WallMinutes', "$wall", '-RunsRoot', $runs,
        '-Transport', '-Scent', '-Concurrent', '-SpinLimit', 'default', '-HeatStop', '-SampleCpu', '-SampleCores', '-LogOutside', '-Exe', $exe) + $extra
    & pwsh @a *>&1 | Out-Null
    $lines = Get-Content "$logs\card-run-$($g.Name).log"
    $lines | Where-Object { $_ -match 'begins|watch pid|farm pid|transport in run.json|ended \(|stopped \(|card |books over|STOPPED' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card group ends$' })) { Stop-Launch "$($g.Name) did not end" }
    foreach ($arm in $g.Arms) {
        $end = $lines | Where-Object { $_ -match "^\S+ \S+   ${arm}: ended \((\w+)\)" } | Select-Object -First 1
        if (-not $end) { Stop-Launch "$arm has no ending" }
        $null = $end -match 'ended \((\w+)\)'
        if ($Matches[1] -ne 'budget') { Stop-Launch "$arm ended ($($Matches[1])); resume it from its last checkpoint on the coordinator's go" }
        if (-not ($lines | Where-Object { $_ -match "books over .* of ${arm}: .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Launch "${arm}'s books did not close" }
        $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -LogArm $g.Name -Since $since.ToString('s') -Transport card -RunsRoot $runs *>&1
        $code = $LASTEXITCODE
        $out | ForEach-Object { Say "  check ${arm}: $_" }
        if ($code -ne 0) { Stop-Launch "$arm failed a condition of card-run-check" }
        $peak = $out | Where-Object { "$_" -match 'card peak (\d+) C' } | Select-Object -First 1
        if ($peak -and [int]$Matches[1] -ge 80) { Stop-Launch "$arm's card peak reached $($Matches[1]) C" }
        $engine = Get-Content "$logs\$arm.log" | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
        Say "  ${arm}'s $engine"
        if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Launch "$arm's engine line lacks a switch" }
        $manifest = Get-Content "$(RunDir $arm)\run.json" -Raw | ConvertFrom-Json
        $src = $manifest.source
        Say "  ${arm}: gitCommit $($src.gitCommit), gitDirty $($src.gitDirty), configHash $($manifest.configHash)"
        if ($null -eq $src -or $null -eq $src.gitDirty) { Stop-Launch "$arm's manifest has no source.gitDirty" }
        if ($src.gitDirty) { Stop-Launch "$arm's manifest says DIRTY" }
        if (-not "$($src.gitCommit)".StartsWith($Commit)) { Stop-Launch "$arm's manifest names $($src.gitCommit), not $Commit" }
    }
    $cpu = CpuPeak $g.Name
    Say "  $($g.Name)'s CPU at the heat lines: $cpu C"
    if ($cpu -ge 85) { Stop-Launch "$($g.Name)'s CPU reached $cpu C" }
}

$env:EVOSIM_GPU_CONCURRENT = '1'
$env:EVOSIM_GPU_SCENT = '1'
foreach ($v in 'EVOSIM_GPU_SCENT_ORDER', 'EVOSIM_GPU_PROBE', 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') {
    [Environment]::SetEnvironmentVariable($v, $null)
}
Say "r58 launch begins: steps $($wanted -join ', '), wall $WallMinutes min, runs in $runs, EVOSIM_GPU_CONCURRENT=1 EVOSIM_GPU_SCENT=1"
Tree
foreach ($s in $wanted) { Free $s }
if ($PreflightOnly) { CardIdle $false; Say 'r58 launch preflight ends: nothing ran on the card'; exit 0 }

$founded = @('-Launcher', "$tree\rounds\env-r57.ps1", '-Seeds', '1,2,3', '-Seconds', '48000')
foreach ($s in $groups.Keys) {
    if ($s -notin $wanted) { continue }
    Before $s
    switch ($s) {
        'join' { Invoke-Group 'join' ($founded + @('-MigrateEvery', '3000', '-MigrateCount', '3')) $WallMinutes }
        'iso' { Invoke-Group 'iso' $founded $WallMinutes }
        'b2' {
            $sources = @($groups['join'].Arms | ForEach-Object { RunDir $_ })
            foreach ($src in $sources) { if (-not (Test-Path "$src\checkpoints\000045000.ckpt")) { Stop-Launch "no 45,000 s checkpoint in $src" } }
            Invoke-Group 'b2' @('-At', '45000', '-Seconds', '3000', '-ResumeFrom', ($sources -join ','), '-MigrateEvery', '3000', '-MigrateCount', '3') 30
            $pairs = for ($k = 0; $k -lt 3; $k++) { $sources[$k]; RunDir $groups['b2'].Arms[$k] }
            python -I "$tree\scripts\reads\group-resume-compare.py" 45000 @pairs 2>&1 | ForEach-Object { Say "  B2 compare: $_" }
            $code = $LASTEXITCODE
            Say "  B2: group-resume-compare of r58-b2 against r58-join after 45,000 s: exit $code (0 is the same)"
            if ($code -ne 0) { Stop-Launch 'B2: the resumed group is not the joined group after 45,000 s' }
        }
    }
}
Say 'r58 launch ends'
