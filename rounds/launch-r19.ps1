# The matter screen (logbook/0055, D071): round 18's world with the matter sink decoupled from
# the detritus sink - EVOSIM_MATTER_SINK back to 0.02 m/s, EVOSIM_SINK held at 0.002 - at the
# 0.02 screening step, 20,000 s. ASCII only.
#   ./rounds/launch-r19.ps1 -Seed 1 -Worker 2                  # r19m-s1
#   ./rounds/launch-r19.ps1 -Seed 1 -Worker 3 -MatterSink 0.002  # r19m0-s1: the control (round 18's sinks) at 0.02
# Verify the header: 'exudation 0.15', 'dt=0.02', 'clearance 10', 'sink 0.002 m/s, matter 0.02 m/s'
# (control: 'matter 0.002 m/s'), 'vent off'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [ValidateSet('0.02','0.002')][string]$MatterSink = '0.02',
    [int]$Seconds = 20000,
    [string]$ExpectSimHash = ''
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
}

$name = if ($MatterSink -eq '0.02') { "r19m-s$Seed" } else { "r19m0-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 300 -Settings $s @extra
