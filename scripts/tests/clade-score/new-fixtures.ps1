<#
.SYNOPSIS
  Write the synthetic run directories scripts/tests/clade-score/run-tests.ps1 scores.

.DESCRIPTION
  Four hand-built worlds, each small enough to reason about by eye, each isolating one thing
  the scorer has to get right. They are generated rather than committed as data because the
  intent of a fixture ("the largest clade dips below ten, a smaller one never does") is
  legible in the code that builds it and invisible in ten thousand rows of JSONL.

  clade-score.ps1 reads exactly two files per arm: <RunsRoot>/<arm>.md for the sample table
  (it needs the `t (s)` header, the `inherit` column, and -- when the producer column
  reading is wanted -- `photo` and `photo inh`) and <RunsRoot>/<arm>/<newest run
  directory>/lineage.jsonl for births and deaths. It never opens stats.jsonl, config.json or
  the snapshots, so the fixtures do not carry them.

  The four cases:

    smaller-passes  the largest clade dips to 5 living inside the last two lifetimes and
                    fails stability; a smaller clade holds 14 throughout. Seed PASSES,
                    naming the smaller clade.
    none-passes     the largest clade fails stability and recruitment; a smaller one fails
                    stability alone. Seed FAILS, naming the smaller clade as the best.
    pho-owner-only  a photosynthetic clade is alive at the end with two inherited
                    photosynthetic births in the last 20 samples, but only holds 5 members
                    through the last two lifetimes: the owner's wording holds, the >= 10
                    reading fails, and the report's own `photo inh` column fails too, so all
                    three readings differ in one arm. Its `"pho"` field sits between `"s"`
                    and `"abs"` -- deliberately not at the end of the row -- because the
                    Sim build is free to put it anywhere and the scorer must find it by
                    name.
    pho-absent      the same world with no `"pho"` field on any row, as every run recorded
                    before the flag existed. Both lineage readings print "flag absent"; the
                    column reading still holds.
    future-birth    clade B as in smaller-passes, but its three recruits are born at
                    t=11,000, after the report's last sample at 10,000 -- the shape of a
                    live run whose lineage is ahead of its report. They must not count:
                    the seed FAILS on recruitment (the Astra review's R4).
    wall-censored   the smaller-passes world with a run.json that says the run ended on
                    its wall clock at 10,000 s of 30,000 requested. The clauses pass, and
                    the line must say CENSORED rather than print a bare PASS (the Astra
                    review's second response).

  Samples run 100..10,000 s at the report's 100 s interval, so "the last 20 samples" is
  t > 8,000 and "the last two lifetimes" is t >= 4,000 -- the same windows the real reports
  give the scorer, at a tenth of the length.
#>
param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

$fixtures = Join-Path $Root 'fixtures'
if (Test-Path -LiteralPath $fixtures) { Remove-Item -LiteralPath $fixtures -Recurse -Force }
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null

$samples = @()
for ($t = 100; $t -le 10000; $t += 100) { $samples += $t }

# One birth row in LineageEvent.ToJson()'s field order. $Pho of $null writes no "pho" field
# at all, which is what a run recorded before the flag looks like.
function New-BirthRow([double]$T, [int64]$Id, [int64]$Parent, [string]$Kind, [int]$Abs, $Pho) {
    $phoField = ''
    if ($null -ne $Pho) { $phoField = ',"pho":' + $Pho }
    return '{"e":"b","t":' + $T + ',"id":' + $Id + ',"p":' + $Parent + ',"k":"' + $Kind +
           '","g":0,"s":0' + $phoField + ',"abs":' + $Abs + ',"jnt":0,"pt":0}'
}

function New-DeathRow([double]$T, [int64]$Id) {
    return '{"e":"d","t":' + $T + ',"id":' + $Id + ',"c":"starved"}'
}

# A founder plus $Count children of it, all expressing the same traits, born together and
# optionally dying together. Returns the rows; ids run from $FirstId.
function New-Cohort([int64]$Parent, [int64]$FirstId, [int]$Count, [double]$BornAt, $DiesAt, [int]$Abs, $Pho) {
    $rows = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $Count; $i++) {
        $id = $FirstId + $i
        $rows.Add((New-BirthRow $BornAt $id $Parent 'r' $Abs $Pho))
        if ($null -ne $DiesAt) { $rows.Add((New-DeathRow $DiesAt $id)) }
    }
    # Comma so the caller receives the string[] itself rather than PowerShell's unrolled
    # Object[], which List[string].AddRange refuses.
    return , ([string[]]$rows.ToArray())
}

# <RunsRoot>/<arm>.md -- only the columns clade-score.ps1 looks up by name.
function Write-Report([string]$Dir, [string]$ArmName, [int[]]$Samples, [int]$Inherit, $PhotoInh) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Synthetic fixture -- $ArmName")
    $lines.Add('')
    $lines.Add('Written by scripts/tests/clade-score/new-fixtures.ps1. Not a real run.')
    $lines.Add('')
    if ($null -eq $PhotoInh) {
        $lines.Add('| t (s) | alive | **inherit** |')
        $lines.Add('|---|---|---|')
        foreach ($t in $Samples) { $lines.Add("| $t | 40 | **$Inherit** |") }
    } else {
        $lines.Add('| t (s) | alive | **inherit** | **photo** | **photo inh** |')
        $lines.Add('|---|---|---|---|---|')
        foreach ($t in $Samples) { $lines.Add("| $t | 40 | **$Inherit** | **$PhotoInh** | **$PhotoInh** |") }
    }
    [System.IO.File]::WriteAllLines((Join-Path $Dir "$ArmName.md"), $lines)
}

