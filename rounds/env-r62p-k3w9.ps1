# D239's pace screen, group 2 (logbook/specs/r62p2-prereg/predictions.md):rounds/env-r62kg.ps1's
# world, the kill gut on, with the pace factor at 3 and the work price at a ninth of its 0.5, so a stroke a world
# second costs a third of today's. A newborn's reserve at half its share. World r62p-k3w9-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_PACE_FACTOR = 3
$s.EVOSIM_WORK_COST = 0.0555556
$s.EVOSIM_NEWBORN_RESERVE = 0.5
$s
