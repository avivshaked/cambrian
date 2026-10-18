# Round 60's screen r60s-i3 (D175): r60s-i1 with burial at 0.003/s, the dose (rounds/env-r60s-i1.ps1 for the rest).
$s = . (Join-Path $PSScriptRoot 'env-r60s-i1.ps1')
$s.EVOSIM_MATTER_BURIAL = 0.003
$s
