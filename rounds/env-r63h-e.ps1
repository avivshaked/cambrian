# r63h's arm E (logbook/specs/r63h-prereg/predictions.md): arm D with no waste on a meal.
$s = . (Join-Path $PSScriptRoot 'env-r63c-b.ps1')
$s.EVOSIM_UPKEEP_CONSUMER = 0.06
$s.EVOSIM_PRICE_ATTACK = 0.001
$s.EVOSIM_PRICE_INTAKE = 0.001
$s.EVOSIM_INTAKE_WASTE = 0
$s
