# D213's spoilage-by-reach screen: rounds/env-r62g.ps1 at its own reach (0.5 m) with the corpse half-life at 500 s.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_CORPSE_HALF_LIFE = 500
$s
