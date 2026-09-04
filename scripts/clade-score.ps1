<#
.SYNOPSIS
  Score a run's connected clade against the goal rule (D063 as amended, DECISIONS.md).

.DESCRIPTION
  A port of scratch/clade-score.py (gitignored) into scripts/, per D063's amendment note
  ("scratch/clade-score.py is the scorer until it moves into scripts/") and logbook/0054's
  addendum "scored again by connected clade".

  A clade begins at an absorptive birth whose parent did not express the trait (or at an
  absorptive founder, parent id -1); membership follows the parent chain forward while the
  trait is inherited. Among the clades with a living member at the run's last sample, the
  one with the most members alive at that sample is scored -- ties keep whichever root was
  found first while walking birth ids in ascending order (scratch/clade-score.py's own
  tie-break: it compares only the "alive at end" figure, not the full record, so cons/recent
  never break a tie). The three original D063 clauses are then asked of that one clade:
  alive for >= 20 consecutive samples ending at the last sample, >= 10 living members at the
  last sample, and at least one inherited absorptive birth within the last 20 samples.

  The two clauses the owner's 2026-09-04 ruling added on top of that are implemented as
  follows:

  (a) Stability -- the scored clade holds >= 10 living members at every report sample with
  t >= t_last - 6,000 (the last two lifetimes). Printed as "min last 6,000 s = N" plus a
  stable/unstable verdict. If no clade has a living member at the last sample, this clause
  is printed as unread rather than guessed at.

  (b) Producer lineage -- read from the report's own `photo` / `photo inh` columns by
  header name (present only from the contract-repairs build onward; printed as
  "photo: column absent" and left unread, not failed, when either is missing). Only the
  population half of the clause is checked here: `photo inh` >= 10 at the last sample and
  at every one of the last 20 samples. The amendment's other half -- "a photosynthetic
  birth in the last 20 samples" -- is NOT checked, because lineage.jsonl rows carry an
  `abs` (absorptive) and a `jnt` (jointed) flag but no photosynthetic flag to read a birth
  from; a PASS on this clause is reported as population-only for that reason.

  Reads runs/<arm>.md for the sample table -- t (s) is the join key -- and
  runs/<arm>/<newest run>/lineage.jsonl for birth and death events (the newest directory
  under runs/<arm>/, same convention as stop-arm.ps1 and lineage-invasion.ps1; the python
  instead takes glob.glob(...)[0], whose order is unspecified -- this only differs from the
  python if an arm ever has more than one run directory). Every report column this script
  reads (`inherit`, `photo`, `photo inh`) is looked up by the report's own header row, not
  by position -- CLAUDE.md's standing warning against positional column reads
  (logbook/0044). lineage.jsonl can run into the hundreds of MB, so it is streamed line by
  line and each line is pulled apart with a regex against the fixed field order
  LineageEvent.ToJson() writes, rather than loaded whole or parsed through ConvertFrom-Json.
  Both files are opened with FileShare.ReadWrite so a live run's own writer is not disturbed.

.PARAMETER Arm
  One or more arm names (runs/<Arm>.md plus runs/<Arm>/<newest run>/lineage.jsonl).

.EXAMPLE
  ./scripts/clade-score.ps1 r18x-s1 r18x-s2 r18x-s3 r18x-s4 r18x-s5
#>
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Arm
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------------------------------
# runs/<arm>.md -- the header row ("| t (s) | ..."), by name -> column index, and
# t (s) -> full row of trimmed, un-bolded cell strings. The data-row filter matches
# scratch/clade-score.py exactly: a line starting "| " that is not the header and whose
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
# lineage.jsonl -- id -> birth record (t, p, k, abs) and id -> death time. Streamed with a
# StreamReader opened ReadWrite so a live run's own writer is not disturbed (same as
# scripts/lineage-invasion.ps1); parsed with a regex against the fixed field order
# LineageEvent.ToJson() writes -- e, t, id, p, k, g, s, abs, jnt, pt for a birth; e, t, id, c
# for a death -- instead of ConvertFrom-Json, which is too slow at hundreds of MB.
# ---------------------------------------------------------------------------------------
function Read-Lineage([string]$Path) {
    $birthOut = @{}
    $deathOut = @{}
    $fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    $reader = New-Object System.IO.StreamReader($fs)
    $rx = [regex]'"e":"(?<e>[bd])","t":(?<t>[0-9.]+),"id":(?<id>\d+)(?:,"p":(?<p>-?\d+),"k":"(?<k>[a-z])","g":\d+,"s":\d+,"abs":(?<abs>[01]))?'
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            $m = $rx.Match($line)
            if (-not $m.Success) { continue }
            $id = [int64]$m.Groups['id'].Value
            $t = [double]$m.Groups['t'].Value
            if ($m.Groups['e'].Value -eq 'b') {
                if ($m.Groups['p'].Success) {
                    $birthOut[$id] = [pscustomobject]@{
                        t   = $t
                        p   = [int64]$m.Groups['p'].Value
                        k   = $m.Groups['k'].Value
                        abs = [int]$m.Groups['abs'].Value
                    }
                }
            } else {
                $deathOut[$id] = $t
            }
        }
    } finally {
        $reader.Close()
        $fs.Close()
    }
    return @{ Birth = $birthOut; Death = $deathOut }
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

