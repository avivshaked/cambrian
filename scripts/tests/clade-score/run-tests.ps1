<#
.SYNOPSIS
  Fixture tests for scripts/clade-score.ps1. Exits non-zero on the first failed assertion.

.DESCRIPTION
  Regenerates the synthetic runs under scripts/tests/clade-score/fixtures/ (see
  new-fixtures.ps1 for what each world is built to prove), scores each with the real
  scorer, and asserts on the lines it prints.

  The assertions are substring matches against the scorer's own output rather than a
  recomputation of the clauses, deliberately: the thing under test is what a reader of the
  verdict line is told, and a test that recomputed the arithmetic would agree with a scorer
  that had stopped saying it.

.EXAMPLE
  ./scripts/tests/clade-score/run-tests.ps1
#>
param()

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$scorer = Join-Path (Split-Path -Parent (Split-Path -Parent $here)) 'clade-score.ps1'
if (-not (Test-Path -LiteralPath $scorer)) { throw "scorer not found at $scorer" }

Write-Host 'Generating fixtures...'
& (Join-Path $here 'new-fixtures.ps1') -Root $here | Out-Null
Write-Host ''

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

# Each case: the fixture directory (used as RunsRoot), the arm inside it, and the substrings
# every one of the scorer's lines is searched for. `Absent` strings must appear nowhere.
$cases = @(
    @{
        Case = 'smaller-passes'
        Arm = 'fx-smaller-passes'
        Expect = @(
            'fx-smaller-passes: PASS | passing clade: root 2000',
            '17 alive at end, min last 6000 s = 14',
            'inherited births in last 20 samples 3',
            'largest: root 1000 (kind f, born 100), 59 members ever, 35 alive at end',
            'stability: min last 6000 s = 5 -> unstable',
            "photo (owner's wording): flag absent | photo lineage: flag absent",
            'photo (>=10, two lifetimes): flag absent | photo lineage: flag absent',
            'photo inh (population column): column absent',
            # The qualifier segment (script-contracts-spec.md section 2): no manifest, so
            # duration reads '?', and the fixture's own config.json names a nonzero floor.
            'duration ? of ? s',
            'floor closes at 3000 s',
            'reading: absorptive clade only; producer clause not decided'
        )
        Absent = @('fx-smaller-passes: FAIL', 'SHORT')
    },
    @{
        Case = 'none-passes'
        Arm = 'fx-none-passes'
        Expect = @(
            'fx-none-passes: FAIL | best clade: root 4000',
            '12 alive at end, min last 6000 s = 9',
            'fails 1 of 4: stability min 9 < 10',
            '0 of 2 living clades pass',
            'largest: root 3000 (kind f, born 100), 20 members ever, 20 alive at end',
            'inherited births in last 20 samples 0 -> clade fail',
            'stability: min last 6000 s = 9 -> unstable',
            # This fixture's config.json sets the floor to 0 (never closes).
            'floor open'
        )
        Absent = @('fx-none-passes: PASS')
    },
    @{
        Case = 'pho-owner-only'
        Arm = 'fx-pho-owner-only'
        Expect = @(
            'fx-pho-owner-only: PASS | passing clade: root 2000',
            "photo (owner's wording): held -- clade root 5000 (born 100), 7 alive at end, 2 inherited photosynthetic births in the last 20 samples",
            'photo (>=10, two lifetimes): failed (best photosynthetic clade min over the last 6000 s = 5)',
            'photo inh (population column): failed -- photo inh at end = 5, min over last 20 = 5'
        )
        Absent = @('flag absent')
    },
    @{
        Case = 'pho-absent'
        Arm = 'fx-pho-absent'
        Expect = @(
            'fx-pho-absent: PASS | passing clade: root 2000',
            "photo (owner's wording): flag absent | photo lineage: flag absent",
            'photo (>=10, two lifetimes): flag absent | photo lineage: flag absent',
            'photo inh (population column): held -- photo inh at end = 12, min over last 20 = 12'
        )
        Absent = @("photo (owner's wording): held", "photo (owner's wording): failed")
    },
    @{
        Case = 'future-birth'
        Arm = 'fx-future-birth'
        Expect = @(
            'fx-future-birth: FAIL | best clade: root 2000',
            '14 alive at end, min last 6000 s = 14',
            'inherited births in last 20 samples 0',
            'fails 1 of 4: no inherited absorptive birth in the last 20 samples',
            'no manifest'
        )
        Absent = @('fx-future-birth: PASS', 'PROVISIONAL')
    },
    @{
        Case = 'wall-censored'
        Arm = 'fx-wall-censored'
        Expect = @(
            'fx-wall-censored: PASS | passing clade: root 2000',
            'CENSORED: ended (wall) at t=10000 of 30000 s requested; a reading, not a verdict',
            'duration 10000 of 30000 s SHORT'
        )
        Absent = @('PROVISIONAL', 'no manifest')
    },
    @{
        # The review's own example manifest (script-contracts-spec.md section 2): ended on
        # its own budget, ie not censored, but at 10,000 of the 30,000 s the goal rule
        # (D063 as amended) is read over -- the case the SHORT token exists for.
        Case = 'short-budget'
        Arm = 'fx-short-budget'
        Expect = @(
            'fx-short-budget: PASS | passing clade: root 2000',
            'duration 10000 of 10000 s SHORT',
            'floor unknown'
        )
        Absent = @('fx-short-budget: FAIL', 'PROVISIONAL', 'CENSORED', 'no manifest')
    }
)

foreach ($c in $cases) {
    $runsRoot = Join-Path (Join-Path $here 'fixtures') $c.Case
    Write-Host "--- $($c.Case)"
    $out = @(& $scorer -RunsRoot $runsRoot -Arm $c.Arm)
    foreach ($line in $out) { Write-Host "    $line" }
    $blob = ($out -join "`n")

    foreach ($e in $c.Expect) {
        $checks++
        if ($blob.Contains($e)) {
            Write-Host "    ok   : $e" -ForegroundColor DarkGreen
        } else {
            Write-Host "    FAIL : expected substring not found: $e" -ForegroundColor Red
            $failures.Add("$($c.Case): missing '$e'")
        }
    }
    foreach ($n in $c.Absent) {
        $checks++
        if ($blob.Contains($n)) {
            Write-Host "    FAIL : forbidden substring present: $n" -ForegroundColor Red
            $failures.Add("$($c.Case): present '$n'")
        } else {
            Write-Host "    ok   : absent: $n" -ForegroundColor DarkGreen
        }
    }
    Write-Host ''
}

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) of $checks assertions failed:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  $f" -ForegroundColor Red }
    exit 1
}

Write-Host "all $checks assertions passed across $($cases.Count) fixtures" -ForegroundColor Green
exit 0
