<#
.SYNOPSIS
  Score an evolution-run report against logbook/0036's pre-registered predictions.

.DESCRIPTION
  logbook/0036-the-floor-gives-back.md was written in two halves on purpose: the
  predictions (P1-P7) were committed before any arm was launched. This script reads a
  run report (runs/<Name>.md) and computes, from the data alone, what each prediction's
  falsifying column actually says — it does not read the logbook file itself, only the
  arithmetic in the "Predictions, and the column that falsifies each" table.

  Columns are located BY NAME from the table header (line 5 of the report, with '**'
  stripped), never by position, because older reports (e.g. runs/d050-mix-heavy.md) have
  fewer columns than newer ones (no 'det deep', no 'remin' in the settings line). Missing
  columns degrade to "n/a" rather than erroring.

  Read-only: this touches nothing under runs/ or unity*/, and writes nothing anywhere.

.PARAMETER Name
  One or more arm names (runs/<Name>.md, without extension). Default: every
  runs/d051-*.md found, sorted by name.

.PARAMETER WindowSamples
  P6's minimum consecutive-sample count for a passing inherit/floor streak. Default 20
  (the pre-registration's "20 consecutive samples (2,000 s)", assuming 100 s spacing --
  this script does not assume that spacing, it counts samples and reports the elapsed
  span it actually found).

.PARAMETER AfterT
  The "arrival must be late enough to mean something" cutoff used by P5 (first absorpt
  after this t) and P6 (the winning streak must start after this t). Default 3000, per
  the pre-registration's own arithmetic for when the floor stops firing.

.EXAMPLE
  ./scripts/read-arm.ps1
  Scores every runs/d051-*.md arm found.

.EXAMPLE
  ./scripts/read-arm.ps1 -Name d051-rem-s1, d051-ctl-s1 -AfterT 2500
#>
[CmdletBinding()]
param(
    [string[]]$Name,
    [int]$WindowSamples = 20,
    [double]$AfterT = 3000
)

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$runsDir = Join-Path $root 'runs'

if (-not $Name -or $Name.Count -eq 0) {
    $files = Get-ChildItem -Path $runsDir -Filter 'd051-*.md' -ErrorAction SilentlyContinue | Sort-Object Name
    $Name = @($files | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) })
    if ($Name.Count -eq 0) { Write-Warning "No runs/d051-*.md files found under $runsDir." }
}

# ---------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------

# Drop a leading/trailing empty element left by a leading/trailing '|' in a table row,
# without PowerShell's "N..(N-1)" descending-range trap when the array has 0 or 1 elements.
function Trim-Edges {
    param([string[]]$Arr)
    if (-not $Arr -or $Arr.Count -eq 0) { return @() }
    $startIdx = 0
    $endIdx   = $Arr.Count - 1
    if ($Arr[0] -eq '')  { $startIdx = 1 }
    if ($Arr[-1] -eq '') { $endIdx = $Arr.Count - 2 }
    if ($endIdx -lt $startIdx) { return @() }
    return $Arr[$startIdx..$endIdx]
}

# A cell may be bold ('**16**'), a percentage ('2.1%'), an em-dash for "not applicable"
# ('-- ' -- e.g. lift/flt m when float count is 0), or plain. Returns $null, never throws.
function ConvertTo-Num {
    param([string]$Raw)
    if ($null -eq $Raw) { return $null }
    $v = ($Raw -replace '\*\*', '').Trim()
    if ($v -eq '' -or $v -eq [char]0x2014 -or $v -eq '-') { return $null }
    $v = $v -replace '%', ''
    $v = $v -replace '`', ''
    $num = [double]0
    $styles = [System.Globalization.NumberStyles]::Float -bor [System.Globalization.NumberStyles]::AllowLeadingSign
    if ([double]::TryParse($v, $styles, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$num)) {
        return $num
    }
    return $null
}

function Get-Setting {
    param([string]$SettingsLine, [string]$Prefix)
    $tokens = $SettingsLine -split ' · '
    $tok = $tokens | Where-Object { $_ -like "$Prefix *" } | Select-Object -First 1
    if ($tok) { return ($tok -replace "^$Prefix ", '').Trim() }
    return $null
}

