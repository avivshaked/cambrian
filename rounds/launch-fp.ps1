# D077's build validation, item 4: the r24 settings at the screening step with the box on, and the
# same settings tiled beside it for the wall clock. 3,000 s, dt 0.02, seed 2. ASCII only.
#   ./rounds/launch-fp.ps1 -Shared 1 -Worker 6 -Name fp-smoke
#   ./rounds/launch-fp.ps1 -Shared 0 -Worker 6 -Name fp-tiled
# Verify the header: 'space shared 4x10x10 m, depth 60, wrap' or 'space tiled 100 m',
# 'surface restore 1', 'area 400 m2', 'dt=0.02'.
param(
    [int]$Shared = 1,
    [int]$Seed = 2,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seconds = 3000,
    [float]$WallMinutes = 240,
    [string]$Restore = '1',
    [string]$Area = '400',
    [string]$ExpectSimHash = '',
    [Parameter(Mandatory)][string]$Name
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = $Area; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.02
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.02
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = 0.6
    EVOSIM_MATTER_INFLUX_AT = 'vent'
    EVOSIM_MATTER_BURIAL = 0.01
    EVOSIM_VENT = 0.05; EVOSIM_VENT_PATCH = 0; EVOSIM_VENT_DEPTH = 60; EVOSIM_VENT_LEG = 1
    EVOSIM_SHARED_SPACE = $Shared
    EVOSIM_SURFACE_RESTORE = $Restore
}

$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $Name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
