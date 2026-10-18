# One farm run on the card (or, with -Engine cpu, the same run on the CPU engine off the card, for a pace read
# beside it), under the driver watch. One run per go: src/Evosim.Farm.Gpu/CLAUDE.md says when the card may run.
#
# -At > 0 resumes -ResumeFrom (a run or arm directory) at -At seconds for -Seconds more, with
# -AllowSourceMismatch, since a card build is not the recording's. -At 0 founds a world from -Launcher and -Seed:
# a one-second preflight on the CPU engine, off the card, founds the same world first and the card run starts
# only if the preflight's configHash is -ConfigHash; the card run's own hash is read again once it starts.
# The card runs in single precision at group -Group, the farm on 16 threads pinned to -Mask (decimal;
# 4294901760 is logical 16-31) at BelowNormal. -Transport also puts the snow's transport on the card
# (EVOSIM_GPU_TRANSPORT=1); the run stops at once if its run.json does not say transport 'card', and without
# the switch it must say 'cpu'. -Exe names the farm build (default: this checkout's Release build).
#
# Before the farm starts it refuses: another farm, test host or card script running; an nvlddmkm event in the
# last minute; heat at WARN; a log or arm of this name already there. The driver watch starts from its own
# settings file, its first line is read back (its end pattern masked in this log), and it is checked alive
# 30 s after it began and at every turn of the wait: a watch gone while the farm runs stops the farm. Any
# nvlddmkm event: the watch stops this script, writes STOP to the farm and kills it 30 s later if it runs.
# The card's heat is sampled inside the wait, one nvidia-smi line a turn (scratch/logs/card-run-<Arm>-gpu.csv),
# and with -SampleCpu the processor's too every sixth turn (-cpu.txt); a timing read leaves -SampleCpu off.
# -SampleCores logs the processor's state every turn (core-sample.ps1: the machine's total utility, and each logical
# processor's performance and utility, -cores.csv) and its summary at the end. One counter read a turn, over a second.
# -HeatStop reads the CPU's heat every turn (into -cpu.txt) and stops the farm, ending "card run STOPPED: heat" with
# exit 4, on heat-stop.ps1's first reason: the CPU at 90 C on two readings in a row (a turn is about 15 s), at 85 C
# for a minute, the card at 80 C, or no CPU reading for two minutes.
# The read at the end: status, the pace over the seconds stepped (run.json's timesRealTime counts the world's
# whole age on a resume), transport, divergedTotal, the limited counts, the card's refusals, overflow and
# neuronsAtCeiling, and the books over every stats row (|auditResidual| under 0.1 J, |matterResidual| under
# 1e-5). card-run-check.ps1 reads every condition back.
# -Probe sets EVOSIM_GPU_PROBE=1 in this script's environment, which the farm inherits (run-farm's -Env does not
# reach it). The runner then joins the card after each part and prints its times, and once the kernels' shapes and
# a launch's cost, on the farm's stderr (scratch/logs/<Arm>.err). A diagnostic, never a scored run's. The step's
# numbers do not move, so the run is still read for identity. Without -Probe the variable is cleared.
# -LogOutside records the processes beside the run, inside the same wait: every dotnet, testhost, other-project process (scratch/machine/beside-names.txt) and Evosim.Farm
# other than this run's farm (and a -Beside partner's), and every python started after the run began, with its pid,
# start, affinity and command line when first seen and the turn it was gone by (-outside.txt); a summary by name at the end.
# -SpinLimit goes to run-farm: 'default' (5000 on the card, unset on the CPU engine), 'unset' or a whole number.
# -CheckpointEvery goes to run-farm on a resume and on a founded card run (never its preflight): simulated seconds
# between checkpoints, 0 (the default) for none, so
# the world's state can be read at those seconds (a grip's count, say) as well as by its digests.
# -AllowBeside is a regular expression, at least 8 characters, for the command line of a dotnet.exe that may run
# beside the run (the nursery room's server, say). Any other dotnet still refuses the launch, as does a matching one
# with the card's driver (nvcuda.dll) loaded. Each one let pass is named in the log, and -LogOutside marks it.
# Nothing beside the run is quiet for a timing read: a pace taken with one is re-taken alone.
# Log: scratch/logs/card-run-<Arm>.log, last line "card run ends" or "card run STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Arm,
    [Parameter(Mandatory)][int]$At,
    [Parameter(Mandatory)][int]$Seconds,
    [string]$ResumeFrom = '',
    [string]$Launcher = '',
    [uint64]$Seed = 0,
    [string]$ConfigHash = '',
    [ValidateSet(32, 64)][int]$Group = 32,
    [int]$WallMinutes = 50,
    [int64]$Mask = 4294901760,
    [ValidateSet('gpu', 'cpu')][string]$Engine = 'gpu',
    [switch]$Transport,
    [string]$Exe = '',
    [string]$RunsRoot = 'scratch\card\runs',
    [switch]$SampleCpu,
    [switch]$SampleCores,
    [switch]$HeatStop,
    # The farm's threads (16 by default; 8 for one of two runs sharing the card, each on its own 8 logical).
    [ValidateRange(1, 16)][int]$Threads = 16,
    # Another card run that is to share the card: its arm under the same -RunsRoot, already running. The busy
    # refusal then lets through exactly that arm's farm (the processId its run.json names) and that arm's
    # driver watch (the pwsh started on its settings file), and nothing else; and it refuses when the arm is
    # not running. A resume only. On a driver event both watches stop both runs, since they share the card.
    [string]$Beside = '',
    [switch]$Probe,
    [switch]$LogOutside,
    [string]$AllowBeside = '',
    [string]$SpinLimit = 'default',
    [double]$CheckpointEvery = 0)
