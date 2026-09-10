# Round 13 launcher (logbook/0049). ASCII only, so it parses in both PowerShell editions.
#   ./rounds/launch-r13.ps1 -Arm a -Seed 1 -Worker 2      # arm A: sink 0.002
#   ./rounds/launch-r13.ps1 -Arm b -Seed 1 -Worker 3      # arm B: sink 0.002 + vent 0.05
# Verify the header after launch: ./scripts/analyse-arm.ps1 r13a-s1 -Header
param(
    [Parameter(Mandatory)][ValidateSet('a','b')][string]$Arm,
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 100; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_CLEARANCE = 1; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    # round 13: marine snow, both fields
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
}
if ($Arm -eq 'b') {
    $s.EVOSIM_VENT = 0.05; $s.EVOSIM_VENT_PATCH = 0; $s.EVOSIM_VENT_DEPTH = 60; $s.EVOSIM_VENT_LEG = 1
}

$name = "r13$Arm-s$Seed"
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds 30000 -WallMinutes 600 -Settings $s
