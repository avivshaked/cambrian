# D270's overpowered arm O (logbook/specs/smell-plume-design.md, "A screen of five worlds"): arm R with the far gradient as well.
# The same base and the same defaults as rounds/env-d270-r.ps1.
$s = . (Join-Path $PSScriptRoot 'env-d270-r.ps1')
$s.EVOSIM_LEAF_PLUME = 'gradient'
$s
