# The grazer control (logbook/specs/grazer-control-spec.md): one step a call, each a lone card run of r58-s1ir
# resumed at 12,000 s through scripts\card\card-run.ps1 on round 58's build, every switch as the round's:
#   zero   r58g-zero   300 s, no grazers. Proves the comparison: a lone card-run resume of r58-s1ir is r58-s1ir,
#                      row for row (group-resume-compare.py 12000). The spec assumes it; nothing has shown it.
#   smoke  r58g-smoke  60 s, grazer-plate-late-m87.json (reserve margin 87 s, the file smoke ran).
#   smoke2 r58g-smoke2 60 s, grazer-plate-late.json at margin 600 s (the coordinator's ruling after smoke, whose
#                      plates budded twice at once and never added the mouth).
#   a      r58g-a      3,000 s, grazer-plate-late.json.
#   b      r58g-b      3,000 s, grazer-plate.json.
# The genomes are under inocula\grazer-control\.
# Each grazer step plants 20 at the first step: EVOSIM_INOCULATE (the genome), _AT 12000, _COUNT 20 and _DEPTH
# -Depth (positive metres). card-run.ps1 has no switch for them, the scent or the concurrent mode, and card-group.ps1
# clears every EVOSIM_ variable and refuses a lone world; card-run clears none of these, the farm reads any setting
# its command line lacks from its environment (Program.cs, RunWorld), and a resume takes the assay's four from there
# (Program.cs, the loop's inoculum). So this script sets them in its own environment and the farm inherits them.
# Before it runs: r58-runner at -Commit, clean, logbook/0138 present, src, scripts (scripts/reads aside) and unity
# 02c448e6's, the binaries check58's pins; r58-s1ir ended by its budget; B2 passed in r58-launch.log; r58-s1ir's
# 12,000 s checkpoint present; the genome present; the names free; a minute's cooling to a card under 60 C with
# temp-check OK, a quiet machine and the card's memory under 4,500 MiB (8,000 beside Resolve).
# After it: an ending by budget, books closed, card-run-check's every condition, the engine line's three switches,
# the manifest's source.gitCommit and gitDirty, the card under 80 C, the card's four overflow counts all 0, and for a
# grazer step the manifest's inoculateGenomeHash present. zero ends with the compare. Any failure stops with exit 3.
# -PreflightOnly runs the checks alone and touches nothing on the card.
# Log: scratch/logs/r58g-<step>.log in the main checkout, last line "r58g <step> ends" or "r58g <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][ValidateSet('zero', 'smoke', 'smoke2', 'a', 'b')][string]$Step,
    [double]$Depth = 0,
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = "$main\scratch\wt-card-r57"
$runs = "$main\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$built = '02c448e6'
$pins = 'Evosim.Farm.exe dacb75e884ba1331, Evosim.Farm.dll 9265ac97886d6c3c, Evosim.Farm.Gpu.dll 678925814379c4c4, Evosim.Core.dll 20c86289650094f5, Evosim.Dynamics.dll 57de011f59186d43'
$at = 12000
$plan = @{
    zero  = @{ Arm = 'r58g-zero'; Seconds = 300; Genome = '' }
    smoke = @{ Arm = 'r58g-smoke'; Seconds = 60; Genome = "$main\inocula\grazer-control\grazer-plate-late-m87.json" }
    smoke2 = @{ Arm = 'r58g-smoke2'; Seconds = 60; Genome = "$main\inocula\grazer-control\grazer-plate-late.json" }
    a     = @{ Arm = 'r58g-a'; Seconds = 3000; Genome = "$main\inocula\grazer-control\grazer-plate-late.json" }
    b     = @{ Arm = 'r58g-b'; Seconds = 3000; Genome = "$main\inocula\grazer-control\grazer-plate.json" }
}[$Step]
$arm = $plan.Arm
$log = if ($PreflightOnly) { "$main\scratch\logs\r58g-$Step-preflight-$(Get-Date -Format HHmmss).log" } else { "$main\scratch\logs\r58g-$Step.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r58g $Step STOPPED: $why"; exit 3 }

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
function RunDir([string]$name) { (Get-ChildItem "$runs\$name" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }

function Tree {
    $head = git -C $tree rev-parse --short HEAD
    $branch = git -C $tree rev-parse --abbrev-ref HEAD
    Say "  $tree on $branch at $head, asked for $Commit"
    if ($branch -ne 'r58-runner') { Stop-Step "$tree is on $branch, not r58-runner" }
    if ($head -ne $Commit) { Stop-Step "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain
    if ($dirty) { Stop-Step "DIRTY: $($dirty -join '; ')" }
    if (-not (git -C $tree ls-tree --name-only HEAD logbook/ | Where-Object { $_ -match '^logbook/0138' })) { Stop-Step "$Commit holds no logbook/0138" }
    $code = @('src', 'scripts', 'unity', ':(exclude)scripts/reads')
    git -C $tree diff --quiet $built HEAD -- @code
    if ($LASTEXITCODE -ne 0) { Stop-Step "src, scripts or unity differ from $built" }
    Say "  clean; logbook/0138 present; src, scripts (but scripts/reads) and unity are ${built}'s"
    $hasCycle = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $scentOrder = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-scent-order'
    $scent = Has "$bin\Evosim.Farm.dll" 'EVOSIM_GPU_SCENT'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: cycle probe $hasCycle, scent order probe $scentOrder, O2 $hasO2, binds EVOSIM_GPU_SCENT $scent; $script:hashes"
    if ($hasCycle -or $scentOrder -or $hasO2 -or -not $scent) { Stop-Step 'not the plain build' }
    if ($script:hashes -ne $pins) { Stop-Step "the binaries are not check58's pins of $built" }
    Say "  binaries match check58's pins of $built"
}

# The round first: the twins ended, and B2 passed, before the card serves a diagnostic.
function Round {
    # r58-iso-resume.ps1 ended STOPPED on r58-s1ir's overflow after every world had reached its budget, so the
    # source's own manifest says whether it ended, not that script's last line.
    $launch = "$main\scratch\logs\r58-launch.log"
    if (-not (Test-Path $launch) -or -not (Select-String -Path $launch -Pattern 'B2: group-resume-compare .* exit 0 ' -Quiet) -or
        -not (Select-String -Path $launch -Pattern 'r58 launch ends$' -Quiet)) { Stop-Step "B2 has not passed ($launch)" }
    Say '  B2 passed and r58 launch ended'
    $script:source = RunDir 'r58-s1ir'
    if (-not $script:source) { Stop-Step 'no run under runs\r58-s1ir' }
    $ck = "$script:source\checkpoints\$('{0:D9}' -f $at).ckpt"
    if (-not (Test-Path $ck)) { Stop-Step "no checkpoint at $at s: $ck" }
    $m = Get-Content "$script:source\run.json" -Raw | ConvertFrom-Json
    if ($m.migrateEverySeconds) { Stop-Step "$script:source traded; r58-s1ir must not" }
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$script:source has not ended by its budget ($($m.status), $($m.reason))" }
    Say "  source $script:source, status $($m.status) ($($m.reason)), source.gitCommit $($m.source.gitCommit), checkpoint $ck"
}

function Grazers {
    if ($Step -eq 'zero') {
        if ($Depth -ne 0) { Stop-Step 'zero plants nothing; -Depth is for a grazer step' }
        return
    }
    if (-not (Test-Path $plan.Genome)) { Stop-Step "no genome at $($plan.Genome)" }
    if (-not ($Depth -gt 0)) { Stop-Step '-Depth is the leaves'' depth at 12,000 s in positive metres, and it was not given' }
    $script:genomeHash = (Get-FileHash $plan.Genome -Algorithm SHA256).Hash.ToLowerInvariant()
    Say "  genome $($plan.Genome), sha256 $script:genomeHash; 20 at $at s, depth $Depth m"
}

function Free {
    foreach ($p in "$logs\card-run-$arm.log", "$logs\card-run-$arm-watch.log", "$runs\$arm", "$runs\$arm.md", "$logs\$arm.out", "$logs\$arm.err",
                   "$logs\$arm.log", "$logs\$arm.args", "$logs\card-run-$arm-gpu.csv") {
        if (Test-Path $p) { Stop-Step "the name $arm is used: $p exists" }
    }
    Say "  name free: $arm"
}

function CardIdle([bool]$refuse) {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    $limit = if ($resolve.Count -gt 0) { 8000 } else { 4500 }
    Say "  card memory used $mem MiB (limit $limit); Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { ($resolve | ForEach-Object { $_.ProcessId }) -join ' ' } else { 'none' })"
    if ($refuse -and $mem -gt $limit) { Stop-Step "the card holds $mem MiB, over ${limit}" }
    if ($refuse -and ($unity | Where-Object { $_.CommandLine -match '-batchmode' })) { Stop-Step 'a batch-mode Unity is running' }
}

function Pause-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Step 'the machine did not cool within 5 minutes of the pause' }
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
            ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run|card-group|driver-watch') })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Step "busy 90 s before the run: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"
}

