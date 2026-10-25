# Round 62's reach arm (D209): rounds/env-r62.ps1 with the mouth's reach at 2.0 m, round 61's 0.5 m four times over, the
# step of D201's grazer search held on 2026-10-19 (the dial screens' b2, rounds/r62d on r61-cousin). Every other
# setting is the unchanged arm's. Worlds r62-r<seed>.
$s = . (Join-Path $PSScriptRoot 'env-r62.ps1')
$s.EVOSIM_INTAKE_REACH = 2
$s
