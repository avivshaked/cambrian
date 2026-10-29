# The bloom rerun's arm B (logbook/specs/r63c-prereg/predictions.md, D256): r63b's arm B with the plan's fallback, each
# island's period drawn from 5,000 to 10,000 s instead of 2,500 to 6,000 s. Everything else is r63b's B.
$s = . (Join-Path $PSScriptRoot 'env-r63b-b.ps1')
$s.EVOSIM_BLOOM_PERIOD_MIN = 5000
$s.EVOSIM_BLOOM_PERIOD_MAX = 10000
$s
