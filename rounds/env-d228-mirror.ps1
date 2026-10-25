# The D228-D229 card identity round: rounds/env-r62cs-w1500.ps1's world with D228's mirrored joint on
# (EVOSIM_REFLECT_MIRRORED_ANCHORS).
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w1500.ps1')
$s.EVOSIM_REFLECT_MIRRORED_ANCHORS = 1
$s
