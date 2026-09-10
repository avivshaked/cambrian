# Round 28 (logbook/0070, D079): round 18's closed world with one change - shared space
# (D077's box, wrap, placement, restoring top, real floor) - under D078's single-threaded
# physics. Everything else is launch-r18.ps1 verbatim. ASCII only.
#   ./rounds/launch-r28.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash> -DigestEvery 100     # r28-s1
#   ./rounds/launch-r28.ps1 -Seed 1 -Worker 7 -ExpectSimHash <hash> -DigestEvery 100 -Seconds 10000 -Name r28p-s1
# Verify the header: 'dt=0.01', 'vent off', 'sink 0.002 m/s, matter 0.002 m/s', 'area 100',
# 'from 1/m3', 'exudation 0.15', 'clearance 10', 'space shared 4x5x5 m, depth 60, wrap, bed',
# 'surface restore 1', 'physics jobs 0'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seconds = 30000,
    [float]$WallMinutes = 1200,
    [string]$ExpectSimHash = '',
    [string]$Name = '',
    [int]$DigestEvery = 0
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
    EVOSIM_DT = 0.01
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
}

# The digest (logbook/specs/digest-spec.md) hashes state and changes no per-step term; set on the
# seed-1 arm and its replay probe only, at 100, so M7 has two files to compare.
if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }

$name = if ($Name -ne '') { $Name } else { "r28-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
