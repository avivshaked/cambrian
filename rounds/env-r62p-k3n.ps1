# D239's pace screen, group 1 (logbook/specs/r62p-prereg/predictions.md): rounds/env-r62kg.ps1's world, the kill gut on,
# with the pace factor at 3 and the newborn's reserve left at 0.2, so the reserve's effect can be told from the pace's.
# World r62p-k3n-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_PACE_FACTOR = 3
$s
