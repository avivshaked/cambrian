# Integration branch 1's card identity round: rounds/env-r62cs-w1500.ps1's world with D232's kill gut
# (EVOSIM_KILL_GUT; the world already runs contact eating, which the gut requires).
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_KILL_GUT = 1
$s
