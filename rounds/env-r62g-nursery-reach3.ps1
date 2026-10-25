# The reach follow-up's nursery arm (proposals/propose-r62-reach.md): rounds/env-r62g-nursery.ps1, the nursery's
# foragers as the trickle's pool, with the mouth's reach at 3.0 m. The screen tested them at 0.5 m only.
$s = . (Join-Path $PSScriptRoot 'env-r62g-nursery.ps1')
$s.EVOSIM_INTAKE_REACH = 3
$s
