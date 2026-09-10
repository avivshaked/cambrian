# The coarse-step validation launcher (logbook/0052). ASCII only.
#   ./rounds/launch-r16.ps1 -Dt 0.05 -Worker 7     # r16dt-05: r13a-s2's world (seed 2, clearance 1) at dt 0.05, 15,000 s
#   ./rounds/launch-r16.ps1 -Dt 0.01 -Worker 4     # r16dt-01: the replay-noise reference
# Verify the header after launch: 'dt=0.05', 'metabolic step 0.5 s', configHash identical to r13a-s2's (1c50cf520f962587).
param(
    [Parameter(Mandatory)][ValidateSet('0.01','0.02','0.05')][string]$Dt,
    [Parameter(Mandatory)][int]$Worker,
    [string]$Suffix = '',         # e.g. 'b' for the rerun with the drag limiter: r16dt-05b
    [int]$Seconds = 15000
)

$s = @{
    EVOSIM_SEED = 2
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 100; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_CLEARANCE = 1; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    EVOSIM_DT = $Dt
}

$name = "r16dt-" + $Dt.Replace('0.', '') + $Suffix
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed 2 -Seconds $Seconds -WallMinutes 300 -Settings $s
