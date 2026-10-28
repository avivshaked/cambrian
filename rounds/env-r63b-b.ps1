# The bloom screen's arm B (logbook/specs/r63b-prereg/predictions.md): arm A with D251's island cycles. Each of the
# plan's islands draws its period from 2,500 to 6,000 s and its amplitude from 0.8 to 1.0, with the world's total
# influx held.
$s = . (Join-Path $PSScriptRoot 'env-r63b-a.ps1')
$s.EVOSIM_ISLAND_BLOOM = 'cycles'
$s.EVOSIM_BLOOM_PERIOD_MIN = 2500
$s.EVOSIM_BLOOM_PERIOD_MAX = 6000
$s.EVOSIM_BLOOM_AMPLITUDE_MIN = 0.8
$s.EVOSIM_BLOOM_AMPLITUDE_MAX = 1.0
$s.EVOSIM_BLOOM_HOLDS_TOTAL = 1
$s