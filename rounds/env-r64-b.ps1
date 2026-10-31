# Phase 1 of D264, arm B (logbook/specs/r64-prereg/predictions.md): r63h's arm D with newborns holding 0.8 of their
# start as reserve, against 0.5.
$s = . (Join-Path $PSScriptRoot 'env-r63h-d.ps1')
$s.EVOSIM_NEWBORN_RESERVE = 0.8
$s.EVOSIM_DEATH_ROW_INCOME = 1
$s
