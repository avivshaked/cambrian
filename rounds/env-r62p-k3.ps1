# D239's pace screen, group 1 (logbook/specs/r62p-prereg/predictions.md): rounds/env-r62kg.ps1's world, the kill gut on,
# with the pace factor at 3 (three physics seconds a world second, the step left at 0.01 s) and a newborn's reserve at
# half its share (EVOSIM_NEWBORN_RESERVE 0.5, against 0.2). World r62p-k3-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_PACE_FACTOR = 3
$s.EVOSIM_NEWBORN_RESERVE = 0.5
$s
