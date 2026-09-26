<#
.SYNOPSIS
  A farm round's overnight queue: its seeds one at a time, each waiting for the one before it,
  with the snow's transport on the card under -GpuTransport (D126).

.DESCRIPTION
  Runs scripts/run-farm.ps1 once a seed from -Tree, a committed and clean checkout: a farm
  round's pre-registration record is the manifest's gitCommit on a clean tree (CLAUDE.md), so a
  dirty tree stops the queue. Each seed waits for no Evosim.Farm process to be running and for
  the seed before it to leave `running`.

  Under -GpuTransport each seed is launched with EVOSIM_GPU_TRANSPORT=1, which gives the CPU's
  bits sooner. Two card faults are handled, and neither changes a result:
  - The launch refuses the card: no run.json, and the card named on stderr. The seed is launched
    again on the CPU's transport, and the log says so.
  - The card ends the seed mid-run: status `error` with the card named in the ending or on
    stderr. The seed is resumed from its last checkpoint on the CPU's transport into the arm
    <arm>c, with the wall that is left, and a read stitches the two at that checkpoint.
  Any other error is left as it is, since the world would do the same on the CPU. The card's
  temperature, power, load and memory are logged at each seed's start and end.

  Start it detached (CLAUDE.md: a queue inside a background task dies with it):
    Start-Process pwsh -WindowStyle Hidden -RedirectStandardOutput <log> -ArgumentList `
      '-NoProfile','-File','scripts/farm-queue.ps1','-Round','r51','-Tree',<tree>, `
      '-Launcher','rounds/env-r51.ps1','-Seeds','1,2,3','-GpuTransport'

