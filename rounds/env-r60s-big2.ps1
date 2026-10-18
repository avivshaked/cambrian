# Round 60's screen r60s-big2 (D181): r60s-big with its dose scaled to its area. Four times round 57's water takes
# four times its influx, 4.8 units/s, and four times its starting matter, 60,000 units. The founders start where
# r60s-big's did (round 59's D116 rule), so the screen tests the dose alone. Seed 1 only (rounds/env-r60s-big.ps1
# for the rest).
$s = . (Join-Path $PSScriptRoot 'env-r60s-big.ps1')
$s.EVOSIM_MATTER_INFLUX = 4.8
$s.EVOSIM_MATTER_BUDGET = 60000
$s
