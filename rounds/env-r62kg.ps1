# D232's kill-meal screen (logbook/specs/r62kg-prereg/predictions.md): rounds/env-r62cs-w500.ps1's world (the store
# share on, the remains' window at 500 s), with a kill swallowed whole into the killer's gut (EVOSIM_KILL_GUT) and the
# intake scale at 16, which under contact eating lifts only the mouth's cap. The bite damage stays at 1. Worlds
# r62kg-6201 and r62kg-6202.
$s = . (Join-Path $PSScriptRoot 'env-r62cs-w500.ps1')
$s.EVOSIM_KILL_GUT = 1
$s.EVOSIM_INTAKE_SCALE = 16
$s
