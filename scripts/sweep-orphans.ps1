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

.EXAMPLE
  ./scripts/sweep-orphans.ps1          # list
  ./scripts/sweep-orphans.ps1 -Kill    # stop them, three passes
#>
param([switch]$Kill)

function Get-Orphans {
    # Younger than three minutes is the caller's own shell, or a command still running.
    $cutoff = (Get-Date).AddMinutes(-3)
    Get-CimInstance Win32_Process | Where-Object { $_.CreationDate -lt $cutoff } | Where-Object {
        ($_.Name -eq 'bash.exe' -and $_.CommandLine -notmatch '--init-file' -and
            $_.CommandLine -match 'scratch/|while true|until ') -or
        ($_.Name -in @('sleep.exe', 'cygwin-console-helper.exe'))
    }
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
