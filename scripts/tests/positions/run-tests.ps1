<#
.SYNOPSIS
  Fixture tests for scripts/positions-read.py. Exits non-zero on the first failed assertion.

.DESCRIPTION
  Regenerates the synthetic run under scripts/tests/positions/fixtures/ (see new-fixtures.ps1
  for the world it builds and the arithmetic each sample was chosen for), reads it with the real
  reader, and asserts on the lines it prints.

  The assertions are patterns against the reader's own output rather than a recomputation of the
  statistics, deliberately and for the reason clade-score's tests are written the same way: what
  is under test is what a reader of the table is told, and a test that recomputed the medians
  would agree with a reader that had stopped printing them. The two nearest-neighbour numbers are
  worked out by hand in new-fixtures.ps1's comment.

  No pictures are drawn here. --at needs matplotlib, which this project does not install, and the
  numbers are the part a test can hold.

.EXAMPLE
  ./scripts/tests/positions/run-tests.ps1
#>
param()

$ErrorActionPreference = 'Stop'

# PowerShell 7.4 turns a native command's nonzero exit into a terminating error while
# ErrorActionPreference is Stop, and the last check below is exactly that: the reader refusing a
# run it cannot read, with the code a launcher would gate on.
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$here = $PSScriptRoot
$repo = Split-Path -Parent (Split-Path -Parent $here)
$reader = Join-Path $repo 'positions-read.py'
if (-not (Test-Path -LiteralPath $reader)) { throw "reader not found at $reader" }

$python = (Get-Command python -ErrorAction SilentlyContinue)
if (-not $python) { throw 'python is not on PATH; the reader is a Python 3 script.' }

Write-Host 'Generating fixtures...'
& (Join-Path $here 'new-fixtures.ps1') -Root $here | Out-Null
Write-Host ''

$fixtures = Join-Path $here 'fixtures'
$out = @(& $python.Source $reader 'fx-positions' '--summary' '--runs-root' $fixtures)
foreach ($line in $out) { Write-Host "    $line" }

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

# Named columns, not positions: the report's own lesson (logbook/0044) applies to this table too,
# and a header that lost a column would otherwise pass every value assertion below.
$expect = [ordered]@{
    'the box, read out of config.json rather than assumed' =
        'box 20 x 5 m, 60 m deep, 4 patch\(es\)'
    'the clades, read out of lineage.jsonl' =
        'lineage\.jsonl: 7 births, 3 clades'
    'every column named in the header' =
        '^\s+t\s+n\s+cols\s+x sd\s+z sd\s+nn h\s+nn 3d\s+leaf\s+leaf y\s+stom\s+stom y\s+mixo\s+mixo y\s+jnt\s+jnt y\s+plain\s+plain y\s+clade 1m\s+jointed\s+joint y\s+rigid y\s*$'

    # Four bodies in three columns of the hundred; the median flat nearest neighbour is 0.75 m
    # and the median in three dimensions is 2.00 m, because the pair that looks adjacent from
    # above is ten metres apart in the water.
    'sample 1: count, footprint and both nearest-neighbour medians' =
        '^\s+100\.0\s+4\s+3/100\s+1\.97\s+0\.00\s+0\.75\s+2\.00\s'
    'sample 1: one of each guild but the jointed, and the clade pair a metre apart' =
        '^\s+100\.0\s.*\s1\s+-10\.00\s+1\s+-10\.00\s+1\s+-20\.00\s+0\s+-\s+1\s+-10\.00\s+2\s+0\s+-\s+-12\.50\s*$'

    # The ribbon: three bodies in one column, 20 cm apart, all of one clade.
    'sample 2: a ribbon in a single column' =
        '^\s+200\.0\s+3\s+1/100\s+0\.16\s+0\.00\s+0\.20\s+0\.20\s'
    'sample 2: three bodies, each with a clade mate within a metre' =
        '^\s+200\.0\s.*\s3\s+3\s+-5\.00\s+-\s*$'

    # Nothing alive: a dash rather than a zero wherever a statistic needs a body.
    'sample 3: an empty world prints dashes, not zeroes' =
        '^\s+300\.0\s+0\s+0/100\s+0\.00\s+0\.00\s+-\s+-\s'
    'every sample printed' = '3 of 3 samples printed'
}

foreach ($case in $expect.GetEnumerator()) {
    $checks++
    $matched = @($out | Where-Object { $_ -match $case.Value }).Count -gt 0

    if ($matched) {
        Write-Host "    ok   : $($case.Key)" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $($case.Key) -- no line matched /$($case.Value)/" -ForegroundColor Red
        $failures.Add("$($case.Key)")
    }
}

# The same water laid two patches by two: the reader has to take the layout out of config.json
# rather than assume a row, or the picture and every spread it prints are of a box the run was
# not in. The bodies are the fixture's own, so the footprint is the same hundred columns and
# only the box changes.
$square = @(& $python.Source $reader 'fx-square' '--summary' '--runs-root' $fixtures)
foreach ($line in $square) { Write-Host "    $line" }

