<#
.SYNOPSIS
  Launch one evolution arm on the farm out of Unity (src/Evosim.Farm).

.DESCRIPTION
  run-arm.ps1's counterpart for the second farm. It is a separate script rather than a mode of
  that one because almost nothing they do is shared: there is no worker project to refuse, no
  Library to corrupt, no simHash over Assets/Evosim, no asset reimport to wait through, and no
  Editor in the process cap. What is left in common is the shape of the thing — a name, a seed,
  an environment, a manifest read back at launch so a stale build is visible on screen rather
  than in a post-mortem.

  The settings reach the run as EVOSIM_NAME=value arguments, which the console program takes and
  an -executeMethod could not. They are the same names EvolutionRun binds, plus EVOSIM_THREADS
  (pace, never a realisation: the solver's digests are equal at 1, 4 and 16 threads) and
  EVOSIM_RUNS_ROOT (where the report and the run directory land).

  Stopping is not killing. The program reads a file named STOP in its own run directory between
  report rows and ends itself in an orderly way; ./scripts/stop-arm.ps1 writes it, and this
  script writes one itself when -ExpectDynamicsHash does not match what the run recorded.

.PARAMETER Arm
  Arm name. Becomes <RunsRoot>/<Arm>.md, <RunsRoot>/<Arm>/<run>/ and the log file names.

.PARAMETER Seed
  The world's seed. Default 1.

.PARAMETER Seconds
  Simulated-second budget. Default 30000.

.PARAMETER WallMinutes
  Wall-clock budget. Default 600.

.PARAMETER Threads
  Threads the body pass runs on (EVOSIM_THREADS). 0 is one per processor. It decides pace and
  not the trajectory, so two arms differing only in this are the same world.

.PARAMETER RunsRoot
  Where the report and the run directory go. Default runs/ under the repository; a relative path
  resolves against the repository, not against the shell's directory.

.PARAMETER Exe
  The program. Default artifacts/Evosim.Farm/bin/Release/net8.0/Evosim.Farm.exe under the
  repository — pass another to run a worktree's build.

.PARAMETER Env
  Hashtable of EVOSIM_* settings, e.g. @{ EVOSIM_AREA = 2200; EVOSIM_DEPTH = 45 }. The named
  parameters above win over anything of the same name in here, so the command line always says
  what it looks like it says.

.PARAMETER Launcher
  A .ps1 that produces the same hashtable, dot-sourced — the round launchers' environment block
  without their call to run-arm.ps1. The script must emit the hashtable, or leave it in $s, and
  must not launch anything itself: a launcher that calls run-arm.ps1 is refused rather than run,
  because dot-sourcing it would start a Unity arm nobody asked for. -Env is merged over it.

.PARAMETER CheckpointEvery
  Simulated seconds between checkpoints (EVOSIM_CHECKPOINT_EVERY). 0, the default, writes none.
  A recording setting like the state stream's: it reaches no config and moves no hash, so an arm
  that checkpoints and one that does not are the same world.

.PARAMETER ResumeFrom
  Start from a recorded world instead of from a founding lottery. Takes a .ckpt file, a run
  directory or an arm directory (the newest run under it that holds checkpoints), under RunsRoot
  or anywhere else. The new run is a new directory with its own report: its config is read from
  the run being resumed, so the world is the same world, and run.json carries a resumedFrom block
  naming the arm, the run, the second and the digest of the checkpoint it read.

.PARAMETER At
  The simulated second to resume at. The checkpoint at that second, or the last one before it; 0,
  the default, takes the last checkpoint there is.

.PARAMETER AllowSourceMismatch
  Resume even when the checkpoint's configHash, coreHash, dynamicsHash or farmHash is not this
  build's. What comes out is a cousin of the recording rather than its continuation, and the
  manifest says so; without this the resume is refused and prints which of the four differ.

.PARAMETER ExpectDynamicsHash
  Optional. The dynamicsHash the solver source is expected to carry. There is nothing to check
  before the launch — the digest is over src/Evosim.Dynamics as the run itself reads it — so the
  check is made against run.json and a mismatch STOPs the run it just started. A prefix of at
  least 8 characters is accepted.

.PARAMETER WaitForManifestSeconds
  How long to wait for run.json. Default 60: this program starts in a second or two, so a minute
  is a wedged world-construction rather than a cold import. 0 returns as soon as it is launched.

.PARAMETER WhatIf
  Print the command and change nothing.

.EXAMPLE
  ./scripts/run-farm.ps1 -Arm r43-s1 -Seed 1 -Seconds 30000 -Threads 8 -Launcher rounds/env-r43.ps1

.EXAMPLE
  ./scripts/run-farm.ps1 -Arm t20 -Seconds 20 -RunsRoot scratch/farm-tooling/runs -Env @{ EVOSIM_AREA = 100 }
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [uint64]$Seed = 1,   # not [ulong]: that accelerator is PowerShell 7 only
    [double]$Seconds = 30000,
    [double]$WallMinutes = 600,
    [int]$Threads = 0,
    [string]$RunsRoot = 'runs',
    [string]$Exe = '',
    [hashtable]$Env = @{},
    [string]$Launcher = '',
    [double]$CheckpointEvery = 0,
    [string]$ResumeFrom = '',
    [double]$At = 0,
    [switch]$AllowSourceMismatch,
    [string]$ExpectDynamicsHash = '',
    [int]$WaitForManifestSeconds = 60
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Resolve-UnderRepo([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    return (Join-Path $root $path)
}

$exePath = if ($Exe -ne '') { Resolve-UnderRepo $Exe }
           else { Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe' }
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "No farm program at $exePath. Build it: dotnet build src/Evosim.Farm -c Release."
}

$runsRootPath = Resolve-UnderRepo $RunsRoot

# The settings. The launcher first, -Env over it, the named parameters over both.
$settings = @{}

if ($Launcher -ne '') {
    $launcherPath = Resolve-UnderRepo $Launcher
    if (-not (Test-Path -LiteralPath $launcherPath)) { throw "No launcher at $launcherPath." }

    # Refused rather than run: a round launcher ends by calling run-arm.ps1, and dot-sourcing one
    # would start a Unity arm on a worker this script knows nothing about. An environment block is
    # a file that produces a hashtable and does nothing else. Comment lines are stripped first,
    # because a file that only mentions the launcher in its own commentary launches nothing.
    $text = ((Get-Content -LiteralPath $launcherPath) -notmatch '^\s*#') -join "`n"
    if ($text -match 'run-arm\.ps1|run-farm\.ps1|Start-Process|Start-Job') {
        throw @"
$launcherPath launches something itself, so it was NOT dot-sourced.
-Launcher wants the environment block alone: a .ps1 whose last statement is the hashtable (or
that leaves it in `$s). Copy the round launcher's `$s block into one and drop its final call.
"@
    }

    $s = $null
    $produced = . $launcherPath

    foreach ($candidate in @($produced) + @($s)) {
        if ($candidate -is [hashtable]) { $settings = $candidate }
    }

    if ($settings.Count -eq 0) {
        throw "$launcherPath produced no hashtable of settings (it must emit one, or leave it in `$s)."
    }
}

foreach ($k in $Env.Keys) { $settings[$k] = $Env[$k] }

foreach ($k in @($settings.Keys)) {
    if ($k -notmatch '^EVOSIM_') { throw "'$k' is not an EVOSIM_ setting; refused rather than ignored." }
}

# The named parameters, last, so the command line is the final word on them.
$settings['EVOSIM_SEED'] = $Seed
$settings['EVOSIM_SECONDS'] = $Seconds
$settings['EVOSIM_WALL_MINUTES'] = $WallMinutes
$settings['EVOSIM_THREADS'] = $Threads
$settings['EVOSIM_RUNS_ROOT'] = $runsRootPath
$settings['EVOSIM_OUT'] = "$Arm.md"
$settings['EVOSIM_REPO_ROOT'] = $root

if ($CheckpointEvery -gt 0) { $settings['EVOSIM_CHECKPOINT_EVERY'] = $CheckpointEvery }

if ($ResumeFrom -ne '') {
    # An arm name alone is the common case, so it is tried under RunsRoot before it is taken as a
    # path. A resumed run reads the world it is continuing out of that run's own config.json, so
    # nothing in -Launcher or -Env can change the world underneath it; a setting of the world
    # passed beside -ResumeFrom is simply not read, which the program warns about.
    $resumePath = Join-Path $runsRootPath $ResumeFrom
    if (-not (Test-Path -LiteralPath $resumePath)) { $resumePath = Resolve-UnderRepo $ResumeFrom }
    if (-not (Test-Path -LiteralPath $resumePath)) {
        throw "-ResumeFrom '$ResumeFrom' is neither under $runsRootPath nor a path that exists."
    }

    $settings['EVOSIM_RESUME'] = (Resolve-Path -LiteralPath $resumePath).Path
    if ($At -gt 0) { $settings['EVOSIM_RESUME_AT'] = $At }
    if ($AllowSourceMismatch) { $settings['EVOSIM_ALLOW_SOURCE_MISMATCH'] = 1 }
}
elseif ($At -gt 0 -or $AllowSourceMismatch) {
    throw '-At and -AllowSourceMismatch say where to resume from, and -ResumeFrom was not given.'
}

# Logs live inside the project (scratch/ is gitignored): nothing of a run is written outside the
# repository, TEMP included. Separate streams, because the program prints its header on stdout
# and every warning about an unread setting on stderr, and the second is the one worth reading.
$logDir = Join-Path $root 'scratch\logs'
$outLog = Join-Path $logDir "$Arm.out"
$errLog = Join-Path $logDir "$Arm.err"

$culture = [Globalization.CultureInfo]::InvariantCulture
$argList = @()
foreach ($k in ($settings.Keys | Sort-Object)) {
    $v = $settings[$k]
    $text = if ($null -eq $v) { '' } elseif ($v -is [IFormattable]) { $v.ToString($null, $culture) } else { [string]$v }
    if ($text -match '\s') { $argList += ('{0}="{1}"' -f $k, $text) } else { $argList += "$k=$text" }
}

Write-Host "$Arm -> farm (dynamics)"
Write-Host "  exe   $exePath"
Write-Host "  seed $Seed, $Seconds s, $WallMinutes min wall, $(if ($Threads -eq 0) { 'one thread per processor' } else { "$Threads threads" })"
Write-Host "  runs  $runsRootPath"
if ($CheckpointEvery -gt 0) { Write-Host "  ckpt  every $CheckpointEvery s" }
if ($ResumeFrom -ne '') {
    Write-Host "  from  $($settings['EVOSIM_RESUME'])$(if ($At -gt 0) { " at $At s" } else { ' (last checkpoint)' })"
}
Write-Host "  out   $(Join-Path $runsRootPath "$Arm.md")"
Write-Host "  log   $outLog / $errLog"
Write-Host "  $($argList.Count) settings"

if (-not $PSCmdlet.ShouldProcess("$Arm on $exePath", 'launch')) {
    Write-Host '  -WhatIf: nothing launched. The command line would be:'
    Write-Host "  $exePath $($argList -join ' ')"
    return
}

New-Item -ItemType Directory -Force -Path $runsRootPath | Out-Null
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$launchedAt = (Get-Date).ToUniversalTime()

# Detached: its own process, its own window (hidden), its streams on disk. Nothing of this shell
# is inherited except the working directory, and every setting is on the command line, so a
# variable left over from an earlier launch cannot reach this run (the Astra review, 2026-09-07).
$process = Start-Process -FilePath $exePath -ArgumentList $argList -WorkingDirectory $root `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $outLog -RedirectStandardError $errLog

Write-Host "  launched, pid $($process.Id)"

if ($WaitForManifestSeconds -le 0) {
    Write-Host "Stop it with: ./scripts/stop-arm.ps1 $Arm -RunsRoot $RunsRoot -Reason manual-other"
    return
}

Write-Host "  waiting up to $WaitForManifestSeconds s for run.json..."

$deadline = (Get-Date).AddSeconds($WaitForManifestSeconds)
$manifestPath = $null

while ((Get-Date) -lt $deadline) {
    $candidate = Get-ChildItem -Path (Join-Path $runsRootPath $Arm) -Filter 'run.json' -Recurse -File `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -ge $launchedAt.AddMinutes(-1) } |
        Sort-Object LastWriteTimeUtc | Select-Object -Last 1

    if ($candidate) { $manifestPath = $candidate.FullName; break }

    # A world the config refuses is refused before a run directory exists, and waiting a minute
    # for a process that is already gone helps nobody. The reason is on stderr.
    if ($process.HasExited) {
        Write-Warning "The farm exited (code $($process.ExitCode)) before writing run.json. Read $errLog."
        break
    }

    Start-Sleep -Seconds 2
}

if (-not $manifestPath) {
    if (-not $process.HasExited) { Write-Warning "No run.json after $WaitForManifestSeconds s. Check $errLog and $outLog." }
    if ($ExpectDynamicsHash -ne '') { Write-Warning "-ExpectDynamicsHash could not be checked: there is no manifest to check it against." }
    return
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

Write-Host "  run.json $manifestPath"
Write-Host "    status       $($manifest.status)"
Write-Host "    engine       $($manifest.engine) $($manifest.engineVersion)"
Write-Host "    gitCommit    $($manifest.source.gitCommit)$(if ($manifest.source.gitDirty) { ' (DIRTY)' })"
Write-Host "    dynamicsHash $($manifest.source.dynamicsHash)"
Write-Host "    farmHash     $($manifest.source.farmHash)"
Write-Host "    coreHash     $($manifest.source.coreHash)"
Write-Host "    configHash   $($manifest.configHash)"
Write-Host "    threads      $($manifest.threads)"
Write-Host "    processId    $($manifest.processId)"
if ($manifest.resumedFrom) {
    Write-Host "    resumedFrom  $($manifest.resumedFrom.arm)/$($manifest.resumedFrom.run) at $($manifest.resumedFrom.seconds) s"
    Write-Host "                 checkpoint $($manifest.resumedFrom.checkpointHash)"
    if ($manifest.resumedFrom.sourceMismatch) {
        Write-Warning "Resumed across a source change: $($manifest.resumedFrom.note)"
    }
}
if ($manifest.source.note) { Write-Warning "run.json note: $($manifest.source.note)" }

if ($ExpectDynamicsHash -ne '') {
    $expected = $ExpectDynamicsHash.Trim().ToLowerInvariant()
    if ($expected.Length -lt 8) { throw "-ExpectDynamicsHash needs at least 8 characters; got '$ExpectDynamicsHash'." }

    $actual = [string]$manifest.source.dynamicsHash
    if (-not $actual.StartsWith($expected)) {
        # STOP rather than kill, for the reason the whole protocol exists: a killed run leaves a
        # manifest that says "running" forever and reads exactly like a crash. The run ends at its
        # next report row and writes its own ending.
        $stopPath = Join-Path (Split-Path -Parent $manifestPath) 'STOP'
        $stopText = "manual-other`nrun-farm.ps1: dynamicsHash $actual is not the expected $expected...`n"
        [System.IO.File]::WriteAllText($stopPath, $stopText, (New-Object System.Text.UTF8Encoding($false)))

        throw @"
This arm is running source that is not what it expects, so it was STOPPED.
  expected dynamicsHash $expected...
  recorded dynamicsHash $actual
STOP written to $stopPath; the run ends at its next report row and records itself stopped.
"@
    }

    Write-Host "    dynamicsHash matches $expected..."
}

Write-Host "Watch it with: Get-Content '$(Join-Path $runsRootPath "$Arm.md")' -Tail 3"
Write-Host "Stop it with:  ./scripts/stop-arm.ps1 $Arm -RunsRoot $RunsRoot -Reason manual-other"
