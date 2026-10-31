# Phase 1 of D264, arm E (logbook/specs/r64-prereg/predictions.md): arm D with the mouths back at r63g's arm B prices,
# ten times r63h's: the first step of dialling back.
$s = . (Join-Path $PSScriptRoot 'env-r63h-d.ps1')
$s.EVOSIM_ANIMAL_HEALTH = 10
$s.EVOSIM_PLANT_HEALTH = 0.2
$s.EVOSIM_NEWBORN_RESERVE = 0.8
$s.EVOSIM_OFFSPRING_DISPERSAL = 30
$s.EVOSIM_UPKEEP_CONSUMER = 0.6
$s.EVOSIM_PRICE_ATTACK = 0.01
$s.EVOSIM_PRICE_INTAKE = 0.01
$s.EVOSIM_DEATH_ROW_INCOME = 1
$s
