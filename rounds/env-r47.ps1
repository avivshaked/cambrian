# Round 47's environment block: round 46's world (rounds/env-r46.ps1: the 22,000 m2 tank at
# 45 m with the beach, 15,000 units as islands stirred at 0.02, light by exposure, the offset
# priced, the trickle with founders in their food, the support cost, contact per part, the
# snow at 0.0005 /s) plus two rules, each with its spec under logbook/specs/:
# - the mushroom reef (reef-spec.md, D118): rock columns with overhanging caps drawn from the
#   seed until a quarter of the surface is under them, each cap 6 to 16 m in radius with a
#   lobed outline, its top at 3 m plus or minus 1, 2 m thick, on a stem a quarter of its
#   radius, overlapping where they fall, the streams faded over 8 m from the rock and
#   multiplied where the fades meet. The cap's top is a lit table where the snow lands and
#   the water under it is dark. Tokens to check in the header: `reefs N cover 0.25 (x got)
#   cap r=6-16 m rough 0.15 at 3 m ±1 t=2 m stem 0.25 fade 8 m`, and the manifest's reefs.
# - the trickle's pool (D117, the pool build of 2026-09-23): one trickle founder in ten is a
#   copy of one of the four stomachs under inocula/pool-r47/ (the screens' inoculum and round
#   46's three best absorptive founders by the ledger, logbook/specs/r46-read/ledger-stomachs.md),
#   drawn from the trickle's own stream, so the other nine in ten are round 46's lottery.
#   Tokens to check: `pool 0.1 of 4` after the trickle token, the `pool` column, `src: pool`
#   on lineage rows and <run>/pool/00..03.json.
#
# Everything else is round 46's, including the checkpoint every 2,500 s (a recording setting).
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
@{
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
    EVOSIM_MIN_NEWBORN_KG = 0.5; EVOSIM_GROWTH_STEP = 10
    EVOSIM_INVEST_MIN = 0.25; EVOSIM_INVEST_MAX = 1.0
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
    EVOSIM_OVERHEAD = 100
    EVOSIM_MODULE_ADD = 300; EVOSIM_MODULE_DROP = 50; EVOSIM_MODULE_DROP_AFTER = 100
    EVOSIM_MODULE_MUT = 0.005
    EVOSIM_HEALTH = 13; EVOSIM_HEAL = 0.01; EVOSIM_HEAL_COST = 1
    EVOSIM_INTAKE_REACH = 0.5; EVOSIM_INTAKE_WASTE = 0.2
    EVOSIM_PRICE_ATTACK = 0.1; EVOSIM_PRICE_INTAKE = 0.1; EVOSIM_PRICE_PROTECTION = 1; EVOSIM_PRICE_TOUGHNESS = 0.1
    EVOSIM_ATTRIBUTE_MUT = 0.005
    EVOSIM_SENSE_CONTACT = 1; EVOSIM_SENSE_DAMAGE = 1
    EVOSIM_CHECKPOINT_EVERY = 2500
}
