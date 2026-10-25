# The r62 grazer screen's reach arm: rounds/env-r62g.ps1 with the mouth's reach at 2.0 m, round 62's reach arm's.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_REACH = 2
$s
