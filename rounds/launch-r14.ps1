# Round 14 launcher (logbook/0050). ASCII only, so it parses in both PowerShell editions.
#   ./rounds/launch-r14.ps1 -Clearance 5 -Seed 1 -Worker 2      # arm c5:  round 13 arm A + EVOSIM_CLEARANCE 5
#   ./rounds/launch-r14.ps1 -Clearance 10 -Seed 1 -Worker 3     # arm c10: ... + EVOSIM_CLEARANCE 10
#   ./rounds/launch-r14.ps1 -Clearance 10 -Seed 1 -Worker 4 -Dt 0.02 -Suffix -flux -Seconds 15000
#       # the detritus-flux instrument's first reading: r14c10-s1's world at the screening step
# Verify the header after launch: the token 'clearance 5' / 'clearance 10', 'sink 0.002 m/s, matter 0.002 m/s', 'vent off',
# and 'dt=0.02' when -Dt is given.
param(
    [Parameter(Mandatory)][ValidateSet(5,10)][int]$Clearance,
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [ValidateSet('0.01','0.02')][string]$Dt = '0.01',
    [string]$Suffix = '',
    [int]$Seconds = 30000
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
    # round 13 arm A: marine snow, both fields; no vent
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    # round 14: the stomach's gearing (D068)
    EVOSIM_CLEARANCE = $Clearance
}
if ($Dt -ne '0.01') { $s.EVOSIM_DT = $Dt }

$name = "r14c$Clearance-s$Seed$Suffix"
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 600 -Settings $s
