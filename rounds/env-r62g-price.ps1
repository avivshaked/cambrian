# The r62 grazer screen's intake-price arm: rounds/env-r62g.ps1 with the mouth's upkeep removed (0 W per unit of
# intake per m², from 0.1). No dial scales the rate a mouth takes at, which is the Intake gene times the part's area;
# the coordinator chose this arm in its place (2026-10-19), on round 61's grazers being short of kills, not of bite.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_PRICE_INTAKE = 0
$s
