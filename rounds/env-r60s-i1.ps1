# Round 60's screen r60s-i1 (D175): the fed islands at the proposed dose. D074's influx at 1.2 units/s placed on
# D109's islands (each island column weighted by its stock at t = 0), D074's burial at 0.001/s from each column's
# lowest live cell (rounds/env-r60s.ps1 for the rest). The founders keep round 59's D116 rule, as every arm does:
# D109's placement by the matter excludes it, and where founders start is the proposal's question 4.
$s = . (Join-Path $PSScriptRoot 'env-r60s.ps1')
$s.EVOSIM_MATTER_INFLUX = 1.2
$s.EVOSIM_MATTER_INFLUX_AT = 'islands'
$s.EVOSIM_MATTER_BURIAL = 0.001
$s
