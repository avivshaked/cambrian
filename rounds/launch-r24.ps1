# The open world at the fine step (logbook/0061): D074 adopted in the vent shape, confirmed at dt 0.01,
# 30,000 s, five seeds, under D063 as amended. Reference world + influx 0.6/s at the vent's base, burial
# 0.01/s, matter sink 0.02 m/s, D067's vent on (0.05 m/s in patch 0 from 60 m, legs 1 m). ASCII only.
#   ./rounds/launch-r24.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>   # r24-s1
# Verify the header: 'dt=0.01', 'sink 0.002 m/s, matter 0.02 m/s', 'matter in 0.6/s at vent, burial 0.01/s',
# 'vent 0.05 m/s in patch 0 from 60 m, legs 1 m', 'conception age', 'from 1/m3', 'exudation 0.15'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [string]$Influx = '0.6',
    [string]$Burial = '0.01',
    [string]$MatterSink = '0.02',
    [int]$Seconds = 30000,
    [float]$WallMinutes = 1800,
    [string]$ExpectSimHash = '',
    [string]$Name = ''
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
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = $MatterSink
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.01
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = 'vent'
    EVOSIM_MATTER_BURIAL = $Burial
    EVOSIM_VENT = 0.05; EVOSIM_VENT_PATCH = 0; EVOSIM_VENT_DEPTH = 60; EVOSIM_VENT_LEG = 1
}

$name = if ($Name -ne '') { $Name } else { "r24-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
