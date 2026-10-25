# The r62 grazer screen's keep arm: rounds/env-r62g.ps1 with no intake waste, so a mouth keeps every joule it takes
# from a corpse (×1.25 on the 0.8 it kept), the most the existing dials give a meal.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_WASTE = 0
$s