$ErrorActionPreference = 'Continue'
# The kill test's checkpoint stall hook is never a sitting's setting (card-next-r57.md Part 8).
if ($env:EVOSIM_TEST_CHECKPOINT_STALL_MS) {
    Write-Host "card-run refused: EVOSIM_TEST_CHECKPOINT_STALL_MS is set in this shell ($env:EVOSIM_TEST_CHECKPOINT_STALL_MS). It is a test hook that pauses every checkpoint's write; unset it."
    exit 2
}
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Under([string]$p) { if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $root $p } }
$exePath = if ($Exe) { Under $Exe } else { Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe' }
$runs = Under $RunsRoot
$logs = Join-Path $root 'scratch\logs'
$log  = Join-Path $logs "card-run-$Arm.log"
$watchLog = Join-Path $logs "card-run-$Arm-watch.log"
$gpuCsv = Join-Path $logs "card-run-$Arm-gpu.csv"
$cpuTxt = Join-Path $logs "card-run-$Arm-cpu.txt"
$coresCsv = Join-Path $logs "card-run-$Arm-cores.csv"
$outsideTxt = Join-Path $logs "card-run-$Arm-outside.txt"
if ($SampleCores) { . (Join-Path $PSScriptRoot 'core-sample.ps1') }
if ($HeatStop) { . (Join-Path $PSScriptRoot 'heat-stop.ps1') }
New-Item -ItemType Directory -Force $logs, (Join-Path $root 'scratch\card') | Out-Null
if (Test-Path $log) { Write-Host "$log exists; rename the last run's logs first"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Heat { $l = (& pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') -Log | Select-Object -Last 1); $c = $LASTEXITCODE; Say "heat: $l"; return $c }

$wantTransport = if ($Transport) { 'card' } else { 'cpu' }
$build = $exePath -replace '\\artifacts\\.*$', ''
Say "card run begins: $Arm, engine $Engine, transport $wantTransport, build $build at $(git -C $build rev-parse --short HEAD 2>$null), from $At s for $Seconds s at group $Group, $Threads threads on mask $Mask, wall $WallMinutes min$(if ($Beside) { ", beside $Beside" })$(if ($Probe) { ', with the card probe (EVOSIM_GPU_PROBE=1)' }), spin $SpinLimit"
if ($Beside -and ($At -le 0 -or $Engine -ne 'gpu' -or $Beside -eq $Arm)) { Say 'card run STOPPED: -Beside is for a card resume beside another arm'; exit 1 }

# The partner's own processes, when -Beside names one: its farm by the processId its run.json gives while it
# runs, and its driver watch by its settings file. Nothing else is let through the refusal below.
$partner = @()
if ($Beside) {
    $pm = Get-ChildItem (Join-Path $runs "$Beside\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    $pj = if ($pm) { Get-Content $pm.FullName -Raw | ConvertFrom-Json } else { $null }
    if (-not $pj -or $pj.status -ne 'running' -or -not (Get-Process -Id $pj.processId -ErrorAction SilentlyContinue)) {
        Say "card run STOPPED: -Beside $Beside is not a running arm under $runs"; exit 1
    }
    $partner += [int]$pj.processId
    $pw = "card-run-$Beside-watch.json"
    $partner += @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'pwsh.exe' -and $_.CommandLine -and $_.CommandLine.Contains($pw) } |
        ForEach-Object { [int]$_.ProcessId })
    Say "  beside ${Beside}: its farm $($pj.processId) and its watch $(($partner | Select-Object -Skip 1) -join ', ') let through"
}
if ($Probe -and $Engine -ne 'gpu') { Say 'card run STOPPED: -Probe times the card engine'; exit 1 }
if ($Transport -and $Engine -eq 'cpu') { Say 'card run STOPPED: -Transport puts the snow on the card, and a CPU run here is an off-card job'; exit 1 }
if ($At -gt 0 -and -not $ResumeFrom) { Say 'card run STOPPED: a resume (-At above 0) needs -ResumeFrom'; exit 1 }
if ($At -le 0 -and ($Engine -eq 'cpu' -or -not $Launcher -or $Seed -eq 0 -or -not $ConfigHash)) {
    Say 'card run STOPPED: founding a world (-At 0) is for the card engine, with -Launcher, -Seed and -ConfigHash'; exit 1
}
if ($AllowBeside) {
    if ($AllowBeside.Length -lt 8) { Say "card run STOPPED: -AllowBeside '$AllowBeside' is under 8 characters; name a command line, not a pattern that passes everything"; exit 1 }
    try { $null = [regex]::new($AllowBeside) } catch { Say "card run STOPPED: -AllowBeside '$AllowBeside' is not a regular expression"; exit 1 }
}
function Test-AllowedBeside($q) { [bool]($AllowBeside -and $q.Name -eq 'dotnet.exe' -and "$($q.CommandLine)" -match $AllowBeside) }
$procsNow = @(Get-CimInstance Win32_Process)
foreach ($q in @($procsNow | Where-Object { Test-AllowedBeside $_ })) {
    $cuda = try { @((Get-Process -Id $q.ProcessId -ErrorAction Stop).Modules | Where-Object ModuleName -eq 'nvcuda.dll').Count -gt 0 } catch { $null }
    if ($cuda) { Say "card run STOPPED: dotnet $($q.ProcessId) matches -AllowBeside but has the card's driver (nvcuda.dll) loaded"; exit 1 }
    $cmd = "$($q.CommandLine)"; if ($cmd.Length -gt 200) { $cmd = $cmd.Substring(0, 200) }
    Say "  allowed beside: dotnet $($q.ProcessId)$(if ($null -eq $cuda) { ' (its modules unreadable)' }): $cmd"
}
$busy = @($procsNow | Where-Object {
    ($_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
     ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|driver-watch')) -and
    $partner -notcontains [int]$_.ProcessId -and -not (Test-AllowedBeside $_) })
if ($busy) { Say "card run STOPPED: busy: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"; exit 1 }
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = (Get-Date).AddMinutes(-1) } -ErrorAction SilentlyContinue)
if ($ev) { Say 'card run STOPPED: an nvlddmkm event in the last minute'; exit 1 }
if (Test-Path (Join-Path $runs $Arm)) { Say "card run STOPPED: $runs\$Arm exists"; exit 1 }
if (Test-Path $watchLog) { Say "card run STOPPED: $watchLog exists"; exit 1 }
if (-not (Test-Path $exePath)) { Say "card run STOPPED: no farm build at $exePath"; exit 1 }
if ((Heat) -ge 1) { Say 'card run STOPPED: the heat is at WARN or above'; exit 1 }

$start = Get-Date
$settings = Join-Path $root "scratch\card\card-run-$Arm-watch.json"
[ordered]@{
    Since      = $start.AddMinutes(-1).ToString('s')
    Deadline   = $start.AddMinutes($WallMinutes + 6).ToString('s')
    JobLog     = $log
    EndPattern = 'books over'
    StopLine   = 'card run STOPPED by the driver watch: nvlddmkm event'
    Controller = 'card-run\.ps1'
    Arm        = $Arm
    RunsRoot   = $runs
} | ConvertTo-Json | Set-Content $settings -Encoding utf8
$w = Start-Process pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'driver-watch.ps1'), '-Settings', $settings) `
     -WindowStyle Hidden -PassThru -RedirectStandardOutput $watchLog -RedirectStandardError "$watchLog.err"
$d = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $d -and -not ((Test-Path $watchLog) -and (Get-Content $watchLog -TotalCount 1))) { Start-Sleep -Milliseconds 500 }
$first = if (Test-Path $watchLog) { Get-Content $watchLog -TotalCount 1 } else { '' }
# The line is copied with its end pattern masked, so nothing in this log says it before the final read.
Say "watch pid $($w.Id): $($first -replace 'books over', '<end pattern>')"
if ($w.HasExited -or $first -notmatch "watch begins: .*end pattern 'books over', controller 'card-run\\\.ps1'") {
    Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue
    Say 'card run STOPPED: the watch did not begin as it should'; exit 1
}

Set-Location $root
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
if ($Engine -eq 'cpu') {
    Enter-OffCard
    $envs = @{ EVOSIM_DIGEST_EVERY = 100 }
} else {
    Remove-Item Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue
    $envs = @{ EVOSIM_ENGINE = 'gpu'; EVOSIM_GPU_PRECISION = 'single'; EVOSIM_GPU_GROUP = $Group; EVOSIM_DIGEST_EVERY = 100 }
    if ($Transport) { $envs.EVOSIM_GPU_TRANSPORT = 1 }
}
if ($Probe) { $env:EVOSIM_GPU_PROBE = '1' } else { Remove-Item Env:EVOSIM_GPU_PROBE -ErrorAction SilentlyContinue }
$runFarm = Join-Path $root 'scripts\run-farm.ps1'
if ($At -gt 0) {
    & $runFarm $Arm -ResumeFrom (Under $ResumeFrom) -At $At -Seconds ($At + $Seconds) -Threads $Threads `
        -RunsRoot $runs -WallMinutes $WallMinutes -AllowSourceMismatch -Exe $exePath -Env $envs -SpinLimit $SpinLimit `
        -CheckpointEvery $CheckpointEvery 6>&1 2>&1 |
        ForEach-Object { Say "  ${Arm}> $_" }
} else {
    # A world founded on the card: its config hash is confirmed before the card steps. A preflight on the CPU
    # engine, off the card, founds the same world, runs 1 s and ends (the engine is not a tunable), and the
    # card run starts only if the preflight's hash is -ConfigHash.
    $pre = "$Arm-preflight"
    if (Test-Path (Join-Path $runs $pre)) { Say "card run STOPPED: $runs\$pre exists"; exit 1 }
    Enter-OffCard
    & $runFarm $pre -Launcher (Under $Launcher) -Seed $Seed -Seconds 1 -Threads $Threads `
        -RunsRoot $runs -WallMinutes 5 -Exe $exePath 6>&1 2>&1 | ForEach-Object { Say "  ${pre}> $_" }
    $preJson = $null
    $d = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $d) {
        foreach ($p in @(Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue)) {
            try { $p.ProcessorAffinity = [IntPtr]$Mask; $p.PriorityClass = 'BelowNormal' } catch {}
        }
        Test-OffCardTree $d
        $m = Get-ChildItem (Join-Path $runs "$pre\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m) { $preJson = Get-Content $m.FullName -Raw | ConvertFrom-Json; if ($preJson.status -ne 'running') { break } }
        Start-Sleep -Seconds 2
    }
    if (-not $preJson -or $preJson.status -eq 'running') { Say 'card run STOPPED: the preflight did not end within 5 minutes'; exit 1 }
    Say "  preflight: engine $($preJson.engine), seed $($preJson.seed), status $($preJson.status) ($($preJson.reason)), configHash $($preJson.configHash)"
    if ($preJson.configHash -ne $ConfigHash) { Say "card run STOPPED: the preflight's configHash is not $ConfigHash"; exit 1 }
    $d = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $d -and (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 1 }
    if (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) { Say 'card run STOPPED: a farm process outlived the preflight'; exit 1 }
    $nv = Resolve-OffCard
    Say "  preflight $nv"
    if ($nv -notmatch 'loaded by none') { Say 'card run STOPPED: the preflight loaded nvcuda'; exit 1 }
    Remove-Item Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue
    Say "  config hash confirmed: $ConfigHash; the card run starts"
    & $runFarm $Arm -Launcher (Under $Launcher) -Seed $Seed -Seconds $Seconds -Threads $Threads `
        -RunsRoot $runs -WallMinutes $WallMinutes -Exe $exePath -Env $envs -SpinLimit $SpinLimit -CheckpointEvery $CheckpointEvery 6>&1 2>&1 |
        ForEach-Object { Say "  ${Arm}> $_" }
    # run-farm waits 60 s for run.json and returns either way; a world founded at 88,000 m² wrote it after 75 s
    # (r60s-big3m-s1, 2026-10-15), and a hash read at once found none, stopped, and left the farm running unwatched
    # when stop-arm, with no run.json to read, threw. The hash is read when run.json is there, up to 5 minutes.
    $m = $null
    $d = (Get-Date).AddMinutes(5)
    while (-not $m -and (Get-Date) -lt $d) {
        $m = Get-ChildItem (Join-Path $runs "$Arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if (-not $m) { Start-Sleep -Seconds 2 }
    }
    $h = if ($m) { (Get-Content $m.FullName -Raw | ConvertFrom-Json).configHash } else { '' }
    if ($h -ne $ConfigHash) {
        try { & (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" } }
        catch { Say "  stop: stop-arm failed: $($_.Exception.Message); the farm may still be running" }
        Say "card run STOPPED: the card run's configHash '$h' is not $ConfigHash"; exit 1
    }
}
# Pin this run's own farm only, by the processId its run.json names: another farm beside it (-Beside) keeps
# its own mask.
$pinned = $false
$d = (Get-Date).AddMinutes(2)
while ((Get-Date) -lt $d -and -not $pinned) {
    $m = Get-ChildItem (Join-Path $runs "$Arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    $own = if ($m) { (Get-Content $m.FullName -Raw | ConvertFrom-Json).processId } else { $null }
    $p = if ($own) { Get-Process -Id $own -ErrorAction SilentlyContinue } else { $null }
    if ($p -and $p.ProcessName -eq 'Evosim.Farm') {
        $p.ProcessorAffinity = [IntPtr]$Mask; $p.PriorityClass = 'BelowNormal'
        Say "  ${Arm}: pid $($p.Id) pinned to $Mask, BelowNormal"; $pinned = $true; $farmPid = [int]$p.Id
    }
    else { Start-Sleep -Seconds 1 }
}
if (-not $pinned) {
    & (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
    Say 'card run STOPPED: its own farm, named by its run.json, was not seen to pin'; exit 1
}

# The transport the run actually took, from its own run.json (run-farm has waited for it): a run that asked for
# the card's transport and did not get it, or the other way round, is not the run that was asked for.
$m = Get-ChildItem (Join-Path $runs "$Arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
$got = if ($m) { (Get-Content $m.FullName -Raw | ConvertFrom-Json).transport } else { '' }
Say "  transport in run.json: '$got' (asked for '$wantTransport')"
if ($got -ne $wantTransport) {
    & (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
    Say "card run STOPPED: the run's transport is '$got', not '$wantTransport'"; exit 1
}

function Stop-Unwatched([string]$when) {
    Say "card run: the driver watch (pid $($w.Id)) is gone $when; stopping the farm"
    & (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
    Say 'card run STOPPED: the driver watch ended before the run'; exit 3
}
while ((Get-Date) -lt $start.AddSeconds(30)) { Start-Sleep -Seconds 1 }
if ($w.HasExited) { Stop-Unwatched 'after its first polls' }
Say "  watch alive after its first polls ($(Get-Date -Format 'HH:mm:ss'))"

# The processes beside the run (-LogOutside), one CIM query a turn.
# Other projects' processes are named in scratch\machine\beside-names.txt (gitignored, optional): one WMI LIKE
# pattern per line, blank lines and lines starting with # skipped. With no file there are no extra patterns.
function Get-BesideFilter {
    $f = "Name='dotnet.exe' OR Name='testhost.exe' OR Name='python.exe' OR Name='Evosim.Farm.exe'"
    $file = Join-Path $root 'scratch\machine\beside-names.txt'
    if (Test-Path $file) {
        foreach ($l in (Get-Content $file)) {
            $l = $l.Trim()
            if ($l -and -not $l.StartsWith('#')) { $f += " OR Name LIKE '" + $l.Replace("'", "''") + "'" }
        }
    }
    $f
}
$outsideSeen = @{}
function Write-Outside {
    $now = Get-Date
    $procs = @(Get-CimInstance Win32_Process -Filter (Get-BesideFilter) -ErrorAction SilentlyContinue |
        Where-Object { [int]$_.ProcessId -ne $farmPid -and $partner -notcontains [int]$_.ProcessId -and
                       ($_.Name -ne 'python.exe' -or ($_.CreationDate -and $_.CreationDate -ge $start)) })
    $live = @{}
    foreach ($q in $procs) {
        $id = [int]$q.ProcessId; $live[$id] = $true
        if ($outsideSeen.ContainsKey($id)) { continue }
        $aff = try { '0x{0:X}' -f [int64](Get-Process -Id $id -ErrorAction Stop).ProcessorAffinity } catch { '?' }
        $cmd = "$($q.CommandLine)"; if ($cmd.Length -gt 200) { $cmd = $cmd.Substring(0, 200) }
        $born = if ($q.CreationDate) { $q.CreationDate.ToString('HH:mm:ss') } else { '?' }
        $outsideSeen[$id] = [pscustomobject]@{ Name = $q.Name; Born = $born; Seen = $now; Gone = $null; Affinity = $aff }
        "$($now.ToString('HH:mm:ss')) + $id $($q.Name)$(if (Test-AllowedBeside $q) { ' (allowed beside)' }) started $born affinity ${aff}: $cmd" | Add-Content $outsideTxt
    }
    foreach ($id in @($outsideSeen.Keys)) {
        if (-not $live.ContainsKey($id) -and -not $outsideSeen[$id].Gone) {
            $outsideSeen[$id].Gone = $now
            "$($now.ToString('HH:mm:ss')) - $id $($outsideSeen[$id].Name) gone" | Add-Content $outsideTxt
        }
    }
}
function Get-OutsideSummary {
    if ($outsideSeen.Count -eq 0) { return 'no dotnet, testhost, other-project processes, new python or other farm beside the run' }
    $end = Get-Date
    $parts = foreach ($g in ($outsideSeen.Values | Group-Object Name | Sort-Object Name)) {
        $seen = ($g.Group | ForEach-Object { ($(if ($_.Gone) { $_.Gone } else { $end }) - $_.Seen).TotalSeconds } | Measure-Object -Sum).Sum
        $first = ($g.Group | Sort-Object Seen | Select-Object -First 1).Seen.ToString('HH:mm:ss')
        $affs = ($g.Group | ForEach-Object Affinity | Sort-Object -Unique) -join '/'
        '{0} x{1}, first seen {2}, affinity {3}, {4:N0} s seen in all' -f $g.Name, $g.Count, $first, $affs, $seen
    }
    return "$($outsideSeen.Count) beside the run ($outsideTxt): $($parts -join '; ')"
}
$stopBy = (Get-Date).AddMinutes($WallMinutes + 3)
$turn = 0
while ((Get-Date) -lt $stopBy) {
    $m = Get-ChildItem (Join-Path $runs "$Arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($m) {
        $j = Get-Content $m.FullName -Raw | ConvertFrom-Json
        if ($j.status -eq 'running' -and $w.HasExited) { Stop-Unwatched 'while the farm runs' }
        if ($Engine -eq 'cpu') { Test-OffCardTree $stopBy }
        if ($j.status -ne 'running') {
            $rows = @(Get-Content (Join-Path $m.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            $s = $rows[-1]
            $audit = ($rows | ForEach-Object { [math]::Abs([double]$_.auditResidual) } | Measure-Object -Maximum).Maximum
            $matter = ($rows | ForEach-Object { [math]::Abs([double]$_.matterResidual) } | Measure-Object -Maximum).Maximum
            $g = $j.gpu
            # run.json's timesRealTime divides the world's age since its founding by this process's wall, so on
            # a resumed run it counts the resume point's seconds too: the pace here is the seconds this run
            # stepped over its own whole wall, start-up included (pace-calc.py gives the stepping pace).
            $pace = ([double]$s.t - $At) / [math]::Max(1e-9, [double]$j.wallClockMinutes * 60)
            Say ("  {0}: {1} ({2}); t {3} alive {4}; {5:N1} wall min, {6:N2} times real time over the {7} s stepped; run {8}" -f $Arm, $j.status, $j.reason, $s.t, $s.alive, $j.wallClockMinutes, $pace, ([double]$s.t - $At), $m.DirectoryName)
            if ($Engine -eq 'cpu') { Say "  engine $($j.engine), threads $($j.threads); $(Resolve-OffCard)" }
            else { Say "  card: transport $($j.transport), precision $($g.precision), group $($g.groupSize), device $($g.device), refusedForClass $($g.refusedForClass), overflow $($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries), neuronsAtCeiling $($g.neuronsAtCeiling), compile $($g.compileMs) ms" }
            # Both limited counts are cumulative from the world's founding: a checkpoint carries them.
            Say "  divergedTotal $($j.divergedTotal); driveImpulsesLimited $($j.driveImpulsesLimited) and dragImpulsesLimited $($j.dragImpulsesLimited), cumulative since the founding"
            [void](Heat)
            if ($SampleCores) { Say "  $(Get-CoreSummary $coresCsv)" }
            if ($LogOutside) { Say "  outside: $(Get-OutsideSummary)" }
            Say "  books over $($rows.Count) stats rows: max |auditResidual| $audit (under 0.1 J: $($audit -lt 0.1)); max |matterResidual| $matter (under 1e-5: $($matter -lt 1e-5))"
            Say 'card run ends'
            exit 0
        }
    }
    # The heat, sampled inside this wait (no separate loop): the card each turn, the processor every sixth.
    $smi = & nvidia-smi --query-gpu=temperature.gpu,utilization.gpu,power.draw,memory.used --format=csv,noheader 2>$null
    if ($smi) { "$(Get-Date -Format 'HH:mm:ss'), $smi" | Add-Content $gpuCsv }
    if (($SampleCpu -and $turn % 6 -eq 0) -or $HeatStop) {
        $t = & pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') | Select-Object -Last 1
        "$(Get-Date -Format 'HH:mm:ss') $t" | Add-Content $cpuTxt
    }
    if ($HeatStop) {
        $cpuC = if ("$t" -match 'cpu (\d+)C') { [int]$Matches[1] } else { $null }
        $cardC = if ($smi -and "$smi" -match '^\s*(\d+)') { [int]$Matches[1] } else { $null }
        $why = Get-HeatStop $cpuC $cardC (Get-Date)
        if ($why) {
            Say "card run: heat stop, $why; stopping the farm"
            & (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
            [void](Heat)
            if ($LogOutside) { Write-Outside; Say "  outside: $(Get-OutsideSummary)" }
            # The farm ends at its next report row; then the watch is stopped, which otherwise waits out its deadline
            # and refuses every card launch meanwhile (round 59's heat stop, 2026-10-14).
            $d = (Get-Date).AddMinutes(5)
            while ((Get-Date) -lt $d -and $farmPid -and (Get-Process -Id $farmPid -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 2 }
            if (-not ($farmPid -and (Get-Process -Id $farmPid -ErrorAction SilentlyContinue))) { Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue; Say "  watch pid $($w.Id) stopped: the farm is gone" }
            else { Say "  the farm $farmPid still runs 5 minutes after its STOP; the watch is left to it" }
            Say "card run STOPPED: heat, $why"; exit 4
        }
    }
    if ($SampleCores) { Write-CoreSample $coresCsv }
    if ($LogOutside) { Write-Outside }
    $turn++
    Start-Sleep -Seconds 10
}
& (Join-Path $root 'scripts\stop-arm.ps1') $Arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
Say "card run STOPPED: past $WallMinutes minutes"
exit 2
