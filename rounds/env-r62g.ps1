# The r62 grazer screen's control (the coordinator's plan of 2026-10-19, after the owner stopped round 62): round 62's
# world (rounds/env-r62.ps1) with the trickle drawing from round 59's pool (rounds/r59-pool.tsv: round 54's four
# stomachs and the leaf tank's ten direction swimmers, which are mouthed) at a share of 0.5, so each world founds
# grazers enough to read. Round 62's own pool is the four stomachs alone. A screen, not a round: worlds r62g-<arm>,
# founded at seed 6201 for 12,000 s. The other arms are rounds/env-r62g-*.ps1, each this file and one change.
$s = . (Join-Path $PSScriptRoot 'env-r62.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r59-pool.tsv').Replace([string][char]92, '/')
$s.EVOSIM_TRICKLE_POOL_SHARE = 0.5
$s
