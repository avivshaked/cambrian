# D241's growth screen, group 2 (logbook/specs/r62gr2-prereg/predictions.md): rounds/env-r62gr-t60.ps1's world, the climb to
# adulthood in 60 world seconds with the juvenile-strength switch off, and the growth-reserve gene starting at 60 s.
# World r62gr2-g60-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62gr-t60.ps1')
$s.EVOSIM_GROWTH_RESERVE_START = 60
$s