# Connected clades from $Birth: root -> ordered List[int64] of member ids. Both dictionaries
# are [ordered] because the caller's tie-break (Get-AliveAt equal at the last sample) keeps
# whichever clade is iterated first, and that order must match scratch/clade-score.py's dict
# insertion order -- ascending birth-id order of the first absorptive descendant reached.
function Get-Clades($Birth) {
    $ids = $Birth.Keys | Sort-Object
    $root = [ordered]@{}
    foreach ($i in $ids) {
        $b = $Birth[$i]
        if ($b.abs -ne 1) { continue }
        $p = $b.p
        $pb = if ($Birth.ContainsKey($p)) { $Birth[$p] } else { $null }
        if ($p -eq -1 -or $null -eq $pb -or $pb.abs -ne 1) {
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

foreach ($a in $Arm) {
    $reportPath = Join-Path $repo "runs/$a.md"
    $report = Read-Report $reportPath
    $header = $report.Header
    $rows = $report.Rows
    $samples = @($rows.Keys | Sort-Object)
    $last = $samples[-1]

    $armDir = Join-Path $repo "runs/$a"
    $runDir = Get-ChildItem -LiteralPath $armDir -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name | Select-Object -Last 1
    if (-not $runDir) { throw "No run directory under runs/$a." }
    $lineagePath = Join-Path $runDir.FullName 'lineage.jsonl'

    $lineage = Read-Lineage $lineagePath
    $birth = $lineage.Birth
    $death = $lineage.Death

    $clades = Get-Clades $birth

    $best = $null
    foreach ($rt in $clades.Keys) {
        $mem = $clades[$rt]
        $atEnd = Get-AliveAt $mem $last $birth $death
        if ($atEnd -eq 0) { continue }

        # Consecutive samples with >= 1 alive, counting back from the last sample.
        $cons = 0
        for ($si = $samples.Count - 1; $si -ge 0; $si--) {
            if ((Get-AliveAt $mem $samples[$si] $birth $death) -ge 1) { $cons++ } else { break }
        }

        # Inherited absorptive births within the last 2,000 s (D063's "last 20 samples", at
        # the report's 100 s sampling interval).
        $recent = 0
        foreach ($m in $mem) {
            $bm = $birth[$m]
            if ($bm.t -gt ($last - 2000) -and $bm.p -ne -1) {
                $pb = if ($birth.ContainsKey($bm.p)) { $birth[$bm.p] } else { $null }
                if ($null -ne $pb -and $pb.abs -eq 1) { $recent++ }
            }
        }

        $firstT = ($mem | ForEach-Object { $birth[$_].t } | Measure-Object -Minimum).Minimum
        $rk = $birth[$rt].k
        $n = $mem.Count

        $ge10 = New-Object System.Collections.Generic.List[int]
        foreach ($t in $samples) {
            if ((Get-AliveAt $mem $t $birth $death) -ge 10) { $ge10.Add($t) }
        }
        $first10 = if ($ge10.Count -gt 0) { $ge10[0] } else { $null }
        $minSince = $null
        if ($ge10.Count -gt 0) {
            foreach ($t in $samples) {
                if ($t -ge $ge10[0]) {
                    $alive = Get-AliveAt $mem $t $birth $death
                    if ($null -eq $minSince -or $alive -lt $minSince) { $minSince = $alive }
                }
            }
        }

        # Tie-break matches scratch/clade-score.py exactly: strictly-greater "alive at end"
        # only. Ties keep the first clade found -- cons/recent are never consulted.
        if ($null -eq $best -or $atEnd -gt $best.AtEnd) {
            $best = [pscustomobject]@{
                AtEnd = $atEnd; Cons = $cons; Recent = $recent; Rt = $rt; Rk = $rk
                FirstT = $firstT; N = $n; First10 = $first10; MinSince = $minSince
                Members = $mem
            }
        }
    }

    $livingClades = 0
    foreach ($cladeMembers in $clades.Values) {
        if ((Get-AliveAt $cladeMembers $last $birth $death) -gt 0) { $livingClades++ }
    }

    if (-not $header.ContainsKey('inherit')) { throw "no 'inherit' column in $reportPath" }
    $inheritAtEnd = $rows[$last][$header['inherit']]

    # ---------------------------------------------------------------------------------
    # (a) Stability clause (D063 amendment, 2026-09-04): the scored clade holds >= 10
    # living members at every sample in the last two lifetimes (t >= t_last - 6,000).
    # ---------------------------------------------------------------------------------
    if ($null -ne $best) {
        $stabilityWindow = @($samples | Where-Object { $_ -ge ($last - 6000) })
        $stabilityMin = $null
        foreach ($t in $stabilityWindow) {
            $alive = Get-AliveAt $best.Members $t $birth $death
            if ($null -eq $stabilityMin -or $alive -lt $stabilityMin) { $stabilityMin = $alive }
        }
        $stableVerdict = if ($stabilityMin -ge 10) { 'stable' } else { 'unstable' }
        $stabilitySeg = "stability: min last 6000 s = $stabilityMin -> $stableVerdict"
    } else {
        $stabilitySeg = 'stability: unread (no absorptive clade alive at the last sample)'
    }

    # ---------------------------------------------------------------------------------
    # (b) Producer-lineage clause (D063 amendment): population half only -- see
    # .DESCRIPTION for why the birth half cannot be read from lineage.jsonl today.
    # ---------------------------------------------------------------------------------
    if ($header.ContainsKey('photo') -and $header.ContainsKey('photo inh')) {
        $phIdx = $header['photo inh']
        $last20 = if ($samples.Count -le 20) { $samples } else { $samples[($samples.Count - 20)..($samples.Count - 1)] }
        try {
            $photoInhAtEnd = [double]$rows[$last][$phIdx]
            $photoInhVals = @($last20 | ForEach-Object { [double]$rows[$_][$phIdx] })
            $photoInhMin = ($photoInhVals | Measure-Object -Minimum).Minimum
            $producerPop = ($photoInhAtEnd -ge 10) -and ($photoInhMin -ge 10)
            $producerVerdict = if ($producerPop) { 'PASS (population only -- birth half unread)' } else { 'fail' }
            $producerSeg = "producer: photo inh at end = $photoInhAtEnd, min over last 20 = $photoInhMin -> $producerVerdict"
        } catch {
            $producerSeg = "producer: photo inh column present but not numeric in $reportPath -> unread"
        }
    } else {
        $producerSeg = 'photo: column absent -> producer clause unread'
    }

    if ($null -ne $best) {
        $first10Str = if ($null -eq $best.First10) { 'None' } else { "$($best.First10)" }
        $minSinceStr = if ($null -eq $best.MinSince) { 'None' } else { "$($best.MinSince)" }
        $verdict = if ($best.Cons -ge 20 -and $best.AtEnd -ge 10 -and $best.Recent -ge 1) { 'PASS' } else { 'fail' }

        $line = "{0}: clades with a living member at {1}: {2} | largest: root {3} (kind {4}, born {5}), {6} members ever, {7} alive at end, alive-streak {8} samples, first>=10 at {9}, min since {10}, inherited births in last 20 samples {11} -> clade {12} | aggregate inherit@end {13}" -f `
            $a, $last, $livingClades, $best.Rt, $best.Rk, $best.FirstT, $best.N, $best.AtEnd, $best.Cons, $first10Str, $minSinceStr, $best.Recent, $verdict, $inheritAtEnd
    } else {
        $line = "{0}: clades with a living member at {1}: {2} | no absorptive clade alive at the last sample -> clade unread | aggregate inherit@end {3}" -f `
            $a, $last, $livingClades, $inheritAtEnd
    }

    $line = "$line | $stabilitySeg | $producerSeg"

    Write-Output $line
}