function Write-Lineage([string]$Dir, [string]$ArmName, $Rows) {
    $runDir = Join-Path (Join-Path $Dir $ArmName) '2026-01-01-000000-fixture'
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    [System.IO.File]::WriteAllLines((Join-Path $runDir 'lineage.jsonl'), [string[]]$Rows)
}

function New-Case([string]$CaseName, [string]$ArmName, $Rows, [int]$Inherit, $PhotoInh) {
    $dir = Join-Path $fixtures $CaseName
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Write-Report $dir $ArmName $samples $Inherit $PhotoInh
    Write-Lineage $dir $ArmName $Rows
    Write-Host "  wrote $CaseName/$ArmName ($($Rows.Count) lineage rows)"
}

# A manifest beside the lineage, with only the fields the scorer reads from it. The real
# one carries thirty more; the scorer must find these by name.
function Write-Manifest([string]$Dir, [string]$ArmName, [string]$Status, [string]$Reason, [double]$Simulated, [double]$Requested) {
    $runDir = Join-Path (Join-Path $Dir $ArmName) '2026-01-01-000000-fixture'
    $text = '{"arm":"' + $ArmName + '","requestedSeconds":' + $Requested + ',"status":"' + $Status +
            '","reason":"' + $Reason + '","simulatedSeconds":' + $Simulated + '}'
    [System.IO.File]::WriteAllText((Join-Path $runDir 'run.json'), $text)
}

# config.json beside the lineage, carrying only the one key the qualifier segment reads
# (RunConfigJson writes it as "floorClosesAfterSeconds" under the "population" group). Omit
# the call entirely for a fixture that should print "floor unknown" -- no config.json at all,
# same as every fixture before the qualifier segment existed.
function Write-FloorConfig([string]$Dir, [string]$ArmName, [double]$FloorClosesAfterSeconds) {
    $runDir = Join-Path (Join-Path $Dir $ArmName) '2026-01-01-000000-fixture'
    $text = '{"format":2,"configHash":"fixture","population":{"floorClosesAfterSeconds":' +
            $FloorClosesAfterSeconds + '}}'
    [System.IO.File]::WriteAllText((Join-Path $runDir 'config.json'), $text)
}

# ---------------------------------------------------------------------------------------
# 1. smaller-passes -- the largest clade fails stability, a smaller clade passes every
#    clause. Clade A: 1 founder + 24 that die at 4,950 + 4 that never die + 25 born at
#    5,050 + 5 born at 9,500, so it holds 29 to t=4,900, collapses to 5 at t=5,000, and
#    recovers to 35. Clade B: 1 founder + 13 from t=250 + 3 at t=9,600 -- never below 14.
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 100 1000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 1000 1001 24 150 4950 1 $null))
$rows.AddRange((New-Cohort 1000 1025 4 150 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1029 25 5050 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1054 5 9500 $null 1 $null))
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 $null))
$rows.AddRange((New-Cohort 2000 2014 3 9600 $null 1 $null))
# Non-absorptive bystanders, so the world is not made only of absorbers.
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'smaller-passes' 'fx-smaller-passes' $rows 52 $null
Write-FloorConfig (Join-Path $fixtures 'smaller-passes') 'fx-smaller-passes' 3000

