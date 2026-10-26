# D239's pace screen, group 2 (logbook/specs/r62p2-prereg/predictions.md, a draft until group 1 ends): rounds/env-r62kg.ps1's
# world, the kill gut on, with the pace factor at 4 (a metabolic step of 200 physics steps at 0.01 s) and a newborn's
# reserve at half its share. The work price is today's 0.5 unless the draft's rule moves it to a ninth (see there).
# World r62p-k4-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_PACE_FACTOR = 4
$s.EVOSIM_NEWBORN_RESERVE = 0.5
$s
