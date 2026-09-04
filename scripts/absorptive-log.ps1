<#
.SYNOPSIS
  Read one run's absorptive.jsonl — the per-creature ledger log.

.DESCRIPTION
  What logbook/0050's dissection could not produce. lineage.jsonl carries birth, death,
  parentage and the expressed-absorptive flag and no physiology; snapshots/ carry genome
  graphs with no id to join on. absorptive.jsonl is one row per living creature with
  absorptive tissue per report sample, plus one final row per such creature when it dies —
  so depth, local nutrient density and the energy budget are readable per creature and over
  time.

  Two views:
    * per creature — birth, death, lifetime, children, mean densityHere, mean netW, min energy
    * per time bin — mean depth, mean densityHere and mean netW across the living eaters

  Reads with FileShare.ReadWrite, so a live run's writer is not disturbed and the file can be
  read while an arm is still going. A row buffered but not yet flushed is simply not there
  yet; the log is buffered like lineage.jsonl, not flushed like stats.jsonl.

  Rows with "truncated" instead of an id are the cap markers (World.AbsorptiveLogRowCap):
  more than 2,000 eaters were alive and the rest of that sample was left out. They are
  reported separately and never averaged.

.PARAMETER Arm
  Arm name; the newest run directory under runs/<Arm>/ is used.

.PARAMETER Path
  An absorptive.jsonl to read directly, instead of an arm name.

.PARAMETER Bin
  Time-bin width for the table, simulated seconds. Default 1000.

.PARAMETER Top
  How many creatures the per-creature list shows, longest-lived first. Default 25.
  0 shows every one of them.

.PARAMETER Ids
  Restrict both views to these lineage ids — e.g. the inoculants from
  scripts/lineage-invasion.ps1.

.EXAMPLE
  ./scripts/absorptive-log.ps1 r15i-c10
  ./scripts/absorptive-log.ps1 r15i-c10 -Bin 500 -Top 0
  ./scripts/absorptive-log.ps1 -Path runs/x/y/absorptive.jsonl
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Arm,
    [string]$Path,
    [double]$Bin = 1000,
    [int]$Top = 25,
    [long[]]$Ids
)

$ErrorActionPreference = 'Stop'

if (-not $Path) {
    if (-not $Arm) { throw "Give an arm name or -Path." }
    $dir = Get-ChildItem -Directory "$PSScriptRoot/../runs/$Arm" | Sort-Object Name | Select-Object -Last 1
    if (-not $dir) { throw "No run directory under runs/$Arm." }
    $Path = Join-Path $dir.FullName 'absorptive.jsonl'
}
if (-not (Test-Path $Path)) { throw "Not found: $Path" }

$wanted = $null
if ($Ids) { $wanted = @{}; foreach ($i in $Ids) { $wanted[[long]$i] = $true } }

# Stream with FileShare.ReadWrite so a live run's writer is not disturbed — the same reason
# JsonlWriter.ReadRows exists rather than callers reaching for File.ReadAllLines, which opens
# with FileShare.Read and throws a sharing violation against a running arm.
$fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
$reader = New-Object System.IO.StreamReader($fs)

# One regex over the whole row rather than a JSON parse per line: these files run to hundreds
# of thousands of rows and the schema is fixed and flat (AbsorptiveSample.ToJson). Named groups
# so nothing here indexes a column by position — CLAUDE.md's rule, learned the hard way when a
# positional misread reported float tissue as the food chain (logbook/0044).
$rx = [regex]('"t":(?<t>-?[0-9.E+-]+),"id":(?<id>\d+),"age":(?<age>-?[0-9.E+-]+),' +
    '"gen":(?<gen>\d+),"patch":(?<patch>\d+),"y":(?<y>-?[0-9.E+-]+),' +
    '"volume":(?<volume>-?[0-9.E+-]+),"absVolume":(?<absv>-?[0-9.E+-]+),' +
    '"photoArea":(?<area>-?[0-9.E+-]+),"parts":(?<parts>\d+),"mixotroph":(?<mixo>true|false),' +
    '"energy":(?<energy>-?[0-9.E+-]+),"tissue":(?<tissue>-?[0-9.E+-]+),' +
    '"endowment":(?<endow>-?[0-9.E+-]+),"densityHere":(?<dens>-?[0-9.E+-]+),' +
    '"share":(?<share>-?[0-9.E+-]+),"foodW":(?<food>-?[0-9.E+-]+),"lightW":(?<light>-?[0-9.E+-]+),' +
    '"upkeepW":(?<upkeep>-?[0-9.E+-]+),"exudedW":(?<exuded>-?[0-9.E+-]+),"netW":(?<net>-?[0-9.E+-]+),' +
    '"children":(?<children>\d+),"lastChildT":(?<lastChild>null|-?[0-9.E+-]+),"dead":(?<dead>true|false)')
