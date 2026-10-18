# Round 60's screen r60s-i2 (D175): r60s-i1 with D071's settling at 0.02 m/s, a stronger export
# (rounds/env-r60s-i1.ps1 for the rest).
$s = . (Join-Path $PSScriptRoot 'env-r60s-i1.ps1')
$s.EVOSIM_MATTER_SINK = 0.02
$s
