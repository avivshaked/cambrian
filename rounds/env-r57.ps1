# Round 57's environment block (D151, proposals/propose-r57-weather.md, ruled by the owner on 2026-10-05):
# round 56's world (rounds/env-r56.ps1) with every seed as round 56's control seed was (the four stomachs
# of rounds/r55-pool-control.tsv, trickle share 0.1, no floor share, so no inoculation), and the light
# made harsher and less predictable: a two-season year of 6,000 s (three summer days at 1.6 of the mean
# light, three winter days at 0.4), switched at midnights over a 250 s ramp, under D035's day of 1,000 s at
# amplitude 0.5, and a weather factor a day, log-normal with mean 1, log s.d. 0.3 and day-to-day
# correlation 0.5, from its own seed, so every seed of the round has the same sky.
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r56.ps1')
$s.EVOSIM_TRICKLE_POOL = '@' + (Join-Path $PSScriptRoot 'r55-pool-control.tsv').Replace([string][char]92, '/')
$s.EVOSIM_TRICKLE_POOL_SHARE = 0.1
$s.EVOSIM_FLOOR_POOL_SHARE = 0
$s.EVOSIM_SEASON_SHAPE = 'two-season'
$s.EVOSIM_SEASON_LENGTH = 6000
$s.EVOSIM_SEASON_AMPLITUDE = 0.6
$s.EVOSIM_SEASON_RAMP = 250
$s.EVOSIM_WEATHER_SPREAD = 0.3
$s.EVOSIM_WEATHER_CORRELATION = 0.5
$s.EVOSIM_WEATHER_SEED = 57
$s
