# D235's nose in the confirmation round's world, with nosed swimmers in the trickle: rounds/env-r62cf-nose.ps1 with the
# pool's ten swimmers replaced by their snout-grafted copies (inocula/nose-snout-r59-tops, the short nose of nursery
# entry 25) and its four stomachs kept, at r62cf's weights and shares (rounds/r62cf-nose-pool.tsv).
$s = . (Join-Path $PSScriptRoot 'env-r62cf-nose.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r62cf-nose-pool.tsv').Replace([string][char]92, '/')
$s
