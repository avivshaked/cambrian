# Round 41 (D098, 2026-09-18): the base round of the one-substance economy. Round 40's world --
# the shaped bed at D093's size, the streams with the acceleration force, the corpses, light reach
# 6 m, 11,000 units of matter -- on the build in which matter is one substance in two states,
# charged and spent (logbook/specs/economy-spec.md; DESIGN 5A.2d). The second currency's dials are
# gone from this launcher (EVOSIM_EXCRETION, EVOSIM_MATTER_PER_TISSUE, EVOSIM_MATTER_PER_CREATURE are
# unbound in the build), EVOSIM_REMIN now sets every cell's remineralisation and not D051's floor
# leak, and six dials are new: EVOSIM_RHO (joules a charged unit carries), EVOSIM_UPTAKE_K and
# EVOSIM_UPTAKE_KS (the leaf's uptake rate per m2 of lit face and the spent density at which it
# halves), EVOSIM_HANDLING (joules burnt per joule eaten), EVOSIM_RESERVE_CAP (seconds of standing
# cost a body may hoard; 0 is off) and EVOSIM_MARGIN_MIN/MAX/CHANCE (the breeding margin gene's
# founder range and mutation rate; genome format 6). Header token 'economy rho 100 J/unit ...'.
# ASCII only.
#   ./rounds/launch-r41.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>                        # r41-s1
#   ./rounds/launch-r41.ps1 -Seed 1 -Worker 2 -Seconds 5000 -Name r41smoke-s1 -Dt 0.02     # the screen
# Verify the header: everything launch-r40.ps1 lists less 'excretion' and 'matter ... each', plus
# the economy token; the config's world group reads joulesPerUnit 100, uptakeRatePerSquareMetre 0.3,
# uptakeHalfSaturation 0.05, remineralisationPerSecond 0.0005, handlingCostPerJouleEaten 0.1,
# reserveCapSeconds 0. Read 'audit' and 'mat resid' first (both 0 on every row), then 'upt lim',
# 'mat top' against 'mat deep', 'detritus J', 'burnt' and 'remin' against 'det in'.
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [int]$Seconds = 30000,
    [float]$WallMinutes = 1800,   # was 1200 until 2026-09-17: 30,000 s ran 28 h at five arms and seeds 3 and 4 were cut short
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
    # The matter the world is seeded with, in units (RunConfig.MatterBudgetUnits,
    # EVOSIM_MATTER_BUDGET). 0 is the density rule and every run before this one. 6,000 is what a
    # 100 m2 by 60 m world holds at 1 unit per cubic metre, which is what rounds 33 through 37
    # ran on, so this round changes the water a unit sits in and not how many there are.
    # Round 41 (logbook/0107): 3,000. Under one substance the count is the capacity over what a
    # breeder holds, and 11,000 units built 6,300 bodies in 1,100 s; the screens at a 100 J
    # overhead plateaued at 400 for 2,000 units, 650 for 3,000, on the same line for 4,000.
    [float]$MatterBudget = 3000,
    # D090's fluid acceleration force, 1 = physical, 0 = every world before round 37b (FluidConfig.
    # FluidAccelerationCoefficient, EVOSIM_FLUID_ACCEL). Header token 'fluidAccel'.
    [float]$FluidAccel = 1,
    # The detritus grid's cell, m. One metre is the vertex kernel's support, so a mouth gets
    # about the reach it had in round 31. In a tank the cells span the bounding square and the
    # mask decides which are water: at 400 m2 that is 23 by 23 columns of which about 400 are
    # live, so the grid is about 24,000 cells -- cheap beside the physics, which follows bodies.
    [float]$Cell = 1,
    # The matter grid's cell, m. Coarser because a child costs 8 to 16 units of matter and seeded
    # water holds about one per cubic metre, so a 1 m cell could never afford a conception. 5 m is
    # D086's ruling; in a tank it does not have to divide the diameter, because everything past
    # the diameter is outside the circle and therefore dead. About sixteen live cells a layer at
    # 400 m2 -- the area over the cell's, give or take where the mask falls at the rim -- each
    # holding about a quarter of what round 37's four held. That quarter is the dilution, and it
    # is the number the ledger has to be run against before this launches.
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
    # rule 9 of fable-propose-growth.md: the tissue ceiling beside EVOSIM_MAX_POP. Off (0) from
    # D098: a child costs its parent's reserve and no field, so founder-sized bodies built 30,000 J
    # of tissue from 67 kJ of light by 672 s and the first smoke ended as a runaway on an
    # instrument sized for the world where tissue cost matter. In a closed one-substance world
    # the budget is the ceiling (11,000 units is 1.1 MJ of charged matter), and EVOSIM_MAX_POP
    # still ends a count runaway.
    [float]$MaxTissue = 0,
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
    # D092: the bed's three dials (RunConfig.BedReliefMetres, BedTiltMetres, BedScaleMetres;
    # logbook/specs/bed-spec.md items 3, 5a and 1). 0, 0 and anything is the flat bed to the bit.
    # D093 (the owner, 2026-09-15 evening: "we need a much bigger tank. Much. So that with 30 deg
    # decline we give a lot more depth variation"): 2,200 m2 (radius 26.46 m, diameter 52.9 m,
    # the smallest round area that takes a 30 m tilt under the 30-degree cap), 45 m deep, the
    # tilt 30 m so the floor runs from 30 m on the shallow arc, the lit band's floor, to 60 m on
    # the deep one; the founders drawn over the whole depth as before. EVOSIM_DEPTH is new with
    # this round (every earlier round ran the 60 m default).
    [float]$Area = 2200,
    [float]$Depth = 45,
    [float]$FounderDepth = 45,
    [float]$BedRelief = 1.5,
    [float]$BedTilt = 30,
    [float]$BedScale = 0,
    [float]$Current = 0.1,
    # D096: the light's attenuation depth, m (LightModel; EVOSIM_LIGHT_REACH). 12 is every round
    # before this one; 6 is the ruling. At 6 a lone leaf's R0 reaches 1 at 10 m and not at 20 m.
    [float]$LightReach = 6,
    # D098's six, in the spec's units. Defaults are the config's; the launcher names them so the
    # command line says what the world is.
    [float]$Rho = 100,
    [float]$UptakeK = 0.3,
    [float]$UptakeKs = 0.05,
    [float]$Remin = 0.0005,
    [float]$Handling = 0.1,
    [float]$ReserveCap = 0,
    [float]$MarginMin = 0,
    [float]$MarginMax = 600,
    [float]$MarginChance = 0.08,
    # What a cubic metre of tissue is worth and costs, J/m3, on every cell type
    # (CellType.TissueEnergyPerCubicMetre, EVOSIM_TISSUE_ENERGY). 500 is every world on file; under
    # one substance it is what a body holds in matter, and so the count the budget can build.
    [float]$TissueEnergy = 500,
    # The founders' part half-extents, m (RandomGenomeOptions.MinHalfExtent/MaxHalfExtent,
    # EVOSIM_FOUNDER_EXTENT_MIN/MAX). 0.15 to 0.40 is every world on file; 0 keeps the default.
    [float]$FounderExtentMin = 0,
    [float]$FounderExtentMax = 0,
    # What a child costs beyond its body, J, burnt (RunConfig.PerOffspringOverheadJoules,
    # EVOSIM_OVERHEAD). 25 is every world on file. Under one substance it is the floor under what a
    # breeder holds, so it bounds the count the budget can build however small bodies get.
    # Round 41 (logbook/0107): 100. At 25 the count drifted several-fold as bodies shrank.
    [float]$Overhead = 100
)

