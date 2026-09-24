# Round 48's environment block: round 47's world (rounds/env-r47.ps1: the 22,000 m2 tank at
# 45 m with the beach, 15,000 units as islands stirred at 0.02, light by exposure, the offset
# priced, the trickle with founders in their food and one in ten from the stomach pool, the
# support cost, contact per part, the mushroom reef at a quarter of the surface) plus the four
# rulings of 2026-09-24 (DECISIONS D119 to D122), each a tunable that is off in every
# recorded config:
# - D119, a cell type arrives only as a bud (no launcher knob: Mutator.ChangeCellType is gone
#   from the build and a bud is drawn at EVOSIM_CELLTYPE_MUTATION per node), and the floors
#   weigh rigid groups so that a born-small bud is born (EVOSIM_RIGID_FLOORS 1; header
#   `floors rigid groups` after the reach token; lineage rows carry `bud` and `budx` on a
#   birth that budded).
# - D120, reproduction paid as it goes as a gene beside the lump (the mode and the share
#   mutate at the reproduction traits' rate, EVOSIM_GESTATION_MODE_CHANCE and
#   EVOSIM_GESTATION_SHARE_CHANCE 0.08; every founder is a lump breeder and draws its share
#   at 0.5; header `gestation mut=0.08 share=0.5-0.5 at 0.08` before the hash; birth rows
#   carry `gm` and `gs`, stats rows gestationBirths, gestatedJoules, gestationJoulesHeld),
#   and the child's overhead is max(floor, k x the child's tissue at birth): the floor 50 J
#   where every round from 41 charged a flat 100 J, k = 2, so a 12 J stomach child pays the
#   50 J floor and a 50 J leaf child pays 100 J (EVOSIM_OVERHEAD is the floor,
#   EVOSIM_OVERHEAD_PER_TISSUE the factor; header `overhead 50 J` and `overhead scale x2 floor
#   50 J`). With it the birth-investment and newborn-mass floors come down by a factor of five
#   and ten (EVOSIM_INVEST_MIN 0.05, EVOSIM_MIN_NEWBORN_KG 0.05; header `minkg=0.05` and
#   `invest=0.05-1`) so that a lineage can choose many small children.
#   The floor is the count screen's (scratch/r48-build/runs, seed 2 at dt 0.02, logbook/0119's
#   screen section): at 10 J the count doubled every 100 s to 6,111 at 680 s (r48plat-s2);
#   at 50 J it peaked at 3,670 at 1,300 s and eased to 3,229 at 1,800 s with the mean
#   investment at 0.26 and no divergence (r48plat2-s2), against round 47's world at the same
#   seed and step peaking at 1,575 (scratch/r48-snow/runs/base). A fee proportional to a child
#   a lineage can make as small as it likes is no floor, so the floor is what bounds the count
#   (0107's finding), and the small-child floors stay down.
# - D121, senescence on upkeep alone (EVOSIM_SENESCENCE_WEARS_INTAKE 0; header
#   `senescence 3000 s on upkeep` where round 47 read `on upkeep and intake`).
# - D122, every founder at the richest cell of its food, depth included
#   (EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH 1; header `founders in their food at its depth`), and
#   born with 600 s of its own standing watts (EVOSIM_FOUNDER_ENDOWMENT 600; header
#   `endowment 600 s`; founder rows carry `endow`). 600 s is the breeding margin's ceiling
#   and about ten times the 20 to 35 s round 47's stomach founders lived; a screen reads
#   what the endowed founders do with it.
# The snow's two knobs (EVOSIM_REMIN, EVOSIM_SINK / EVOSIM_MATTER_SINK) stay at round 47's:
# the snow screen's base (scratch/r48-snow/runs/base) read the stomach founders dying long
# before the snow's slow decline could matter, so the founders' arrival (D122) is what this
# round changes and the dials are a between-rounds screen (HANDOFF's snow bullet).
#
# Everything else is round 47's, including the checkpoint every 2,500 s (a recording setting).
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
@{
    EVOSIM_RIGID_FLOORS = 1
    EVOSIM_GESTATION_MODE_CHANCE = 0.08; EVOSIM_GESTATION_SHARE_CHANCE = 0.08
    EVOSIM_GESTATION_SHARE_MIN = 0.5; EVOSIM_GESTATION_SHARE_MAX = 0.5
    EVOSIM_OVERHEAD = 50; EVOSIM_OVERHEAD_PER_TISSUE = 2
    EVOSIM_INVEST_MIN = 0.05; EVOSIM_INVEST_MAX = 1.0
    EVOSIM_MIN_NEWBORN_KG = 0.05
    EVOSIM_SENESCENCE_WEARS_INTAKE = 0
    EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH = 1
    EVOSIM_FOUNDER_ENDOWMENT = 600
    EVOSIM_MATTER_ISLANDS = 60; EVOSIM_MATTER_ISLAND_COVER = 0.1; EVOSIM_MATTER_ISLAND_DEPTH = 12
    EVOSIM_FOUNDERS_FOLLOW_MATTER = 0
    EVOSIM_FOUNDERS_FOLLOW_FOOD = 1
    EVOSIM_TRICKLE = '1/30'
    EVOSIM_REEF_COVER = 0.25; EVOSIM_REEF_MAX_COUNT = 64
    EVOSIM_REEF_CAP_RADIUS_MIN = 6; EVOSIM_REEF_CAP_RADIUS_MAX = 16; EVOSIM_REEF_ROUGHNESS = 0.15
    EVOSIM_REEF_CAP_DEPTH = 3; EVOSIM_REEF_CAP_DEPTH_JITTER = 1; EVOSIM_REEF_CAP_THICKNESS = 2
    EVOSIM_REEF_STEM_FRACTION = 0.25; EVOSIM_REEF_FADE = 8
    EVOSIM_TRICKLE_POOL = 'D:/Projects/experiments/evolution-simulator/inocula/pool-r47/stomach-r45s1-100.json;D:/Projects/experiments/evolution-simulator/inocula/pool-r47/stomach-link-r46s1-1182.json;D:/Projects/experiments/evolution-simulator/inocula/pool-r47/stomach-link-r46s3-1513.json;D:/Projects/experiments/evolution-simulator/inocula/pool-r47/stomach-link-r46s3-2156.json'
    EVOSIM_TRICKLE_POOL_SHARE = 0.1
    EVOSIM_LIGHT_EXPOSURE = 1
    EVOSIM_BUOYANCY_OFFSET_COST = 0.02
    EVOSIM_SUPPORT = 0.1
    EVOSIM_CONTACT_PER_PART = 1
    EVOSIM_BED_TILT = 96; EVOSIM_BED_SHORE = 1; EVOSIM_BED_SHORE_FADE = 15
    EVOSIM_LIGHT_SHADE = 0; EVOSIM_LIGHT_SHADE_DRIFT = 0
    EVOSIM_MATTER_MIXING = 0.02
    EVOSIM_REPORT_EVERY = 20
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.1; EVOSIM_MIXING = 0.02; EVOSIM_REMIN = 0.0005
    EVOSIM_CURRENT_MODE = 'Transport'
    EVOSIM_AREA = 22000; EVOSIM_DEPTH = 45; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 25000
    EVOSIM_MAX_TISSUE = 0
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 12; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.002
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = 0.01
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
    EVOSIM_SENSE_CHEMICAL = 1; EVOSIM_SENSE_ENERGY = 1; EVOSIM_SENSE_FLOW = 1
    EVOSIM_ADDED_MASS = 0.5
    EVOSIM_FIELD = 'grid'; EVOSIM_FIELD_KERNEL = 1; EVOSIM_FIELD_MATTER_KERNEL = 1.8; EVOSIM_FIELD_MERGE = 0.25
    EVOSIM_FIELD_CAP = 100000; EVOSIM_FIELD_QUANTUM = 0.125
    EVOSIM_FIELD_CELL = 1; EVOSIM_FIELD_MATTER_CELL = 5
    EVOSIM_NEURON_COST = 0; EVOSIM_CONNECTION_COST = 0; EVOSIM_WORK_COST = 0
    EVOSIM_IDLE = 0.0001; EVOSIM_SILHOUETTE = 1
    EVOSIM_WATER_HOLD = 0; EVOSIM_SELF_OVERLAP = 0.1
    EVOSIM_LINK_PHOTO = 0.5
    EVOSIM_FLUID_ACCEL = 1
    EVOSIM_H_MIXING = 0.02
    EVOSIM_CORPSE_DECAY = 0.005
    EVOSIM_NEWBORN_RESERVE = 0.2; EVOSIM_GROWTH_FLOOR = 0.1
    EVOSIM_GROWTH_STEP = 10
    EVOSIM_ADULT_SCALE_CHANCE = 0.08; EVOSIM_INVEST_CHANCE = 0.08
    EVOSIM_OFFSPRING_DISPERSAL = 5
    EVOSIM_SHAPE = 'tank'
    EVOSIM_MATTER_BUDGET = 15000
    EVOSIM_DRIVE_LIMIT_ALWAYS = 0
    EVOSIM_BED_RELIEF = 1.5; EVOSIM_BED_SCALE = 17.641891
    EVOSIM_LIGHT_REACH = 6
    EVOSIM_RHO = 100; EVOSIM_UPTAKE_K = 0.3; EVOSIM_UPTAKE_KS = 0.05
    EVOSIM_HANDLING = 0.1; EVOSIM_RESERVE_CAP = 0
    EVOSIM_MARGIN_MIN = 0; EVOSIM_MARGIN_MAX = 600; EVOSIM_MARGIN_CHANCE = 0.08
    EVOSIM_TISSUE_ENERGY = 500
    EVOSIM_FOUNDER_EXTENT_MIN = 0; EVOSIM_FOUNDER_EXTENT_MAX = 0
    EVOSIM_MODULE_ADD = 300; EVOSIM_MODULE_DROP = 50; EVOSIM_MODULE_DROP_AFTER = 100
    EVOSIM_MODULE_MUT = 0.005
    EVOSIM_HEALTH = 13; EVOSIM_HEAL = 0.01; EVOSIM_HEAL_COST = 1
    EVOSIM_INTAKE_REACH = 0.5; EVOSIM_INTAKE_WASTE = 0.2
    EVOSIM_PRICE_ATTACK = 0.1; EVOSIM_PRICE_INTAKE = 0.1; EVOSIM_PRICE_PROTECTION = 1; EVOSIM_PRICE_TOUGHNESS = 0.1
    EVOSIM_ATTRIBUTE_MUT = 0.005
    EVOSIM_SENSE_CONTACT = 1; EVOSIM_SENSE_DAMAGE = 1
    # Seeds 1 and 2 were launched at 2,500 s (12:51, 2026-09-24). Seed 3 and anything after
    # it write a checkpoint every 500 s, by the owner's ruling of 15:40 that day, so that a
    # film never re-runs more than 500 s of the world to reach a scene. A recording setting:
    # it moves no hash and changes no trajectory.
    EVOSIM_CHECKPOINT_EVERY = 500
}
