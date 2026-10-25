# Two worlds or more in one farm process on the card (--group, card-next-r57.md part 4), under the driver watch.
# One run per go: src/Evosim.Farm.Gpu/CLAUDE.md says when the card may run. card-pair.ps1 is the same for two
# worlds without the switches below; this one is round 58's runner's launcher (D159).
#
# -Arms names the worlds, a comma list as one string ('a,b,c'). With -At above 0 each resumes -ResumeFrom (a run or
# arm directory, one for every world or a comma list of one a world) at -At for -Seconds more, with
# -AllowSourceMismatch, since a card build is not the recording's. -At may be a comma list of one second a world,
# for worlds stopped at different seconds, and -Until in place of -Seconds ends every world at one second; the log
# says each world's resume second and end. With -At 0 (the default) the group is founded:
# each world from the environment block -Launcher names (a .ps1 that emits the hashtable, as run-farm.ps1's
# -Launcher wants, refused if it launches anything) with its own seed from -Seeds ('1,2,3', one a world), for
# -Seconds. -Launcher may instead be a comma list of one block a world, in -Arms' order, so that worlds of different
# settings share the card's process (round 60's screens). The settings below win over the block's of the same name, as run-farm.ps1's named parameters do.
# -MigrateEvery T above 0 makes the group trade -MigrateCount genomes every T simulated seconds in a ring, the
# worlds in -Arms' order (EVOSIM_MIGRATE_EVERY and EVOSIM_MIGRATE_COUNT); 0 runs them unjoined, which is how
# round 58's isolated arm runs beside the connected one. -CheckpointEvery writes checkpoints (EVOSIM_CHECKPOINT_EVERY).
# A migrating group resumes only from a migrating group's own runs, one a world at one second (the farm refuses
# anything else). A resumed group may carry on as D201's cousins: -ResumeConfig names an edited copy of the source's
# config.json, one for all or one a world ('none' for none), and -TrickleOff turns every world's trickle off
# (EVOSIM_RESUME_CONFIG, EVOSIM_RESUME_TRICKLE_OFF; the farm refuses any field but the dials D201 allows).
# -WaitForManifestSeconds (120 by default) is how long every world has to write run.json. Every world runs
# in single precision at group -Group, with -Transport putting its snow transport on the card, -Scent its scent's
# fill (EVOSIM_GPU_SCENT=1) and -Concurrent the card's concurrent mode (EVOSIM_GPU_CONCURRENT=1). The process steps
# them on a thread each over one device, a stream each, and Core's one pool of -Threads (16 by default) serves all
# of them. The process is pinned to -Mask (decimal; 4294901760 is logical 16-31) at BelowNormal. -Exe names the
# farm build (default: this checkout's Release build).
#
# The thread pool's spin, as run-farm.ps1 sets it: -SpinLimit 'default' is 5000 (every world here is on the card
# engine), 'unset' clears it, a whole number sets it. DOTNET_ThreadPool_UnfairSemaphoreSpinLimit is set for the
# launch alone; each world's run.json and engine line name it.
#
# Each world's settings are written to scratch/logs/<Arm>.args, as run-farm.ps1 would pass them on a command line,
# and the farm writes what each world prints to <Arm>.log and <Arm>.err beside it. The process's own streams go to
# scratch/logs/card-group-<Name>.out and .err. Every EVOSIM_ variable is cleared from this script's environment but
# EVOSIM_GPU_PROBE, which -Probe sets, because a world's settings fall back to the environment.
#
# Before the farm starts it refuses as card-run.ps1 does: another farm, test host or card script running; an
# nvlddmkm event in the last minute; heat at WARN; a log or arm of these names already there. The driver watch
# starts from its own settings file and is read back and checked alive as card-run's is. On an nvlddmkm event it
# stops this script, writes STOP to the first world, and kills the farm process 30 s later if it runs, which ends
# every world. The card's heat is sampled each turn of the wait (scratch/logs/card-run-<Name>-gpu.csv, with the
# card's memory), and as card-run.ps1 does: -SampleCpu the processor every sixth turn (-cpu.txt), -SampleCores each
# logical processor every turn (-cores.csv), -HeatStop the CPU every turn with heat-stop.ps1's stops (exit 4; 90 C on two readings in a row), and
# -LogOutside the processes beside the run (-outside.txt).
#
# -AllowBeside is a regular expression, at least 8 characters, for the command line of a dotnet.exe that may run
# beside the group (the nursery room's server, say). Any other dotnet still refuses the launch, as does a matching
# one with the card's driver (nvcuda.dll) loaded. Each one let pass is named in the log, and -LogOutside marks it.
# Nothing beside the run is quiet for a timing read: a pace taken with one is re-taken alone.
#
# The read at the end, after every world ends, for each: status, the pace over the seconds stepped, the card's
# line, the limited counts and the books, in card-run.ps1's words, so card-run-check.ps1 -Arm <Arm> -LogArm <Name>
# checks each world from this log.
# Log: scratch/logs/card-run-<Name>.log, last line "card group ends" or "card group STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Arms,
    # One second for every world, or a comma list of one a world in -Arms' order ('58640,56960'); 0 founds the group.
    [string]$At = '0',
    # The span each world steps past its -At; or -Until, the one second every world ends at, whatever its -At.
    [int]$Seconds = 0,
    [int]$Until = 0,
    [string]$ResumeFrom = '',
    [string]$Launcher = '',
    [string]$Seeds = '',
    [double]$MigrateEvery = 0,
    [ValidateRange(1, 1000)][int]$MigrateCount = 1,
    [int]$CheckpointEvery = 0,
    [ValidateSet(32, 64)][int]$Group = 32,
    [int]$WallMinutes = 50,
    [int64]$Mask = 4294901760,
    [ValidateRange(1, 16)][int]$Threads = 16,
    [switch]$Transport,
    [switch]$Scent,
    [switch]$Concurrent,
    [string]$SpinLimit = 'default',
    [switch]$SampleCpu,
    [switch]$SampleCores,
    [switch]$HeatStop,
    [switch]$LogOutside,
    [string]$AllowBeside = '',
    [string]$Exe = '',
    [string]$RunsRoot = 'scratch\card\runs',
    # D201, a resumed group only: an edited copy of the source's config.json to carry on under, one for every world or a
    # comma list of one a world ('none' for a world under its source's own config; '-' too, but a list that starts with
    # it is read as a parameter name on a -File command line), and -TrickleOff for every world.
    [string]$ResumeConfig = '',
    [switch]$TrickleOff,
    # How long every world has to write run.json: a world of thousands resumed from a late checkpoint loads for a minute or more.
    [ValidateRange(30, 1800)][int]$WaitForManifestSeconds = 120,
    [switch]$Probe,
    # cpu runs the same group on ILGPU's CPU accelerator, off the card (nvcuda-guard.ps1): CUDA_VISIBLE_DEVICES=-1.
    [ValidateSet('cuda', 'cpu')][string]$Device = 'cuda')
