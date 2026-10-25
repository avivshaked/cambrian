# The D228-D229 card identity round: rounds/env-r62cs-w1500.ps1's world with D228's contact on the true shape
# (EVOSIM_CONTACT_SHAPE=trueshape; the world already runs per-part contact).
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'trueshape'
$s
