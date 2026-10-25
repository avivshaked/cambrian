# D226's deeper-wound screen: rounds/env-r62cs-w500.ps1's world (the store share on, the remains' window at 500 s),
# with a mouth's blow at 4 times its damage (EVOSIM_BITE_DAMAGE_SCALE) and the intake scale, which under contact
# eating lifts only the bite's cap, at 16.
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w500.ps1')
$s.EVOSIM_BITE_DAMAGE_SCALE = 4
$s.EVOSIM_INTAKE_SCALE = 16
$s