# Outside the hashtable: an `if` is a statement and a hashtable literal wants expressions.
$driveLimit = if ($DriveLimitAlways) { 1 } else { 0 }

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = $Current; EVOSIM_MIXING = $Mixing; EVOSIM_REMIN = $Remin
    EVOSIM_CURRENT_MODE = $CurrentMode
    EVOSIM_AREA = $Area; EVOSIM_DEPTH = $Depth; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_MAX_TISSUE = $MaxTissue
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = $FounderDepth; EVOSIM_PATCHES = 4
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
    # fable-propose-aquarium.md ruling 1: the container, round 37's.
    EVOSIM_SHAPE = 'tank'
    # Ruling 2: the matter as a total rather than a density. This round's one knob against round
    # 37, beside the area above. 6,000 units is round 33's own stock and round 37's -- 1 unit per
    # cubic metre over 100 m2 by 60 m -- held while the water quadruples, so the world holds the
    # same matter at a quarter of the concentration. 0 would be the density rule, at which 400 m2
    # would seed 24,000 units and the round would be a bigger world rather than a thinner one.
    EVOSIM_MATTER_BUDGET = $MatterBudget
    # fable-propose-limiter.md. 0 is the rule every recorded run was driven under.
    EVOSIM_DRIVE_LIMIT_ALWAYS = $driveLimit
    # D092: the floor's shape. Every recorded config lacks the bed group and is refused by this
    # build, per the tunable rule; the flat bed is 0, 0.
    EVOSIM_BED_RELIEF = $BedRelief; EVOSIM_BED_TILT = $BedTilt; EVOSIM_BED_SCALE = $BedScale
    # D096: the light's reach. The one dial this round moves against round 39.
    EVOSIM_LIGHT_REACH = $LightReach
    # D098: the one-substance economy's dials and the margin gene's founder range.
    EVOSIM_RHO = $Rho; EVOSIM_UPTAKE_K = $UptakeK; EVOSIM_UPTAKE_KS = $UptakeKs
    EVOSIM_HANDLING = $Handling; EVOSIM_RESERVE_CAP = $ReserveCap
    EVOSIM_MARGIN_MIN = $MarginMin; EVOSIM_MARGIN_MAX = $MarginMax; EVOSIM_MARGIN_CHANCE = $MarginChance
    EVOSIM_TISSUE_ENERGY = $TissueEnergy
    EVOSIM_FOUNDER_EXTENT_MIN = $FounderExtentMin; EVOSIM_FOUNDER_EXTENT_MAX = $FounderExtentMax
    EVOSIM_OVERHEAD = $Overhead
}

if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }

$name = if ($Name -ne '') { $Name } else { "r41-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
