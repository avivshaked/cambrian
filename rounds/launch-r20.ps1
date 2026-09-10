# The queue (logbook/0056, D072): the reference world (round 18's, exudation 0.15, sinks 0.002)
# at the 0.02 screening step, 20,000 s, with EVOSIM_CONCEPTION_ORDER shuffled. ASCII only.
#   ./rounds/launch-r20.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>          # r20q-s1
#   ./rounds/launch-r20.ps1 -Seed 4 -Worker 4 -Order age -ExpectSimHash <hash> # r20q0-s4: the control
# Verify the header: 'conception shuffled' (control: 'conception age'), 'dt=0.02', 'exudation 0.15',
# 'sink 0.002 m/s, matter 0.002 m/s', 'clearance 10', 'vent off'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [ValidateSet('shuffled','age')][string]$Order = 'shuffled',
    [int]$Seconds = 20000,
    [string]$ExpectSimHash = '',
    # Overrides the derived arm name. Added for the divergence replay, which re-runs r20q-s1's
    # exact configuration on a new build and must NOT write over the report it is checked
    # against: run-arm.ps1 names runs/<Name>.md after the arm, so re-launching under the
    # original name would destroy the only copy of the rows being compared.
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
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.02
    EVOSIM_CONCEPTION_ORDER = $Order
}

$name = if ($Name -ne '') { $Name }
        elseif ($Order -eq 'shuffled') { "r20q-s$Seed" }
        else { "r20q0-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 300 -Settings $s @extra
