# Round 53's founding screens (D132): four 10,000 s runs of seed 1 on round 52's world
# (rounds/env-r52.ps1), each with one change, launched together at four threads each so the four
# hold sixteen processors, half the machine. Run from the committed worktree scratch/wt-r53.
# - r53sc-b30: the tank 30 m deep, the bed's tilt 56 m, chosen so the bed shallower than 6 and
#   12 m stays near round 52's 4.7% and 10.0% (3.2% and 12.1%); the matter held at 15,000.
# - r53sc-b25: 25 m deep, tilt 41 m (1.1% and 12.5%); the matter held.
# - r53sc-f27: 45 m, the matter budget 27,000 units, 1.8 times round 52's, the density a 25 m tank
#   would have.
# - r53sc-a005: 45 m, a settled corpse decays at 0.005 /s (D132's measurement of the rule it set
#   aside).
# Founders' depth, the islands' depth and the reefs' caps stay in metres from the surface, since
# the light is. Read with scripts/reads/r53-screens.py.
param([int]$Threads = 4, [int]$Seconds = 10000)
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-r53"
$screens = [ordered]@{
    'r53sc-b30'  = @{ EVOSIM_DEPTH = 30; EVOSIM_BED_TILT = 56 }
    'r53sc-b25'  = @{ EVOSIM_DEPTH = 25; EVOSIM_BED_TILT = 41 }
    'r53sc-f27'  = @{ EVOSIM_MATTER_BUDGET = 27000 }
    'r53sc-a005' = @{ EVOSIM_CORPSE_DECAY_SETTLED = 0.005 }
}
Set-Location $w
foreach ($arm in $screens.Keys) {
    $env = $screens[$arm]; $env['EVOSIM_CHECKPOINT_EVERY'] = 1000
    & "$w\scripts\run-farm.ps1" $arm -Seed 1 -Seconds $Seconds -Threads $Threads -Launcher rounds/env-r52.ps1 `
        -RunsRoot "$main\runs" -WallMinutes 300 -Env $env | Select-String 'launched|configHash|refus|error'
}
Set-Location $main
