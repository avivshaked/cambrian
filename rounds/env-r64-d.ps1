# Phase 1 of D264, arm D (logbook/specs/r64-prereg/predictions.md): arms A, B and C together on r63h's arm D.
$s = . (Join-Path $PSScriptRoot 'env-r63h-d.ps1')
$s.EVOSIM_ANIMAL_HEALTH = 10
$s.EVOSIM_PLANT_HEALTH = 0.2
$s.EVOSIM_NEWBORN_RESERVE = 0.8
$s.EVOSIM_OFFSPRING_DISPERSAL = 30
$s.EVOSIM_DEATH_ROW_INCOME = 1
$s
