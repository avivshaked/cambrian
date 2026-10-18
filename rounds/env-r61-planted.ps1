# Round 61's planted world (D192): rounds/env-r61.ps1's world and landscape with the trickle drawing from round 59's
# pool (rounds/r59-pool.tsv: round 54's four stomachs at one founder in ten and the leaf tank's ten direction swimmers
# of inocula/r59-direction-tops at one in two, so the pool's share of the trickle is 0.6), as round 59's planted seeds
# drew it (rounds/env-r59-planted.ps1). The floor draws nothing from the pool. Only the planting is round 59's: the
# prices and upkeep stay round 60's.
$s = . (Join-Path $PSScriptRoot 'env-r61.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r59-pool.tsv').Replace([string][char]92, '/')
$s.EVOSIM_TRICKLE_POOL_SHARE = 0.6
$s.EVOSIM_FLOOR_POOL_SHARE = 0
$s
