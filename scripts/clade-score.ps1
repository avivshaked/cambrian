<#
.SYNOPSIS
  Score a run's connected clades against the goal rule (D063 as amended, DECISIONS.md).

.DESCRIPTION
  A port of scripts/reads/clade-score.py into scripts/, per D063's amendment note
  ("scripts/reads/clade-score.py is the scorer until it moves into scripts/") and logbook/0054's
  addendum "scored again by connected clade".

  A clade begins at an absorptive birth whose parent did not express the trait (or at an
  absorptive founder, parent id -1); membership follows the parent chain forward while the
  trait is inherited.

  **Every clade is scored, not only the largest** (2026-09-06; the Sol/GPT review of that
  night, item 1 of logbook/specs/review-2026-09-06-response.md). D063 asks whether *a* connected
  clade meets the bar, so the seed PASSES when any clade with a living member at the last
  sample meets every clause:

    1. alive for >= 20 consecutive samples ending at the last sample;
    2. >= 10 living members at the last sample;
    3. at least one inherited absorptive birth inside the clade within the last 20 samples;
    4. stability (2026-09-04 amendment) -- >= 10 living members at every report sample with
       t >= t_last - 6,000 (the last two lifetimes).

  Scoring the largest clade alone, as this script did until 2026-09-06, can only ever
  under-report: a world in which a smaller clade holds the bar was recorded as a failure.

  Output, per arm, is five lines, each prefixed with the arm name so a grep still works:

    <arm>: PASS | ... passing clade ...      (or FAIL | ... best failing clade ...)
    <arm>: clades with a living member ... | largest: ...
    <arm>: photo (owner's wording): ...
    <arm>: photo (>=10, two lifetimes): ...
    <arm>: photo inh (population column): ...

  The **largest** line is the line this script printed before the change, byte for byte,
  including its own three-clause "-> clade PASS/fail" token and its stability segment. It is
  kept so logbook/0054's addendum reproduces: round 18's four passing seeds print minima
  48 / 41 / 24 / 127 over the last two lifetimes. Read the first line for the verdict; read
  the second for continuity with the record.

  **The verdict line (only) carries a qualifier segment** (Astra review F5,
  logbook/specs/script-contracts-spec.md, 2026-09-12), appended after its standing suffix
  (PROVISIONAL / CENSORED / no manifest / none of those) and never touching the largest
  line's byte-for-byte guarantee above:

    `| duration <simulated> of <requested> s [SHORT] | floor open|closes at <n> s|unknown
    | reading: absorptive clade only; producer clause not decided`

  **SHORT** means the manifest's simulated seconds are below 30,000, the campaign's round
  length and the span D063 as amended is read over -- this script cannot otherwise tell a
  10,000 s budget from a full one. It qualifies the reading; it does not change the verdict.
  **The floor** segment reads `FloorClosesAfterSeconds` (RunConfigJson's camel-cased
  `floorClosesAfterSeconds`, under "population") out of the run's own `config.json`, and
  reads `floor unknown` when the file lacks the key (a config older than the knob) or is
  missing outright. **The reading** segment exists because scripts/tests/clade-score's own
  fixture suite greps the literal `arm: PASS` this script has always printed, so that token
  is not renamed to `clade PASS`/`clade FAIL` -- the qualifier says in words what the PASS
  covers (the absorptive clade clauses only; the photosynthetic producer clause is reported
  on lines 3 and 4 and decides nothing yet).

  When no clade passes, the first line names the clade that fails the fewest clauses; ties
  go to the clade with the most living members at the last sample, then to the lowest root
  id. The largest clade is chosen exactly as scripts/reads/clade-score.py chose it -- strictly
  greater "alive at end" while walking clades in insertion order, so ties keep the first
  root found.

  **The producer clause (D063's (b)) is reported three ways and changes no verdict.** The
  owner's wording is "an inherited photosynthetic line alive at the end with a
  photosynthetic birth in the last 20 samples"; the threshold has not been ruled, so all
  three readings print side by side and the ruling can pick one without a rebuild:

  (a) "owner's wording" -- some photosynthetic clade has a living member at the last sample
      *and* an inherited photosynthetic birth (parent photosynthetic) inside that same clade
      within the last 20 samples. The "with" in the owner's wording binds the two, so they
      are asked of one clade; the detail text also prints the world-wide counts, so the
      looser two-conjunct reading is visible in the same line.

  (b) ">=10, two lifetimes" -- some photosynthetic clade holds >= 10 living members at every
      sample with t >= t_last - 6,000 *and* has an inherited photosynthetic birth within the
      last 20 samples. This is the reading the 2026-09-06 review recommended.

  Both (a) and (b) read a `"pho":0|1` field on lineage birth rows (1 when the newborn
  expresses photosynthesis), written from the Sim build that adds it. Every run recorded
  before that build has no such field, and both lines then print `flag absent` --
  "photo lineage: flag absent" -- rather than a failure. The field is found by name
  anywhere on the row, so its position among `abs`, `jnt` and `pt` does not matter, and a
  row without it (a death row, or an older birth row) parses normally.

  (c) "photo inh (population column)" -- the population-only reading this script has printed
      since 2026-09-04, unchanged: the report's `photo inh` column >= 10 at the last sample
      and at every one of the last 20 samples. Printed as "column absent" on a report
      written before the contract-repairs build added the columns.

  Reads runs/<arm>.md for the sample table -- t (s) is the join key -- and
  runs/<arm>/<newest run>/lineage.jsonl for birth and death events (the newest directory
  under runs/<arm>/, same convention as stop-arm.ps1 and lineage-invasion.ps1). Every report
  column this script reads (`inherit`, `photo`, `photo inh`) is looked up by the report's
  own header row, not by position -- CLAUDE.md's standing warning against positional column
  reads (logbook/0044). lineage.jsonl can run into the hundreds of MB, so it is streamed
  line by line and pulled apart with one anchored regex plus two IndexOf lookups, rather
  than loaded whole or parsed through ConvertFrom-Json. Both files are opened with
  FileShare.ReadWrite so a live run's own writer is not disturbed.

.PARAMETER Arm
  One or more arm names (<RunsRoot>/<Arm>.md plus <RunsRoot>/<Arm>/<newest run>/lineage.jsonl).

.PARAMETER RunsRoot
  Directory holding the arm reports and arm directories. Defaults to the repository's
  runs/. The fixture tests under scripts/tests/clade-score/ point it at synthetic runs.

.EXAMPLE
  ./scripts/clade-score.ps1 r18x-s1 r18x-s2 r18x-s3 r18x-s4 r18x-s5
#>
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Arm,

    [string]$RunsRoot
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $RunsRoot) { $RunsRoot = Join-Path $repo 'runs' }

# ---------------------------------------------------------------------------------------
# runs/<arm>.md -- the header row ("| t (s) | ..."), by name -> column index, and
# t (s) -> full row of trimmed, un-bolded cell strings. The data-row filter matches
# scripts/reads/clade-score.py exactly: a line starting "| " that is not the header and whose
# first cell is all digits. Opened ReadWrite -- run-arm.ps1 keeps writing this file for the
# life of a live arm.
# ---------------------------------------------------------------------------------------
function Read-Report([string]$Path) {
    $headerMap = $null
    $rowsOut = @{}
    $fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    $reader = New-Object System.IO.StreamReader($fs)
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.StartsWith('| t ')) {
                $cells = $line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim().Trim('*') }
                $headerMap = @{}
                for ($i = 0; $i -lt $cells.Count; $i++) { $headerMap[$cells[$i]] = $i }
            } elseif ($line.StartsWith('| ')) {
                $cells = $line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim().Trim('*') }
                if ($cells[0] -match '^\d+$') { $rowsOut[[int]$cells[0]] = $cells }
            }
        }
    } finally {
        $reader.Close()
        $fs.Close()
    }
    if ($null -eq $headerMap) { throw "no header row ('| t (s) | ...') found in $Path" }
    return @{ Header = $headerMap; Rows = $rowsOut }
}