# ---------------------------------------------------------------------------------------
# 2. none-passes -- clade C (largest, 20 alive at the end) fails stability AND recruitment;
#    clade E (12 alive) fails stability alone, so it is the best failing clade even though
#    it is not the largest.
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 100 3000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 3000 3001 8 150 $null 1 $null))
$rows.AddRange((New-Cohort 3000 3009 11 6050 $null 1 $null))
$rows.Add((New-BirthRow 300 4000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 4000 4001 8 350 $null 1 $null))
$rows.AddRange((New-Cohort 4000 4009 3 9700 $null 1 $null))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'none-passes' 'fx-none-passes' $rows 32 $null
Write-FloorConfig (Join-Path $fixtures 'none-passes') 'fx-none-passes' 0

# ---------------------------------------------------------------------------------------
# 3. pho-owner-only -- absorptive clade B passes, so the seed's verdict is PASS; the
#    photosynthetic clade holds 5 through the last two lifetimes and takes 2 inherited
#    births at t=9,800. Owner's wording holds; >= 10 through two lifetimes fails; the
#    report's photo inh column is 5, so the population reading fails too.
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 0))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 0))
$rows.AddRange((New-Cohort 2000 2014 3 9600 $null 1 0))
$rows.Add((New-BirthRow 100 5000 -1 'f' 0 1))
$rows.AddRange((New-Cohort 5000 5001 4 150 $null 0 1))
$rows.AddRange((New-Cohort 5000 5005 2 9800 $null 0 1))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 0))
New-Case 'pho-owner-only' 'fx-pho-owner-only' $rows 17 5

# ---------------------------------------------------------------------------------------
# 4. pho-absent -- the same world with no "pho" field anywhere, and a photo inh column of
#    12 so the population reading holds while both lineage readings say "flag absent".
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 $null))
$rows.AddRange((New-Cohort 2000 2014 3 9600 $null 1 $null))
$rows.Add((New-BirthRow 100 5000 -1 'f' 0 $null))
$rows.AddRange((New-Cohort 5000 5001 4 150 $null 0 $null))
$rows.AddRange((New-Cohort 5000 5005 2 9800 $null 0 $null))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'pho-absent' 'fx-pho-absent' $rows 17 12

Write-Host "fixtures written to $fixtures"

# ---------------------------------------------------------------------------------------
# 5. future-birth -- clade B holds 14 throughout and its only recruits are born after the
#    last sample. Recruitment must read 0 and the seed must FAIL.
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 $null))
$rows.AddRange((New-Cohort 2000 2014 3 11000 $null 1 $null))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'future-birth' 'fx-future-birth' $rows 14 $null

# ---------------------------------------------------------------------------------------
# 6. wall-censored -- smaller-passes again, under a manifest that ended on the wall clock
#    a third of the way through its request. Every clause passes; the line is CENSORED.
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 100 1000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 1000 1001 24 150 4950 1 $null))
$rows.AddRange((New-Cohort 1000 1025 4 150 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1029 25 5050 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1054 5 9500 $null 1 $null))
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 $null))
$rows.AddRange((New-Cohort 2000 2014 3 9600 $null 1 $null))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'wall-censored' 'fx-wall-censored' $rows 52 $null
Write-Manifest (Join-Path $fixtures 'wall-censored') 'fx-wall-censored' 'ended' 'wall' 10000 30000

# ---------------------------------------------------------------------------------------
# 7. short-budget -- smaller-passes again, ended on its own budget (not censored) at
#    10,000 s of a 10,000 s request: a full run by its own request, but short of the
#    campaign's 30,000 s round, which is what the duration qualifier's SHORT token is for
#    (the review's own example manifest, script-contracts-spec.md section 2).
# ---------------------------------------------------------------------------------------
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add((New-BirthRow 100 1000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 1000 1001 24 150 4950 1 $null))
$rows.AddRange((New-Cohort 1000 1025 4 150 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1029 25 5050 $null 1 $null))
$rows.AddRange((New-Cohort 1000 1054 5 9500 $null 1 $null))
$rows.Add((New-BirthRow 200 2000 -1 'f' 1 $null))
$rows.AddRange((New-Cohort 2000 2001 13 250 $null 1 $null))
$rows.AddRange((New-Cohort 2000 2014 3 9600 $null 1 $null))
$rows.AddRange((New-Cohort -1 9000 10 100 $null 0 $null))
New-Case 'short-budget' 'fx-short-budget' $rows 52 $null
Write-Manifest (Join-Path $fixtures 'short-budget') 'fx-short-budget' 'ended' 'budget' 10000 10000
