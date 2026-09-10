# Round 32 (fable-propose-grid.md, D082): round 31's world with the water as a grid of cubic
# cells instead of vertices. Everything else is launch-r31.ps1 verbatim. ASCII only.
#   ./rounds/launch-r32.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>                       # r32-s1
#   ./rounds/launch-r32.ps1 -Seed 1 -Worker 4 -Seconds 300 -Name r32smoke -Dt 0.02       # a compile check
#   ./rounds/launch-r32.ps1 -Seed 1 -Worker 4 -Seconds 300 -Name r32smoke -Dt 0.02 -CorpseDecay 0.005
# Verify the header: everything launch-r31.ps1 lists, plus 'field grid' with 'cell=1 mcell=5',
# 'mixing 0.02 m2/s', 'h-mix 0.02 m2/s', 'corpse 0/s' and 'neuron 0.005 W + 0.001 W/input, work x0.25'.
# The vertex tokens (h, mh, merge, cap, q) still print and mean nothing in a grid world.
# h-mix equals mixing on a grid by World's own rule: see the -HMix parameter's note below.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seconds = 30000,
    [float]$WallMinutes = 1200,
    [string]$ExpectSimHash = '',
    [string]$Name = '',
    [int]$DigestEvery = 0,
    [float]$AddedMass = 0.5,
    [float]$Dt = 0.01,
    [string]$Field = 'grid',
    [float]$Kernel = 1,
    [float]$MatterKernel = 1.8,
    [float]$Merge = 0.25,
    [int]$Cap = 100000,
    [float]$Quantum = 0.125,
    # The detritus grid's cell, m. One metre is the vertex kernel's support, so a mouth gets
    # about the reach it had in round 31 and the round reads the representation, not the scale.
    [float]$Cell = 1,
    # The matter grid's cell, m. Coarser because a child costs 8 to 16 units of matter and seeded
    # water holds about one per cubic metre, so a 1 m cell could never afford a conception. The
    # RunConfig default is 3 m, which does not divide this campaign's box (100 m2 over 4 patches
    # and 60 m deep is 20 m by 5 m by 60 m) and GridField refuses it; 2.5 m divides all three and
    # holds 15.6 m3, the nearest divisor to the old cell's volume.
    [float]$MatterCell = 5,
    [float]$NeuronCost = 0.005,
    [float]$ConnectionCost = 0.001,
    [float]$WorkCost = 0.25,
    # Sideways diffusivity of the detritus grid, m2/s. A grid mixes at one rate on all six faces,
    # so World refuses a grid world whose h-mix differs from EVOSIM_MIXING (the review of
    # 2026-09-08); the matter grid stirs on every axis at its own MatterMixingDiffusivity (2 m2/s,
    # not launchable) where round 31 stirred it sideways at 0.02. Header token 'h-mix' reads the
    # detritus rate.
    [float]$HMix = 0.02,
    [float]$Mixing = 0.02,
    # Rule 6 of fable-propose-grid.md: a death founds a particle that sinks, rides the current and
    # leaks this fraction of what it still holds into the water each second. 0 is off and is what
    # every arm on record ran; 0.005 is the proposal's rate, a half-life near 139 s. Header token
    # 'corpse'; report column 'corpses'.
    [float]$CorpseDecay = 0.005
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = $Mixing; EVOSIM_REMIN = 0
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
    EVOSIM_DT = $Dt
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
    EVOSIM_SENSE_CHEMICAL = 1; EVOSIM_SENSE_ENERGY = 1; EVOSIM_SENSE_FLOW = 1
    EVOSIM_ADDED_MASS = $AddedMass
    # D083: the water as vertices. fable-propose-grid.md: the water as a grid.
    EVOSIM_FIELD = $Field; EVOSIM_FIELD_KERNEL = $Kernel; EVOSIM_FIELD_MATTER_KERNEL = $MatterKernel; EVOSIM_FIELD_MERGE = $Merge
    EVOSIM_FIELD_CAP = $Cap; EVOSIM_FIELD_QUANTUM = $Quantum
    EVOSIM_FIELD_CELL = $Cell; EVOSIM_FIELD_MATTER_CELL = $MatterCell
    # D082: the price of a bud.
    EVOSIM_NEURON_COST = $NeuronCost; EVOSIM_CONNECTION_COST = $ConnectionCost; EVOSIM_WORK_COST = $WorkCost
    EVOSIM_H_MIXING = $HMix
    EVOSIM_CORPSE_DECAY = $CorpseDecay
}

if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }

$name = if ($Name -ne '') { $Name } else { "r32-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
