# The footprint world confirmed at the fine step (logbook/0067): dt 0.01, 30,000 s, five seeds. The vent-shape
# open world in the shared box (4 x 10 x 10 m, 60 m deep, area 400), restoring surface, real floor, influx
# 0.3/s at the vent's base, burial 0.01/s, matter sink 0.02, starting stock 0.25/m3. ASCII only.
#   ./rounds/launch-r26.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>   # r26-s1
# Verify the header: 'dt=0.01', 'space shared 4x10x10 m, depth 60, wrap', 'surface restore 1', 'area 400',
# 'matter in 0.3/s at vent, burial 0.01/s', 'from 0.25/m3', the vent tokens, the floor token if the build adds one.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [string]$Influx = '0.3',
    [string]$MatterInitial = '0.25',
    [int]$Seconds = 30000,
    [float]$WallMinutes = 2400,
    [string]$ExpectSimHash = '',
    [string]$Name = ''
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 400; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = $MatterInitial; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.02
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.01
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = 'vent'
    EVOSIM_MATTER_BURIAL = 0.01
    EVOSIM_VENT = 0.05; EVOSIM_VENT_PATCH = 0; EVOSIM_VENT_DEPTH = 60; EVOSIM_VENT_LEG = 1
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
}

$name = if ($Name -ne '') { $Name } else { "r26-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
