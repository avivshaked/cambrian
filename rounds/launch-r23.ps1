# The outflow (logbook/0060, D074's dose correction): the reference world at the 0.02 screening step,
# 20,000 s, influx 0.6/s, burial 0.01/s, matter sink 0.02 m/s; the influx at the surface (vent off) or
# at the vent's base with D067's vent on (0.05 m/s in patch 0 from 60 m, legs 1 m). ASCII only.
#   ./rounds/launch-r23.ps1 -Seed 2 -Worker 2 -At surface -ExpectSimHash <hash>   # r23s-s2
#   ./rounds/launch-r23.ps1 -Seed 2 -Worker 3 -At vent    -ExpectSimHash <hash>   # r23v-s2
# Verify the header: 'sink 0.002 m/s, matter 0.02 m/s', 'matter in 0.6/s at surface|vent, burial 0.01/s',
# 'vent off' or 'vent 0.05 m/s in patch 0 from 60 m, legs 1 m', 'conception age', 'from 1/m3', 'dt=0.02'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [Parameter(Mandatory)][ValidateSet('surface','vent')][string]$At,
    [string]$Influx = '0.6',
    [string]$Burial = '0.01',
    [string]$MatterSink = '0.02',
    [int]$Seconds = 20000,
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
    EVOSIM_DT = 0.02
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = $At
    EVOSIM_MATTER_BURIAL = $Burial
}
if ($At -eq 'vent') {
    $s.EVOSIM_VENT = 0.05; $s.EVOSIM_VENT_PATCH = 0; $s.EVOSIM_VENT_DEPTH = 60; $s.EVOSIM_VENT_LEG = 1
}

$name = if ($Name -ne '') { $Name } elseif ($At -eq 'vent') { "r23v-s$Seed" } else { "r23s-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 420 -Settings $s @extra
