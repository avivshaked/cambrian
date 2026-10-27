# D241's growth screen, group 1 (logbook/specs/r62gr-prereg/predictions.md): rounds/env-r62p-t3.ps1's world (the thrust arm,
# the kill gut, the newborn reserve at 0.5) with growth as a climb to adulthood in 240 world seconds, the growth-reserve gene
# starting at 30 s in [1, 300] s and mutating at the margin's rate. World r62gr-t240-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62p-t3.ps1')
$s.EVOSIM_GROWTH_ADULT = 240
$s.EVOSIM_GROWTH_RESERVE_START = 30
$s.EVOSIM_GROWTH_RESERVE_MIN = 1
$s.EVOSIM_GROWTH_RESERVE_MAX = 300
$s.EVOSIM_GROWTH_RESERVE_MUT = 0.08
$s
