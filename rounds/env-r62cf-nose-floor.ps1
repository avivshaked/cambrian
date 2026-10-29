# rounds/env-r62cf-nose-snout.ps1 with every floor founder drawn from the nosed pool (EVOSIM_FLOOR_POOL_SHARE 1): the
# world founds on the pool's swimmers and stomachs alone, most of them nosed. The worst case of the nose's cost, for
# option 3 of its card sittings (G0); not a world anyone has ruled for a round.
$s = . (Join-Path $PSScriptRoot 'env-r62cf-nose-snout.ps1')
$s.EVOSIM_FLOOR_POOL_SHARE = 1
$s
