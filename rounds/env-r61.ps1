# Round 61's unplanted world (D192): round 60's world (rounds/env-r60.ps1: its dose, its desert burial, its founders
# in the islands) on one landscape for every world of the round, drawn from its own seed (RunConfig.SeedOverrides,
# "landscape"), apart from the life seed the launcher gives (6101, 6102). The landscape seed is chosen by
# rounds/r61.ps1 -Step mapcheck before any life runs, and is the pre-registration's to fill: until $landscape below is
# set, the block refuses. rounds/env-r61-planted.ps1 is this world with the swimmers planted.
# Landscape 1: of 40 candidates (2026-10-16), the most islands of 10+ columns (6) with nearest centres at least 40 m
# apart (51 m) and the largest at most 0.4 of the island columns (0.35), the coordinator's rule for the pick, read
# from scripts/reads/r61-maps.py's lines.
$landscape = 1   # a whole number from 0
if ($null -eq $landscape) { throw 'rounds/env-r61.ps1: the landscape seed is not set; fill $landscape from the pre-registration' }
$s = . (Join-Path $PSScriptRoot 'env-r60.ps1')
$s.EVOSIM_SEED_OVERRIDES = "landscape=$landscape"
$s