# The environment the farm inherits: the round's two card switches, the assay's four for a grazer step, and no
# other EVOSIM_ setting, so nothing left in this shell reaches the world.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
$env:EVOSIM_GPU_CONCURRENT = '1'
$env:EVOSIM_GPU_SCENT = '1'
if ($plan.Genome) {
    $env:EVOSIM_INOCULATE = $plan.Genome
    $env:EVOSIM_INOCULATE_AT = "$at"
    $env:EVOSIM_INOCULATE_COUNT = '20'
    $env:EVOSIM_INOCULATE_DEPTH = $Depth.ToString([Globalization.CultureInfo]::InvariantCulture)
}
Say "r58g $Step begins: $arm, r58-s1ir from $at s for $($plan.Seconds) s, runs in $runs; environment $((Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ')"
Tree
Round
Grazers
Free
if ($PreflightOnly) { CardIdle $false; Say "r58g $Step preflight ends: nothing ran on the card"; exit 0 }

Pause-Cool
Wait-Quiet
CardIdle $true
if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
$since = (Get-Date).AddSeconds(-30)
[string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm, '-At', "$at", '-Seconds', "$($plan.Seconds)",
    '-ResumeFrom', $script:source, '-Group', '32', '-WallMinutes', '60', '-RunsRoot', $runs, '-Transport', '-SpinLimit', 'default',
    '-HeatStop', '-SampleCpu', '-SampleCores', '-LogOutside', '-Exe', $exe)
