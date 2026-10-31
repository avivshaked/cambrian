# Phase 1 of D264, arm C (logbook/specs/r64-prereg/predictions.md): r63h's arm D with the young spread over a 30 m
# disc round their parent, against 5 m.
$s = . (Join-Path $PSScriptRoot 'env-r63h-d.ps1')
$s.EVOSIM_OFFSPRING_DISPERSAL = 30
$s.EVOSIM_DEATH_ROW_INCOME = 1
$s
