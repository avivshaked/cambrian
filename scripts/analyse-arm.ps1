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
#   ./scripts/analyse-arm.ps1 r42farm2-s1 -RunsRoot scratch/farm-port/runs

param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Names,
    [switch]$Timeline,
    [double]$Every = 1000,
    [double]$From = 0,
    [double]$To = [double]::MaxValue,
    [string[]]$Columns = @(),
    [switch]$Header,
    [switch]$ListColumns,
    # Where the reports are. Default runs/ under the repository, which is where both farms write.
    [string]$RunsRoot = 'runs'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runsRootPath = $RunsRoot
if (-not [System.IO.Path]::IsPathRooted($runsRootPath)) { $runsRootPath = Join-Path $repo $RunsRoot }

# The contact instrument's four columns under two names. The farm out of Unity counts overlapping
# bounding spheres between creatures where PhysX counted contact manifolds between colliders, so
# the columns are named for what they count -- but a script, a round read or a habit that asks for
# the old name should get the number rather than a '?'. Asked either way, answered either way, and
# the report says once which name it actually read. The numbers are a different census and do not
# compare across the change; this resolves a name, not a measurement.
$columnAliases = @{
    'contacts'   = 'overlaps';   'overlaps'  = 'contacts'
    'pairs/body' = 'ovl/body';   'ovl/body'  = 'pairs/body'
    'pairs jnt %' = 'ovl jnt %'; 'ovl jnt %' = 'pairs jnt %'
    'stuck %'    = 'ovl held %'; 'ovl held %' = 'stuck %'
}
$script:aliasNotes = @{}

# Columns shown by default. Named, not positional - resolved per report.
# 'mat blk' became 'upt lim' with D098: there is no conception the world refuses for want of
# matter any more, and what a reader wants from a producer is whether its fixation was bound
# by the water or by the light.
$statusCols   = @('alive', 'births', 'absorpt', 'inherit', 'det deep', 'mat top', 'upt lim', 'refuge J')
$timelineCols = @('alive', 'births', 'absorpt', 'inherit', 'det deep', 'mat top', 'upt lim', 'floor', 'refuge J')

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
    $key = $name
    if (-not $map.ContainsKey($key)) {
        if ($columnAliases.ContainsKey($name) -and $map.ContainsKey($columnAliases[$name])) {
            $key = $columnAliases[$name]
            $script:aliasNotes[$name] = $key
        }
        else { return '?' }
    }
    $idx = $map[$key]
    if ($idx -ge $fields.Count) { return '?' }
    return ($fields[$idx].Trim() -replace '\*', '')
}

foreach ($name in $Names) {
    $script:aliasNotes = @{}
    $path = Join-Path $runsRootPath "$name.md"
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

    # Said once, and after the numbers: a reader who asked for 'contacts' and got 'overlaps' is
    # reading a different census, and a silent substitution is how a name change becomes invisible.
    if ($script:aliasNotes.Count -gt 0) {
        $said = ($script:aliasNotes.GetEnumerator() | Sort-Object Key |
            ForEach-Object { "$($_.Key) read as $($_.Value)" }) -join '; '
        Write-Output "   (this report names them differently: $said -- a different census, not the same number)"
    }
}
