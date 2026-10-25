# Integration branch 1's card identity round: rounds/env-r62cs-w1500.ps1's world with both of the branch's switches
# on, D228's level (b) and D232's kill gut.
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'trueshapeatpoint'
$s.EVOSIM_KILL_GUT = 1
$s
