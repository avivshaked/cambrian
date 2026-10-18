# Round 59's planted seeds 2 and 3 (D174, D176): round 59's world (rounds/env-r59.ps1) with the trickle drawing from
# rounds/r59-pool.tsv, round 54's four stomachs at one founder in ten and the leaf tank's ten direction swimmers at one
# in two (rounds/r59-pool-list.py writes it), so the pool's share of the trickle is 0.6. The floor draws nothing from
# the pool, so the first swimmers arrive after the floor closes at 3,000 s.
$s = . (Join-Path $PSScriptRoot 'env-r59.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r59-pool.tsv').Replace([string][char]92, '/')
$s.EVOSIM_TRICKLE_POOL_SHARE = 0.6
$s.EVOSIM_FLOOR_POOL_SHARE = 0
$s