Say "== ${arm}: card-run $($a[3..($a.Count - 1)] -join ' ')"
& pwsh @a *>&1 | Out-Null
$lines = Get-Content "$logs\card-run-$arm.log"
$lines | Where-Object { $_ -match 'begins|watch pid|farm pid|transport in run.json|ended \(|stopped \(|card |books over|STOPPED|warning' } | ForEach-Object { Say "  $_" }
if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$arm did not end" }
if (-not ($lines | Where-Object { $_ -match 'ended \(budget\)' })) { Stop-Step "$arm did not end by its budget" }
if (-not ($lines | Where-Object { $_ -match "books over .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${arm}'s books did not close" }
$out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs *>&1
$code = $LASTEXITCODE
$out | ForEach-Object { Say "  check ${arm}: $_" }
if ($code -ne 0) { Stop-Step "$arm failed a condition of card-run-check" }
$peak = $out | Where-Object { "$_" -match 'card peak (\d+) C' } | Select-Object -First 1
if ($peak -and [int]$Matches[1] -ge 80) { Stop-Step "$arm's card peak reached $($Matches[1]) C" }
# A lone card run's farm writes its engine line to <arm>.out (a group's world, to <arm>.log). A missing line stops the
# step rather than passing it: an empty match passes every -notmatch below.
$engine = Get-Content "$logs\$arm.out" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
Say "  ${arm}'s $engine"
if (-not $engine) { Stop-Step "no engine line in $logs\$arm.out" }
if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "$arm's engine line lacks a switch" }
$m = Get-Content "$(RunDir $arm)\run.json" -Raw | ConvertFrom-Json
Say "  ${arm}: source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty), configHash $($m.configHash), inoculateGenomeHash $($m.inoculateGenomeHash)"
if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Step "$arm's manifest does not name a clean $Commit" }
if ($plan.Genome -and $m.inoculateGenomeHash -ne $script:genomeHash) { Stop-Step "$arm's manifest does not carry the genome's hash $script:genomeHash" }
if (-not $plan.Genome -and $m.inoculateGenomeHash) { Stop-Step "$arm carries an inoculum and zero plants nothing" }
# A run whose card dropped a candidate or an overlap is unreadable (the coordinator, 2026-10-12): stated here as well
# as in card-run-check, so the reason is the log's own line.
$g = $m.gpu
$over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
Say "  ${arm}: overflow candidates/overlaps/held/entries $over"
if ($over -ne '0/0/0/0') { Stop-Step "$arm's card dropped contacts (overflow $over); the run is unreadable" }
if ($Step -eq 'zero') {
    python -I "$tree\scripts\reads\group-resume-compare.py" $at $script:source (RunDir $arm) 2>&1 | ForEach-Object { Say "  zero compare: $_" }
    Say "  group-resume-compare exit ${LASTEXITCODE}: a first DIFFERS at r58g-zero's own row count is the overlap's end, not a difference"
}
Say "r58g $Step ends"