$rxTrunc = [regex]'"t":(?<t>-?[0-9.E+-]+),"truncated":(?<n>\d+)'

# Per creature. A hashtable of small accumulators rather than the rows themselves: a bloom's
# file is hundreds of megabytes and only the summary is wanted.
$c = @{}
$bins = @{}
$truncRows = 0
$truncMax = 0
$malformed = 0
$rowCount = 0
$sampleRows = @{}
$firstT = [double]::PositiveInfinity
$lastT = 0.0
$deadRows = 0
$mixoIds = @{}

while ($null -ne ($line = $reader.ReadLine())) {
    $m = $rx.Match($line)
    if (-not $m.Success) {
        $t = $rxTrunc.Match($line)
        if ($t.Success) {
            $truncRows++
            $n = [int]$t.Groups['n'].Value
            if ($n -gt $truncMax) { $truncMax = $n }
        } else { $malformed++ }
        continue
    }

    $id = [long]$m.Groups['id'].Value
    if ($wanted -and -not $wanted.ContainsKey($id)) { continue }

    $rowCount++
    $tt = [double]$m.Groups['t'].Value
    if ($tt -lt $firstT) { $firstT = $tt }
    if ($tt -gt $lastT) { $lastT = $tt }

    if (-not $sampleRows.ContainsKey($tt)) { $sampleRows[$tt] = 0 }
    $sampleRows[$tt]++

    $dens = [double]$m.Groups['dens'].Value
    $net = [double]$m.Groups['net'].Value
    $y = [double]$m.Groups['y'].Value
    $energy = [double]$m.Groups['energy'].Value
    $dead = $m.Groups['dead'].Value -eq 'true'
    if ($dead) { $deadRows++ }
    if ($m.Groups['mixo'].Value -eq 'true') { $mixoIds[$id] = $true }

    if (-not $c.ContainsKey($id)) {
        $c[$id] = [pscustomobject]@{
            Id = $id; First = $tt; Last = $tt; Age = [double]$m.Groups['age'].Value
            Rows = 0; DensSum = 0.0; NetSum = 0.0; DepthSum = 0.0
            MinEnergy = $energy; Children = 0; LastChildT = $null
            Gen = [int]$m.Groups['gen'].Value; Patch = [int]$m.Groups['patch'].Value
            AbsVolume = [double]$m.Groups['absv'].Value
            Mixotroph = ($m.Groups['mixo'].Value -eq 'true')
            Died = $false; DiedAt = $null
        }
    }

    $e = $c[$id]
    $e.Rows++
    $e.Last = $tt
    $e.Age = [double]$m.Groups['age'].Value
    $e.DensSum += $dens
    $e.NetSum += $net
    $e.DepthSum += $y
    if ($energy -lt $e.MinEnergy) { $e.MinEnergy = $energy }
    $e.Children = [int]$m.Groups['children'].Value
    if ($m.Groups['lastChild'].Value -ne 'null') { $e.LastChildT = [double]$m.Groups['lastChild'].Value }
    $e.Patch = [int]$m.Groups['patch'].Value
    if ($dead) { $e.Died = $true; $e.DiedAt = $tt }

    # Living rows only in the time table: a death row is the same creature's last step counted
    # a second time, and a bin's mean depth should be a mean over the living, not over the
    # living plus everything that had just stopped being alive.
    if ($dead) { continue }

    $b = [math]::Floor($tt / $Bin) * $Bin
    if (-not $bins.ContainsKey($b)) {
        $bins[$b] = [pscustomobject]@{ Bin = $b; N = 0; Depth = 0.0; Dens = 0.0; Net = 0.0; Ids = @{} }
    }
    $bins[$b].N++
    $bins[$b].Depth += $y
    $bins[$b].Dens += $dens
    $bins[$b].Net += $net
    $bins[$b].Ids[$id] = $true
}
$reader.Close()

