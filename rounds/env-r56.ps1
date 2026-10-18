# Round 56's environment block (proposals/propose-r56-light-cycles.md, ruled by the owner on 2026-10-04):
# round 55's world (rounds/env-r55.ps1, every dial as it is there, the pool and its shares included) with
# the light cycled: D035's day of 1,000 s and the season of 10,000 s, each at amplitude 0.5, so the light
# runs between a quarter and two and a quarter times its mean and averages to it over each season.
# The control seed overrides the pool on its command line as round 55's did: EVOSIM_TRICKLE_POOL
# '@<tree>/rounds/r55-pool-control.tsv', EVOSIM_TRICKLE_POOL_SHARE 0.1 and EVOSIM_FLOOR_POOL_SHARE 0.
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r55.ps1')
$s.EVOSIM_DAY_AMPLITUDE = 0.5
$s.EVOSIM_DAY_LENGTH = 1000
$s.EVOSIM_SEASON_AMPLITUDE = 0.5
$s.EVOSIM_SEASON_LENGTH = 10000
$s
