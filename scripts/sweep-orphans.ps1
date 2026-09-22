<#
.SYNOPSIS
  Lists, and with -Kill stops, shell loops an agent's session left behind.

.DESCRIPTION
  A background shell loop outlives the session that armed it. On 2026-09-20 dozens of orphaned
  watch loops (Git Bash, one copy per re-arm over four days) opened a Windows Terminal tab for
  every child they forked, took the desktop down and cost round 41e its arms. Run this at the
  start of every session and before arming any watch. It never touches Unity, a queue's pwsh or
  VS Code's integrated shells (bash --init-file).

  An orphan here is a bash.exe whose command line names a script under scratch/ or an
  until/while loop, and the sleep and console helpers that belong to such loops.

  One thing that matches that shape and is not an orphan: the farm out of Unity is launched by a
  shell script under scratch/farm-port/ or scripts/ that starts Evosim.Farm.exe and waits for it,
  and it lives for the length of a run. It is one script that exits, not a loop that re-forks, so
  it costs nothing and killing it would kill an arm. The exclusion is deliberately narrow — the
  command line must name Evosim.Farm.exe, or a .sh under one of those two directories, and must
  carry no loop of its own — and excluded processes are printed rather than hidden.

.EXAMPLE
  ./scripts/sweep-orphans.ps1          # list
  ./scripts/sweep-orphans.ps1 -Kill    # stop them, three passes
#>
param([switch]$Kill)

function Test-FarmLauncher($p) {
    $c = $p.CommandLine
    if (-not $c) { return $false }
    # A loop in the command line is a loop whoever wrote it, and it forks per iteration. That is
    # the thing this script exists for, so it is never excused by where the script sits.
    if ($c -match 'while true|while :|until |for \(\(') { return $false }
    return ($c -match 'Evosim\.Farm\.exe') -or
           ($c -match '(scratch[\\/]farm-port|scripts)[\\/][^\s"'']*\.sh')
}

function Get-Candidates {
    # Younger than three minutes is the caller's own shell, or a command still running.
    $cutoff = (Get-Date).AddMinutes(-3)
    Get-CimInstance Win32_Process | Where-Object { $_.CreationDate -lt $cutoff } | Where-Object {
        ($_.Name -eq 'bash.exe' -and $_.CommandLine -notmatch '--init-file' -and
            $_.CommandLine -match 'scratch/|while true|until ') -or
        ($_.Name -in @('sleep.exe', 'cygwin-console-helper.exe'))
    }
}

function Get-Orphans {
    Get-Candidates | Where-Object { -not (Test-FarmLauncher $_) }
}

@(Get-Candidates | Where-Object { Test-FarmLauncher $_ }) | ForEach-Object {
    $c = $_.CommandLine
    '  {0,6} {1:HH:mm dd/MM}  {2}   <- farm launcher, not an orphan' -f `
        $_.ProcessId, $_.CreationDate, $c.Substring([Math]::Max(0, $c.Length - 90))
}

$found = @(Get-Orphans)
if ($found.Count -eq 0) { Write-Output 'no orphaned loops'; exit 0 }
$found | Group-Object Name | ForEach-Object { '{0,-28} {1}' -f $_.Name, $_.Count }
$found | Where-Object Name -eq 'bash.exe' | ForEach-Object {
    $c = $_.CommandLine; '  {0,6} {1:HH:mm dd/MM}  {2}' -f $_.ProcessId, $_.CreationDate, $c.Substring([Math]::Max(0, $c.Length - 110))
}
if (-not $Kill) { Write-Output 'listed only; pass -Kill to stop them'; exit 0 }
for ($i = 0; $i -lt 3; $i++) {
    foreach ($p in @(Get-Orphans)) { try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop } catch {} }
    Start-Sleep -Seconds 2
}
'{0} left after the sweep' -f @(Get-Orphans).Count
