# Round 60's screen r60s-big (D175): the winner of the eight screens, r60s-i2 (the fed islands with D071's settling
# at 0.02 m/s; read 2026-10-15, logbook/specs/r60-screens/summary.md), at four times round 57's area. 88,000 m2 (radius
# 167 m), D109's islands at a 90 m wavelength over 0.05 of the water, and up to 256 reef stones, a quarter of the
# floor as at 22,000 m2. The bed's 41 m tilt is held. Seed 1 only (rounds/env-r60s-i2.ps1 for the rest).
$s = . (Join-Path $PSScriptRoot 'env-r60s-i2.ps1')
$s.EVOSIM_AREA = 88000
$s.EVOSIM_MATTER_ISLANDS = 90
$s.EVOSIM_MATTER_ISLAND_COVER = 0.05
$s.EVOSIM_REEF_MAX_COUNT = 256
$s
