# Energy buys matter (logbook/0057, D073): the reference world at the 0.02 screening step, 20,000 s.
#   ./rounds/launch-r21.ps1 -Seed 2 -Worker 2 -Arm b -ExpectSimHash <hash>   # r21b-s2: conception reserve
#   ./rounds/launch-r21.ps1 -Seed 2 -Worker 3 -Arm a -ExpectSimHash <hash>   # r21a-s2: age order, matter 3/m3
# Verify the header: B 'conception reserve' and 'from 1/m3'; A 'conception age' and 'from 3/m3';
# both 'dt=0.02', 'exudation 0.15', 'sink 0.002 m/s, matter 0.002 m/s', 'clearance 10'. ASCII only.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [Parameter(Mandatory)][ValidateSet('a','b')][string]$Arm,
    [int]$Seconds = 20000,
    [string]$ExpectSimHash = ''
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 100; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.02
}
if ($Arm -eq 'b') {
    $s.EVOSIM_MATTER_INITIAL = 1; $s.EVOSIM_CONCEPTION_ORDER = 'reserve'
} else {
    $s.EVOSIM_MATTER_INITIAL = 3; $s.EVOSIM_CONCEPTION_ORDER = 'age'
}

$name = "r21$Arm-s$Seed"
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 420 -Settings $s @extra
