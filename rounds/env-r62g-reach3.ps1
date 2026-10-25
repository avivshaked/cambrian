# The r62 reach follow-up's arm: rounds/env-r62g.ps1 with the mouth's reach at 3.0 m (proposals/propose-r62-reach.md).
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_REACH = 3
$s
