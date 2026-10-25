# The r62 grazer screen's nursery arm (the owner's, 2026-10-19): rounds/env-r62g.ps1 with the trickle's pool drawn
# from the nursery's newest smell-following foragers in round 59's pool's place, at the same share: the 23 examined
# genomes of leaf-direction-patches (rounds/r62g-pool-nursery.tsv, inocula/r62g-nursery-d178). It asks whether
# skill helps once food pays.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r62g-pool-nursery.tsv').Replace([string][char]92, '/')
$s
