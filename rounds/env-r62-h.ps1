# Round 62 as relaunched (D214, logbook/specs/r62-prereg/predictions.md): rounds/env-r62-n.ps1's world with a corpse
# half-life of 500 s. The two differ only in EVOSIM_CORPSE_HALF_LIFE. Worlds r62-h6201 to r62-h6203.
$s = . (Join-Path $PSScriptRoot 'env-r62-n.ps1')
$s.EVOSIM_CORPSE_HALF_LIFE = 500
$s
