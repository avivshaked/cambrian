# The r62 grazer screen's combination (the owner's, 2026-10-19): rounds/env-r62g.ps1 with the reach, eating, food and
# endowment arms' changes together.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_REACH = 2
$s.EVOSIM_INTAKE_SCALE = 4
$s.EVOSIM_LEAF_TISSUE_ENERGY = 2
$s.EVOSIM_FOUNDER_ENDOWMENT = 2400
$s
