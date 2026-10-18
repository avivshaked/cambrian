# The off-card guard, dot-sourced by every job meant to stay off the card (src/Evosim.Farm.Gpu/CLAUDE.md:
# a job meant to stay off the card proves it). Enter-OffCard hides every CUDA device from this process and all
# it starts (CUDA_VISIBLE_DEVICES=-1), so an accidental path to the card finds none and throws. Wait-OffCard
# waits for a started process with a deadline and, every two seconds, looks in this script's whole process tree
# for a loaded nvcuda module, and in every Evosim.Farm process started since Enter-OffCard (a farm that
# run-farm.ps1 launched detached outlives its parent, so the tree no longer reaches it). Resolve-OffCard gives
# the line each job prints at its end: "nvcuda: loaded by none of this job's processes (...)" or "LOADED by".
$script:OffCardHits = @{}
$script:OffCardSince = Get-Date

function Enter-OffCard {
    $env:CUDA_VISIBLE_DEVICES = '-1'
    $script:OffCardSince = Get-Date
}

# The process tree under a root, from one snapshot of the process table. A parent id is only a number, and
# Windows reuses a dead parent's id, so a snapshot can hold a cycle of parent ids: a walk without a visited
# set then never ends (it spun a controller for half an hour on 2026-10-08). So each id is taken once, and a
# child counts only if it started after its parent. And whatever that logic does, the walk stops after as
# many steps as the snapshot has rows: no tree is larger than the table it was read from. Returns ids.
function Get-ProcessTree([int]$Root, $All = (Get-CimInstance Win32_Process)) {
    $All = @($All)
    $byId = @{}
    foreach ($p in $All) { $byId[[int]$p.ProcessId] = $p }
    $seen = @{ $Root = $true }
    $tree = [System.Collections.Generic.List[int]]::new()
    $tree.Add($Root)
    for ($k = 0; $k -lt $tree.Count -and $k -le $All.Count -and $tree.Count -le $All.Count + 1; $k++) {
        $parent = $byId[$tree[$k]]
        foreach ($c in $All) {
            $id = [int]$c.ProcessId
            if ($c.ParentProcessId -ne $tree[$k] -or $seen.ContainsKey($id)) { continue }
            if ($parent -and $c.CreationDate -lt $parent.CreationDate) { continue }
            $seen[$id] = $true
            $tree.Add($id)
        }
    }
    return , $tree.ToArray()
}

# One look. With a deadline it gives up on the remaining processes once the deadline has passed, so the
# look cannot hold its caller past it.
function Test-OffCardTree([datetime]$Deadline = [datetime]::MaxValue) {
    $tree = Get-ProcessTree $PID
    $farms = @(Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $script:OffCardSince.AddSeconds(-5) } | ForEach-Object { $_.Id })
    foreach ($id in (@($tree | Select-Object -Skip 1) + $farms | Select-Object -Unique)) {
        if ((Get-Date) -ge $Deadline) { return }
        try {
            $p = Get-Process -Id $id -ErrorAction Stop
            if ($p.Modules | Where-Object { $_.ModuleName -match '^nvcuda' }) {
                $script:OffCardHits["$($p.ProcessName) $id"] = $true
            }
        } catch {}
    }
}

# True when the process ended before the deadline; false when it was still running at it.
function Wait-OffCard([System.Diagnostics.Process]$Process, [datetime]$Deadline) {
    while (-not $Process.HasExited) {
        Test-OffCardTree $Deadline
        if ((Get-Date) -ge $Deadline) { return $false }
        Start-Sleep -Seconds 2
    }
    [void]$Process.WaitForExit(10000)
    return $true
}

function Resolve-OffCard {
    $visible = $env:CUDA_VISIBLE_DEVICES
    if ($script:OffCardHits.Count -eq 0) {
        return "nvcuda: loaded by none of this job's processes (CUDA_VISIBLE_DEVICES=$visible)"
    }
    return "nvcuda: LOADED by $(($script:OffCardHits.Keys | Sort-Object) -join ', ') (CUDA_VISIBLE_DEVICES=$visible)"
}