$ErrorActionPreference = 'Continue'
if ($env:EVOSIM_TEST_CHECKPOINT_STALL_MS) {
    Write-Host "card-group refused: EVOSIM_TEST_CHECKPOINT_STALL_MS is set in this shell ($env:EVOSIM_TEST_CHECKPOINT_STALL_MS). It is a test hook that pauses every checkpoint's write; unset it."
    exit 2
}
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Under([string]$p) { if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $root $p } }
$exePath = if ($Exe) { Under $Exe } else { Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe' }
$runs = Under $RunsRoot
$logs = Join-Path $root 'scratch\logs'
$log  = Join-Path $logs "card-run-$Name.log"
$watchLog = Join-Path $logs "card-run-$Name-watch.log"
$gpuCsv = Join-Path $logs "card-run-$Name-gpu.csv"
$cpuTxt = Join-Path $logs "card-run-$Name-cpu.txt"
$coresCsv = Join-Path $logs "card-run-$Name-cores.csv"
$outsideTxt = Join-Path $logs "card-run-$Name-outside.txt"
$armList = @($Arms -split '[,\s]+' | Where-Object { $_ -ne '' })
if ($SampleCores) { . (Join-Path $PSScriptRoot 'core-sample.ps1') }
if ($HeatStop) { . (Join-Path $PSScriptRoot 'heat-stop.ps1') }
New-Item -ItemType Directory -Force $logs, (Join-Path $root 'scratch\card') | Out-Null
if (Test-Path $log) { Write-Host "$log exists; rename the last run's logs first"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Heat { $l = (& pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') -Log | Select-Object -Last 1); $c = $LASTEXITCODE; Say "heat: $l"; return $c }
function Manifest([string]$arm) {
    $m = Get-ChildItem (Join-Path $runs "$arm\*\run.json") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($m) { [pscustomobject]@{ Path = $m; Json = (Get-Content $m.FullName -Raw | ConvertFrom-Json) } } else { $null }
}

$spinName = 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit'
$spin = switch -Regex ($SpinLimit) {
    '^default$' { '5000'; break }
    '^unset$'   { $null; break }
    '^\d+$'     { $SpinLimit; break }
    default     { $null }
}
$wantTransport = if ($Transport) { 'card' } else { 'cpu' }
$build = $exePath -replace '\\artifacts\\.*$', ''
$atText = @($At -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
$atBad = @($atText | Where-Object { $_ -notmatch '^\d+$' })
$atList = @($atText | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
$founding = @($atList | Where-Object { $_ -ne 0 }).Count -eq 0
# Each world's resume second and the second it ends at.
function AtOf([int]$k) { $atList[[math]::Min($k, $atList.Count - 1)] }
function EndOf([int]$k) { if ($Until -gt 0) { $Until } else { (AtOf $k) + $Seconds } }
$seedList = @($Seeds -split '[,\s]+' | Where-Object { $_ -ne '' })
$sourceList = @($ResumeFrom -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
$origin = if ($founding) { "founded from $Launcher with seeds $($seedList -join ', ')" } else { "from $($sourceList -join ', ')" }
$exchangeWords = if ($MigrateEvery -gt 0) { "trading $MigrateCount every $MigrateEvery s in a ring" } else { 'no exchange' }
Say "card group begins: $Name on $Device, $($armList.Count) worlds $($armList -join ', ') $origin, $exchangeWords, checkpoints $(if ($CheckpointEvery -gt 0) { "every $CheckpointEvery s" } else { 'none' }), transport $wantTransport, scent $(if ($Scent) { 'card' } else { 'host' }), concurrent $([bool]$Concurrent), build $build at $(git -C $build rev-parse --short HEAD 2>$null), from $(if ($atList.Count -gt 1) { "$($atList -join '/') s" } else { "$At s" }) $(if ($Until -gt 0) { "to $Until s" } else { "for $Seconds s" }) at group $Group, $Threads threads on mask $Mask, wall $WallMinutes min, spin $(if ($null -ne $spin) { $spin } else { 'unset' })$(if ($Probe) { ', with the card probe (EVOSIM_GPU_PROBE=1)' })"
if ($SpinLimit -notmatch '^(default|unset|\d+)$') { Say "card group STOPPED: -SpinLimit '$SpinLimit' is neither 'default', 'unset' nor a whole number"; exit 1 }
if ($armList.Count -lt 2) { Say 'card group STOPPED: a group is two worlds or more'; exit 1 }
if (@($armList | Sort-Object -Unique).Count -ne $armList.Count -or $armList -contains $Name) { Say 'card group STOPPED: the group and its worlds need distinct names'; exit 1 }
if ($atBad.Count -gt 0 -or $atList.Count -eq 0) { Say "card group STOPPED: -At '$At' is not a second, or a comma list of seconds, of the source runs"; exit 1 }
if ($atList.Count -ne 1 -and $atList.Count -ne $armList.Count) { Say "card group STOPPED: -At names $($atList.Count) seconds for $($armList.Count) worlds (one for all, or one a world)"; exit 1 }
if (-not $founding -and @($atList | Where-Object { $_ -eq 0 }).Count -gt 0) { Say 'card group STOPPED: -At mixes 0, which founds, with resume seconds'; exit 1 }
if (($Seconds -gt 0) -eq ($Until -gt 0)) { Say 'card group STOPPED: give -Seconds, a span, or -Until, an end, and not both'; exit 1 }
for ($k = 0; $k -lt $armList.Count; $k++) {
    if ((EndOf $k) -le (AtOf $k)) { Say "card group STOPPED: $($armList[$k]) would end at $(EndOf $k) s, not past its -At $(AtOf $k) s"; exit 1 }
}
if ($MigrateEvery -lt 0) { Say 'card group STOPPED: -MigrateEvery is simulated seconds above 0, or 0 for no exchange'; exit 1 }
if ($founding) {
    if (-not $Launcher) { Say 'card group STOPPED: a founded group (-At 0) needs -Launcher, the environment block of its world'; exit 1 }
    $launcherCount = @($Launcher -split ',' | Where-Object { $_.Trim() -ne '' }).Count
    if ($launcherCount -ne 1 -and $launcherCount -ne $armList.Count) { Say "card group STOPPED: -Launcher names $launcherCount blocks for $($armList.Count) worlds (one for all, or one a world)"; exit 1 }
    if ($seedList.Count -ne $armList.Count) { Say "card group STOPPED: -Seeds names $($seedList.Count) seeds for $($armList.Count) worlds"; exit 1 }
    if (@($seedList | Where-Object { $_ -notmatch '^[1-9]\d*$' }).Count -gt 0) { Say "card group STOPPED: -Seeds '$Seeds' is not a list of whole numbers above 0"; exit 1 }
    if ($sourceList.Count -gt 0) { Say 'card group STOPPED: -ResumeFrom with -At 0; a founded group has no source'; exit 1 }
} else {
    if ($sourceList.Count -ne 1 -and $sourceList.Count -ne $armList.Count) { Say "card group STOPPED: -ResumeFrom names $($sourceList.Count) sources for $($armList.Count) worlds (one for all, or one a world)"; exit 1 }
    if ($Launcher -or $Seeds) { Say 'card group STOPPED: -Launcher and -Seeds found a group, and a resumed world reads both from its source'; exit 1 }
}
$cousinList = @($ResumeConfig -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
if ($founding -and ($cousinList.Count -gt 0 -or $TrickleOff)) { Say 'card group STOPPED: -ResumeConfig and -TrickleOff change a resumed world, and this group is founded'; exit 1 }
if ($cousinList.Count -gt 0 -and $cousinList.Count -ne 1 -and $cousinList.Count -ne $armList.Count) { Say "card group STOPPED: -ResumeConfig names $($cousinList.Count) configs for $($armList.Count) worlds (one for all, or one a world)"; exit 1 }
$cousinPaths = @()
foreach ($c in $cousinList) {
    if ($c -eq '-' -or $c -eq 'none') { $cousinPaths += ''; continue }
    $cp = Under $c
    if (-not (Test-Path -LiteralPath $cp)) { Say "card group STOPPED: no config at $cp"; exit 1 }
    $cousinPaths += (Resolve-Path -LiteralPath $cp).Path
}

if ($AllowBeside) {
    if ($AllowBeside.Length -lt 8) { Say "card group STOPPED: -AllowBeside '$AllowBeside' is under 8 characters; name a command line, not a pattern that passes everything"; exit 1 }
    try { $null = [regex]::new($AllowBeside) } catch { Say "card group STOPPED: -AllowBeside '$AllowBeside' is not a regular expression"; exit 1 }
}
function Test-AllowedBeside($q) { [bool]($AllowBeside -and $q.Name -eq 'dotnet.exe' -and "$($q.CommandLine)" -match $AllowBeside) }
$procsNow = @(Get-CimInstance Win32_Process)
foreach ($q in @($procsNow | Where-Object { Test-AllowedBeside $_ })) {
    $cuda = try { @((Get-Process -Id $q.ProcessId -ErrorAction Stop).Modules | Where-Object ModuleName -eq 'nvcuda.dll').Count -gt 0 } catch { $null }
    if ($cuda) { Say "card group STOPPED: dotnet $($q.ProcessId) matches -AllowBeside but has the card's driver (nvcuda.dll) loaded"; exit 1 }
    $cmd = "$($q.CommandLine)"; if ($cmd.Length -gt 200) { $cmd = $cmd.Substring(0, 200) }
    Say "  allowed beside: dotnet $($q.ProcessId)$(if ($null -eq $cuda) { ' (its modules unreadable)' }): $cmd"
}
$busy = @($procsNow | Where-Object {
    ($_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
     ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run\.ps1|card-pair\.ps1|driver-watch')) -and
    -not (Test-AllowedBeside $_) })
if ($busy) { Say "card group STOPPED: busy: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"; exit 1 }
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = (Get-Date).AddMinutes(-1) } -ErrorAction SilentlyContinue)
if ($ev) { Say 'card group STOPPED: an nvlddmkm event in the last minute'; exit 1 }
foreach ($arm in $armList) {
    if (Test-Path (Join-Path $runs $arm)) { Say "card group STOPPED: $runs\$arm exists"; exit 1 }
    foreach ($f in "$arm.args", "$arm.log", "$arm.err") {
        if (Test-Path (Join-Path $logs $f)) { Say "card group STOPPED: $logs\$f exists"; exit 1 }
    }
}
foreach ($f in $watchLog, $gpuCsv, $cpuTxt, $coresCsv, $outsideTxt) { if (Test-Path $f) { Say "card group STOPPED: $f exists"; exit 1 } }
if (-not (Test-Path $exePath)) { Say "card group STOPPED: no farm build at $exePath"; exit 1 }
$buildRoot = & git -C (Split-Path -Parent $exePath) rev-parse --show-toplevel 2>$null
if (-not $buildRoot) { Say "card group STOPPED: no git checkout holds $exePath"; exit 1 }
$buildRoot = [IO.Path]::GetFullPath($buildRoot)
$sources = @()
foreach ($src in $sourceList) {
    $from = Join-Path $runs $src
    if (-not (Test-Path -LiteralPath $from)) { $from = Under $src }
    if (-not (Test-Path -LiteralPath $from)) { Say "card group STOPPED: '$src' is neither under $runs nor a path that exists"; exit 1 }
    $sources += (Resolve-Path -LiteralPath $from).Path
}
# A founded group's world, as run-farm.ps1 reads -Launcher: refused rather than dot-sourced if the block launches
# anything (comment lines aside), and every key must be an EVOSIM_ setting.
$blocks = @(@{})
if ($founding) {
    $blocks = @()
    foreach ($one in @($Launcher -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })) {
        $launcherPath = Under $one
        if (-not (Test-Path -LiteralPath $launcherPath)) { Say "card group STOPPED: no launcher at $launcherPath"; exit 1 }
        $text = ((Get-Content -LiteralPath $launcherPath) -notmatch '^\s*#') -join "`n"
        if ($text -match 'run-arm\.ps1|run-farm\.ps1|card-run\.ps1|card-group\.ps1|Start-Process|Start-Job') {
            Say "card group STOPPED: $launcherPath launches something itself, so it was not dot-sourced"; exit 1
        }
        $s = $null
        $block = @{}
        $produced = . $launcherPath
        foreach ($candidate in @($produced) + @($s)) { if ($candidate -is [hashtable]) { $block = $candidate } }
        if ($block.Count -eq 0) { Say "card group STOPPED: $launcherPath produced no hashtable of settings"; exit 1 }
        $bad = @($block.Keys | Where-Object { $_ -notmatch '^EVOSIM_' })
        if ($bad) { Say "card group STOPPED: $launcherPath sets $($bad -join ', '), not EVOSIM_ settings"; exit 1 }
        if ($block.ContainsKey('EVOSIM_TEST_CHECKPOINT_STALL_MS')) { Say 'card group STOPPED: the launcher sets the checkpoint stall test hook'; exit 1 }
        Say "  launcher: $launcherPath, $($block.Count) settings"
        $blocks += , $block
    }
}
if ((Heat) -ge 1) { Say 'card group STOPPED: the heat is at WARN or above'; exit 1 }

# Each world's settings, as run-farm.ps1 puts them on a command line, one EVOSIM_NAME=value a line.
$culture = [Globalization.CultureInfo]::InvariantCulture
$files = @()
for ($k = 0; $k -lt $armList.Count; $k++) {
    $arm = $armList[$k]
    # The world's block first, sorted, then this script's settings over it.
    $block = $blocks[[math]::Min($k, $blocks.Count - 1)]
    $s = [ordered]@{}
    foreach ($key in ($block.Keys | Sort-Object)) { $s[$key] = $block[$key] }
    if (-not $founding) {
        $s.EVOSIM_ALLOW_SOURCE_MISMATCH = 1
        $s.EVOSIM_RESUME = $sources[[math]::Min($k, $sources.Count - 1)]
        $s.EVOSIM_RESUME_AT = AtOf $k
        if ($cousinPaths.Count -gt 0) {
            $cousin = $cousinPaths[[math]::Min($k, $cousinPaths.Count - 1)]
            if ($cousin) { $s.EVOSIM_RESUME_CONFIG = $cousin }
        }
        if ($TrickleOff) { $s.EVOSIM_RESUME_TRICKLE_OFF = 1 }
    }
    $s.EVOSIM_DIGEST_EVERY = 100
    $s.EVOSIM_ENGINE = 'gpu'
    $s.EVOSIM_GPU_DEVICE = $Device
    $s.EVOSIM_GPU_GROUP = $Group
    $s.EVOSIM_GPU_PRECISION = 'single'
    $s.EVOSIM_OUT = "$arm.md"
    $s.EVOSIM_REPO_ROOT = $buildRoot
    $s.EVOSIM_RUNS_ROOT = $runs
    $s.EVOSIM_SECONDS = EndOf $k
    Say "  ${arm}: $(if ($founding) { "founded with seed $($seedList[$k])" } else { "resumes $($sources[[math]::Min($k, $sources.Count - 1)]) at $(AtOf $k) s$(if ($s.Contains('EVOSIM_RESUME_CONFIG')) { " under $($s.EVOSIM_RESUME_CONFIG)" })$(if ($TrickleOff) { ', trickle off' })" }), to $(EndOf $k) s"
    $s.EVOSIM_SEED = if ($founding) { [uint64]$seedList[$k] } else { 1 }
    $s.EVOSIM_THREADS = $Threads
    $s.EVOSIM_WALL_MINUTES = $WallMinutes
    if ($CheckpointEvery -gt 0) { $s.EVOSIM_CHECKPOINT_EVERY = $CheckpointEvery }
    if ($MigrateEvery -gt 0) {
        $s.EVOSIM_MIGRATE_EVERY = $MigrateEvery
        $s.EVOSIM_MIGRATE_COUNT = $MigrateCount
    }
    if ($Transport) { $s.EVOSIM_GPU_TRANSPORT = 1 }
    if ($Scent) { $s.EVOSIM_GPU_SCENT = 1 }
    if ($Concurrent) { $s.EVOSIM_GPU_CONCURRENT = 1 }
    $path = Join-Path $logs "$arm.args"
    @("# $arm, one world of the card group $Name (card-group.ps1)") + @($s.Keys | ForEach-Object {
        $v = $s[$_]; "$_=$(if ($v -is [IFormattable]) { $v.ToString($null, $culture) } else { [string]$v })" }) |
        Set-Content $path -Encoding utf8NoBOM
    Say "  $arm settings: $path"
    $files += $path
}

$start = Get-Date
$settings = Join-Path $root "scratch\card\card-run-$Name-watch.json"
[ordered]@{
    Since      = $start.AddMinutes(-1).ToString('s')
    Deadline   = $start.AddMinutes($WallMinutes + 6).ToString('s')
    JobLog     = $log
    EndPattern = 'books over'
    StopLine   = 'card group STOPPED by the driver watch: nvlddmkm event'
    Controller = 'card-group\.ps1'
    Arm        = $armList[0]
    RunsRoot   = $runs
} | ConvertTo-Json | Set-Content $settings -Encoding utf8
$w = Start-Process pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'driver-watch.ps1'), '-Settings', $settings) `
     -WindowStyle Hidden -PassThru -RedirectStandardOutput $watchLog -RedirectStandardError "$watchLog.err"
$d = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $d -and -not ((Test-Path $watchLog) -and (Get-Content $watchLog -TotalCount 1))) { Start-Sleep -Milliseconds 500 }
$first = if (Test-Path $watchLog) { Get-Content $watchLog -TotalCount 1 } else { '' }
# The line is copied with its end pattern masked, so nothing in this log says it before the final read.
Say "watch pid $($w.Id): $($first -replace 'books over', '<end pattern>')"
if ($w.HasExited -or $first -notmatch "watch begins: .*end pattern 'books over', controller 'card-group\\\.ps1'") {
    Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue
    Say 'card group STOPPED: the watch did not begin as it should'; exit 1
}

Set-Location $root
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
if ($Device -eq 'cpu') { Enter-OffCard } else { Remove-Item Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue }
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
if ($Probe) { $env:EVOSIM_GPU_PROBE = '1' }
$out = Join-Path $logs "card-group-$Name.out"
# The spin for the launch alone, as run-farm.ps1 sets it: the farm reads it from the environment it starts in.
$spinBefore = [Environment]::GetEnvironmentVariable($spinName)
[Environment]::SetEnvironmentVariable($spinName, $spin)
try {
    $p = Start-Process -FilePath $exePath -ArgumentList (@('--group') + $files) -WorkingDirectory $root `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
} finally {
    [Environment]::SetEnvironmentVariable($spinName, $spinBefore)
}
try { $p.ProcessorAffinity = [IntPtr]$Mask; $p.PriorityClass = 'BelowNormal' } catch {}
$farmPid = [int]$p.Id
$partner = @()
Say "  farm pid $($p.Id), --group $($files -join ' '), $spinName $(if ($null -ne $spin) { $spin } else { '(unset)' })"

function Stop-All([string]$why, [int]$code) {
    # A world that never wrote run.json has nothing to stop, and stop-arm throws on it ("Cannot tell which
    # worker"). Uncaught, that ended this script before its STOPPED line, and left the watch running to its
    # deadline holding the caller's output pipe (r60s check, 2026-10-14).
    foreach ($arm in $armList) {
        if (-not (Manifest $arm)) { Say "  stop ${arm}: no run.json, the world never began"; continue }
        try { & (Join-Path $root 'scripts\stop-arm.ps1') $arm -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop ${arm}: $_" } }
        catch { Say "  stop ${arm}: $_" }
    }
    # Five minutes: a world reaches its next report row slowly on the CPU accelerator (about 200 s for 10 s).
    $d = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $d -and -not $p.HasExited) { Start-Sleep -Seconds 1 }
    Say "  farm exited: $($p.HasExited)$(if ($p.HasExited) { ", code $($p.ExitCode)" })"
    # A farm still founding its last worlds when the stop came reads its STOP files only after it has founded
    # them all. The watch guards the card while it runs, and is stopped once it has gone: it waits for the
    # books line, which a stopped group never writes, so left alone it held the card's busy check to its
    # deadline (16 worlds founded as one group, 2026-10-16).
    if (-not $p.HasExited) {
        Say "  the farm still runs; the watch stays until it exits, for up to 30 minutes more"
        $d = (Get-Date).AddMinutes(30)
        while ((Get-Date) -lt $d -and -not $p.HasExited) { Start-Sleep -Seconds 5 }
        Say "  farm exited: $($p.HasExited)$(if ($p.HasExited) { ", code $($p.ExitCode)" })"
    }
    if ($p.HasExited) { Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue; Say "  watch pid $($w.Id) stopped: the farm is gone" }
    else { Say "  watch pid $($w.Id) left running: the farm $($p.Id) has not exited; stop it with scripts/stop-arm.ps1 and the watch by id" }
    Say "card group STOPPED: $why"; exit $code
}

# Every world's run.json, each naming this process: the pin is checked on the process they name.
$d = (Get-Date).AddSeconds($WaitForManifestSeconds)
$seen = @{}
while ((Get-Date) -lt $d -and $seen.Count -lt $armList.Count -and -not $p.HasExited) {
    foreach ($arm in $armList) { $m = Manifest $arm; if ($m) { $seen[$arm] = $m.Json } }
    if ($seen.Count -lt $armList.Count) { Start-Sleep -Seconds 1 }
}
if ($seen.Count -lt $armList.Count) {
    $why = if ($p.HasExited) { "the farm exited (code $($p.ExitCode)) before every world wrote run.json; read $out.err and each world's .err" } else { "not every world wrote run.json within $WaitForManifestSeconds s" }
    if (-not $p.HasExited) { Stop-All $why 1 }
    # The farm is gone before a world began, so nothing is left on the card to watch; without this the watch
    # waits out its deadline and the next launch's busy check finds it.
    Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue
    Say "  watch pid $($w.Id) stopped: the farm exited before any world began"
    Say "card group STOPPED: $why"; exit 1
}
foreach ($arm in $armList) {
    $j = $seen[$arm]
    if ([int]$j.processId -ne $p.Id) { Stop-All "$arm's run.json names process $($j.processId), not $($p.Id)" 1 }
    Say "  ${arm}: pid $($p.Id) pinned to $([int64]$p.ProcessorAffinity), $($p.PriorityClass); group '$($j.group)'; sharedProcessWith '$($j.gpu.sharedProcessWith)'; threadPoolSpinLimit '$($j.threadPoolSpinLimit)'"
    Say "  ${arm}: transport in run.json: '$($j.transport)' (asked for '$wantTransport')"
    if ($j.transport -ne $wantTransport) { Stop-All "${arm}'s transport is '$($j.transport)', not '$wantTransport'" 1 }
}

while ((Get-Date) -lt $start.AddSeconds(30)) { Start-Sleep -Seconds 1 }
if ($w.HasExited) { Stop-All 'the driver watch ended before the run' 3 }
Say "  watch alive after its first polls ($(Get-Date -Format 'HH:mm:ss'))"

# The processes beside the run (-LogOutside), one CIM query a turn, as card-run.ps1 logs them.
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
        $firstSeen = ($g.Group | Sort-Object Seen | Select-Object -First 1).Seen.ToString('HH:mm:ss')
        $affs = ($g.Group | ForEach-Object Affinity | Sort-Object -Unique) -join '/'
        '{0} x{1}, first seen {2}, affinity {3}, {4:N0} s seen in all' -f $g.Name, $g.Count, $firstSeen, $affs, $seen
    }
    return "$($outsideSeen.Count) beside the run ($outsideTxt): $($parts -join '; ')"
}

$stopBy = (Get-Date).AddMinutes($WallMinutes + 3)
$turn = 0
$paceCheck = Join-Path $root 'scripts\pace-check.py'
$nextPace = (Get-Date).AddMinutes(15)
if (-not (Test-Path $paceCheck)) { Say "  no $paceCheck in this build; the group's pace is not read" }
while ((Get-Date) -lt $stopBy) {
    $done = @($armList | Where-Object { $m = Manifest $_; $m -and $m.Json.status -ne 'running' })
    if ($done.Count -lt $armList.Count -and $w.HasExited) { Stop-All 'the driver watch ended while the farm runs' 3 }
    if ($done.Count -eq $armList.Count) {
        $d = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $d -and -not $p.HasExited) { Start-Sleep -Seconds 1 }
        Say "  farm exited: $($p.HasExited)$(if ($p.HasExited) { ", code $($p.ExitCode)" })"
        if ($Device -eq 'cpu') { Say "  $(Resolve-OffCard)" }
        [void](Heat)
        foreach ($arm in $armList) {
            $m = Manifest $arm
            $j = $m.Json
            $rows = @(Get-Content (Join-Path $m.Path.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            $s = $rows[-1]
            $g = $j.gpu
            # As card-run.ps1's: the seconds this world stepped over its own whole wall, start-up included.
            $from = AtOf ([array]::IndexOf($armList, $arm))
            $pace = ([double]$s.t - $from) / [math]::Max(1e-9, [double]$j.wallClockMinutes * 60)
            Say ("  {0}: {1} ({2}); t {3} alive {4}; {5:N1} wall min, {6:N2} times real time over the {7} s stepped; run {8}" -f $arm, $j.status, $j.reason, $s.t, $s.alive, $j.wallClockMinutes, $pace, ([double]$s.t - $from), $m.Path.DirectoryName)
            Say "  card ${arm}: transport $($j.transport), precision $($g.precision), group $($g.groupSize), device $($g.device), refusedForClass $($g.refusedForClass), overflow $($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries), neuronsAtCeiling $($g.neuronsAtCeiling), compile $($g.compileMs) ms, sharedProcessWith $($g.sharedProcessWith)"
            Say "  ${arm}: divergedTotal $($j.divergedTotal); driveImpulsesLimited $($j.driveImpulsesLimited) and dragImpulsesLimited $($j.dragImpulsesLimited), cumulative since the founding"
            $engine = Get-Content (Join-Path $logs "$arm.log") -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
            Say "  ${arm}'s $engine"
        }
        if ($SampleCores) { Say "  $(Get-CoreSummary $coresCsv)" }
        if ($LogOutside) { Say "  outside: $(Get-OutsideSummary)" }
        # The books last, one line a world, so the watch's end pattern falls after every other read.
        foreach ($arm in $armList) {
            $m = Manifest $arm
            $rows = @(Get-Content (Join-Path $m.Path.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            $audit = ($rows | ForEach-Object { [math]::Abs([double]$_.auditResidual) } | Measure-Object -Maximum).Maximum
            $matter = ($rows | ForEach-Object { [math]::Abs([double]$_.matterResidual) } | Measure-Object -Maximum).Maximum
            Say "  books over $($rows.Count) stats rows of ${arm}: max |auditResidual| $audit (under 0.1 J: $($audit -lt 0.1)); max |matterResidual| $matter (under 1e-5: $($matter -lt 1e-5))"
        }
        Say 'card group ends'
        exit 0
    }
    # The worlds' pace every 15 minutes (proposals/propose-farm-pace-guard.md, item 5): pace-check.py's lines
    # into this log, SLOW among them for the watching agent's next look. It stops nothing; the farm's guard does.
    if ((Test-Path $paceCheck) -and (Get-Date) -ge $nextPace) {
        $nextPace = (Get-Date).AddMinutes(15)
        $paceDirs = @($armList | ForEach-Object { $m = Manifest $_; if ($m) { $m.Path.DirectoryName } })
        if ($paceDirs.Count -gt 0) { & python -I $paceCheck @paceDirs 2>&1 | ForEach-Object { Say "  $_" } }
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
            Say "card group: heat stop, $why; stopping every world"
            [void](Heat)
            if ($LogOutside) { Write-Outside; Say "  outside: $(Get-OutsideSummary)" }
            # Stop-All writes each world's STOP, waits for the farm, and stops the watch once it is gone, which
            # otherwise waits out its deadline and refuses every card launch meanwhile (round 59's heat stop).
            Stop-All "heat, $why" 4
        }
    }
    if ($SampleCores) { Write-CoreSample $coresCsv }
    if ($LogOutside) { Write-Outside }
    $turn++
    Start-Sleep -Seconds 10
}
Stop-All "past $WallMinutes minutes" 2
