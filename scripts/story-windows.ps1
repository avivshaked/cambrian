<#
.SYNOPSIS
  Records the farm film windows a story's scenes are filmed from, one at a time (B3's second step).

.DESCRIPTION
  scripts/story-windows.py plans the windows and writes windows.json into a folder; this runs the
  farm for each of them in turn, as

    Evosim.Farm.exe --film-window <run> <from> <to> <folder>/<out> --fps <fps> --threads <n>

  and prints each window's verdict word at the end (FAITHFUL, COUSIN or UNVERIFIED, from its
  identity.jsonl). scripts/theatre-safari.ps1 -FromWindows <folder> then films the scenes from them.

  A window already recorded over the span, frame rate and run the plan asks for is skipped, so the
  script can be run again after a stop, or after the plan grew by a scene. A window directory that
  holds anything else (a window over another span, one stopped before its verdict, a stray file)
  is renamed aside with a date on it and recorded again; nothing is deleted.

  One window at a time and never two: each is one farm process on -Threads threads, a third of the
  machine's logical processors by default (the owner's load ruling of 2026-09-25). The farm's own
  output goes to <folder>/logs/<out>.log.

.PARAMETER Plan
  The folder scripts/story-windows.py wrote windows.json into, or the file itself.

.PARAMETER Threads
  The farm's threads for each window. 0 (the default) is a third of the logical processors.

.PARAMETER Exe
  The farm program. Default artifacts/Evosim.Farm/bin/Release/net8.0/Evosim.Farm.exe under the
  repository (dotnet build src/Evosim.Farm -c Release).

.PARAMETER Scenes
  Only these story numbers, as a comma list or an array.

.PARAMETER WhatIf
  Says what it would record, skip and move aside, and runs nothing.

.EXAMPLE
  python scripts/story-windows.py scratch/story-v2/story.json --out scratch/story-windows/r48-v2 --runs-root runs --runs r48-s1 --scenes 1,3
  ./scripts/story-windows.ps1 scratch/story-windows/r48-v2 -Threads 8
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Plan,
    [int]$Threads = 0,
    [string]$Exe = '',
    [string[]]$Scenes = @(),
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function Resolve-UnderRepo([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    return (Join-Path $root $path)
}

$planPath = Resolve-UnderRepo $Plan
if (Test-Path -LiteralPath $planPath -PathType Container) { $planPath = Join-Path $planPath 'windows.json' }
if (-not (Test-Path -LiteralPath $planPath)) { throw "No windows.json at $planPath. Plan the windows first: python scripts/story-windows.py <story.json> --out <folder>." }
$planDir = Split-Path -Parent $planPath

$manifest = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.format -ne 'story-windows 1') { throw "$planPath is not a 'story-windows 1' plan." }