.PARAMETER Seeds
  A comma list as one string ('1,2,3'), because a list passed through -File arrives as one
  string anyway (CLAUDE.md, new-worker.ps1's gotcha).

.PARAMETER After
  A script run once the last seed has ended (round 50's V2, say).

.PARAMETER RehearseFault
  Rehearses the second fault: the first seed is stopped at this simulated second and treated as
  a card fault, so the continuation path runs. 0, the default, rehearses nothing.
#>
param(
    [Parameter(Mandatory)][string]$Round,
    [Parameter(Mandatory)][string]$Tree,
    [Parameter(Mandatory)][string]$Launcher,
    [string]$Seeds = '1,2,3',
    [double]$Seconds = 30000,
    [int]$Threads = 16,
    [double]$WallMinutes = 600,
    [string]$RunsRoot = 'D:\Projects\experiments\evolution-simulator\runs',
    [switch]$GpuTransport,
    [string]$After = '',
    [double]$RehearseFault = 0
)
$ErrorActionPreference = 'Continue'
$cardPattern = 'EVOSIM_GPU_TRANSPORT|ILGPU|Cuda|CUDA|GpuTransport|[Aa]ccelerator'
$seedList = @($Seeds -split '[,\s]+' | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ })
$launcherPath = if ([IO.Path]::IsPathRooted($Launcher)) { $Launcher } else { Join-Path $Tree $Launcher }
$runFarm = Join-Path $Tree 'scripts\run-farm.ps1'

function Say([string]$text) { Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $text" }

function Card {
    $r = & nvidia-smi --query-gpu=temperature.gpu,power.draw,utilization.gpu,memory.used --format=csv,noheader 2>$null
    if ($LASTEXITCODE -eq 0 -and $r) { return "card $r (C, W, load, memory)" }
    return 'card: nvidia-smi read nothing'
}

function NewestRun([string]$arm, [datetime]$since) {
    $dir = Join-Path $RunsRoot $arm
    if (-not (Test-Path -LiteralPath $dir)) { return $null }
    Get-ChildItem -LiteralPath $dir -Directory |
        Where-Object { (Test-Path (Join-Path $_.FullName 'run.json')) -and $_.CreationTime -ge $since.AddMinutes(-1) } |
        Sort-Object Name | Select-Object -Last 1
}

function Manifest($run) { Get-Content -LiteralPath (Join-Path $run.FullName 'run.json') -Raw | ConvertFrom-Json }

function LastSecond($run) {
    $p = Join-Path $run.FullName 'stats.jsonl'
    if (-not (Test-Path -LiteralPath $p)) { return 0 }
    $fs = [IO.File]::Open($p, 'Open', 'Read', 'ReadWrite')
    try {
        $n = [Math]::Min($fs.Length, 65536)
        $fs.Seek(-$n, 'End') | Out-Null
        $buf = New-Object byte[] $n
        [void]$fs.Read($buf, 0, $n)
        $lines = [Text.Encoding]::UTF8.GetString($buf) -split "`n" | Where-Object { $_ -match '^\{"t":' }
        if (-not $lines) { return 0 }
        return [double](([regex]'^\{"t":([0-9.]+)').Match($lines[-1]).Groups[1].Value)
    } finally { $fs.Dispose() }
}

function ErrLog([string]$arm) { Join-Path $Tree "scratch\logs\$arm.err" }

function Launch([string]$arm, [int]$seed, [bool]$onCard, [double]$wall) {
    $envs = @{}
    if ($onCard) { $envs['EVOSIM_GPU_TRANSPORT'] = 1 }
    Push-Location $Tree
    try {
        & $runFarm $arm -Launcher $launcherPath -Seed $seed -Seconds $Seconds -Threads $Threads `
            -RunsRoot $RunsRoot -WallMinutes $wall -Env $envs |
            Select-String 'launched|status|gitCommit|configHash|refus|error|warn' | ForEach-Object { Say "  $_" }
    } finally { Pop-Location }
}

# Waits for the run to leave `running`; returns its manifest, or $null past the deadline.
function WaitEnd($run, [double]$wall, [double]$stopAt) {
    $deadline = (Get-Date).AddMinutes($wall + 30)
    $stopped = $false
    while ((Get-Date) -lt $deadline) {
        $m = Manifest $run
        if ($m.status -and $m.status -ne 'running') { return $m }
        if ($stopAt -gt 0 -and -not $stopped -and (LastSecond $run) -ge $stopAt) {
            Set-Content -LiteralPath (Join-Path $run.FullName 'STOP') -Value 'rehearsed card fault'
            Say "  rehearsal: STOP written at t=$(LastSecond $run) s"
            $stopped = $true
        }
        Start-Sleep -Seconds 30
    }
    return $null
}

$queueStart = Get-Date
Say "== $Round queue: seeds $($seedList -join ', '), $Seconds s at $Threads threads, wall $WallMinutes min, transport $(if ($GpuTransport) { 'card' } else { 'cpu' }), tree $Tree"
foreach ($seed in $seedList) {
    $arm = "$Round-s$seed"
    $dirty = git -C $Tree status --porcelain --untracked-files=no
    if ($dirty) { Say "seed ${seed}: the tree is dirty, not launched:"; $dirty; exit 1 }
    while (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 60 }

    Say "== seed $seed, commit $(git -C $Tree rev-parse --short HEAD); $(Card)"
    $launchedAt = Get-Date
    $onCard = [bool]$GpuTransport
    Launch $arm $seed $onCard $WallMinutes
    $run = NewestRun $arm $launchedAt
    if (-not $run -and $onCard) {
        $why = if (Test-Path (ErrLog $arm)) { (Get-Content (ErrLog $arm) | Select-String $cardPattern | Select-Object -First 1) } else { $null }
        if ($why) {
            Say "  the card refused the launch: $why"
            Say "  launching seed $seed again on the CPU's transport (the same bits)"
            $onCard = $false
            $launchedAt = Get-Date
            Launch $arm $seed $false $WallMinutes
            $run = NewestRun $arm $launchedAt
        }
    }
    if (-not $run) { Say "  seed ${seed}: no run.json; read $(ErrLog $arm). On to the next seed."; continue }

    $m = Manifest $run
    $engine = Get-Content -LiteralPath (Join-Path $RunsRoot "$arm.md") -TotalCount 40 -ErrorAction SilentlyContinue |
        Select-String 'engine=([^·]+)' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }
    Say "  run $($run.Name), transport $($m.transport), threads $($m.threads), header engine=$engine"
    if ($onCard -and $m.transport -ne 'card') { Say "  WARNING: launched on the card and the manifest says transport $($m.transport)" }

    $stopAt = if ($RehearseFault -gt 0 -and $seed -eq $seedList[0]) { $RehearseFault } else { 0 }
    $end = WaitEnd $run $WallMinutes $stopAt
    if (-not $end) { Say "  seed ${seed}: still running past its wall and half an hour; on to the next seed"; continue }
    Say "  seed $seed ended: $($end.status) $($end.reason); $(Card)"

    $cardFault = $false
    if ($stopAt -gt 0 -and $end.status -eq 'stopped') { $cardFault = $true; Say "  rehearsal: treated as a card fault" }
    elseif ($onCard -and $end.status -eq 'error') {
        $text = "$($end.ending) " + $(if (Test-Path (ErrLog $arm)) { Get-Content (ErrLog $arm) -Raw } else { '' })
        if ($text -match $cardPattern) { $cardFault = $true; Say "  the card ended the seed: $($end.ending)" }
        else { Say "  an error the card is not named in; left as it is: $($end.ending)" }
    }
    if ($cardFault) {
        $ck = Get-ChildItem -LiteralPath (Join-Path $run.FullName 'checkpoints') -Filter '*.ckpt' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        if (-not $ck) { Say "  no checkpoint to resume from; seed $seed stays as it ended"; continue }
        $left = [Math]::Max(60, $WallMinutes - ((Get-Date) - $launchedAt).TotalMinutes)
        $at = [double]($ck.BaseName.TrimStart('0'))
        Say "  resuming seed $seed from $at s on the CPU's transport into $arm`c, wall $([Math]::Round($left)) min"
        $resumedAt = Get-Date
        Push-Location $Tree
        try {
            & $runFarm "$arm`c" -ResumeFrom $run.FullName -At $at -Seconds $Seconds -Threads $Threads `
                -RunsRoot $RunsRoot -WallMinutes $left |
                Select-String 'launched|status|from|refus|error|warn' | ForEach-Object { Say "  $_" }
        } finally { Pop-Location }
        $cont = NewestRun "$arm`c" $resumedAt
        if (-not $cont) { Say "  the continuation wrote no run.json; read $(ErrLog "$arm`c")"; continue }
        $cm = Manifest $cont
        Say "  continuation $($cont.Name), transport $($cm.transport)"
        $cend = WaitEnd $cont $left 0
        if ($cend) { Say "  continuation ended: $($cend.status) $($cend.reason)" } else { Say "  continuation still running past its wall" }
    }
}
if ($After -ne '') {
    while (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 60 }
    Say "== after: $After"
    & $After
}
Say "== $Round queue done, $([Math]::Round(((Get-Date) - $queueStart).TotalMinutes)) min"