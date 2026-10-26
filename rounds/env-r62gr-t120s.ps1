# D241's growth screen, group 1 (logbook/specs/r62gr-prereg/predictions.md): rounds/env-r62p-t3.ps1's world (the thrust arm,
# the kill gut, the newborn reserve at 0.5) with growth as a climb to adulthood in 120 world seconds, the growth-reserve gene
# starting at 30 s in [1, 300] s and mutating at the margin's rate. A juvenile's joint power scales with its mass fraction (the juvenile-strength switch). World r62gr-t120s-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62p-t3.ps1')
$s.EVOSIM_GROWTH_ADULT = 120
$s.EVOSIM_GROWTH_RESERVE_START = 30
$s.EVOSIM_GROWTH_RESERVE_MIN = 1
$s.EVOSIM_GROWTH_RESERVE_MAX = 300
$s.EVOSIM_GROWTH_RESERVE_MUT = 0.08
$s.EVOSIM_JUVENILE_STRENGTH = 1
$s
