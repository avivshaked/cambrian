# Integration branch 1's card identity round: rounds/env-r62cs-w1500.ps1's world with D228's level (b), the force at
# the contact point (EVOSIM_CONTACT_SHAPE=trueshapeatpoint; the world already runs per-part contact).
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'trueshapeatpoint'
$s
