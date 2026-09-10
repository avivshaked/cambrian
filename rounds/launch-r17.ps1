# The leak (logbook/0053, D070): round 14's clearance-10 world at the 0.02 screening step with
# EVOSIM_EXUDATION. ASCII only.
#   ./rounds/launch-r17.ps1 -Seed 1 -Exudation 0.15 -Worker 4     # r17x-s1
#   ./rounds/launch-r17.ps1 -Seed 2 -Exudation 0.15 -Worker 5     # r17x-s2
#   ./rounds/launch-r17.ps1 -Seed 2 -Exudation 0 -Worker 2        # r17x0-s2, the control
# Verify the header: 'exudation 0.15' (no token on the control), 'dt=0.02', 'clearance 10'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][string]$Exudation,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seconds = 20000
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 100; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    EVOSIM_CLEARANCE = 10
    EVOSIM_DT = '0.02'
}
if ($Exudation -ne '0') { $s.EVOSIM_EXUDATION = $Exudation }

$name = if ($Exudation -eq '0') { "r17x0-s$Seed" } else { "r17x-s$Seed" }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 300 -Settings $s
