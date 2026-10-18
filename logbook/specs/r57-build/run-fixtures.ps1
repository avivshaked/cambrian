# Round 57's fixtures and suites (the build agent's fixtures.ps1 in scratch/wt-r57), pinned to the
# efficiency cores 16 to 31; every child (dotnet, the farm) inherits the affinity. Log in scratch/logs.
$R = 'D:\Projects\experiments\evolution-simulator'
(Get-Process -Id $PID).ProcessorAffinity = [IntPtr][int64]4294901760
& "$R\scratch\wt-r57\logbook\specs\r57-build\fixtures.ps1" *>&1 | Out-File -Encoding utf8 "$R\scratch\logs\r57-fixtures.log"
