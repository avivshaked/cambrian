# Round 37b (D090, logbook/0095): round 37's world, the tank, with the water carrying bodies as water
# does. Two things move against round 37 and both are D090's one rule: the tank's current is the
# streams (a 27-term spectrum with no swirl about the axis, selected by the shape, so no knob
# here), and every part feels the water's acceleration force at EVOSIM_FLUID_ACCEL 1 (the
# physical value; 0 is every recorded world). Nothing else moves: round 36's prices, linkPhoto
# 0.5, dispersal 5 m, the current knob at 0.1 m/s RMS, 1 m and 5 m cells, area 100 m2. The
# throw trace rides along and costs no trajectory (its digest is identical). ASCII only.
#   ./rounds/launch-r37b.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>                      # r37b-s1
#   ./rounds/launch-r37b.ps1 -Seed 3 -Worker 7 -Seconds 600 -Name r37bsmoke -Dt 0.02       # the smoke
# Verify the header: everything launch-r37.ps1 lists ('space tank r=5.64 m (100 m2), depth 60,
# wall, bed', 'driveLimit >0.01', 'matterBudget 0', 'linkPhoto 0.5') and 'fluidAccel 1'. The
# 'current' token still names the mode ('0.1 m/s transport'); in a tank the field is the streams
# and the shape token is the proof. Read 'p3' over 'alive' first: round 37 read 58 to 96% and a
# tank that carries reads near a quarter. Expect about half round 37's pace (the acceleration
# is nine field samples per part per step until the analytic derivative lands).
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
    # about the reach it had in round 31. In a tank the cells span the bounding square and the
    # mask decides which are water: at 100 m2 that is 12 by 12 columns of which 100 are live,
    # which is the disc's own area to the square metre.
    [float]$Cell = 1,
    # The matter grid's cell, m. Coarser because a child costs 8 to 16 units of matter and seeded
    # water holds about one per cubic metre, so a 1 m cell could never afford a conception. 5 m is
    # D086's ruling; in a tank it does not have to divide the diameter, because everything past
    # the diameter is outside the circle and therefore dead. Four live cells a layer at 100 m2.
    [float]$MatterCell = 5,
    [float]$NeuronCost = 0,
    [float]$ConnectionCost = 0,
    [float]$WorkCost = 0,
    # Round 34 (logbook/0080): the muscle's standing charge, W per newton-metre of capacity, 0.02 in
    # every round before. Header token 'idle'. Not zero: Core refuses a free capacity outright
    # (seed 1's first launch, 2026-09-10, died on it before its first step), so this is the
    # price divided by two hundred.
    [float]$Idle = 0.0001,
    # Round 36 (logbook/0089): the link part's photosynthetic efficiency as a fraction of a leaf's,
    # 0 in every round before. At 1 the trade-off vanishes and joints drift neutrally (the Core
    # comment's warning); 0.5 is the first rung.
    [float]$LinkPhoto = 0.5,
    # fable-propose-limiter.md: whether EffectorDriver's 30 rad/s drive cap applies at every step
    # rather than only above dt 0.01. False is the rule every recorded run was driven under and
    # the value this round is specified at; the caller flips it once the limiter's own check has
    # passed (a replay of r34-s5 at 0.01 with it on, read on driveImpulsesLimited against the
    # jointed count and diverged against 17).
    [switch]$DriveLimitAlways,
    # D090's fluid acceleration force, 1 = physical, 0 = every recorded world (FluidConfig.
    # FluidAccelerationCoefficient, EVOSIM_FLUID_ACCEL). Header token 'fluidAccel'.
    [float]$FluidAccel = 1,
    # Sideways diffusivity of the detritus grid, m2/s. A grid mixes at one rate on all six faces,
    # so World refuses a grid world whose h-mix differs from EVOSIM_MIXING (the review of
    # 2026-09-08); the matter grid stirs on every axis at its own MatterMixingDiffusivity (2 m2/s,
    # not launchable). In a tank a face between a live cell and a dead one is glass and nothing
    # crosses it, which is what keeps the stirring inside the water.
    [float]$HMix = 0.02,
    [float]$Mixing = 0.02,
    # Rule 6 of fable-propose-grid.md: a death founds a particle that sinks, rides the current and
    # leaks this fraction of what it still holds into the water each second. 0.005 is a half-life
    # near 139 s, which is what round 32 ran.
    [float]$CorpseDecay = 0.005,
    # Rule 2 of fable-propose-growth.md: the share of a newborn's whole start that it holds as
    # reserve rather than as body. Out of the genome deliberately, or a lineage would set it to
    # zero and pocket the difference as size.
    [float]$NewbornReserve = 0.2,
    # Rule 5: the buffer a growing body keeps back rather than investing, as a fraction of the
    # tissue it already has. At 0.1 that is twelve to seventeen seconds of upkeep whatever the
    # body's size, so a growing creature sits near the edge and one quiet patch of water kills it.
    [float]$GrowthFloor = 0.1,
    # Rule 3: the lightest part a newborn may be built with, kg. A conception producing anything
    # lighter does not happen. Every divergence on record was a newborn and the lightest link
    # among them weighed 0.143 kg, so this is the guard that has to exist before the round does.
    [float]$MinNewbornKg = 0.5,
    # Rule 8: how often the harness puts a grown body into the physics, s. The economy grows a
    # body every metabolic step; resizing colliders, mass, anchors and drag panels is the
    # expensive half and runs at this cadence. It changes what the physics does, so it is in the
    # config hash.
    [float]$GrowthStep = 10,
    # Rule 1 and rule 7: the range generation zero draws its birth investment from, as a fraction
    # of a parent's own tissue value. A single value would found a world in which every lineage
    # begins with the same life history and selection has nothing to sort.
    [float]$InvestMin = 0.25,
    [float]$InvestMax = 1.0,
    # The two dials' mutation rates, per birth. Both are realised rates and neither is gated a
    # second time by ScalarChance, which is the bug the growth build's review found.
    [float]$AdultScaleChance = 0.08,
    [float]$InvestChance = 0.08,
    # rule 9 of fable-propose-growth.md: the tissue ceiling beside EVOSIM_MAX_POP. Idle in a
    # closed-matter world -- 6,000 units cannot build 30,000 J of tissue -- and kept as an
    # instrument for a world with influx.
    [float]$MaxTissue = 30000,
    # The disc a newborn is set down in, about its parent, m (RunConfig.OffspringDispersalMetres,
    # EVOSIM_OFFSPRING_DISPERSAL). 5 m is round 35's value and is kept here unchanged; in a tank a
    # draw that lands past the glass is refused and the attempt budget takes another, exactly as a
    # draw onto an occupied spot is.
    [float]$Dispersal = 5,
    # Which current field the world runs (RunConfig.Current.Mode, EVOSIM_CURRENT_MODE). In a tank
    # the water is the gyre, which is selected by the shape rather than by the mode; the mode is
    # still set to Transport because Core refuses a tank under Rolls at a nonzero speed -- the
    # rolls are a field over the box's row of patches and a tank has no such row.
    [string]$CurrentMode = 'Transport',
    # The current's RMS speed over the water, m/s. The gyre's fastest water runs about 3.8 times
    # its RMS, so at 1 m detritus cells and a 0.5 s metabolic step 0.1 m/s puts the Courant number
    # near 0.2 and the grid advects in one pass. Past a half it substeps, and past eight substeps
    # it refuses and names the arithmetic.
    [float]$Current = 0.1
)

