# The r62 grazer screen's endowment arm: rounds/env-r62g.ps1 with every founder born holding 2,400 s of its own
# standing cost, four times 600. The endowment has no form for mouthed founders alone, so floor, trickle and pool
# founders all get it, under D124's cap as it stands.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_FOUNDER_ENDOWMENT = 2400
$s
