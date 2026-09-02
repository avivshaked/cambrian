# analyse-arm.ps1 -- read run reports with named columns instead of positional guesses.
#
# The report table has grown columns over time (the 'species' column broke a naive diff,
# and a positional misread once reported float tissue as the food chain - logbook/0044's
# instrument correction). This script parses each report's OWN header row to build the
# name -> index map, so a column is always what its header says it is.
#
# Usage:
#   ./scripts/analyse-arm.ps1 r9-s1 r9-s2              # one status line per arm
#   ./scripts/analyse-arm.ps1 r9-s1 -Timeline           # key columns every 1000 s
#   ./scripts/analyse-arm.ps1 r9-s1 -Timeline -Every 500 -From 11000 -To 15000
#   ./scripts/analyse-arm.ps1 r9-s1 -Timeline -Columns 'depth m','shade %','sun'
#   ./scripts/analyse-arm.ps1 r9-s1 -Header             # print the settings line
#   ./scripts/analyse-arm.ps1 r9-s1 -ListColumns        # print the name -> index map

param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Names,
    [switch]$Timeline,
    [double]$Every = 1000,
    [double]$From = 0,
    [double]$To = [double]::MaxValue,
    [string[]]$Columns = @(),
    [switch]$Header,
    [switch]$ListColumns
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

# Columns shown by default. Named, not positional - resolved per report.
$statusCols   = @('alive', 'births', 'absorpt', 'inherit', 'det deep', 'mat top', 'mat blk', 'refuge J')
$timelineCols = @('alive', 'births', 'absorpt', 'inherit', 'det deep', 'mat top', 'mat blk', 'floor', 'refuge J')

function Get-ColumnMap([string[]]$lines) {
    $headerLine = $lines | Where-Object { $_ -match '^\| *t \(s\)' } | Select-Object -First 1
    if (-not $headerLine) { throw 'no table header row found' }
    $map = @{}
    $i = 0
    foreach ($cell in ($headerLine -split '\|')) {
        $name = ($cell.Trim() -replace '\*', '')
        if ($name -ne '') { $map[$name] = $i }
        $i++
    }
    return $map
}

function Get-Cell([string[]]$fields, [hashtable]$map, [string]$name) {
    if (-not $map.ContainsKey($name)) { return '?' }
    $idx = $map[$name]
    if ($idx -ge $fields.Count) { return '?' }
    return ($fields[$idx].Trim() -replace '\*', '')
}

foreach ($name in $Names) {
    $path = Join-Path $repo "runs/$name.md"
    if (-not (Test-Path $path)) { Write-Output "== $name : no report at $path"; continue }
    $lines = Get-Content $path
    $map = Get-ColumnMap $lines

    if ($ListColumns) {
        Write-Output "== $name columns:"
        $map.GetEnumerator() | Sort-Object Value | ForEach-Object { Write-Output ("  {0,2}  {1}" -f $_.Value, $_.Key) }
        continue
    }
    if ($Header) {
        Write-Output "== $name"
        Write-Output (($lines | Select-String 'configHash' | Select-Object -First 1).Line)
        continue
    }

    $rows = $lines | Where-Object { $_ -match '^\| *\d' }
    $endLine = $lines | Where-Object { $_ -match '^\*\*Ended:' } | Select-Object -First 1
    $ending = if ($endLine) { $endLine } else { 'running' }

    if ($Timeline) {
        $want = if ($Columns.Count -gt 0) { $Columns } else { $timelineCols }
        Write-Output "== $name  ($ending)"
        Write-Output ("  t`t" + ($want -join "`t"))
        $lastPrinted = -1e18
        $prevFields = $null
        foreach ($row in $rows) {
            $fields = $row -split '\|'
            $t = [double](Get-Cell $fields $map 't (s)')
            if ($t -lt $From -or $t -gt $To) { $prevFields = $fields; continue }
            if (($t - $lastPrinted) -ge $Every) {
                $vals = $want | ForEach-Object { Get-Cell $fields $map $_ }
                Write-Output ("  $t`t" + ($vals -join "`t"))
                $lastPrinted = $t
            }
            $prevFields = $fields
        }
        # Always show the final row - the run's last word matters more than the cadence.
        if ($prevFields) {
            $t = [double](Get-Cell $prevFields $map 't (s)')
            if ($t -gt $lastPrinted -and $t -ge $From -and $t -le $To) {
                $vals = $want | ForEach-Object { Get-Cell $prevFields $map $_ }
                Write-Output ("  $t`t" + ($vals -join "`t"))
            }
        }
    }
    else {
        $want = if ($Columns.Count -gt 0) { $Columns } else { $statusCols }
        $last = $rows | Select-Object -Last 1
        if (-not $last) { Write-Output "== $name : no data rows yet"; continue }
        $fields = $last -split '\|'
        $t = Get-Cell $fields $map 't (s)'
        $pairs = $want | ForEach-Object { "$_=$(Get-Cell $fields $map $_)" }
        Write-Output "== $name : t=$t $($pairs -join ' ') | $ending"
    }
}
