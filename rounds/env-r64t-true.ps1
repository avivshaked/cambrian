# The true-shape screen's arm "t" (logbook/specs/r64t-prereg/predictions.md, D228's level (a)): r64's arm D
# (rounds/env-r64-d.ps1) with mouths that must touch the true surface, EVOSIM_CONTACT_SHAPE = 'trueshape'. The smell build's
# switches (D270) are set off by name so that no arm leans on a default.
$s = . (Join-Path $PSScriptRoot 'env-r64-d.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'trueshape'
$s.EVOSIM_LEAF_PLUME = 'none'
$s.EVOSIM_CURRENT_SENSE = 0
$s
