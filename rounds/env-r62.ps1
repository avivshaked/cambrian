# Round 62's world (D209, the owner's ruling of 2026-10-19): the open square of rounds/env-r62-box.ps1 with the ocean
# current at 0.02 m/s everywhere, a fifth of round 61's, D206's kept matter, D207's own leaves first, and D194's coral
# on the islands. The founder trickle and its pool are env-r62-box's. The snow's transport goes on the card with
# -Transport on the launch, which is not a world setting. The unchanged arm, r62-s<seed>; rounds/env-r62-reach.ps1 is
# the reach arm.
$s = . (Join-Path $PSScriptRoot 'env-r62-box.ps1')
$s.EVOSIM_CURRENT = 0.02
$s.EVOSIM_PLANTS_KEEP_MATTER = 1
$s.EVOSIM_PLANTS_KEEP_OWN_FIRST = 1
$s.EVOSIM_REEF_SHAPE = 'coral'
$s
