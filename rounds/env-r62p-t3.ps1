# D239's pace screen, group 1 (logbook/specs/r62p-prereg/predictions.md): rounds/env-r62kg.ps1's world, the kill gut on,
# with the thrust arm in place of the pace factor: the limb push at 9 times its 0.3 (both stroke-thrust terms scale with
# it) and the work price at a ninth of its 0.5, so a body swims about 3 times faster at the same billed power a metre.
# The pace factor stays at 1. A newborn's reserve at half its share, as in r62p-k3. World r62p-t3-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_LIMB_PUSH = 2.7
$s.EVOSIM_WORK_COST = 0.0555556
$s.EVOSIM_NEWBORN_RESERVE = 0.5
$s
