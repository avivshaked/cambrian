# Round 55's seed 1, the control (logbook/0134): starts when seed 2 or seed 3 ends, so that two seeds run
# at a time within half the machine. The stomachs alone, in the trickle alone, as round 54 drew them:
# rounds/r55-pool-control.tsv at 0.1, the floor's share at 0. Gives up at its deadline.
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r55"
$deadline = (Get-Date).AddHours(16)
while ((Get-Date) -lt $deadline -and (Get-Process -Id 262152 -ErrorAction SilentlyContinue) -and (Get-Process -Id 261276 -ErrorAction SilentlyContinue)) {
    Start-Sleep -Seconds 60
}
if ((Get-Process -Id 262152 -ErrorAction SilentlyContinue) -and (Get-Process -Id 261276 -ErrorAction SilentlyContinue)) {
    Write-Host "deadline passed with both seeds running; seed 1 not started"; return
}
if (Test-Path "$R\scratch\r55-build\HOLD-S1") { Write-Host "HOLD-S1 present; seed 1 not started"; return }
Write-Host "starting seed 1 at $(Get-Date -Format HH:mm:ss)"
$pool = '@' + "$w\rounds\r55-pool-control.tsv".Replace([string][char]92, '/')
& "$w\scripts\run-farm.ps1" r55-s1 -Launcher "$w\rounds\env-r55.ps1" -Seed 1 -Seconds 30000 -Threads 8 -RunsRoot "$R\runs" -WallMinutes 900 -Env @{ EVOSIM_TRICKLE_POOL = $pool; EVOSIM_TRICKLE_POOL_SHARE = 0.1; EVOSIM_FLOOR_POOL_SHARE = 0 }
