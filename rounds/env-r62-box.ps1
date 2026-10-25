# The open square's world (D204), a draft for round 62's screens: round 61's world (rounds/env-r61.ps1, round 60's
# dose, desert burial and islands, on landscape 1) moved from the tank into the box, a square 300 m a side whose
# opposite edges are joined. The coordinator's list of 2026-10-19: 90,000 m², depth as round 61, islands 45 m off
# every edge, no tilt, the open ocean's current, D204's seams read across, D206's kept matter, and round 61's matter
# and island dials otherwise.
# - Nine patches, three across, so each patch is 100 m square and the field grids (1 m, 5 m) tile the side. The
#   number is the speed agent's choice: in a box the patches set the per-patch rows and nothing in the water.
# - No reefs. D204 leaves the mushrooms in the tank (cover 0 here), and the coral (RunConfig.ReefShape) has no
#   launcher setting until round 62's coral step lands.
#   With no reef the world wants every reef dial at its default, so round 61's are removed rather than set.
# - The floor keeps round 61's relief and band scale, without its shore: the world refuses a shore in a box
#   (logbook/specs/beach-spec.md). At 25 m deep and 1.5 m of relief round 61's 1 m shore and 15 m fade never touched
#   the floor anyway, so the floor is the same, and the current's fade is the identity, as its spec asks of the
#   first build.
# - The islands' cover is 0.067 where round 61's was 0.05 (the coordinator's ruling of 2026-10-19). The cover applies
#   inside the margin, and 45 m off every edge leaves 44,100 m² there against about 59,000 in round 61's tank, so 0.05
#   seeds 88 island columns of 25 m² where round 61 seeded about 118. 0.067 seeds about 118, round 61's oasis area.
# - The influx stays round 61's 3.6 units/s, over 2% more water than its tank.
# - Landscape 11, where round 61's tank took 1 (EVOSIM_SEED_OVERRIDES). A landscape is a different map in the
#   square. Of seeds 0 to 39 at cover 0.067, three gave five or six islands of 10+ columns in the square: 1, 3 and
#   11 (src/Evosim.OceanTracers --maps). 11's six are the most even (the largest holds 0.21 of the island
#   columns) and its tracers reach another island at about the tank's rate (1.6% at 12,000 s against the tank's
#   2.0% at 6,000 s, and 1's 0.7%); logbook/specs/open-ocean-current-build.md has the screen.
$s = . (Join-Path $PSScriptRoot 'env-r61.ps1')
$s.EVOSIM_SHAPE = 'box'
$s.EVOSIM_AREA = 90000
$s.EVOSIM_PATCHES = 9
$s.EVOSIM_PATCHES_ACROSS = 3
$s.EVOSIM_BED_TILT = 0
$s.EVOSIM_BED_SHORE = 0
$s.EVOSIM_BED_SHORE_FADE = 0
$s.EVOSIM_MATTER_ISLAND_MARGIN = 45
$s.EVOSIM_MATTER_ISLAND_COVER = 0.067
$s.EVOSIM_REEF_COVER = 0
foreach ($k in 'EVOSIM_REEF_MAX_COUNT', 'EVOSIM_REEF_CAP_RADIUS_MIN', 'EVOSIM_REEF_CAP_RADIUS_MAX', 'EVOSIM_REEF_ROUGHNESS', 'EVOSIM_REEF_CAP_DEPTH', 'EVOSIM_REEF_CAP_DEPTH_JITTER', 'EVOSIM_REEF_CAP_THICKNESS', 'EVOSIM_REEF_STEM_FRACTION', 'EVOSIM_REEF_FADE') { $s.Remove($k) }
$s.EVOSIM_CURRENT_MODE = 'ocean'
$s.EVOSIM_READ_ACROSS_SEAMS = 1
$s.EVOSIM_PLANTS_KEEP_MATTER = 1
$s.EVOSIM_SEED_OVERRIDES = 'landscape=11'
$s
