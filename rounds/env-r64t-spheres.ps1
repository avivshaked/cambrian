# The true-shape screen's arm "s" (logbook/specs/r64t-prereg/predictions.md): r64's arm D (rounds/env-r64-d.ps1) with sphere
# contact set explicitly, EVOSIM_CONTACT_SHAPE = 'spheres', the off arm on seed 6203. The smell build's switches (D270) are
# set off by name so that no arm leans on a default.
$s = . (Join-Path $PSScriptRoot 'env-r64-d.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'spheres'
$s.EVOSIM_LEAF_PLUME = 'none'
$s.EVOSIM_CURRENT_SENSE = 0
$s
