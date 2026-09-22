# Round 45's environment block: round 44's world (rounds/env-r44.ps1, the module gene live) plus
# the mouth's tunables (D106, logbook/specs/mouth-spec.md, the "As built" section and the screen
# note above it). The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
# Every founder is at zero attack and protection and the attributes enter by mutation at the
# cell-type rate.
#
# The dials, from the ledger screen of 2026-09-22 night (scratch/r45-build/ledger-*.txt, copied
# beside the round's read): EVOSIM_HEALTH 13 is where a capped claw takes three metabolic steps to
# kill round 43's median leaf part (at 1 it took one, 4.5 times over); healing at 1% of the pool a
# second at 1 J per unit of health; intake reaches half a metre past a corpse's surface and a fifth
# of what is eaten goes to the water as snow. The prices are per attribute per unit per square
# metre of the part: protection 1 W (a capped cuticle costs about 12% of a leaf's surface income, so
# armour is not free), attack, intake and toughness 0.1 W (a capped claw costs about 5% of a leaf's
# income and one leaf-sized corpse repays about 450 s of it). The two senses the mouth answers,
# Contact and Damage, are in the pool.
@{
    EVOSIM_REPORT_EVERY = 20
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.1; EVOSIM_MIXING = 0.02; EVOSIM_REMIN = 0.002
    EVOSIM_CURRENT_MODE = 'Transport'
    EVOSIM_AREA = 2200; EVOSIM_DEPTH = 45; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_MAX_TISSUE = 0
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_INITIAL = 1; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 45; EVOSIM_PATCHES = 4
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
    EVOSIM_MATTER_BUDGET = 1500
    EVOSIM_DRIVE_LIMIT_ALWAYS = 0
    EVOSIM_BED_RELIEF = 1.5; EVOSIM_BED_TILT = 30; EVOSIM_BED_SCALE = 0
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
    # A recording setting, no hash moves: round 44's seed 1 slowed tenfold at 20,000 s with the
    # solver taking 99% of the wall and no checkpoint to profile it from; a checkpoint every
    # 2,500 s makes a slow seed's state a fixture for the profiler.
    EVOSIM_CHECKPOINT_EVERY = 2500
}