# Reads a run report and returns the settings line, the name->index column map, and the
# raw string cells for every data row (line 7 onward, stopping at the first non-'|' line
# or EOF -- covers both a finished run's trailing prose and an in-progress run's bare
# EOF-after-last-row).
function Read-RunReport {
    param([string]$Path)
    $lines = Get-Content -Path $Path -Encoding UTF8
    if ($lines.Count -lt 7) { throw "runs/$([System.IO.Path]::GetFileName($Path)) is too short to be a run report (line 7 = first data row)." }

    $settingsLine = $lines[2]   # line 3
    $headerLine   = $lines[4]   # line 5

    $headerCells = Trim-Edges (($headerLine.Trim() -split '\|') | ForEach-Object { $_.Trim() })
    $colNames    = $headerCells | ForEach-Object { $_ -replace '\*\*', '' }
    $colIndex    = @{}
    for ($i = 0; $i -lt $colNames.Count; $i++) { $colIndex[$colNames[$i]] = $i }

    $rows = New-Object System.Collections.Generic.List[object]
    for ($li = 6; $li -lt $lines.Count; $li++) {
        $line = $lines[$li]
        if ($null -eq $line -or -not $line.TrimStart().StartsWith('|')) { break }
        $cells = Trim-Edges (($line.Trim() -split '\|') | ForEach-Object { $_.Trim() })
        $rows.Add($cells) | Out-Null
    }

    [PSCustomObject]@{
        SettingsLine = $settingsLine
        ColIndex     = $colIndex
        Rows         = $rows
    }
}

function Get-Cell {
    param($Row, $ColIndex, [string]$ColName)
    if (-not $ColIndex.ContainsKey($ColName)) { return $null }
    $idx = $ColIndex[$ColName]
    if ($idx -ge $Row.Count) { return $null }
    return $Row[$idx]
}

# Value at a checkpoint t: exact match preferred; if the data doesn't reach that far yet,
# say so rather than silently substituting the nearest (misleadingly early) sample.
function Get-AtT {
    param($Samples, [double]$TargetT)
    $exact = $Samples | Where-Object { $_.T -eq $TargetT } | Select-Object -First 1
    if ($exact) { return [PSCustomObject]@{ Sample = $exact; Note = 'exact' } }
    $lastT = ($Samples | Select-Object -Last 1).T
    if ($null -eq $lastT -or $lastT -lt $TargetT) {
        return [PSCustomObject]@{ Sample = $null; Note = "not reached (data ends at t=$lastT)" }
    }
    $best = $null; $bestDiff = [double]::MaxValue
    foreach ($s in $Samples) {
        if ($null -eq $s.T) { continue }
        $d = [Math]::Abs($s.T - $TargetT)
        if ($d -lt $bestDiff) { $bestDiff = $d; $best = $s }
    }
    return [PSCustomObject]@{ Sample = $best; Note = "nearest t=$($best.T)" }
}

function Format-Num {
    param($V, [string]$Suffix = '')
    if ($null -eq $V) { return 'n/a' }
    return "$([Math]::Round($V, 4))$Suffix"
}

# ---------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------

$summaryRows = @()

