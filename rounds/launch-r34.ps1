# Round 34 (logbook/0080, amended twice): round 35's world with a joint free. The muscle's idle
# charge, the stroke's work, the neuron and the connection prices all at 0; nothing else moves.
# Written first on round 33's launcher; rebased on round 35's on 2026-09-10 when the
# three-dimensional world became the base (logbook/0087), so it carries D088's dispersal disc,
# the transport current at 0.1 m/s and founders reserving the adult. Everything else is
# launch-r35.ps1 verbatim. ASCII only.
#   ./rounds/launch-r34.ps1 -Seed 1 -Worker 2 -ExpectSimHash <hash>                       # r34-s1
#   ./rounds/launch-r34.ps1 -Seed 1 -Worker 7 -Seconds 600 -Name r34smoke -Dt 0.02        # the smoke
# Verify the header: everything launch-r35.ps1 lists ('dispersal=5 m', 'current 0.1 m/s
# transport'), plus 'idle 0 W/N', 'work x0' and 'neuron 0 W + 0 W/input'. Read 'jnt inh'
# against 'alive' every 1,000 s, and 'diverged' against round 35's same seed: a free joint that
# dies of the solver is 0080's second hypothesis.
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
    # and 60 m deep is 20 m by 5 m by 60 m) and GridField refuses it; 5 m divides all three and
    # is D086's ruling.
    [float]$MatterCell = 5,
    [float]$NeuronCost = 0,
    [float]$ConnectionCost = 0,
    [float]$WorkCost = 0,
    # Round 34 (logbook/0080): the muscle's standing charge, W per newton of capacity, 0.02 in every
    # round before. Header token 'idle'.
    [float]$Idle = 0,
    # Sideways diffusivity of the detritus grid, m2/s. A grid mixes at one rate on all six faces,
    # so World refuses a grid world whose h-mix differs from EVOSIM_MIXING (the review of
    # 2026-09-08); the matter grid stirs on every axis at its own MatterMixingDiffusivity (2 m2/s,
    # not launchable) where round 31 stirred it sideways at 0.02. Header token 'h-mix' reads the
    # detritus rate.
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
    # That is the cost of growing fast rather than an oversight, and it is the first knob to move
    # if the world turns out to be too hard on juveniles.
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
    # rule 9 of fable-propose-growth.md (the owner's ruling, 2026-09-09): the tissue ceiling
    # beside EVOSIM_MAX_POP. Growth means a body count alone can miss the mat: a newborn is a
    # fraction of its adult body, so a healthy population can hold far less tissue than the same
    # count of adults did. Calibrated from round 32's own build (pre-growth, so every body was
    # already its full adult tissue): r32-s1's snapshots at t=5,000..14,000 (the 10 available at
    # the time this was set, the run still going) develop to a mean of 3.77 J of tissue per body
    # (mean of each snapshot's own mean; the pooled sum/count reading is close, 3.51 J). 8,000x
    # that, rounded to two significant figures, is 30,000 J -- eight population-ceilings' worth
    # of round 32-scale tissue, so this should not bind unless growth changes the scale a lot.
    [float]$MaxTissue = 30000,
    # The disc a newborn is set down in, about its parent, m
    # (RunConfig.OffspringDispersalMetres, EVOSIM_OFFSPRING_DISPERSAL). 0 is D077's touching rule
    # and every run on file, and it takes the old code path exactly. 5 m is a first cut: a quarter
    # of the box's 20 m length and half a patch, so a clade spreads over the water it is in within
    # a few generations without a child being flung across the world at birth. The value is the
    # owner's to rule on.
    [float]$Dispersal = 5,
    # Which current field the world runs (RunConfig.Current.Mode, EVOSIM_CURRENT_MODE).
    # 'rolls' is D059 and D066, every run on file, and the value at which this launcher describes
    # round 35 exactly. 'transport' is the owner's ruling of 2026-09-10: a three-dimensional
    # divergence-free field over the whole box, drawn from the seed, which moves a body along x and
    # z as well as up and down and carries both grids with the local water. Under 'transport' the
    # speed below is the RMS over the box, not a peak.
    [string]$CurrentMode = 'Transport',
    # The current's speed, m/s. A peak under 'rolls' and an RMS over the box under 'transport',
    # which is why it is a parameter now: the two numbers do not mean the same thing and a header
    # that named only the number would describe two worlds identically. The transport field's
    # fastest water runs about 2.5 times its RMS (the analytic ceiling is 4.6x), so at 1 m detritus
    # cells and a 0.5 s metabolic step 0.3 m/s puts the Courant number near 0.7 and GridField
    # splits the step in two rather than refusing it or clamping it. Past eight substeps, a Courant
    # number of four, it refuses and names the arithmetic.
    [float]$Current = 0.1
)

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
    # D083: the water as vertices. fable-propose-grid.md: the water as a grid.
    EVOSIM_FIELD = $Field; EVOSIM_FIELD_KERNEL = $Kernel; EVOSIM_FIELD_MATTER_KERNEL = $MatterKernel; EVOSIM_FIELD_MERGE = $Merge
    EVOSIM_FIELD_CAP = $Cap; EVOSIM_FIELD_QUANTUM = $Quantum
    EVOSIM_FIELD_CELL = $Cell; EVOSIM_FIELD_MATTER_CELL = $MatterCell
    # D082: the price of a bud.
    EVOSIM_NEURON_COST = $NeuronCost; EVOSIM_CONNECTION_COST = $ConnectionCost; EVOSIM_WORK_COST = $WorkCost
    EVOSIM_IDLE = $Idle
    EVOSIM_H_MIXING = $HMix
    EVOSIM_CORPSE_DECAY = $CorpseDecay
    # fable-propose-growth.md: the three world constants, the harness cadence, the founder
    # investment range and the two dials' mutation rates.
    EVOSIM_NEWBORN_RESERVE = $NewbornReserve; EVOSIM_GROWTH_FLOOR = $GrowthFloor
    EVOSIM_MIN_NEWBORN_KG = $MinNewbornKg; EVOSIM_GROWTH_STEP = $GrowthStep
    EVOSIM_INVEST_MIN = $InvestMin; EVOSIM_INVEST_MAX = $InvestMax
    EVOSIM_ADULT_SCALE_CHANCE = $AdultScaleChance; EVOSIM_INVEST_CHANCE = $InvestChance
    # The dispersal disc. Not EVOSIM_DISPERSAL, which is D061's retired per-step patch lottery.
    EVOSIM_OFFSPRING_DISPERSAL = $Dispersal
}

if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }

$name = if ($Name -ne '') { $Name } else { "r34-s$Seed" }
$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