# Outside the hashtable: an `if` is a statement and a hashtable literal wants expressions.
$driveLimit = if ($DriveLimitAlways) { 1 } else { 0 }

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = $Current; EVOSIM_MIXING = $Mixing; EVOSIM_REMIN = 0
    EVOSIM_CURRENT_MODE = $CurrentMode
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 100; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_MAX_TISSUE = $MaxTissue
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
    # D083: the water as vertices. fable-propose-grid.md: the water as a grid. A tank is grid-only.
    EVOSIM_FIELD = $Field; EVOSIM_FIELD_KERNEL = $Kernel; EVOSIM_FIELD_MATTER_KERNEL = $MatterKernel; EVOSIM_FIELD_MERGE = $Merge
    EVOSIM_FIELD_CAP = $Cap; EVOSIM_FIELD_QUANTUM = $Quantum
    EVOSIM_FIELD_CELL = $Cell; EVOSIM_FIELD_MATTER_CELL = $MatterCell
    # D082: the price of a bud.
    EVOSIM_NEURON_COST = $NeuronCost; EVOSIM_CONNECTION_COST = $ConnectionCost; EVOSIM_WORK_COST = $WorkCost
    EVOSIM_IDLE = $Idle
    EVOSIM_LINK_PHOTO = $LinkPhoto
    EVOSIM_FLUID_ACCEL = $FluidAccel
    EVOSIM_H_MIXING = $HMix
    EVOSIM_CORPSE_DECAY = $CorpseDecay
    # fable-propose-growth.md: the three world constants, the harness cadence, the founder
    # investment range and the two dials' mutation rates.
    EVOSIM_NEWBORN_RESERVE = $NewbornReserve; EVOSIM_GROWTH_FLOOR = $GrowthFloor
    EVOSIM_MIN_NEWBORN_KG = $MinNewbornKg; EVOSIM_GROWTH_STEP = $GrowthStep
    EVOSIM_INVEST_MIN = $InvestMin; EVOSIM_INVEST_MAX = $InvestMax
    EVOSIM_ADULT_SCALE_CHANCE = $AdultScaleChance; EVOSIM_INVEST_CHANCE = $InvestChance
    # The dispersal disc. Not EVOSIM_DISPERSAL, which is D061's retired per-step patch lottery and
    # which Core refuses outright in a tank.
    EVOSIM_OFFSPRING_DISPERSAL = $Dispersal
    # fable-propose-aquarium.md ruling 1: the container. This round's one knob.
    EVOSIM_SHAPE = 'tank'
    # fable-propose-limiter.md. 0 is the rule every recorded run was driven under.
    EVOSIM_DRIVE_LIMIT_ALWAYS = $driveLimit
}

if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }

$name = if ($Name -ne '') { $Name } else { "r37b-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
