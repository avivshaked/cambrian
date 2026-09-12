<#
.SYNOPSIS
  Fixture tests for scripts/compare-det.py. Exits non-zero on the first failed assertion.

.DESCRIPTION
  Regenerates the synthetic runs/ tree under scripts/tests/compare-det/fixtures/ (see
  new-fixtures.ps1), then runs the real script against each pair of arms with the fixtures
  directory as the working directory -- compare-det.py globs runs/<arm>/*/stats.jsonl
  relative to its own working directory and takes no --runs-root of its own, so the harness
  moves rather than the script.

  Checks are against the script's exit code (what a launcher or a test would gate on) and, for
  the exit-2/3 cases, that the message names the thing it refused -- as the positions and
  clade-score suites do, ok/FAIL per case, one nonzero exit on any failure.

.EXAMPLE
  ./scripts/tests/compare-det/run-tests.ps1
#>
param()

$ErrorActionPreference = 'Stop'

# PowerShell 7.4+ turns a native command's nonzero exit into a terminating error while
# ErrorActionPreference is Stop; every case below runs a script expected to exit nonzero.
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$here = $PSScriptRoot
$repo = Split-Path -Parent (Split-Path -Parent $here)
$script = Join-Path $repo 'compare-det.py'
if (-not (Test-Path -LiteralPath $script)) { throw "script not found at $script" }

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { throw 'python is not on PATH; compare-det.py is a Python 3 script.' }

Write-Host 'Generating fixtures...'
& (Join-Path $here 'new-fixtures.ps1') -Root $here | Out-Null
Write-Host ''

$fixturesDir = Join-Path $here 'fixtures'

function Invoke-CompareDet([string[]]$CdArgs) {
    Push-Location $fixturesDir
    try {
        $out = & $python.Source $script @CdArgs 2>&1 | ForEach-Object { [string]$_ }
        $code = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    return @{ Out = $out; Code = $code }
}

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

function Assert-Code([string]$Name, [int]$Expected, [int]$Actual, [string[]]$Out) {
    $script:checks++
    if ($Actual -eq $Expected) {
        Write-Host "    ok   : $Name (exit $Actual)" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $Name -- expected exit $Expected, got $Actual" -ForegroundColor Red
        Write-Host "           $($Out -join ' | ')" -ForegroundColor Red
        $script:failures.Add($Name)
    }
}

function Assert-Match([string]$Name, [string[]]$Out, [string]$Pattern) {
    $script:checks++
    $matched = @($Out | Where-Object { $_ -match $Pattern }).Count -gt 0
    if ($matched) {
        Write-Host "    ok   : $Name" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $Name -- no line matched /$Pattern/" -ForegroundColor Red
        Write-Host "           $($Out -join ' | ')" -ForegroundColor Red
        $script:failures.Add($Name)
    }
}

Write-Host '--- identity'
$r = Invoke-CompareDet @('cd-id-a', 'cd-id-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'identity exits 0' 0 $r.Code $r.Out
Assert-Match 'identity: identical on all 3 shared samples' $r.Out 'identical on all 3 shared samples'

Write-Host '--- disagreement'
$r = Invoke-CompareDet @('cd-diff-a', 'cd-diff-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'disagreement exits 1' 1 $r.Code $r.Out
Assert-Match 'disagreement: first difference at t=200' $r.Out 'first difference at t=200'

Write-Host '--- no shared samples'
$r = Invoke-CompareDet @('cd-noshare-a', 'cd-noshare-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'no shared samples exits 2' 2 $r.Code $r.Out
Assert-Match 'no shared samples: says so' $r.Out 'no shared samples'

Write-Host '--- unequal coverage, no flag'
$r = Invoke-CompareDet @('cd-cov-a', 'cd-cov-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'unequal coverage without --allow-partial exits 3' 3 $r.Code $r.Out
Assert-Match 'unequal coverage: reports the coverage line' $r.Out 'unequal coverage'

Write-Host '--- unequal coverage, --allow-partial'
$r = Invoke-CompareDet @('cd-cov-a', 'cd-cov-b', '--allow-partial')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'unequal coverage with --allow-partial exits 0' 0 $r.Code $r.Out
Assert-Match '--allow-partial still prints the coverage line' $r.Out 'unequal coverage'
Assert-Match '--allow-partial: identical over the shared samples' $r.Out 'identical on all 2 shared samples'

Write-Host '--- missing field'
$r = Invoke-CompareDet @('cd-field-a', 'cd-field-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'missing field exits 2' 2 $r.Code $r.Out
Assert-Match 'missing field: names contactPairsPerStep' $r.Out 'contactPairsPerStep'

Write-Host '--- missing arm'
$r = Invoke-CompareDet @('cd-missing-a', 'cd-missing-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'missing arm exits 2' 2 $r.Code $r.Out
Assert-Match 'missing arm: no stats.jsonl' $r.Out 'no stats\.jsonl'

Write-Host '--- ambiguous arm, no --run'
$r = Invoke-CompareDet @('cd-ambig-a', 'cd-ambig-b')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code 'ambiguous arm without --run exits 2' 2 $r.Code $r.Out
Assert-Match 'ambiguous arm: lists the first directory' $r.Out '2026-01-01-000000-fixture-one'
Assert-Match 'ambiguous arm: lists the second directory' $r.Out '2026-01-02-000000-fixture-two'

Write-Host '--- ambiguous arm, --run names one'
$r = Invoke-CompareDet @('cd-ambig-a', 'cd-ambig-b', '--run', '2026-01-02-000000-fixture-two')
$r.Out | ForEach-Object { Write-Host "    $_" }
Assert-Code '--run picks a directory and the pair agrees, exits 0' 0 $r.Code $r.Out

Write-Host ''

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) of $checks assertions failed:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  $f" -ForegroundColor Red }
    exit 1
}

Write-Host "all $checks assertions passed" -ForegroundColor Green
exit 0
