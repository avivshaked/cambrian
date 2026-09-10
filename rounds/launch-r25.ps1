# The first shared water (logbook/0065, D077's screen): the vent-shape open world in the footprint
# world at dt 0.02, 20,000 s. Shared space, restoring top and bottom, area 400 (4 x 10 x 10 m), influx
# at the vent's base, burial 0.01/s, matter sink 0.02, D067's vent on. ASCII only.
#   ./rounds/launch-r25.ps1 -Seed 2 -Worker 2 -ExpectSimHash <hash>                          # r25-s2
#   ./rounds/launch-r25.ps1 -Seed 2 -Worker 4 -Influx 0.3 -ExpectSimHash <hash>              # r25h-s2
#   ./rounds/launch-r25.ps1 -Seed 2 -Worker 6 -MatterInitial 0.25 -Name r25q-s2 -ExpectSimHash <hash>
# Verify the header: 'dt=0.02', 'space shared 4x10x10 m, depth 60, wrap', 'surface restore 1', 'area 400',
# 'matter in 0.6/s at vent, burial 0.01/s' (or 0.3/s), 'from 1/m3' (or 0.25/m3), the vent tokens.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [string]$Influx = '0.6',
    [string]$MatterInitial = '1',
    [string]$Burial = '0.01',
    [string]$MatterSink = '0.02',
    [string]$Area = '400',
    [int]$Seconds = 20000,
    [float]$WallMinutes = 600,
    [string]$ExpectSimHash = '',
    [string]$Name = ''
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = $Area; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = $MatterInitial; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = $MatterSink
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.02
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = 'vent'
    EVOSIM_MATTER_BURIAL = $Burial
    EVOSIM_VENT = 0.05; EVOSIM_VENT_PATCH = 0; EVOSIM_VENT_DEPTH = 60; EVOSIM_VENT_LEG = 1
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
}

$name = if ($Name -ne '') { $Name } elseif ($Influx -eq '0.3') { "r25h-s$Seed" } else { "r25-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
