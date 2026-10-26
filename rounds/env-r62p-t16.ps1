# D239's pace screen, group 2 (logbook/specs/r62p2-prereg/predictions.md, a draft until group 1 ends), the third slot's
# alternative by the draft's rule: rounds/env-r62kg.ps1's world, the kill gut on, with the thrust arm pushed past
# r62p-t3: the limb push at 16 times its 0.3 and the work price at a sixteenth of its 0.5, so a body swims about 4
# times faster against quadratic drag. The pace factor stays at 1. A newborn's reserve at half its share.
# World r62p-t16-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62kg.ps1')
$s.EVOSIM_LIMB_PUSH = 4.8
$s.EVOSIM_WORK_COST = 0.03125
$s.EVOSIM_NEWBORN_RESERVE = 0.5
$s
