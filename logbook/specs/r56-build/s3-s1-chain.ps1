# Round 56's seeds 3 and 1 (logbook/0135's launch section): G held on seed 2 at 10,000 s, so each starts
# when a farm slot frees, seed 3 first, so that at most two farm runs share the machine at 8 threads each.
# Seed 1 is the control, with round 55's control overrides. A file scratch/r56-build/HOLD stops whatever
# has not started. Gives up at its deadline.
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r56"
$deadline = (Get-Date).AddHours(16)
$queue = @(
    @{ Arm = 'r56-s3'; Seed = 3; Env = @{} },
    @{ Arm = 'r56-s1'; Seed = 1; Env = @{ EVOSIM_TRICKLE_POOL = '@' + "$w\rounds\r55-pool-control.tsv".Replace([string][char]92, '/'); EVOSIM_TRICKLE_POOL_SHARE = 0.1; EVOSIM_FLOOR_POOL_SHARE = 0 } }
)
foreach ($q in $queue) {
    while ((Get-Date) -lt $deadline -and @(Get-Process Evosim.Farm -ErrorAction SilentlyContinue).Count -ge 2) { Start-Sleep -Seconds 60 }
    if (@(Get-Process Evosim.Farm -ErrorAction SilentlyContinue).Count -ge 2) { Write-Host "deadline passed; $($q.Arm) not started"; return }
    if (Test-Path "$R\scratch\r56-build\HOLD") { Write-Host "HOLD present; $($q.Arm) not started"; return }
    if (git -C $w status --short) { Write-Host "round-56 tree is not clean; $($q.Arm) not started"; return }
    Write-Host "starting $($q.Arm) at $(Get-Date -Format HH:mm:ss)"
    & "$w\scripts\run-farm.ps1" $q.Arm -Launcher "$w\rounds\env-r56.ps1" -Seed $q.Seed -Seconds 30000 -Threads 8 -RunsRoot "$R\runs" -WallMinutes 900 -Env $q.Env
    Start-Sleep -Seconds 90
}
