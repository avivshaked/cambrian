# Round 60's desert screen r60s-d005 (D190): r60s-big3m's world and dose (3.6 units/s, 45,000 units) with the
# desert's matter buried at 0.005/s, five times the islands' 0.001/s. The desert is the water outside the
# islands' influx plan (RunConfig.MatterBurialDesertPerSecond). Seed 1 only.
$s = . (Join-Path $PSScriptRoot 'env-r60s-big3m.ps1')
$s.EVOSIM_MATTER_BURIAL_DESERT = 0.005
$s