"== $Path"
if ($rowCount -eq 0) {
    "no absorptive rows" + $(if ($wanted) { " for the {0} id(s) asked for" -f $wanted.Count } else { "" }) +
        $(if ($truncRows -gt 0) { " ({0} truncation markers)" -f $truncRows } else { "" })
    if ($malformed -gt 0) { "{0} unparsed line(s)" -f $malformed }
    return
}

"{0} rows over {1} creatures; t {2:0} to {3:0}; {4} death rows; {5} mixotroph(s)" -f
    $rowCount, $c.Count, $firstT, $lastT, $deadRows, $mixoIds.Count
if ($truncRows -gt 0) {
    "CAPPED: {0} sample(s) hit the 2,000-row cap, worst {1} creatures left out" -f $truncRows, $truncMax
}
if ($malformed -gt 0) { "{0} line(s) did not parse — the row schema may have changed" -f $malformed }

$all = $c.Values
$lifetimes = @($all | Where-Object { $_.Died } | ForEach-Object { $_.Age })
if ($lifetimes.Count -gt 0) {
    $ls = $lifetimes | Measure-Object -Average -Minimum -Maximum
    "lifetime of the {0} that died: mean {1:0} s, min {2:0}, max {3:0}; {4} still logging at the end" -f
        $lifetimes.Count, $ls.Average, $ls.Minimum, $ls.Maximum, ($c.Count - $lifetimes.Count)
}
$kids = @($all | ForEach-Object { $_.Children })
$ks = $kids | Measure-Object -Sum -Average
"children: {0} total, mean {1:0.00} per creature, {2} of {3} bred at all" -f
    $ks.Sum, $ks.Average, @($kids | Where-Object { $_ -gt 0 }).Count, $c.Count

$shown = $all | Sort-Object -Property @{ Expression = { $_.Last - $_.First }; Descending = $true }
if ($Top -gt 0) { $shown = $shown | Select-Object -First $Top }

''
"per creature (longest-logged first{0}):" -f $(if ($Top -gt 0 -and $c.Count -gt $Top) { ", $Top of $($c.Count)" } else { "" })
"{0,8} {1,4} {2,3} {3,8} {4,8} {5,7} {6,4} {7,9} {8,10} {9,10} {10,10}" -f
    'id', 'gen', 'pch', 'first s', 'last s', 'age s', 'kids', 'mean y m', 'mean J/m3', 'mean netW', 'min E J'
foreach ($e in $shown) {
    "{0,8} {1,4} {2,3} {3,8:0} {4,8:0} {5,7:0} {6,4} {7,9:0.0} {8,10:0.####} {9,10:0.####} {10,10:0.##}{11}" -f
        $e.Id, $e.Gen, $e.Patch, $e.First, $e.Last, $e.Age, $e.Children,
        ($e.DepthSum / $e.Rows), ($e.DensSum / $e.Rows), ($e.NetSum / $e.Rows), $e.MinEnergy,
        $(if ($e.Died) { ' died' } else { '' })
}

''
"per {0} s bin, across the living eaters:" -f $Bin
"{0,8} {1,7} {2,7} {3,10} {4,12} {5,12}" -f 't', 'rows', 'alive', 'mean y m', 'mean J/m3', 'mean netW'
foreach ($b in ($bins.Values | Sort-Object Bin)) {
    "{0,8:0} {1,7} {2,7} {3,10:0.0} {4,12:0.####} {5,12:0.####}" -f
        $b.Bin, $b.N, $b.Ids.Count, ($b.Depth / $b.N), ($b.Dens / $b.N), ($b.Net / $b.N)
}

''
"rows per sample (t: rows), last 12:"
$samples = @($sampleRows.Keys | Sort-Object)
$tail = if ($samples.Count -gt 12) { $samples[-12..-1] } else { $samples }
($tail | ForEach-Object { "{0:0}:{1}" -f $_, $sampleRows[$_] }) -join '  '
