# Round 15 launcher: the invasion assay (logbook/0051). ASCII only.
#   ./rounds/launch-r15.ps1 -Clearance 10 -Worker 6     # r15i-c10: round 13 arm A world, seed 2, clearance 10,
#                                                        #   50 copies of the r13a-s2 stomach inoculated at t=5000, -12 m
#   ./rounds/launch-r15.ps1 -Clearance 10 -Worker 7 -Exudation 0.15 -Dt 0.02 -Seconds 20000
#                                                        # r15i-c10-x15: the assay in the leak world (0051 amendment 2)
# Verify the header after launch: 'clearance N', 'sink 0.002 m/s, matter 0.002 m/s', 'vent off', the inoculum hash 342f472a7dda,
# and 'exudation 0.15' / 'dt=0.02' when given.
param(
    [Parameter(Mandatory)][ValidateSet(1,5,10)][int]$Clearance,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seed = 2,
    [int]$Seconds = 12000,
    [string]$Exudation = '0',
    [ValidateSet('0.01','0.02')][string]$Dt = '0.01'
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
    EVOSIM_CLEARANCE = $Clearance
    # the assay
    EVOSIM_INOCULATE = "$PSScriptRoot\..\inocula\inoculum-r13a-s2-t17000.json"
    EVOSIM_INOCULATE_AT = 5000; EVOSIM_INOCULATE_COUNT = 50; EVOSIM_INOCULATE_DEPTH = 12
}
if ($Exudation -ne '0') { $s.EVOSIM_EXUDATION = $Exudation }
if ($Dt -ne '0.01') { $s.EVOSIM_DT = $Dt }

$name = "r15i-c$Clearance"
if ($Seed -ne 2) { $name = "$name-s$Seed" }
if ($Exudation -ne '0') { $name = "$name-x" + $Exudation.Replace('0.', '') }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes 300 -Settings $s
