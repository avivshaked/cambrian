# Round 60's screen r60s-big3m (D186): r60s-big3 at the middle dose, 3.6 units/s and 45,000 units, between big3's
# scaled dose and big3h's half (rounds/env-r60s-big3.ps1 for the rest). Seed 1 only.
$s = . (Join-Path $PSScriptRoot 'env-r60s-big3.ps1')
$s.EVOSIM_MATTER_INFLUX = 3.6
$s.EVOSIM_MATTER_BUDGET = 45000
$s
