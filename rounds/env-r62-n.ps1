# Round 62 as relaunched (D214, logbook/specs/r62-prereg/predictions.md): rounds/env-r62g.ps1, round 62's world with
# the trickle drawing from round 59's pool at a share of 0.5, and the mouth's reach at 3 m measured from the body's
# centre, without spoilage (the half-life 0 is off). Its twin is rounds/env-r62-h.ps1. Worlds r62-n6201 to r62-n6203.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_REACH = 3
$s.EVOSIM_CORPSE_HALF_LIFE = 0
$s