# ---------------------------------------------------------------------------------------
# lineage.jsonl -- id -> birth record (t, p, k, abs, pho) and id -> death time. Streamed
# with a StreamReader opened ReadWrite so a live run's own writer is not disturbed (same as
# scripts/lineage-invasion.ps1).
#
# One anchored regex reads the fields whose order LineageEvent.ToJson() fixes at the head
# of the row -- e, t, id, then p and k on a birth. `abs` and `pho` are then found by name
# with IndexOf, not by position: `pho` is a later addition and the build is free to write
# it anywhere among abs/jnt/pt, and a regex that assumed a position would silently drop
# every birth row the day the field moved. HasPho records whether the flag was seen at all,
# which is what separates "no photosynthetic lineage" from "this run predates the flag".
# ---------------------------------------------------------------------------------------
function Read-Lineage([string]$Path) {
    $birthOut = @{}
    $deathOut = @{}
    $sawPho = $false
    $fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    $reader = New-Object System.IO.StreamReader($fs)
    $rx = [regex]'"e":"(?<e>[bd])","t":(?<t>[0-9.]+),"id":(?<id>\d+)(?:,"p":(?<p>-?\d+),"k":"(?<k>[a-z])")?'
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            $m = $rx.Match($line)
            if (-not $m.Success) { continue }
            $id = [int64]$m.Groups['id'].Value
            $t = [double]$m.Groups['t'].Value
            if ($m.Groups['e'].Value -eq 'b') {
                if (-not $m.Groups['p'].Success) { continue }

                $abs = 0
                $ia = $line.IndexOf('"abs":')
                if ($ia -ge 0) { $abs = [int]::Parse($line.Substring($ia + 6, 1)) }

                $pho = 0
                $ip = $line.IndexOf('"pho":')
                if ($ip -ge 0) {
                    $pho = [int]::Parse($line.Substring($ip + 6, 1))
                    $sawPho = $true
                }

                $birthOut[$id] = [pscustomobject]@{
                    t   = $t
                    p   = [int64]$m.Groups['p'].Value
                    k   = $m.Groups['k'].Value
                    abs = $abs
                    pho = $pho
                }
            } else {
                $deathOut[$id] = $t
            }
        }
    } finally {
        $reader.Close()
        $fs.Close()
    }
    return @{ Birth = $birthOut; Death = $deathOut; HasPho = $sawPho }
}

