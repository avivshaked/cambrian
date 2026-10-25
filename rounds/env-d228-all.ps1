# The D228-D229 card identity round: rounds/env-r62cs-w1500.ps1's world with all three of D228's and D229's switches on.
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_SHAPED_ADDED_MASS = 1
$s.EVOSIM_REFLECT_MIRRORED_ANCHORS = 1
$s.EVOSIM_CONTACT_SHAPE = 'trueshape'
$s
