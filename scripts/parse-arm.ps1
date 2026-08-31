<#
.SYNOPSIS
  Generic parser for an evolution-run report (runs/<name>.md). No verdicts, no round logic.

.DESCRIPTION
  Reads one run report and exposes it as data: the settings-line metadata tokens, the
  named data columns (located BY NAME from the table header, never by position, because
  older reports have fewer columns than newer ones), the parsed footer/termination line,
  and a couple of generic conveniences (per-column peak/trough/last, longest streak where
  a named column satisfies a simple threshold predicate).

  This script used to be the front half of read-arm.ps1, which bolted logbook/0036's
  (D051) P1-P6 pass/fail rules onto the same parsing. That coupling meant every later
  round's arms got scored against round 1's predictions by accident -- read-arm.ps1 now
  does that scoring, on top of this file's parsing, and refuses to run against a
  configHash it does not recognise. This file makes no claim about what any number
  *means* -- it only locates and reports it.

  Read-only: this touches nothing under runs/ or unity*/, and writes nothing anywhere.

.PARAMETER Path
  Path to a run report (runs/<name>.md), relative to the current directory or absolute.

.PARAMETER Column
  Optional. Restrict the printed peak/trough/last table to these column names (as they
  appear in the report's header, '**' stripped). Default: every column found.

.PARAMETER StreakColumn
  Optional. Name of a column to compute the longest consecutive-sample streak over, using
  a simple threshold predicate against -StreakAtLeast / -StreakAtMost / -StreakEquals (at
  least one of which must be given if -StreakColumn is given; more than one combines with
  AND, e.g. a closed range).

.PARAMETER StreakAtLeast
  Streak predicate: column value >= this.

.PARAMETER StreakAtMost
  Streak predicate: column value <= this.

.PARAMETER StreakEquals
  Streak predicate: column value == this.

.EXAMPLE
  ./scripts/parse-arm.ps1 -Path runs/d056-s2.md

.EXAMPLE
  ./scripts/parse-arm.ps1 -Path runs/d056-s2.md -StreakColumn inherit -StreakAtLeast 1

.EXAMPLE
  # As a library, from another script:
  . ./scripts/parse-arm.ps1
  $report = Read-ArmReport -Path 'runs/d056-s2.md'
  $configHash = (Get-ArmMetaValue -Tokens $report.Metadata -Prefix 'configHash') -replace '`', ''
#>
[CmdletBinding()]
param(
    # Not [Parameter(Mandatory)]: dot-sourcing this file as a library (read-arm.ps1 does)
    # binds this same param block with no arguments, and a mandatory parameter would
    # block on a prompt non-interactively. Required-ness is enforced below instead, only
    # on the direct-invocation path that actually needs it.
    [string]$Path,

    [string[]]$Column,

    [string]$StreakColumn,
    [Nullable[double]]$StreakAtLeast,
    [Nullable[double]]$StreakAtMost,
    [Nullable[double]]$StreakEquals
)

$ErrorActionPreference = 'Stop'

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

# The settings line (line 3) split into its raw '·'-separated tokens, e.g. 'seed 1',
# 'idle 0.02 W/N·m' (the '·' inside a unit survives because it has no surrounding spaces).
function Split-MetaTokens {
    param([string]$Line)
    if (-not $Line) { return @() }
    return @($Line -split ' · ' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

# Value of the token whose prefix matches, e.g. Get-ArmMetaValue $tokens 'seed' -> '1'.
# $null when no token has that prefix -- callers decide what "absent" means, this never guesses.
function Get-ArmMetaValue {
    param([string[]]$Tokens, [string]$Prefix)
    $tok = $Tokens | Where-Object { $_ -eq $Prefix -or $_ -like "$Prefix *" } | Select-Object -First 1
    if ($null -eq $tok) { return $null }
    if ($tok -eq $Prefix) { return '' }
    return $tok.Substring($Prefix.Length + 1).Trim()
}

# Named-column access into one parsed row (a PSCustomObject with one property per column).
function Get-ArmCell {
    param($Row, [string]$ColumnName)
    if ($null -eq $Row) { return $null }
    $prop = $Row.PSObject.Properties[$ColumnName]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-ArmNumericCell {
    param($Row, [string]$ColumnName)
    return ConvertTo-Num (Get-ArmCell $Row $ColumnName)
}

# The footer prose after the last table row: '**Ended:** ...', the physics-steps/wall-clock
# line, the fastest-creature line, and the genomes path. Any piece not found stays $null --
# an in-progress run (killed before it wrote a footer) parses with every field null rather
# than throwing.
function Parse-ArmFooter {
    param([string[]]$Lines)

    $text = (($Lines) -join "`n").Trim()

    $result = [PSCustomObject]@{
        Raw              = $text
        EndedReason      = $null
        PhysicsSteps     = $null
        SimSeconds       = $null
        Births           = $null
        WallClockMinutes = $null
        RealTimeMultiple = $null
        FastestSpeedMps  = $null
        FastestSpeedAtT  = $null
        GenomesPath      = $null
    }

    if ($text -match '\*\*Ended:\*\*\s*(?<reason>[^\r\n]+?)\.?\s*(\r?\n|$)') {
        $result.EndedReason = $Matches['reason'].Trim()
    }
    if ($text -match '(?<steps>[\d,]+)\s+physics steps\s*·\s*(?<sim>[\d,]+)\s+simulated seconds\s*·\s*(?<births>[\d,]+)\s+births\s*·\s*(?<wall>[\d.]+)\s+min wall clock\s*\((?<mult>[\d.]+)x real time\)') {
        $result.PhysicsSteps     = [double]($Matches['steps'] -replace ',', '')
        $result.SimSeconds       = [double]($Matches['sim'] -replace ',', '')
        $result.Births           = [double]($Matches['births'] -replace ',', '')
        $result.WallClockMinutes = [double]$Matches['wall']
        $result.RealTimeMultiple = [double]$Matches['mult']
    }
    if ($text -match '\*\*Fastest creature seen at any point:\s*(?<speed>[\d.]+)\s*m/s,\s*at t=(?<t>[\d.]+)\s*s\.\*\*') {
        $result.FastestSpeedMps = [double]$Matches['speed']
        $result.FastestSpeedAtT = [double]$Matches['t']
    }
    if ($text -match 'Genomes:\s*`(?<path>[^`]+)`') {
        $result.GenomesPath = $Matches['path']
    }

    return $result
}

# Reads a run report end to end: title, settings-line metadata tokens, named columns, one
# row per sample (a PSCustomObject keyed by column name, raw string values), and the
# parsed footer. Data rows run from line 7 to the first line that is not a '|' table row
# (a finished run's trailing prose, or an in-progress run's bare EOF).
function Read-ArmReport {
    param([string]$Path)

    if (-not (Test-Path $Path)) { throw "Report not found: $Path" }
    $full  = (Resolve-Path $Path).Path
    $lines = Get-Content -Path $full -Encoding UTF8

    if ($lines.Count -lt 7) { throw "$full is too short to be a run report (line 7 = first data row)." }

    $title        = $lines[0]
    $settingsLine = $lines[2]   # line 3
    $headerLine   = $lines[4]   # line 5

    $metaTokens  = Split-MetaTokens $settingsLine
    $headerCells = Trim-Edges (($headerLine.Trim() -split '\|') | ForEach-Object { $_.Trim() })
    $columnNames = @($headerCells | ForEach-Object { $_ -replace '\*\*', '' })

    $rows        = New-Object System.Collections.Generic.List[object]
    $footerLines = New-Object System.Collections.Generic.List[string]
    $inFooter    = $false

    for ($li = 6; $li -lt $lines.Count; $li++) {
        $line = $lines[$li]
        if (-not $inFooter -and $null -ne $line -and $line.TrimStart().StartsWith('|')) {
            $cells = Trim-Edges (($line.Trim() -split '\|') | ForEach-Object { $_.Trim() })
            $row = [ordered]@{}
            for ($i = 0; $i -lt $columnNames.Count; $i++) {
                $row[$columnNames[$i]] = if ($i -lt $cells.Count) { $cells[$i] } else { $null }
            }
            $rows.Add([PSCustomObject]$row) | Out-Null
        } else {
            $inFooter = $true
            if ($null -ne $line) { $footerLines.Add($line) }
        }
    }

    [PSCustomObject]@{
        Path         = $full
        ArmName      = [System.IO.Path]::GetFileNameWithoutExtension($full)
        Title        = $title
        SettingsLine = $settingsLine
        Metadata     = $metaTokens
        ColumnNames  = $columnNames
        Rows         = $rows
        Footer       = (Parse-ArmFooter -Lines $footerLines)
    }
}

# Peak / trough / last numeric value of one named column, over every row that parses.
function Get-ArmColumnSummary {
    param($Report, [string]$ColumnName)

    $values  = New-Object System.Collections.Generic.List[double]
    $lastVal = $null
    foreach ($row in $Report.Rows) {
        $v = Get-ArmNumericCell $row $ColumnName
        if ($null -ne $v) { $values.Add($v); $lastVal = $v }
    }
    if ($values.Count -eq 0) {
        return [PSCustomObject]@{ Column = $ColumnName; Count = 0; Trough = $null; Peak = $null; Last = $null }
    }
    [PSCustomObject]@{
        Column = $ColumnName
        Count  = $values.Count
        Trough = ($values | Measure-Object -Minimum).Minimum
        Peak   = ($values | Measure-Object -Maximum).Maximum
        Last   = $lastVal
    }
}

# Longest run of consecutive samples (in report order) where the named column's numeric
# value satisfies a simple threshold predicate. AND's together whichever of
# AtLeast/AtMost/Equals are given (e.g. a closed range); a row where the column does not
# parse breaks the streak. Returns Length 0 (StartT/EndT $null) when no sample qualifies.
function Get-ArmStreak {
    param(
        $Report,
        [Parameter(Mandatory = $true)][string]$ColumnName,
        [Nullable[double]]$AtLeast,
        [Nullable[double]]$AtMost,
        [Nullable[double]]$Equals
    )

    if ($null -eq $AtLeast -and $null -eq $AtMost -and $null -eq $Equals) {
        throw 'Get-ArmStreak needs at least one of -AtLeast, -AtMost or -Equals.'
    }

    $bestLen = 0; $bestStartT = $null; $bestEndT = $null
    $curLen  = 0; $curStartT  = $null

    foreach ($row in $Report.Rows) {
        $v  = Get-ArmNumericCell $row $ColumnName
        $t  = Get-ArmNumericCell $row 't (s)'
        $ok = $false
        if ($null -ne $v) {
            $ok = $true
            if ($null -ne $AtLeast -and -not ($v -ge $AtLeast)) { $ok = $false }
            if ($null -ne $AtMost  -and -not ($v -le $AtMost))  { $ok = $false }
            if ($null -ne $Equals  -and -not ($v -eq $Equals))  { $ok = $false }
        }
        if ($ok) {
            if ($curLen -eq 0) { $curStartT = $t }
            $curLen++
            if ($curLen -gt $bestLen) { $bestLen = $curLen; $bestStartT = $curStartT; $bestEndT = $t }
        } else {
            $curLen = 0; $curStartT = $null
        }
    }

    [PSCustomObject]@{ Column = $ColumnName; Length = $bestLen; StartT = $bestStartT; EndT = $bestEndT }
}

# ---------------------------------------------------------------------------------------
# Main -- only when invoked directly, never when dot-sourced as a library (read-arm.ps1
# does ". ./scripts/parse-arm.ps1" to reuse the functions above without this printing).
# ---------------------------------------------------------------------------------------

if ($MyInvocation.InvocationName -ne '.') {

    if (-not $Path) { throw '-Path is required.' }
    $report = Read-ArmReport -Path $Path

    Write-Host $report.Title
    Write-Host $report.SettingsLine
    Write-Host ''
    Write-Host "Metadata tokens ($($report.Metadata.Count)):"
    foreach ($tok in $report.Metadata) { Write-Host "  $tok" }

    Write-Host ''
    Write-Host "$($report.Rows.Count) sample rows. Columns ($($report.ColumnNames.Count)): $($report.ColumnNames -join ', ')"

    $cols = if ($Column -and $Column.Count -gt 0) { $Column } else { $report.ColumnNames }
    Write-Host ''
    Write-Host 'Per-column trough / peak / last:'
    $summaries = foreach ($c in $cols) { Get-ArmColumnSummary -Report $report -ColumnName $c }
    $summaries | Where-Object { $_.Count -gt 0 } | Format-Table -AutoSize Column, Count, Trough, Peak, Last | Out-Host

    if ($StreakColumn) {
        $streak = Get-ArmStreak -Report $report -ColumnName $StreakColumn -AtLeast $StreakAtLeast -AtMost $StreakAtMost -Equals $StreakEquals
        Write-Host ''
        if ($streak.Length -gt 0) {
            Write-Host "Longest streak on '$StreakColumn': $($streak.Length) samples, t=$($streak.StartT) to t=$($streak.EndT)"
        } else {
            Write-Host "Longest streak on '$StreakColumn': none"
        }
    }

    Write-Host ''
    Write-Host 'Footer:'
    if ($report.Footer.Raw) {
        Write-Host "  ended: $($report.Footer.EndedReason)"
        Write-Host "  physics steps: $($report.Footer.PhysicsSteps)  sim seconds: $($report.Footer.SimSeconds)  births: $($report.Footer.Births)"
        Write-Host "  wall clock: $($report.Footer.WallClockMinutes) min ($($report.Footer.RealTimeMultiple)x real time)"
        Write-Host "  fastest: $($report.Footer.FastestSpeedMps) m/s at t=$($report.Footer.FastestSpeedAtT) s"
        Write-Host "  genomes: $($report.Footer.GenomesPath)"
    } else {
        Write-Host '  (no footer found -- run may be in progress)'
    }
}
