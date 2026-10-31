# Phase 1 of D264, arm A (logbook/specs/r64-prereg/predictions.md): r63h's arm D with animals ten times as hard to kill
# and plants at a fifth of their health.
$s = . (Join-Path $PSScriptRoot 'env-r63h-d.ps1')
$s.EVOSIM_ANIMAL_HEALTH = 10
$s.EVOSIM_PLANT_HEALTH = 0.2
$s.EVOSIM_DEATH_ROW_INCOME = 1
$s
