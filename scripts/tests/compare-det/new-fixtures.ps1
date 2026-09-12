<#
.SYNOPSIS
  Write the synthetic runs/ tree scripts/tests/compare-det/run-tests.ps1 reads.

.DESCRIPTION
  compare-det.py globs runs/<arm>/*/stats.jsonl relative to its working directory -- it takes
  no --runs-root of its own -- so the test harness runs it with this fixtures/ directory as
  the working directory and each case's arm folders sit directly under fixtures/runs/.

  Eight arms, in pairs (run-tests.ps1 pairs them up per case):

    cd-id-a / cd-id-b           three samples, every field identical -- exit 0.
    cd-diff-a / cd-diff-b       identical at t=100, 'alive' differs at t=200 (39 vs 40) --
                                exit 1, the unchanged difference message.
    cd-noshare-a / cd-noshare-b disjoint sample times (100/200/300 vs 1000/2000/3000) --
                                exit 2, no shared samples.
    cd-cov-a / cd-cov-b         cd-cov-a has a third sample (t=300) cd-cov-b lacks; the two
                                shared samples (100, 200) agree in full -- exit 3 without
                                --allow-partial, exit 0 with it (both print the coverage line).
    cd-field-a / cd-field-b     agree on t and alive at both shared samples, but cd-field-b's
                                rows carry no 'contactPairsPerStep' -- exit 2, names the field.
    cd-missing-a                exists; its pair 'cd-missing-b' names no directory at all --
                                exit 2, no stats.jsonl.
    cd-ambig-a                  two run directories under runs/cd-ambig-a/, both holding the
                                same three samples as cd-id-a -- exit 2 (lists both directory
                                names) without --run, exit 0 against cd-ambig-b (identical
                                content) with --run naming the second directory.
    cd-ambig-b                  a single run directory, the same three samples as cd-id-a --
                                cd-ambig-a's counterpart once --run picks a directory.
#>
param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

$fixtures = Join-Path $Root 'fixtures'
if (Test-Path -LiteralPath $fixtures) { Remove-Item -LiteralPath $fixtures -Recurse -Force }

$utf8 = New-Object System.Text.UTF8Encoding($false)

function Write-Stats([string]$Dir, [string[]]$Rows) {
    New-Item -ItemType Directory -Path $Dir -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $Dir 'stats.jsonl'), ($Rows -join "`n") + "`n", $utf8)
}

$runsRoot = Join-Path $fixtures 'runs'

# --- identity -----------------------------------------------------------------------------
$idRows = @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
    '{"t":300,"alive":37,"contactPairsPerStep":46.0}'
)
Write-Stats (Join-Path $runsRoot 'cd-id-a/2026-01-01-000000-fixture') $idRows
Write-Stats (Join-Path $runsRoot 'cd-id-b/2026-01-01-000000-fixture') $idRows

# --- disagreement ---------------------------------------------------------------------------
$diffA = @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
    '{"t":300,"alive":37,"contactPairsPerStep":46.0}'
)
$diffB = @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":40,"contactPairsPerStep":48.0}'
    '{"t":300,"alive":37,"contactPairsPerStep":46.0}'
)
Write-Stats (Join-Path $runsRoot 'cd-diff-a/2026-01-01-000000-fixture') $diffA
Write-Stats (Join-Path $runsRoot 'cd-diff-b/2026-01-01-000000-fixture') $diffB

# --- no shared samples ------------------------------------------------------------------
Write-Stats (Join-Path $runsRoot 'cd-noshare-a/2026-01-01-000000-fixture') @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
    '{"t":300,"alive":37,"contactPairsPerStep":46.0}'
)
Write-Stats (Join-Path $runsRoot 'cd-noshare-b/2026-01-01-000000-fixture') @(
    '{"t":1000,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":2000,"alive":39,"contactPairsPerStep":48.0}'
    '{"t":3000,"alive":37,"contactPairsPerStep":46.0}'
)

# --- unequal coverage ---------------------------------------------------------------------
Write-Stats (Join-Path $runsRoot 'cd-cov-a/2026-01-01-000000-fixture') @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
    '{"t":300,"alive":37,"contactPairsPerStep":46.0}'
)
Write-Stats (Join-Path $runsRoot 'cd-cov-b/2026-01-01-000000-fixture') @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
)

# --- missing field --------------------------------------------------------------------------
Write-Stats (Join-Path $runsRoot 'cd-field-a/2026-01-01-000000-fixture') @(
    '{"t":100,"alive":41,"contactPairsPerStep":50.0}'
    '{"t":200,"alive":39,"contactPairsPerStep":48.0}'
)
Write-Stats (Join-Path $runsRoot 'cd-field-b/2026-01-01-000000-fixture') @(
    '{"t":100,"alive":41}'
    '{"t":200,"alive":39}'
)

# --- missing arm ------------------------------------------------------------------------
# cd-missing-a exists; 'cd-missing-b' is never created, so runs/cd-missing-b/ does not exist.
Write-Stats (Join-Path $runsRoot 'cd-missing-a/2026-01-01-000000-fixture') $idRows

# --- ambiguous arm --------------------------------------------------------------------------
# Two run directories under runs/cd-ambig-a/; both carry the identity rows, so picking either
# with --run agrees with cd-ambig-b.
Write-Stats (Join-Path $runsRoot 'cd-ambig-a/2026-01-01-000000-fixture-one') $idRows
Write-Stats (Join-Path $runsRoot 'cd-ambig-a/2026-01-02-000000-fixture-two') $idRows
Write-Stats (Join-Path $runsRoot 'cd-ambig-b/2026-01-01-000000-fixture') $idRows

Write-Host "wrote fixtures under $runsRoot"
