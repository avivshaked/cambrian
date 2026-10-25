# The r62 grazer screen's food arm (the owner's, 2026-10-19): rounds/env-r62g.ps1 with leaf tissue worth twice the
# joules (EVOSIM_LEAF_TISSUE_ENERGY, the photosynthetic cell's tissue energy in the config's cell table). A leaf costs
# twice as much to grow, paid from light, and its corpse holds twice as much, so a grazer's meal is worth more and
# nothing is created.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_LEAF_TISSUE_ENERGY = 2
$s