$exePath = if ($Exe -ne '') { Resolve-UnderRepo $Exe }
           else { Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe' }
if (-not $WhatIf -and -not (Test-Path -LiteralPath $exePath)) {
    throw "No farm program at $exePath. Build it: dotnet build src/Evosim.Farm -c Release."
}

if ($Threads -le 0) { $Threads = [Math]::Max(1, [int][Math]::Floor([Environment]::ProcessorCount / 3)) }

# A comma list arrives as one string from powershell -File or bash (CLAUDE.md); split it here.
$wanted = @()
foreach ($value in $Scenes) {
    foreach ($word in ($value -split '[,\s]+')) {
        if ($word -eq '') { continue }
        if ($word -notmatch '^\d+$') { throw "-Scenes: '$word' is not a story number." }
        $wanted += [int]$word
    }
}

function Format-Seconds([double]$value) { return $value.ToString('R', $inv) }

# The verdict line of a window directory, or $null: the last line of identity.jsonl naming one.
function Read-Verdict([string]$dir) {
    $identity = Join-Path $dir 'identity.jsonl'
    if (-not (Test-Path -LiteralPath $identity)) { return $null }
    $last = $null
    foreach ($line in [System.IO.File]::ReadAllLines($identity)) {
        $t = $line.Trim()
        if ($t.EndsWith('}') -and $t.Contains('"verdict"')) { $last = $t }
    }
    if ($null -eq $last) { return $null }
    return ($last | ConvertFrom-Json)
}

function Word([string]$verdict) {
    switch ($verdict) { 'faithful' { 'FAITHFUL' } 'cousin' { 'COUSIN' } default { 'UNVERIFIED' } }
}

# Null when the directory holds this window's recording; else why not (StoryWindows.Recorded's rule).
function Why-Not-Recorded($w, [string]$dir) {
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'film.poses.bin'))) { return 'no frames' }
    $v = Read-Verdict $dir
    if ($null -eq $v) { return 'no verdict: the farm stopped before the window ended' }
    if ([Math]::Abs([double]$v.from - [double]$w.from) -gt 1e-6 -or [Math]::Abs([double]$v.to - [double]$w.to) -gt 1e-6) {
        return [string]::Format($inv, 'filmed from {0} to {1} s, and the plan asks {2} to {3} s', $v.from, $v.to, $w.from, $w.to)
    }
    if ([Math]::Abs([double]$v.fps - [double]$w.fps) -gt 1e-6) { return [string]::Format($inv, 'filmed at {0} fps, and the plan asks {1}', $v.fps, $w.fps) }
    $asked = Split-Path -Leaf ($w.run.TrimEnd('/', '\'))
    if ($v.run -and $v.run -ne $asked) { return "filmed from run $($v.run), and the plan asks $asked" }
    return $null
}

$logs = Join-Path $planDir 'logs'
$rows = @()
$todo = @($manifest.windows | Where-Object { $wanted.Count -eq 0 -or $wanted -contains [int]$_.scene })

Write-Host "story windows: $($todo.Count) of $(@($manifest.windows).Count) window(s) from $planPath"
Write-Host "  farm $exePath, $Threads thread(s) a window, one window at a time"
foreach ($s in @($manifest.skipped)) {
    if ($null -ne $s -and ($wanted.Count -eq 0 -or $wanted -contains [int]$s.scene)) { Write-Host "  scene $($s.scene) has no window: $($s.why)" }
}

foreach ($w in $todo) {
    $dir = Join-Path $planDir $w.out
    $name = "scene $($w.scene) $($w.part) ($($w.arm), $($w.station), $(Format-Seconds $w.from) to $(Format-Seconds $w.to) s)"

    if (Test-Path -LiteralPath $dir) {
        $why = Why-Not-Recorded $w $dir
        if ($null -eq $why) {
            $v = Read-Verdict $dir
            Write-Host "  $name`: recorded already, $(Word $v.verdict); skipped"
            $rows += [pscustomobject]@{ Scene = $w.scene; Part = $w.part; Word = (Word $v.verdict); Reason = $v.reason; Action = 'skipped' }
            continue
        }

        $aside = $dir + '.aside-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
        if ($WhatIf) {
            Write-Host "  $name`: $why; would move $dir aside to $aside and record it"
            continue
        }
        Rename-Item -LiteralPath $dir -NewName (Split-Path -Leaf $aside)
        Write-Host "  $name`: $why; moved aside to $aside"
    }

    $arguments = @('--film-window', $w.run, (Format-Seconds $w.from), (Format-Seconds $w.to), $dir,
                   '--fps', (Format-Seconds $w.fps), '--threads', "$Threads")

    if ($WhatIf) {
        Write-Host "  $name`: would record: $exePath $($arguments -join ' ')"
        continue
    }

    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    $log = Join-Path $logs ($w.out + '.log')
    $err = Join-Path $logs ($w.out + '.err.log')
    Write-Host "  $name`: recording, log $log"
    $started = Get-Date
    $quoted = $arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $process = Start-Process -FilePath $exePath -ArgumentList $quoted -WorkingDirectory $root -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $log -RedirectStandardError $err
    $minutes = ((Get-Date) - $started).TotalMinutes

    $v = Read-Verdict $dir
    $code = $process.ExitCode
    if ($null -ne $v -and ($code -eq 0 -or $code -eq 2 -or $code -eq 3)) {
        Write-Host ([string]::Format($inv, '    {0} in {1:0.0} min: {2}', (Word $v.verdict), $minutes, $v.reason))
        $rows += [pscustomobject]@{ Scene = $w.scene; Part = $w.part; Word = (Word $v.verdict); Reason = $v.reason; Action = 'recorded' }
    }
    else {
        $tail = if (Test-Path -LiteralPath $err) { (Get-Content -LiteralPath $err -Tail 3) -join ' | ' } else { '' }
        Write-Host "    FAILED (exit $code) in $([Math]::Round($minutes, 1)) min: $tail"
        $rows += [pscustomobject]@{ Scene = $w.scene; Part = $w.part; Word = 'FAILED'; Reason = "exit $code $tail"; Action = 'failed' }
    }
}

if ($rows.Count -gt 0) {
    Write-Host ''
    $rows | Format-Table -AutoSize Scene, Part, Word, Action, Reason | Out-String -Width 200 | Write-Host
    $words = @($rows | ForEach-Object { $_.Word } | Sort-Object -Unique)
    if ($words.Count -gt 1) { Write-Host "The windows carry mixed verdicts ($($words -join ', ')): each scene's label carries its own." }
}

if (@($rows | Where-Object { $_.Action -eq 'failed' }).Count -gt 0) { exit 1 }
