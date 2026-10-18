# Round 51's environment block: round 50's world (rounds/env-r50.ps1, every dial below as it
# was) with the owner's rulings of 2026-09-27 (D127, D128) and the build's code:
# - Links catch no light (EVOSIM_LINK_PHOTO 0, from 0.5), and a body's first link holds no energy
#   up to 0.01 m3 of its adult volume (EVOSIM_FREE_LINK; round 50's first links measured a median
#   of 0.003 to 0.014 m3 a seed, logbook/specs/r51-build/linkvol.txt).
# - A dead body is a husk: it sinks at 0.05 m/s (25 times the snow), settles on the first reef cap
#   or the bed under it and stays, dissolves with a half-life of half an hour (EVOSIM_CORPSE_DECAY
#   0.000385, from 0.005), and gives the rest to the water and leaves at 5% of what it was.
# - Death by age: a Gompertz hazard with its median at 3,000 s, doubling every 600 s (a fifth of
#   the base), a birth pushing the parent's clock one doubling for a child the parent's size and a
#   destroyed part one doubling for the whole body, each in proportion. D038's wear is off
#   (EVOSIM_SENESCENCE 0, from 3000), since D128 refuses the two together. The lifespan gene is
#   carried by every body at 1 and does not mutate (the defaults of EVOSIM_LIFESPAN_*), and the
#   repair exponent is 0.
# Every rule is a new realisation of every seed, so no seed replays round 50's.
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
    # The pool is read from the tree this launcher sits in, so a launch from a worktree reads its own
    # format-10 files; the config records the pool's contents by hash, not its path.
    EVOSIM_TRICKLE_POOL = (@('stomach-r45s1-100', 'stomach-link-r46s1-1182', 'stomach-link-r46s3-1513', 'stomach-link-r46s3-2156') |
        ForEach-Object { (Join-Path (Split-Path $PSScriptRoot -Parent) "inocula/pool-r47/$_.json").Replace([string][char]92, '/') }) -join ';'
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
    EVOSIM_SENESCENCE = 0; EVOSIM_EXCESS_DENSITY = 0.02
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
    EVOSIM_LINK_PHOTO = 0
    EVOSIM_FLUID_ACCEL = 1
    EVOSIM_H_MIXING = 0.02
    EVOSIM_CORPSE_DECAY = 0.000385
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
    # Round 49 (rounds/env-r49.ps1's header).
    EVOSIM_FOUNDER_RESERVE_CAP = 0.9
    EVOSIM_RECORD_FORMAT = 2
    EVOSIM_CHECKPOINT_EVERY = 100
    # Round 50 (see the header).
    EVOSIM_FOUNDERS_INCOME_DEPTH = 1
    # Round 51 (see the header).
    EVOSIM_FREE_LINK = 0.01
    EVOSIM_CORPSE_SINK = 0.05; EVOSIM_CORPSE_SETTLE = 1; EVOSIM_CORPSE_FLOOR = 0.05
    EVOSIM_AGE_BASE = 3000; EVOSIM_AGE_DOUBLING = 0.2; EVOSIM_AGE_BIRTH = 1; EVOSIM_AGE_WOUND = 1
}
