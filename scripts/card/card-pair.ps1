# Two worlds in one farm process on the card (--pair, card-next-r57.md part 4), under the driver watch. One run
# per go: src/Evosim.Farm.Gpu/CLAUDE.md says when the card may run.
#
# World A resumes -ResumeFromA (a run or arm directory) at -At for -Seconds more, and world B resumes
# -ResumeFromB (A's by default) at the same point, both with -AllowSourceMismatch, since a card build is not
# the recording's. Both run in single precision at group -Group, with -Transport putting both worlds' snow
# transport on the card. The process steps them on two threads over one device, a stream each, and Core's one
# pool of -Threads (16 by default) serves both. The process is pinned to -Mask (decimal; 4294901760 is logical
# 16-31) at BelowNormal. -Exe names the farm build (default: this checkout's Release build).
#
# Each world's settings are written to scratch/logs/<Arm>.args, as run-farm.ps1 would pass them on a command
# line, and the farm writes what each world prints to <Arm>.log and <Arm>.err beside it. The process's own
# streams go to scratch/logs/card-pair-<Pair>.out and .err. Every EVOSIM_ variable is cleared from this script's
# environment but EVOSIM_GPU_PROBE, which -Probe sets (as card-run.ps1's -Probe does), because a pair's
# settings fall back to the environment.
#
# Before the farm starts it refuses as card-run.ps1 does: another farm, test host or card script running; an
# nvlddmkm event in the last minute; heat at WARN; a log or arm of these names already there. The driver watch
# starts from its own settings file and is read back and checked alive as card-run's is. On an nvlddmkm event it
# stops this script, writes STOP to world A, and kills the farm process 30 s later if it runs, which ends both
# worlds. The card's heat is sampled each turn of the wait (scratch/logs/card-run-<Pair>-gpu.csv).
#
# The read at the end, after both worlds end, for each: status, the pace over the seconds stepped, the card's
# line, the limited counts and the books, in card-run.ps1's words, so card-run-check.ps1 -Arm <Arm>
# -LogArm <Pair> checks each world from this log.
# Log: scratch/logs/card-run-<Pair>.log, last line "card pair ends" or "card pair STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Pair,
    [Parameter(Mandatory)][string]$ArmA,
    [Parameter(Mandatory)][string]$ArmB,
    [Parameter(Mandatory)][int]$At,
    [Parameter(Mandatory)][int]$Seconds,
    [Parameter(Mandatory)][string]$ResumeFromA,
    [string]$ResumeFromB = '',
    [ValidateSet(32, 64)][int]$Group = 32,
    [int]$WallMinutes = 50,
    [int64]$Mask = 4294901760,
    [ValidateRange(1, 16)][int]$Threads = 16,
    [switch]$Transport,
    [string]$Exe = '',
    [string]$RunsRoot = 'scratch\card\runs',
    [switch]$Probe,
    # cpu runs the same pair on ILGPU's CPU accelerator, off the card (nvcuda-guard.ps1), as a dry run of
    # this launcher on a small world: CUDA_VISIBLE_DEVICES=-1 and the nvcuda line at the end.
    [ValidateSet('cuda', 'cpu')][string]$Device = 'cuda')
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Under([string]$p) { if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $root $p } }
$exePath = if ($Exe) { Under $Exe } else { Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe' }
$runs = Under $RunsRoot
$logs = Join-Path $root 'scratch\logs'
$log  = Join-Path $logs "card-run-$Pair.log"
$watchLog = Join-Path $logs "card-run-$Pair-watch.log"
$gpuCsv = Join-Path $logs "card-run-$Pair-gpu.csv"
$arms = @($ArmA, $ArmB)
New-Item -ItemType Directory -Force $logs, (Join-Path $root 'scratch\card') | Out-Null
if (Test-Path $log) { Write-Host "$log exists; rename the last run's logs first"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Heat { $l = (& pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') -Log | Select-Object -Last 1); $c = $LASTEXITCODE; Say "heat: $l"; return $c }
function Manifest([string]$arm) {
    $m = Get-ChildItem (Join-Path $runs "$arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($m) { [pscustomobject]@{ Path = $m; Json = (Get-Content $m.FullName -Raw | ConvertFrom-Json) } } else { $null }
}

if (-not $ResumeFromB) { $ResumeFromB = $ResumeFromA }
$wantTransport = if ($Transport) { 'card' } else { 'cpu' }
$build = $exePath -replace '\\artifacts\\.*$', ''
Say "card pair begins: $Pair on $Device, $ArmA from $ResumeFromA and $ArmB from $ResumeFromB, transport $wantTransport, build $build at $(git -C $build rev-parse --short HEAD 2>$null), from $At s for $Seconds s at group $Group, $Threads threads on mask $Mask, wall $WallMinutes min$(if ($Probe) { ', with the card probe (EVOSIM_GPU_PROBE=1)' })"
if ($ArmA -eq $ArmB -or $Pair -eq $ArmA -or $Pair -eq $ArmB) { Say 'card pair STOPPED: the pair and its two arms need three names'; exit 1 }
if ($At -le 0) { Say 'card pair STOPPED: a pair is a resume (-At above 0)'; exit 1 }

$busy = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
    ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run\.ps1|driver-watch') })
if ($busy) { Say "card pair STOPPED: busy: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"; exit 1 }
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = (Get-Date).AddMinutes(-1) } -ErrorAction SilentlyContinue)
if ($ev) { Say 'card pair STOPPED: an nvlddmkm event in the last minute'; exit 1 }
foreach ($arm in $arms) {
    if (Test-Path (Join-Path $runs $arm)) { Say "card pair STOPPED: $runs\$arm exists"; exit 1 }
    foreach ($f in "$arm.args", "$arm.log", "$arm.err") {
        if (Test-Path (Join-Path $logs $f)) { Say "card pair STOPPED: $logs\$f exists"; exit 1 }
    }
}
if (Test-Path $watchLog) { Say "card pair STOPPED: $watchLog exists"; exit 1 }
if (-not (Test-Path $exePath)) { Say "card pair STOPPED: no farm build at $exePath"; exit 1 }
$buildRoot = & git -C (Split-Path -Parent $exePath) rev-parse --show-toplevel 2>$null
if (-not $buildRoot) { Say "card pair STOPPED: no git checkout holds $exePath"; exit 1 }
$buildRoot = [IO.Path]::GetFullPath($buildRoot)
$from = @{}
foreach ($k in 0, 1) {
    $r = @($ResumeFromA, $ResumeFromB)[$k]
    $p = Join-Path $runs $r
    if (-not (Test-Path -LiteralPath $p)) { $p = Under $r }
    if (-not (Test-Path -LiteralPath $p)) { Say "card pair STOPPED: '$r' is neither under $runs nor a path that exists"; exit 1 }
    $from[$arms[$k]] = (Resolve-Path -LiteralPath $p).Path
}
if ((Heat) -ge 1) { Say 'card pair STOPPED: the heat is at WARN or above'; exit 1 }

# Each world's settings, as run-farm.ps1 puts them on a command line, one EVOSIM_NAME=value a line.
$culture = [Globalization.CultureInfo]::InvariantCulture
$args2 = @()
foreach ($arm in $arms) {
    $s = [ordered]@{
        EVOSIM_ALLOW_SOURCE_MISMATCH = 1
        EVOSIM_DIGEST_EVERY = 100
        EVOSIM_ENGINE = 'gpu'
        EVOSIM_GPU_DEVICE = $Device
        EVOSIM_GPU_GROUP = $Group
        EVOSIM_GPU_PRECISION = 'single'
        EVOSIM_OUT = "$arm.md"
        EVOSIM_REPO_ROOT = $buildRoot
        EVOSIM_RESUME = $from[$arm]
        EVOSIM_RESUME_AT = $At
        EVOSIM_RUNS_ROOT = $runs
        EVOSIM_SECONDS = $At + $Seconds
        EVOSIM_SEED = 1
        EVOSIM_THREADS = $Threads
        EVOSIM_WALL_MINUTES = $WallMinutes
    }
    if ($Transport) { $s.EVOSIM_GPU_TRANSPORT = 1 }
    $path = Join-Path $logs "$arm.args"
    @("# $arm, one world of the card pair $Pair (card-pair.ps1)") + @($s.Keys | ForEach-Object {
        $v = $s[$_]; "$_=$(if ($v -is [IFormattable]) { $v.ToString($null, $culture) } else { [string]$v })" }) |
        Set-Content $path -Encoding utf8NoBOM
    Say "  $arm settings: $path"
    $args2 += $path
}

$start = Get-Date
$settings = Join-Path $root "scratch\card\card-run-$Pair-watch.json"
[ordered]@{
    Since      = $start.AddMinutes(-1).ToString('s')
    Deadline   = $start.AddMinutes($WallMinutes + 6).ToString('s')
    JobLog     = $log
    EndPattern = 'books over'
    StopLine   = 'card pair STOPPED by the driver watch: nvlddmkm event'
    Controller = 'card-pair\.ps1'
    Arm        = $ArmA
    RunsRoot   = $runs
} | ConvertTo-Json | Set-Content $settings -Encoding utf8
$w = Start-Process pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'driver-watch.ps1'), '-Settings', $settings) `
     -WindowStyle Hidden -PassThru -RedirectStandardOutput $watchLog -RedirectStandardError "$watchLog.err"
$d = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $d -and -not ((Test-Path $watchLog) -and (Get-Content $watchLog -TotalCount 1))) { Start-Sleep -Milliseconds 500 }
$first = if (Test-Path $watchLog) { Get-Content $watchLog -TotalCount 1 } else { '' }
# The line is copied with its end pattern masked, so nothing in this log says it before the final read.
Say "watch pid $($w.Id): $($first -replace 'books over', '<end pattern>')"
if ($w.HasExited -or $first -notmatch "watch begins: .*end pattern 'books over', controller 'card-pair\\\.ps1'") {
    Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue
    Say 'card pair STOPPED: the watch did not begin as it should'; exit 1
}

Set-Location $root
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
if ($Device -eq 'cpu') { Enter-OffCard } else { Remove-Item Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue }
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
if ($Probe) { $env:EVOSIM_GPU_PROBE = '1' }
$out = Join-Path $logs "card-pair-$Pair.out"
$p = Start-Process -FilePath $exePath -ArgumentList (@('--pair') + $args2) -WorkingDirectory $root `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
try { $p.ProcessorAffinity = [IntPtr]$Mask; $p.PriorityClass = 'BelowNormal' } catch {}
Say "  farm pid $($p.Id), --pair $($args2 -join ' ')"

function Stop-Both([string]$why, [int]$code) {
    foreach ($arm in $arms) {
        & (Join-Path $root 'scripts\stop-arm.ps1') $arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop ${arm}: $_" }
    }
    Say "card pair STOPPED: $why"; exit $code
}

# Both worlds' run.json, each naming this process: the pin is checked on the process they name.
$d = (Get-Date).AddMinutes(2)
$seen = @{}
while ((Get-Date) -lt $d -and $seen.Count -lt 2 -and -not $p.HasExited) {
    foreach ($arm in $arms) { $m = Manifest $arm; if ($m) { $seen[$arm] = $m.Json } }
    if ($seen.Count -lt 2) { Start-Sleep -Seconds 1 }
}
if ($seen.Count -lt 2) {
    $why = if ($p.HasExited) { "the farm exited (code $($p.ExitCode)) before both worlds wrote run.json; read $out.err and each world's .err" } else { 'both worlds did not write run.json within 2 minutes' }
    if (-not $p.HasExited) { Stop-Both $why 1 }
    Say "card pair STOPPED: $why"; exit 1
}
foreach ($arm in $arms) {
    $j = $seen[$arm]
    if ([int]$j.processId -ne $p.Id) { Stop-Both "$arm's run.json names process $($j.processId), not $($p.Id)" 1 }
    Say "  ${arm}: pid $($p.Id) pinned to $([int64]$p.ProcessorAffinity), $($p.PriorityClass); sharedProcessWith '$($j.gpu.sharedProcessWith)'"
    Say "  ${arm}: transport in run.json: '$($j.transport)' (asked for '$wantTransport')"
    if ($j.transport -ne $wantTransport) { Stop-Both "${arm}'s transport is '$($j.transport)', not '$wantTransport'" 1 }
}

while ((Get-Date) -lt $start.AddSeconds(30)) { Start-Sleep -Seconds 1 }
if ($w.HasExited) { Stop-Both 'the driver watch ended before the run' 3 }
Say "  watch alive after its first polls ($(Get-Date -Format 'HH:mm:ss'))"

$stopBy = (Get-Date).AddMinutes($WallMinutes + 3)
while ((Get-Date) -lt $stopBy) {
    $done = @($arms | Where-Object { $m = Manifest $_; $m -and $m.Json.status -ne 'running' })
    if ($done.Count -lt 2 -and $w.HasExited) { Stop-Both 'the driver watch ended while the farm runs' 3 }
    if ($done.Count -eq 2) {
        $d = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $d -and -not $p.HasExited) { Start-Sleep -Seconds 1 }
        Say "  farm exited: $($p.HasExited)$(if ($p.HasExited) { ", code $($p.ExitCode)" })"
        if ($Device -eq 'cpu') { Say "  $(Resolve-OffCard)" }
        [void](Heat)
        foreach ($arm in $arms) {
            $m = Manifest $arm
            $j = $m.Json
            $rows = @(Get-Content (Join-Path $m.Path.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            $s = $rows[-1]
            $g = $j.gpu
            # As card-run.ps1's: the seconds this world stepped over its own whole wall, start-up included.
            $pace = ([double]$s.t - $At) / [math]::Max(1e-9, [double]$j.wallClockMinutes * 60)
            Say ("  {0}: {1} ({2}); t {3} alive {4}; {5:N1} wall min, {6:N2} times real time over the {7} s stepped; run {8}" -f $arm, $j.status, $j.reason, $s.t, $s.alive, $j.wallClockMinutes, $pace, ([double]$s.t - $At), $m.Path.DirectoryName)
            Say "  card ${arm}: transport $($j.transport), precision $($g.precision), group $($g.groupSize), device $($g.device), refusedForClass $($g.refusedForClass), overflow $($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries), neuronsAtCeiling $($g.neuronsAtCeiling), compile $($g.compileMs) ms, sharedProcessWith $($g.sharedProcessWith)"
            Say "  ${arm}: divergedTotal $($j.divergedTotal); driveImpulsesLimited $($j.driveImpulsesLimited) and dragImpulsesLimited $($j.dragImpulsesLimited), cumulative since the founding"
        }
        # The books last, one line a world, so the watch's end pattern falls after every other read.
        foreach ($arm in $arms) {
            $m = Manifest $arm
            $rows = @(Get-Content (Join-Path $m.Path.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            $audit = ($rows | ForEach-Object { [math]::Abs([double]$_.auditResidual) } | Measure-Object -Maximum).Maximum
            $matter = ($rows | ForEach-Object { [math]::Abs([double]$_.matterResidual) } | Measure-Object -Maximum).Maximum
            Say "  books over $($rows.Count) stats rows of ${arm}: max |auditResidual| $audit (under 0.1 J: $($audit -lt 0.1)); max |matterResidual| $matter (under 1e-5: $($matter -lt 1e-5))"
        }
        Say 'card pair ends'
        exit 0
    }
    $smi = & nvidia-smi --query-gpu=temperature.gpu,utilization.gpu,power.draw,memory.used --format=csv,noheader 2>$null
    if ($smi) { "$(Get-Date -Format 'HH:mm:ss'), $smi" | Add-Content $gpuCsv }
    Start-Sleep -Seconds 10
}
Stop-Both "past $WallMinutes minutes" 2
