# The open budget (logbook/0058, D074): the reference world at the 0.02 screening step, 20,000 s,
# with a surface matter influx and floor burial. ASCII only.
#   ./rounds/launch-r22.ps1 -Seed 2 -Worker 3 -Influx 0.6 -ExpectSimHash <hash>   # r22o-s2
#   ./rounds/launch-r22.ps1 -Seed 2 -Worker 4 -Influx 1.2 -ExpectSimHash <hash>   # r22o2-s2
# Verify the header: 'matter in 0.6/s at surface, burial 0.01/s' (or 1.2/s), 'conception age',
# 'from 1/m3', 'dt=0.02', 'exudation 0.15', 'sink 0.002 m/s, matter 0.002 m/s', 'clearance 10'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [ValidateSet('0.6','1.2')][string]$Influx = '0.6',
    [string]$Burial = '0.01',
    [int]$Seconds = 20000,
    [string]$ExpectSimHash = '',
    [string]$Name = ''   # override the derived name (the burial-dose hedge arm)
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
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.02
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = 'surface'
    EVOSIM_MATTER_BURIAL = $Burial
}

$name = if ($Name -ne '') { $Name } elseif ($Influx -eq '0.6') { "r22o-s$Seed" } else { "r22o2-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 420 -Settings $s @extra
