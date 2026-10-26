# Round 62's read at -End seconds (D215; the pre-registration is logbook/specs/r62-prereg/predictions.md): every reader
# on the six worlds, each table to its own file in -Out. Refuses to start unless every world's stats reach -End, so a
# read is never taken on a world still short of it. Pinned to -Mask (default logical 4-7, the speed agent's lane) at
# BelowNormal, one reader at a time. Reads the run files only.
#   pwsh -NoProfile -File scratch/wt-r62launch/scripts/reads/r62-read.ps1
# What answers which of D215's tests:
#   1 born share at 12,000 and -End     r62-health.txt (part 1)
#   1 best cohort's replacement         r62g-cohorts.txt (mouthed kids per body per 3,000 s cohort)
#   1 deepest mouthed chain             r62g-read.txt (chain)
#   2 grazers alive at each season end  r62g-seasons.txt (g end, per half-year)
#   2 upkeep met at summer's start      r62g-windows.txt (the 500 s windows from 250 s before each summer)
#   3 own-kill share, 3,000 s bins      r62-health.txt (part 2, a proxy: see its header)
# The pre-registration's oases, crowd and hybrids are r60-read.txt and r61-grazer-economy-<world>.txt.
param(
    [int]$End = 24000,
    [string]$Out = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r62launch\logbook\specs\r62-read',
    [string]$RunsRoot = 'D:\Projects\experiments\evolution-simulator\runs',
    [int64]$Mask = 240,
    # The worlds, a comma list as one string (an array does not bind through -File); round 62's six by default.
    [string]$Worlds = 'r62-n6201,r62-h6201,r62-n6202,r62-h6202,r62-n6203,r62-h6203')
$ErrorActionPreference = 'Stop'
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
$reads = $PSScriptRoot
$list = @($Worlds -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$arms = @($list | ForEach-Object { Join-Path $RunsRoot $_ })

# Every world's last stats row must have reached -End.
foreach ($a in $arms) {
    $run = Get-ChildItem $a -Directory | Sort-Object Name | Select-Object -Last 1
    $stats = Join-Path $run.FullName 'stats.jsonl'
    $last = Get-Content $stats -Tail 1 | ConvertFrom-Json
    if ($last.t -lt $End) { throw "$($run.FullName) reaches $($last.t) s, short of $End s; read later." }
}

New-Item -ItemType Directory -Force $Out | Out-Null
function Read([string]$name, [string[]]$argv) {
    $file = Join-Path $Out "$name.txt"
    $started = Get-Date
    & python -I @argv *> $file
    $code = $LASTEXITCODE
    "$(Get-Date -Format s) $name exit $code, $([int]((Get-Date) - $started).TotalSeconds) s" | Add-Content (Join-Path $Out 'read.log')
    if ($code -ne 0) { Write-Warning "$name exited $code; see $file" }
}

"$(Get-Date -Format s) round 62 read to $End s begins, mask $Mask" | Set-Content (Join-Path $Out 'read.log')
Read 'r62-health' (@("$reads\r62-health.py") + $arms + @('--end', "$End", '--at', "12000,$End"))
Read 'r62g-read' (@("$reads\r62g-read.py") + $arms + @('--end', "$End"))
Read 'r62g-cohorts' (@("$reads\r62g-cohorts.py") + $arms + @('--end', "$End"))
Read 'r62g-seasons' (@("$reads\r62g-seasons.py") + $arms + @('--end', "$End"))
Read 'r62g-windows' (@("$reads\r62g-windows.py") + $arms + @('--end', "$End"))
Read 'r60-read' (@("$reads\r60-read.py") + $arms + @('--end', "$End", '--year', '6000'))
foreach ($i in 0..($list.Count - 1)) {
    Read "r61-grazer-economy-$($list[$i])" @("$reads\r61-grazer-economy.py", $arms[$i], '--end', "$End", '--year', '6000')
}
"$(Get-Date -Format s) read ends" | Add-Content (Join-Path $Out 'read.log')