# Count of $Members alive at time $T: born by $T, not yet dead (or dead only after $T).
function Get-AliveAt($Members, [double]$T, $Birth, $Death) {
    $c = 0
    foreach ($m in $Members) {
        $bt = $Birth[$m].t
        $dt = if ($Death.ContainsKey($m)) { $Death[$m] } else { 1e12 }
        if ($bt -le $T -and $dt -gt $T) { $c++ }
    }
    return $c
}

# Alive count of $Members at every sample, in one pass over the members rather than one
# pass per sample: each member contributes a contiguous run of samples (born by t, dead
# after t), so a difference array plus a prefix sum gives the whole series. The predicate is
# Get-AliveAt's, exactly -- this is the same numbers, computed in O(members log samples)
# instead of O(members x samples), which is what makes scoring every clade affordable.
function Get-AliveSeries($Members, [int[]]$Samples, $Birth, $Death) {
    $n = $Samples.Count
    $diff = New-Object 'int[]' ($n + 1)
    foreach ($m in $Members) {
        $bt = $Birth[$m].t
        $dt = if ($Death.ContainsKey($m)) { $Death[$m] } else { 1e12 }

        # First index with Samples[i] >= bt (the member is alive from here).
        $lo = 0; $hi = $n
        while ($lo -lt $hi) {
            $mid = [int][math]::Floor(($lo + $hi) / 2)
            if ($Samples[$mid] -lt $bt) { $lo = $mid + 1 } else { $hi = $mid }
        }
        $iLo = $lo

        # First index with Samples[i] >= dt (exclusive end: dead at and after here).
        $lo = 0; $hi = $n
        while ($lo -lt $hi) {
            $mid = [int][math]::Floor(($lo + $hi) / 2)
            if ($Samples[$mid] -lt $dt) { $lo = $mid + 1 } else { $hi = $mid }
        }
        $iHi = $lo

        if ($iHi -gt $iLo) { $diff[$iLo]++; $diff[$iHi]-- }
    }
    $out = New-Object 'int[]' $n
    $run = 0
    for ($i = 0; $i -lt $n; $i++) { $run += $diff[$i]; $out[$i] = $run }
    return , $out
}

