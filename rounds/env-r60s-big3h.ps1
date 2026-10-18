# Round 60's screen r60s-big3h (D185): r60s-big3 at half its dose, 2.4 units/s and 30,000 units, which asks whether a
# crowd lives when the water between the islands is hungry (rounds/env-r60s-big3.ps1 for the rest). Seed 1 only.
$s = . (Join-Path $PSScriptRoot 'env-r60s-big3.ps1')
$s.EVOSIM_MATTER_INFLUX = 2.4
$s.EVOSIM_MATTER_BUDGET = 30000
$s
