# Dissect an invasion assay (logbook/0051) from a run's lineage.jsonl.
#   ./scripts/lineage-invasion.ps1 r15i-c10                 # latest run directory under runs/r15i-c10/
#   ./scripts/lineage-invasion.ps1 r15i-c10 -At 12000       # "alive at" instant (default: the last event's time)
#   ./scripts/lineage-invasion.ps1 -Path runs/x/y/lineage.jsonl
# Inoculants are births with k = "i"; the lineage is every birth whose parent chain reaches one.
# Prints: inoculant count and lifetimes, children per inoculant, children per lineage member (R0
# observed), lineage size by generation, expressed-absorptive share, births by time bin, alive at -At.
param(
    [Parameter(Position = 0)][string]$Arm,
    [string]$Path,
    [double]$At = -1,
    [double]$Bin = 1000
)

if (-not $Path) {
    if (-not $Arm) { throw "Give an arm name or -Path." }
    $dir = Get-ChildItem -Directory "$PSScriptRoot/../runs/$Arm" | Sort-Object Name | Select-Object -Last 1
    if (-not $dir) { throw "No run directory under runs/$Arm." }
    $Path = Join-Path $dir.FullName 'lineage.jsonl'
}
if (-not (Test-Path $Path)) { throw "Not found: $Path" }

# Stream with FileShare.ReadWrite so a live run's writer is not disturbed.
$fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
$reader = New-Object System.IO.StreamReader($fs)

$parent = @{}      # id -> parent id
$born = @{}        # id -> birth time
$died = @{}        # id -> death time
$gen = @{}         # id -> generation depth
$abs = @{}         # id -> expressed absorptive
$inoc = New-Object System.Collections.Generic.List[long]
$lastT = 0.0
$rx = [regex]'"e":"(?<e>[bd])","t":(?<t>[0-9.]+),"id":(?<id>\d+)(?:,"p":(?<p>-?\d+),"k":"(?<k>[a-z])","g":(?<g>\d+),"s":\d+,"abs":(?<abs>[01]))?'
while ($null -ne ($line = $reader.ReadLine())) {
    $m = $rx.Match($line)
    if (-not $m.Success) { continue }
    $id = [long]$m.Groups['id'].Value
    $t = [double]$m.Groups['t'].Value
    if ($t -gt $lastT) { $lastT = $t }
    if ($m.Groups['e'].Value -eq 'b') {
        $born[$id] = $t
        $parent[$id] = [long]$m.Groups['p'].Value
        $gen[$id] = [int]$m.Groups['g'].Value
        $abs[$id] = [int]$m.Groups['abs'].Value
        if ($m.Groups['k'].Value -eq 'i') { $inoc.Add($id) }
    } else {
        $died[$id] = $t
    }
}
$reader.Close()
if ($At -lt 0) { $At = $lastT }

if ($inoc.Count -eq 0) { "No inoculation births (k = ""i"") in $Path"; return }

# Membership: walk each birth's parent chain until an inoculant or a non-member is found (memoised).
$member = @{}
foreach ($i in $inoc) { $member[$i] = $true }
$children = @{}
foreach ($id in $born.Keys) {
    if ($member.ContainsKey($id)) { continue }
    $chain = New-Object System.Collections.Generic.List[long]
    $cur = $id
    $isMember = $false
    while ($cur -ge 0 -and $parent.ContainsKey($cur)) {
        if ($member.ContainsKey($cur)) { $isMember = $member[$cur]; break }
        $chain.Add($cur)
        $cur = $parent[$cur]
    }
    foreach ($c in $chain) { $member[$c] = $isMember }
}
$lineage = @($born.Keys | Where-Object { $member[$_] })
foreach ($id in $lineage) {
    $p = $parent[$id]
    if ($p -ge 0 -and $member[$p]) { if (-not $children.ContainsKey($p)) { $children[$p] = 0 }; $children[$p]++ }
}

$inocBirth = ($inoc | ForEach-Object { $born[$_] } | Measure-Object -Minimum).Minimum
$lifetimes = @($inoc | Where-Object { $died.ContainsKey($_) } | ForEach-Object { $died[$_] - $born[$_] })
$inocChildren = @($inoc | ForEach-Object { if ($children.ContainsKey($_)) { $children[$_] } else { 0 } })
$descendants = @($lineage | Where-Object { -not $inoc.Contains([long]$_) })
$doneMembers = @($lineage | Where-Object { $died.ContainsKey($_) -and $died[$_] -le $At })
$doneChildren = @($doneMembers | ForEach-Object { if ($children.ContainsKey($_)) { $children[$_] } else { 0 } })
$alive = @($lineage | Where-Object { $born[$_] -le $At -and (-not $died.ContainsKey($_) -or $died[$_] -gt $At) })
$absShare = if ($descendants.Count -gt 0) { (@($descendants | Where-Object { $abs[$_] -eq 1 }).Count / $descendants.Count) } else { [double]::NaN }

"== $Path"
"inoculated {0} at t={1}; last event t={2}; 'alive at' t={3}" -f $inoc.Count, $inocBirth, $lastT, $At
if ($lifetimes.Count -gt 0) {
    $ls = $lifetimes | Measure-Object -Average -Minimum -Maximum
    "inoculant lifetimes: {0} dead, mean {1:0} s, min {2:0}, max {3:0}; {4} still alive" -f $lifetimes.Count, $ls.Average, $ls.Minimum, $ls.Maximum, ($inoc.Count - $lifetimes.Count)
}
$ic = $inocChildren | Measure-Object -Sum -Average
"children per inoculant: mean {0:0.00} (total {1}; {2} of {3} bred at all)" -f $ic.Average, $ic.Sum, @($inocChildren | Where-Object { $_ -gt 0 }).Count, $inoc.Count
if ($doneMembers.Count -gt 0) {
    $dc = $doneChildren | Measure-Object -Average
    "R0 observed (children per completed lineage member, n={0}): {1:0.00}" -f $doneMembers.Count, $dc.Average
}
"descendants born: {0}; lineage alive at t={1}: {2}; expressed absorptive among descendants: {3:P0}" -f $descendants.Count, $At, $alive.Count, $absShare
"by generation (relative to inoculants):"
$g0 = $gen[$inoc[0]]
$lineage | Group-Object { $gen[$_] - $g0 } | Sort-Object { [int]$_.Name } | ForEach-Object { "  gen +{0}: {1} born" -f $_.Name, $_.Count }
"births by {0} s bin:" -f $Bin
$descendants | Group-Object { [math]::Floor($born[$_] / $Bin) * $Bin } | Sort-Object { [double]$_.Name } | ForEach-Object { "  t {0,6}: {1}" -f $_.Name, $_.Count }
