# D224's overshoot screen (logbook/specs/r62cs-prereg/predictions.md): rounds/env-r62ce.ps1's world, with a living
# bite taking its share of the reserve and the gestation account (EVOSIM_BITE_TAKES_STORE) and the remains' window
# at 60 s. Worlds r62cs-w60-6201 and r62cs-w60-6202.
$s = . (Join-Path $PSScriptRoot 'env-r62ce.ps1')
$s.EVOSIM_BITE_TAKES_STORE = 1
$s.EVOSIM_REMAINS_WINDOW = 60
$s