$expectSquare = [ordered]@{
    'the layout, read out of config.json' =
        'box 10 x 10 m, 60 m deep, 4 patch\(es\) laid 2 x 2'
    'a square box has the same hundred columns' =
        '^\s+100\.0\s+4\s+3/100\s'
}

foreach ($case in $expectSquare.GetEnumerator()) {
    $checks++
    $matched = @($square | Where-Object { $_ -match $case.Value }).Count -gt 0

    if ($matched) {
        Write-Host "    ok   : $($case.Key)" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $($case.Key) -- no line matched /$($case.Value)/" -ForegroundColor Red
        $failures.Add("$($case.Key)")
    }
}

# The tank (fable-propose-aquarium.md ruling 1, logbook/specs/tank-spec.md). The same 100 m^2 of
# water as a cylinder, with the first arm's bodies moved to stand about its axis: what is under
# test is that the reader takes the shape out of config.json and then measures the world that
# shape is -- plain separations where the box folds them, plain deviations where the box takes
# circular ones, and a denominator of the columns inside the glass rather than of the bounding
# square. See new-fixtures.ps1 for where each number below comes from.
$tank = @(& $python.Source $reader 'fx-tank' '--summary' '--runs-root' $fixtures)
foreach ($line in $tank) { Write-Host "    $line" }

$expectTank = [ordered]@{
    'the tank, read out of config.json rather than assumed' =
        'tank r=5\.64 m \(100 m2\), 60 m deep, 4 ring\(s\)'

    # 12 x 12 columns cover the bounding square and exactly 100 of them have their centres in the
    # water. A reader that had forgotten the mask would print 144 here and call a full tank
    # two-thirds empty.
    'the footprint is the columns inside the glass, not the bounding square' =
        'columns of 1 m: 100 '

    # Three columns of the hundred; nearest neighbours 0.75 m flat and 2.00 m in three dimensions,
    # the same pair the box arm reads because the bodies are the box arm's shifted sideways.
    'sample 1: count, footprint and both nearest-neighbour medians' =
        '^\s+100\.0\s+4\s+3/100\s+1\.92\s+0\.00\s+0\.75\s+2\.00\s'

    # The one number that is not the box's: x sd is the ordinary standard deviation of 4, 5, 8
    # and 8.5, which is 1.92 m, where the box's circular statistic on the same spread reads 1.97.
    # A tank has a wall where a box has a seam and the two statistics must not agree.
    'sample 1: the spread is plain, not circular' =
        '^\s+100\.0\s+4\s+3/100\s+1\.92\s'

    'sample 2: a ribbon in a single column of the hundred' =
        '^\s+200\.0\s+3\s+1/100\s+0\.16\s+0\.00\s+0\.20\s+0\.20\s'
    'sample 3: an empty tank prints dashes, not zeroes' =
        '^\s+300\.0\s+0\s+0/100\s+0\.00\s+0\.00\s+-\s+-\s'
    'every sample printed' = '3 of 3 samples printed'
}

foreach ($case in $expectTank.GetEnumerator()) {
    $checks++
    $matched = @($tank | Where-Object { $_ -match $case.Value }).Count -gt 0

    if ($matched) {
        Write-Host "    ok   : $($case.Key)" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $($case.Key) -- no line matched /$($case.Value)/" -ForegroundColor Red
        $failures.Add("$($case.Key)")
    }
}

# Both ways a run can have nothing to read are refused with an exit code a launcher can gate on,
# rather than read as a world with nothing in it: an arm that does not exist, and a run with no
# positions.jsonl -- which is every tiled world and every run recorded before 2026-09-10.
$refusals = [ordered]@{
    'an arm that does not exist exits 2' = 'fx-absent'
    'a run with no positions.jsonl exits 2' = 'fx-no-positions'
}

foreach ($refusal in $refusals.GetEnumerator()) {
    $checks++

    # Windows PowerShell 5.1 has no $PSNativeCommandUseErrorActionPreference (that guard
    # above is 7.3+ only): instead it writes each line of a native command's stderr as its
    # own error record, and with $ErrorActionPreference = 'Stop' in force the first such
    # record is promoted to a terminating error before $LASTEXITCODE is ever read -- the
    # reader's own refusal message turns into a script-ending exception rather than a
    # nonzero exit code. 'Continue' around just this call keeps every stderr line as a
    # plain warning-level write in both editions, so the exit code is what decides the case.
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $said = @(& $python.Source $reader $refusal.Value '--summary' '--runs-root' $fixtures 2>&1)
    } finally {
        $ErrorActionPreference = $previousEap
    }

    if ($LASTEXITCODE -eq 2) {
        Write-Host "    ok   : $($refusal.Key)" -ForegroundColor DarkGreen
    } else {
        Write-Host "    FAIL : $($refusal.Key) -- exited $LASTEXITCODE" -ForegroundColor Red
        Write-Host "           $($said -join ' ')" -ForegroundColor Red
        $failures.Add($refusal.Key)
    }
}

Write-Host ''

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) of $checks assertions failed:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  $f" -ForegroundColor Red }
    exit 1
}

Write-Host "all $checks assertions passed" -ForegroundColor Green
exit 0