# Connected clades from $Birth over one expressed trait ('abs' or 'pho'): root -> ordered
# List[int64] of member ids. A clade begins at a birth expressing the trait whose parent did
# not express it, or at a founder (parent id -1) that expresses it; membership follows the
# parent chain forward while the trait is inherited. Both dictionaries are [ordered] because
# the largest-clade tie-break keeps whichever clade is iterated first, and that order must
# match scripts/reads/clade-score.py's dict insertion order -- ascending birth-id order of the
# first trait-expressing descendant reached.
function Get-Clades($Birth, [string]$Trait) {
    $ids = $Birth.Keys | Sort-Object
    $root = [ordered]@{}
    foreach ($i in $ids) {
        $b = $Birth[$i]
        if ($b.$Trait -ne 1) { continue }
        $p = $b.p
        $pb = if ($Birth.ContainsKey($p)) { $Birth[$p] } else { $null }
        if ($p -eq -1 -or $null -eq $pb -or $pb.$Trait -ne 1) {
            $root[$i] = $i
        } else {
            $root[$i] = if ($root.Contains($p)) { $root[$p] } else { $p }
        }
    }
    $clades = [ordered]@{}
    foreach ($i in $root.Keys) {
        $rt = $root[$i]
        if (-not $clades.Contains($rt)) { $clades[$rt] = New-Object System.Collections.Generic.List[int64] }
        $clades[$rt].Add([int64]$i)
    }
    return $clades
}

# The report's sampling interval, from its last two rows. The reports have always been
# sampled every 100 s, and the fixtures copy that, but the cadence is a launch setting and
# the window must follow it rather than assume it.
function Get-SampleInterval([int[]]$Samples) {
    if ($Samples.Count -lt 2) { return 100 }
    return $Samples[$Samples.Count - 1] - $Samples[$Samples.Count - 2]
}

# Inherited births inside $Members within the last 20 samples, i.e. in ($Last - $Window,
# $Last] -- the newborn expresses $Trait and so did its parent. Bounded above as well as
# below: a live run's lineage runs ahead of its report by up to a sample, and a birth after
# the last sample must not recruit for a window that has not seen it (the Astra review's
# R4, 2026-09-07). $Window is 20 sampling intervals read from the sample axis, not an
# assumed 2,000 s. O(members), so it is cheap enough to ask of every clade before deciding
# which to measure.
function Get-RecentInherited($Members, [double]$Last, [double]$Window, $Birth, [string]$Trait) {
    $recent = 0
    foreach ($m in $Members) {
        $bm = $Birth[$m]
        if ($bm.t -gt ($Last - $Window) -and $bm.t -le $Last -and $bm.p -ne -1) {
            $pb = if ($Birth.ContainsKey($bm.p)) { $Birth[$bm.p] } else { $null }
            if ($null -ne $pb -and $pb.$Trait -eq 1) { $recent++ }
        }
    }
    return $recent
}

# Everything the clauses need about one clade, from one alive-series pass plus one pass over
# the members' birth records. $Trait selects which flag "inherited" means. Every loop in here
# is over the sample axis, so it is called only for the handful of clades that survive the
# cheap O(members) filter above -- a world can carry thousands of dead clades, and measuring
# each of them across 300 samples is the difference between three seconds and three minutes.
function Measure-Clade($Members, [int[]]$Samples, $Birth, $Death, [string]$Trait) {
    $n = $Samples.Count
    $last = $Samples[$n - 1]
    $window = 20 * (Get-SampleInterval $Samples)
    $series = Get-AliveSeries $Members $Samples $Birth $Death
    $atEnd = $series[$n - 1]

    # Consecutive samples with >= 1 alive, counting back from the last sample.
    $cons = 0
    for ($i = $n - 1; $i -ge 0; $i--) {
        if ($series[$i] -ge 1) { $cons++ } else { break }
    }

    # first sample at which >= 10 were alive, and the minimum alive from there on.
    $first10 = $null
    $minSince = $null
    for ($i = 0; $i -lt $n; $i++) {
        if ($null -eq $first10 -and $series[$i] -ge 10) { $first10 = $Samples[$i] }
        if ($null -ne $first10) {
            if ($null -eq $minSince -or $series[$i] -lt $minSince) { $minSince = $series[$i] }
        }
    }

    # Stability (D063 amendment 2026-09-04): the minimum alive over the last two lifetimes.
    $stabilityMin = $null
    for ($i = 0; $i -lt $n; $i++) {
        if ($Samples[$i] -ge ($last - 6000)) {
            if ($null -eq $stabilityMin -or $series[$i] -lt $stabilityMin) { $stabilityMin = $series[$i] }
        }
    }

    $recent = Get-RecentInherited $Members $last $window $Birth $Trait
    $firstT = $null
    foreach ($m in $Members) {
        $bm = $Birth[$m]
        if ($null -eq $firstT -or $bm.t -lt $firstT) { $firstT = $bm.t }
    }

    return [pscustomobject]@{
        AtEnd = $atEnd; Cons = $cons; Recent = $recent
        First10 = $first10; MinSince = $minSince; StabilityMin = $stabilityMin
        FirstT = $firstT; N = $Members.Count; Members = $Members
    }
}