foreach ($armName in $Name) {
    $path = Join-Path $runsDir "$armName.md"
    Write-Host ''
    Write-Host "==== $armName ====" -ForegroundColor Cyan
    if (-not (Test-Path $path)) {
        Write-Warning "  runs/$armName.md not found -- skipping."
        continue
    }

    $report = Read-RunReport -Path $path
    $colIndex = $report.ColIndex
    $hasDetDeep = $colIndex.ContainsKey('det deep')

    $seed   = Get-Setting $report.SettingsLine 'seed'
    $mixing = Get-Setting $report.SettingsLine 'mixing'
    $remin  = Get-Setting $report.SettingsLine 'remin'
    $reminNum = if ($remin) { ConvertTo-Num ($remin -replace '/s', '') } else { $null }
    $armType = if ($null -eq $reminNum) { 'unknown' } elseif ($reminNum -eq 0) { 'control' } else { 'treatment' }

    $samples = foreach ($row in $report.Rows) {
        [PSCustomObject]@{
            T        = ConvertTo-Num (Get-Cell $row $colIndex 't (s)')
            Alive    = ConvertTo-Num (Get-Cell $row $colIndex 'alive')
            FloorPct = ConvertTo-Num (Get-Cell $row $colIndex '% on floor')
            DetDeep  = ConvertTo-Num (Get-Cell $row $colIndex 'det deep')
            Absorpt  = ConvertTo-Num (Get-Cell $row $colIndex 'absorpt')
            Inherit  = ConvertTo-Num (Get-Cell $row $colIndex 'inherit')
            Floor    = ConvertTo-Num (Get-Cell $row $colIndex 'floor')
            GenMin   = ConvertTo-Num (Get-Cell $row $colIndex 'gen min')
        }
    }
    $samples = @($samples | Where-Object { $null -ne $_.T } | Sort-Object T)

    if ($samples.Count -eq 0) {
        Write-Warning "  runs/$armName.md has a header but no parseable data rows -- skipping."
        continue
    }

    $lastSample = $samples[-1]
    $lastT = $lastSample.T

    # --- header info ------------------------------------------------------------------
    $logPath = Join-Path $env:TEMP "evosim-$armName.log"
    if (Test-Path $logPath) {
        $runaway = Select-String -Path $logPath -Pattern 'PopulationRunaway' -Quiet
        $endedBy = if ($runaway) { 'ceiling/runaway (PopulationRunaway found in log)' } else { 'no runaway found in log' }
    } else {
        $endedBy = 'log absent'
    }

    Write-Host "  seed $seed  ·  mixing $(if ($mixing) { $mixing } else { 'n/a' })  ·  remin $(if ($remin) { $remin } else { 'n/a' })  ·  arm type: $armType"
    Write-Host "  last sample: t=$lastT  alive=$($lastSample.Alive)"
    Write-Host "  ended: $endedBy"

    # --- P1/P3: % on floor -------------------------------------------------------------
    $at2000  = Get-AtT $samples 2000
    $at5000  = Get-AtT $samples 5000
    $at10000 = Get-AtT $samples 10000

    $after2000 = @($samples | Where-Object { $_.T -ge 2000 -and $null -ne $_.FloorPct })
    $monotonic = $null
    $plateaued = $null
    $rangeMin = $null; $rangeMax = $null
    if ($after2000.Count -ge 2) {
        $monotonic = $true
        for ($i = 1; $i -lt $after2000.Count; $i++) {
            if ($after2000[$i].FloorPct -lt ($after2000[$i - 1].FloorPct - 0.5)) { $monotonic = $false }
        }
        $rangeMin = ($after2000 | Measure-Object -Property FloorPct -Minimum).Minimum
        $rangeMax = ($after2000 | Measure-Object -Property FloorPct -Maximum).Maximum
        $plateaued = (($rangeMax - $rangeMin) -le 15)
    }
    $firstPast50 = $samples | Where-Object { $_.T -le 5000 -and $null -ne $_.FloorPct -and $_.FloorPct -ge 50 } | Select-Object -First 1

    Write-Host "  P1/P3 (% on floor):"
    Write-Host "    t=2000:  $(if ($at2000.Sample) { Format-Num $at2000.Sample.FloorPct '%' } else { 'n/a' })  [$($at2000.Note)]"
    Write-Host "    t=5000:  $(if ($at5000.Sample) { Format-Num $at5000.Sample.FloorPct '%' } else { 'n/a' })  [$($at5000.Note)]"
    Write-Host "    t=10000: $(if ($at10000.Sample) { Format-Num $at10000.Sample.FloorPct '%' } else { 'n/a' })  [$($at10000.Note)]"
    Write-Host "    end:     $(Format-Num $lastSample.FloorPct '%') (t=$lastT)"
    if ($after2000.Count -ge 2) {
        Write-Host "    monotonic non-decreasing after t=2000 (0.5pt jitter): $monotonic  (range $(Format-Num $rangeMin)-$(Format-Num $rangeMax)%)"
        Write-Host "    plateaued after t=2000 (max-min <= 15pt): $plateaued"
    } else {
        Write-Host "    monotonic/plateau: insufficient data (need >=2 samples at t>=2000, have $($after2000.Count))"
    }
    Write-Host "    passes 50% by t=5000: $(if ($firstPast50) { "yes, first at t=$($firstPast50.T)" } else { 'no' })"

    $p1p3Verdict = 'n/a (remin unknown)'
    if ($armType -eq 'control') {
        if ($after2000.Count -ge 2) {
            $p1Pass = $monotonic -and ($firstPast50 -ne $null)
            $p1p3Verdict = if ($p1Pass) { 'PASS P1' } else { 'FAIL P1' }
        } else {
            $p1p3Verdict = 'insufficient data'
        }
    } elseif ($armType -eq 'treatment') {
        if ($after2000.Count -ge 2) {
            $inRange = -not ($after2000 | Where-Object { $_.FloorPct -lt 10 -or $_.FloorPct -gt 25 })
            $p3Pass = $plateaued -and $inRange
            $p1p3Verdict = if ($p3Pass) { 'PASS P3' } else { 'FAIL P3' }
        } else {
            $p1p3Verdict = 'insufficient data'
        }
    }
    Write-Host "    verdict: $p1p3Verdict"

    # --- P2/P4: det deep -----------------------------------------------------------
    Write-Host "  P2/P4 (det deep):"
    $maxDetDeep = $null
    $reach2 = $null; $reach4 = $null; $reach8 = $null
    $p2p4Verdict = 'n/a (no det deep column)'
    if ($hasDetDeep) {
        $withDetDeep = @($samples | Where-Object { $null -ne $_.DetDeep })
        if ($withDetDeep.Count -gt 0) {
            $maxDetDeep = ($withDetDeep | Measure-Object -Property DetDeep -Maximum).Maximum
            $reach2 = ($withDetDeep | Where-Object { $_.DetDeep -ge 2 } | Select-Object -First 1).T
            $reach4 = ($withDetDeep | Where-Object { $_.DetDeep -ge 4 } | Select-Object -First 1).T
            $reach8 = ($withDetDeep | Where-Object { $_.DetDeep -ge 8 } | Select-Object -First 1).T
        }
        Write-Host "    max: $(Format-Num $maxDetDeep ' J/m3')"
        Write-Host "    first t >= 2 J/m3: $(if ($null -ne $reach2) { $reach2 } else { 'never' })"
        Write-Host "    first t >= 4 J/m3: $(if ($null -ne $reach4) { $reach4 } else { 'never' })"
        Write-Host "    first t >= 8 J/m3: $(if ($null -ne $reach8) { $reach8 } else { 'never' })"

        if ($armType -eq 'control') {
            $violator = $withDetDeep | Where-Object { $_.DetDeep -ge 2 } | Select-Object -First 1
            $p2p4Verdict = if ($violator) { "FAIL P2 (first >=2 at t=$($violator.T))" } else { "PASS P2 (through t=$lastT)" }
        } elseif ($armType -eq 'treatment') {
            $by5000 = if ($null -ne $reach4 -and $reach4 -le 5000) { 'PASS' } elseif ($lastT -ge 5000) { 'FAIL' } else { "insufficient (ends t=$lastT)" }
            $by10000 = if ($null -ne $reach8 -and $reach8 -le 10000) { 'PASS' } elseif ($lastT -ge 10000) { 'FAIL' } else { "insufficient (ends t=$lastT)" }
            $p2p4Verdict = "P4: >=4 by 5000: $by5000; >=8 by 10000: $by10000"
        } else {
            $p2p4Verdict = 'n/a (remin unknown)'
        }
    } else {
        Write-Host "    (column absent in this report)"
    }
    Write-Host "    verdict: $p2p4Verdict"

    # --- P5: absorpt arrival after AfterT --------------------------------------------
    $withAbsorpt = @($samples | Where-Object { $null -ne $_.Absorpt })
    $maxAbsorpt = if ($withAbsorpt.Count -gt 0) { ($withAbsorpt | Measure-Object -Property Absorpt -Maximum).Maximum } else { $null }
    $firstAbsorptAfter = $samples | Where-Object { $_.T -gt $AfterT -and $null -ne $_.Absorpt -and $_.Absorpt -gt 0 } | Select-Object -First 1
    Write-Host "  P5 (absorpt after t=$AfterT):"
    Write-Host "    first t: $(if ($firstAbsorptAfter) { $firstAbsorptAfter.T } else { 'none' })"
    Write-Host "    max absorpt over run: $(Format-Num $maxAbsorpt)"
    $p5Verdict = 'n/a (remin unknown)'
    if ($armType -eq 'treatment') {
        $p5Verdict = if ($firstAbsorptAfter) { 'PASS P5' } elseif ($lastT -gt $AfterT) { 'FAIL P5' } else { "insufficient data (ends t=$lastT)" }
    } elseif ($armType -eq 'control') {
        $p5Verdict = 'n/a (P5 is a treatment prediction)'
    }
    Write-Host "    verdict: $p5Verdict"

    # --- P6: longest inherit>=1 & floor=0 streak -------------------------------------
    $bestLen = 0; $bestStartT = $null; $bestEndT = $null
    $curLen = 0; $curStartT = $null; $curEndT = $null
    foreach ($s in $samples) {
        $ok = ($null -ne $s.Inherit -and $s.Inherit -ge 1) -and ($null -ne $s.Floor -and $s.Floor -eq 0)
        if ($ok) {
            if ($curLen -eq 0) { $curStartT = $s.T }
            $curLen++
            $curEndT = $s.T
            if ($curLen -gt $bestLen) { $bestLen = $curLen; $bestStartT = $curStartT; $bestEndT = $curEndT }
        } else {
            $curLen = 0; $curStartT = $null
        }
    }
    $p6Pass = ($bestLen -ge $WindowSamples) -and ($null -ne $bestStartT) -and ($bestStartT -gt $AfterT)
    $genMinAtEnd = if ($null -ne $bestEndT) { ($samples | Where-Object { $_.T -eq $bestEndT } | Select-Object -First 1).GenMin } else { $null }

    Write-Host "  P6 (inherit>=1 & floor=0 streak):"
    if ($bestLen -gt 0) {
        Write-Host "    longest run: start t=$bestStartT, end t=$bestEndT, $bestLen samples (span $($bestEndT - $bestStartT)s)"
        Write-Host "    gen min at window end: $(if ($null -ne $genMinAtEnd) { $genMinAtEnd } else { 'n/a' })"
    } else {
        Write-Host "    longest run: none"
    }
    Write-Host "    verdict: $(if ($p6Pass) { 'PASS P6' } else { 'FAIL P6' })"

    # --- floor spawns after t=1000 (contamination) -----------------------------------
    $floorSpawns = ($samples | Where-Object { $_.T -gt 1000 -and $null -ne $_.Floor } | Measure-Object -Property Floor -Sum).Sum
    Write-Host "  floor spawns after t=1000: $(if ($null -ne $floorSpawns) { $floorSpawns } else { 'n/a' })"

    # --- overall verdict --------------------------------------------------------------
    $checks = @()
    if ($armType -eq 'control') {
        if ($p1p3Verdict -like 'PASS*') { $checks += 'pass' } elseif ($p1p3Verdict -like 'FAIL*') { $checks += 'fail' }
        if ($p2p4Verdict -like 'PASS*') { $checks += 'pass' } elseif ($p2p4Verdict -like 'FAIL*') { $checks += 'fail' }
    } elseif ($armType -eq 'treatment') {
        if ($p1p3Verdict -like 'PASS*') { $checks += 'pass' } elseif ($p1p3Verdict -like 'FAIL*') { $checks += 'fail' }
        if ($p2p4Verdict -match 'PASS') { $checks += 'pass' }
        if ($p2p4Verdict -match 'FAIL') { $checks += 'fail' }
        if ($p5Verdict -like 'PASS*') { $checks += 'pass' } elseif ($p5Verdict -like 'FAIL*') { $checks += 'fail' }
    }
    if ($p6Pass) { $checks += 'pass' } else { $checks += 'fail' }
    $overall = if ($checks.Count -eq 0) { 'n/a' }
               elseif (-not ($checks -contains 'fail')) { 'PASS' }
               elseif (-not ($checks -contains 'pass')) { 'FAIL' }
               else { 'PARTIAL' }

    $summaryRows += [PSCustomObject]@{
        Name    = $armName
        Remin   = if ($remin) { $remin } else { 'n/a' }
        ReminNum = $reminNum
        ArmType = $armType
        P1P3    = "$p1p3Verdict (end $(Format-Num $lastSample.FloorPct '%'))"
        P2P4    = if ($hasDetDeep) { "$(Format-Num $maxDetDeep) max -- $p2p4Verdict" } else { 'n/a' }
        P5      = if ($firstAbsorptAfter) { "t=$($firstAbsorptAfter.T)" } elseif ($armType -eq 'treatment') { 'none' } else { 'n/a' }
        P6      = if ($bestLen -gt 0) { "start=$bestStartT len=$bestLen $(if ($p6Pass) {'PASS'} else {'FAIL'})" } else { 'none FAIL' }
        Verdict = $overall
    }
}

# ---------------------------------------------------------------------------------------
# Summary table
# ---------------------------------------------------------------------------------------

if ($summaryRows.Count -gt 0) {
    Write-Host ''
    Write-Host '==== Summary ====' -ForegroundColor Cyan
    $summaryRows | Format-Table -AutoSize Name, Remin, P1P3, P2P4, P5, P6, Verdict | Out-Host

    $remin0Pass  = @($summaryRows | Where-Object { $_.ReminNum -eq 0 -and $_.P6 -like '*PASS*' }).Count
    $remin0Total = @($summaryRows | Where-Object { $_.ReminNum -eq 0 }).Count
    $reminTPass  = @($summaryRows | Where-Object { $_.ReminNum -gt 0 -and $_.P6 -like '*PASS*' }).Count
    $reminTTotal = @($summaryRows | Where-Object { $_.ReminNum -gt 0 }).Count

    Write-Host "remin 0 arms passing P6: $remin0Pass of $remin0Total  ·  remin 0.01 arms passing P6: $reminTPass of $reminTTotal"
}
