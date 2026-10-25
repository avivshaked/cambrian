# D213's spoilage-by-reach screen (logbook/specs/r62-spoil-screen-proposal.md): rounds/env-r62g.ps1 with the mouth's
# reach at 3 m and the corpse half-life at 2000 s (0 is off).
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_REACH = 3
$s.EVOSIM_CORPSE_HALF_LIFE = 2000
$s