foreach ($a in $Arm) {
    $reportPath = Join-Path $RunsRoot "$a.md"
    $report = Read-Report $reportPath
    $header = $report.Header
    $rows = $report.Rows
    $samples = [int[]]@($rows.Keys | Sort-Object)
    $last = $samples[$samples.Count - 1]

    $armDir = Join-Path $RunsRoot $a
    $runDir = Get-ChildItem -LiteralPath $armDir -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name | Select-Object -Last 1
    if (-not $runDir) { throw "No run directory under $armDir." }
    $lineagePath = Join-Path $runDir.FullName 'lineage.jsonl'

    # The manifest's status and reason decide whether this is a reading or a verdict. A
    # running arm's report and lineage are both live, so its line is provisional; a run
    # that ended for any reason but its budget or extinction -- the wall clock, the
    # population ceiling, an error, a stop -- is censored (D058, logbook/0065), and its
    # line says so with how far it got, because a PASS at 10,000 s of a 30,000-s request
    # is not a pass (the Astra review's second response, 2026-09-07); a run with no
    # manifest (a fixture, or a run older than the manifest) says so. The clauses are
    # computed the same way in every case -- only the label changes.
    $manifestPath = Join-Path $runDir.FullName 'run.json'
    $standing = ''
    $simulated = '?'
    $requested = '?'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $standing = ' | no manifest'
    } else {
        $manifestText = [System.IO.File]::ReadAllText($manifestPath)
        $m = [regex]::Match($manifestText, '"status"\s*:\s*"([^"]+)"')
        $status = if ($m.Success) { $m.Groups[1].Value } else { 'unknown' }
        $m = [regex]::Match($manifestText, '"reason"\s*:\s*"([^"]+)"')
        $reason = if ($m.Success) { $m.Groups[1].Value } else { 'unknown' }
        $m = [regex]::Match($manifestText, '"simulatedSeconds"\s*:\s*([0-9.]+)')
        $simulated = if ($m.Success) { $m.Groups[1].Value } else { '?' }
        $m = [regex]::Match($manifestText, '"requestedSeconds"\s*:\s*([0-9.]+)')
        $requested = if ($m.Success) { $m.Groups[1].Value } else { '?' }
        if ($status -ne 'ended' -and $status -ne 'stopped') {
            $standing = " | PROVISIONAL: manifest status $status, the report and the lineage are still being written"
        } elseif ($status -eq 'stopped' -or ($reason -ne 'budget' -and $reason -ne 'extinct')) {
            $standing = " | CENSORED: $status ($reason) at t=$simulated of $requested s requested; a reading, not a verdict"
        }
    }

    # -------------------------------------------------------------------------------
    # The qualifier segment (Astra review F5, logbook/specs/script-contracts-spec.md): three
    # readings appended to both the verdict line and the largest-clade line, none of which
    # change PASS/FAIL -- they say what it does not yet cover.
    #
    # Duration: the goal rule (D063 as amended) is read over a 30,000 s round, and this
    # script cannot otherwise tell a 10,000 s budget from a full one. SHORT qualifies the
    # reading; it does not change it.
    # -------------------------------------------------------------------------------
    $durationSeg = "duration $simulated of $requested s"
    $simNum = 0.0
    if (($simulated -ne '?') -and [double]::TryParse($simulated, [ref]$simNum)) {
        if ($simNum -lt 30000) { $durationSeg += ' SHORT' }
    }

    # The floor: config.json sits beside the manifest and lineage, in the run directory.
    # FloorClosesAfterSeconds (RunConfig.cs) is written as "floorClosesAfterSeconds" under
    # the "population" group (RunConfigJson.cs / ConfigSchema's camel-case of the property
    # name). A config written before the knob existed has no such key.
    $floorSeg = 'floor unknown'
    $configPath = Join-Path $runDir.FullName 'config.json'
    if (Test-Path -LiteralPath $configPath) {
        $configText = [System.IO.File]::ReadAllText($configPath)
        $m = [regex]::Match($configText, '"floorClosesAfterSeconds"\s*:\s*([0-9.]+)')
        if ($m.Success) {
            $floorVal = [double]$m.Groups[1].Value
            $floorSeg = if ($floorVal -gt 0) { "floor closes at $($m.Groups[1].Value) s" } else { 'floor open' }
        }
    }

    # Name the reading: scripts/tests/clade-score/run-tests.ps1 (under scripts/tests/) greps
    # its own fixtures for the literal "arm: PASS" the verdict line prints today, so the
    # token is not renamed to "clade PASS"/"clade FAIL" -- the qualifier says the same thing
    # in words instead.
    $readingSeg = 'reading: absorptive clade only; producer clause not decided'

    $qualifierSeg = " | $durationSeg | $floorSeg | $readingSeg"

    $lineage = Read-Lineage $lineagePath
    $birth = $lineage.Birth
    $death = $lineage.Death

    # -------------------------------------------------------------------------------
    # Every absorptive clade with a living member at the last sample, against all four
    # clauses. The largest is tracked separately, on scripts/reads/clade-score.py's own
    # tie-break (strictly greater "alive at end", first root found wins a tie).
    # -------------------------------------------------------------------------------
    $clades = Get-Clades $birth 'abs'
    $largest = $null
    $scored = New-Object System.Collections.Generic.List[object]

    foreach ($rt in $clades.Keys) {
        $mem = $clades[$rt]
        if ((Get-AliveAt $mem $last $birth $death) -eq 0) { continue }
        $s = Measure-Clade $mem $samples $birth $death 'abs'

        $fails = New-Object System.Collections.Generic.List[string]
        if ($s.Cons -lt 20) { $fails.Add("streak $($s.Cons) < 20") }
        if ($s.AtEnd -lt 10) { $fails.Add("alive at end $($s.AtEnd) < 10") }
        if ($s.Recent -lt 1) { $fails.Add('no inherited absorptive birth in the last 20 samples') }
        if ($s.StabilityMin -lt 10) { $fails.Add("stability min $($s.StabilityMin) < 10") }

        $rec = [pscustomobject]@{
            Rt = [int64]$rt; Rk = $birth[$rt].k; Stats = $s
            Fails = $fails; FailCount = $fails.Count
        }
        $scored.Add($rec)

        if ($null -eq $largest -or $s.AtEnd -gt $largest.Stats.AtEnd) { $largest = $rec }
    }

    $livingClades = $scored.Count

    if (-not $header.ContainsKey('inherit')) { throw "no 'inherit' column in $reportPath" }
    $inheritAtEnd = $rows[$last][$header['inherit']]

    # -------------------------------------------------------------------------------
    # Line 1 -- the verdict. PASS if any clade meets every clause. Otherwise the clade
    # that fails fewest, ties to the most living members at the last sample, then to
    # the lowest root id, so the choice does not move between runs of the script.
    # -------------------------------------------------------------------------------
    $passing = @($scored | Where-Object { $_.FailCount -eq 0 } |
        Sort-Object @{ Expression = { $_.Stats.AtEnd }; Descending = $true },
                    @{ Expression = { $_.Rt }; Descending = $false })

    if ($passing.Count -gt 0) {
        $w = $passing[0]
        $verdictLine = "{0}: PASS | passing clade: root {1} (kind {2}, born {3}), {4} alive at end, min last 6000 s = {5}, alive-streak {6} samples, inherited births in last 20 samples {7} | {8} of {9} living clades pass | aggregate inherit@end {10}" -f `
            $a, $w.Rt, $w.Rk, $w.Stats.FirstT, $w.Stats.AtEnd, $w.Stats.StabilityMin,
            $w.Stats.Cons, $w.Stats.Recent, $passing.Count, $livingClades, $inheritAtEnd
    } elseif ($scored.Count -gt 0) {
        $ranked = @($scored | Sort-Object @{ Expression = { $_.FailCount }; Descending = $false },
                                          @{ Expression = { $_.Stats.AtEnd }; Descending = $true },
                                          @{ Expression = { $_.Rt }; Descending = $false })
        $w = $ranked[0]
        $why = ($w.Fails -join '; ')
        $verdictLine = "{0}: FAIL | best clade: root {1} (kind {2}, born {3}), {4} alive at end, min last 6000 s = {5}, alive-streak {6} samples, inherited births in last 20 samples {7} | fails {8} of 4: {9} | 0 of {10} living clades pass | aggregate inherit@end {11}" -f `
            $a, $w.Rt, $w.Rk, $w.Stats.FirstT, $w.Stats.AtEnd, $w.Stats.StabilityMin,
            $w.Stats.Cons, $w.Stats.Recent, $w.FailCount, $why, $livingClades, $inheritAtEnd
    } else {
        $verdictLine = "{0}: FAIL | no absorptive clade alive at the last sample | aggregate inherit@end {1}" -f `
            $a, $inheritAtEnd
    }
    $verdictLine += $standing
    $verdictLine += $qualifierSeg

    # -------------------------------------------------------------------------------
    # Line 2 -- the largest clade, in the format this script printed before every clade
    # was scored, so logbook/0054's addendum reproduces from it. Its "-> clade" token is
    # still the three original clauses only, with stability as its own segment, exactly
    # as recorded.
    # -------------------------------------------------------------------------------
    if ($null -ne $largest) {
        $s = $largest.Stats
        $first10Str = if ($null -eq $s.First10) { 'None' } else { "$($s.First10)" }
        $minSinceStr = if ($null -eq $s.MinSince) { 'None' } else { "$($s.MinSince)" }
        $cladeVerdict = if ($s.Cons -ge 20 -and $s.AtEnd -ge 10 -and $s.Recent -ge 1) { 'PASS' } else { 'fail' }
        $stableVerdict = if ($s.StabilityMin -ge 10) { 'stable' } else { 'unstable' }
        $stabilitySeg = "stability: min last 6000 s = $($s.StabilityMin) -> $stableVerdict"

        $largestLine = "{0}: clades with a living member at {1}: {2} | largest: root {3} (kind {4}, born {5}), {6} members ever, {7} alive at end, alive-streak {8} samples, first>=10 at {9}, min since {10}, inherited births in last 20 samples {11} -> clade {12} | aggregate inherit@end {13} | {14}" -f `
            $a, $last, $livingClades, $largest.Rt, $largest.Rk, $s.FirstT, $s.N, $s.AtEnd,
            $s.Cons, $first10Str, $minSinceStr, $s.Recent, $cladeVerdict, $inheritAtEnd, $stabilitySeg
    } else {
        $largestLine = "{0}: clades with a living member at {1}: {2} | no absorptive clade alive at the last sample -> clade unread | aggregate inherit@end {3} | stability: unread (no absorptive clade alive at the last sample)" -f `
            $a, $last, $livingClades, $inheritAtEnd
    }

    # -------------------------------------------------------------------------------
    # Lines 3 and 4 -- the producer clause read from the lineage flag, two ways. Neither
    # changes the verdict; the owner's ruling picks one.
    # -------------------------------------------------------------------------------
    if ($lineage.HasPho) {
        $phoClades = Get-Clades $birth 'pho'
        $phoScored = New-Object System.Collections.Generic.List[object]
        foreach ($rt in $phoClades.Keys) {
            $mem = $phoClades[$rt]
            if ((Get-AliveAt $mem $last $birth $death) -eq 0 -and
                (Get-RecentInherited $mem $last (20 * (Get-SampleInterval $samples)) $birth 'pho') -eq 0) { continue }
            $s = Measure-Clade $mem $samples $birth $death 'pho'
            $phoScored.Add([pscustomobject]@{ Rt = [int64]$rt; Stats = $s })
        }

        $phoAlive = @($phoScored | Where-Object { $_.Stats.AtEnd -gt 0 })
        $phoAliveMax = 0
        foreach ($c in $phoAlive) { if ($c.Stats.AtEnd -gt $phoAliveMax) { $phoAliveMax = $c.Stats.AtEnd } }
        $phoRecentTotal = 0
        foreach ($c in $phoScored) { $phoRecentTotal += $c.Stats.Recent }

        # (a) owner's wording: one clade, alive at the end, with an inherited
        # photosynthetic birth in the last 20 samples.
        $ownerHit = @($phoScored | Where-Object { $_.Stats.AtEnd -ge 1 -and $_.Stats.Recent -ge 1 } |
            Sort-Object @{ Expression = { $_.Stats.AtEnd }; Descending = $true },
                        @{ Expression = { $_.Rt }; Descending = $false })
        if ($ownerHit.Count -gt 0) {
            $h = $ownerHit[0]
            $ownerSeg = "held -- clade root {0} (born {1}), {2} alive at end, {3} inherited photosynthetic births in the last 20 samples" -f `
                $h.Rt, $h.Stats.FirstT, $h.Stats.AtEnd, $h.Stats.Recent
        } else {
            $ownerSeg = 'failed'
        }
        $ownerSeg = "$ownerSeg (world-wide: photosynthetic clades alive at the last sample {0}, largest {1}; inherited photosynthetic births in the last 20 samples {2})" -f `
            $phoAlive.Count, $phoAliveMax, $phoRecentTotal

        # (b) >= 10 through the last two lifetimes, plus the same recent birth.
        $tenHit = @($phoScored | Where-Object { $_.Stats.StabilityMin -ge 10 -and $_.Stats.Recent -ge 1 } |
            Sort-Object @{ Expression = { $_.Stats.StabilityMin }; Descending = $true },
                        @{ Expression = { $_.Rt }; Descending = $false })
        if ($tenHit.Count -gt 0) {
            $h = $tenHit[0]
            $tenSeg = "held -- clade root {0} (born {1}), min last 6000 s = {2}, {3} alive at end, {4} inherited photosynthetic births in the last 20 samples" -f `
                $h.Rt, $h.Stats.FirstT, $h.Stats.StabilityMin, $h.Stats.AtEnd, $h.Stats.Recent
        } else {
            $bestTen = 0
            foreach ($c in $phoScored) { if ($c.Stats.StabilityMin -gt $bestTen) { $bestTen = $c.Stats.StabilityMin } }
            $tenSeg = "failed (best photosynthetic clade min over the last 6000 s = $bestTen)"
        }
    } else {
        $ownerSeg = 'flag absent | photo lineage: flag absent (no "pho" field on lineage birth rows -- run recorded before the flag existed)'
        $tenSeg = 'flag absent | photo lineage: flag absent'
    }

    # -------------------------------------------------------------------------------
    # Line 5 -- the population-only column reading, unchanged since 2026-09-04.
    # -------------------------------------------------------------------------------
    if ($header.ContainsKey('photo') -and $header.ContainsKey('photo inh')) {
        $phIdx = $header['photo inh']
        $last20 = if ($samples.Count -le 20) { $samples } else { $samples[($samples.Count - 20)..($samples.Count - 1)] }
        try {
            $photoInhAtEnd = [double]$rows[$last][$phIdx]
            $photoInhVals = @($last20 | ForEach-Object { [double]$rows[$_][$phIdx] })
            $photoInhMin = ($photoInhVals | Measure-Object -Minimum).Minimum
            $producerPop = ($photoInhAtEnd -ge 10) -and ($photoInhMin -ge 10)
            $popVerdict = if ($producerPop) { 'held' } else { 'failed' }
            $popSeg = "$popVerdict -- photo inh at end = $photoInhAtEnd, min over last 20 = $photoInhMin"
        } catch {
            $popSeg = "unread -- photo inh column present but not numeric in $reportPath"
        }
    } else {
        $popSeg = 'column absent'
    }

    Write-Output $verdictLine
    Write-Output $largestLine
    Write-Output ("{0}: photo (owner's wording): {1}" -f $a, $ownerSeg)
    Write-Output ("{0}: photo (>=10, two lifetimes): {1}" -f $a, $tenSeg)
    Write-Output ("{0}: photo inh (population column): {1}" -f $a, $popSeg)
}
