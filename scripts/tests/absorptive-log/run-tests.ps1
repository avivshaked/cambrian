<#
.SYNOPSIS
  Fixture test for scripts/absorptive-log.ps1. Exits non-zero on the first failed assertion.

.DESCRIPTION
  The reader is one anchored regex over every key of AbsorptiveSample.ToJson in order, so a
  renamed key does not mis-read a column: the row stops matching at all, every line lands in
  the malformed counter, and the script prints an empty summary above a message about the
  schema. That is what happened when fable-propose-growth.md (2026-09-08) replaced the
  `endowment` key with `investment`, and it is the one failure this file exists to catch.

  The fixture is not hand-written. It is thirty-one rows taken verbatim from a world stepped
  through Evosim.Core and serialised by AbsorptiveSample.ToJson, which is the point: a fixture
  typed out by a person would have carried the same stale key as the regex and agreed with it.
  Regenerate it the same way if the row schema changes on purpose.

  Evosim.Core.Tests holds the other half of the guard — TheReaderScriptMatchesARealRow lifts
  this script's own regex out of this file and matches it against a freshly serialised row, so
  the two drift apart on the build rather than on the next person to open a log.

.EXAMPLE
  ./scripts/tests/absorptive-log/run-tests.ps1
#>
param()

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$reader = Join-Path (Split-Path -Parent (Split-Path -Parent $here)) 'absorptive-log.ps1'
if (-not (Test-Path -LiteralPath $reader)) { throw "reader not found at $reader" }

$log = Join-Path $here 'fixtures/fx-schema/2026-01-01-000000-fixture/absorptive.jsonl'
if (-not (Test-Path -LiteralPath $log)) { throw "fixture not found at $log" }

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

$out = (& $reader -Path $log -Bin 300 -Top 5) -join [Environment]::NewLine

# What a reader of the summary is told, asserted as substrings of the script's own output
# rather than recomputed here: a test that recomputed the arithmetic would agree with a
# reader that had stopped printing it.
$expect = @(
    '30 rows over 18 creatures; t 10 to 300; 6 death rows',
    'CAPPED: 1 sample(s) hit the 2,000-row cap, worst 17 creatures left out',
    'lifetime of the 6 that died',
    'per creature (longest-logged first, 5 of 18):',
    'per 300 s bin, across the living eaters:',
    'rows per sample (t: rows), last 12:'
)

foreach ($e in $expect) {
    $checks++
    if ($out -notmatch [regex]::Escape($e)) { $failures.Add("missing: $e") }
}

# The whole point. One malformed line means the regex and the row schema have parted, and the
# summary above it is a summary of nothing.
$absent = @('did not parse', 'no absorptive rows')
foreach ($a in $absent) {
    $checks++
    if ($out -match [regex]::Escape($a)) { $failures.Add("present and should not be: $a") }
}

Write-Host $out
Write-Host ''

if ($failures.Count -gt 0) {
    Write-Host ("FAILED {0} of {1} check(s):" -f $failures.Count, $checks)
    foreach ($f in $failures) { Write-Host "  $f" }
    exit 1
}

Write-Host ("PASSED {0} check(s)." -f $checks)
