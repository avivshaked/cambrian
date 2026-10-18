# Round 59's second price smoke (D178): the planted world (rounds/env-r59-planted.ps1) with a mouth's equipment at a
# sixth of its old price, against the third the round carries: the attack and intake prices at 0.0167 W a unit and
# the consumer cell's upkeep at 1 W/m3. A smoke's block, not the round's unless the smokes choose it.
$s = . (Join-Path $PSScriptRoot 'env-r59-planted.ps1')
$s.EVOSIM_PRICE_ATTACK = 0.0167
$s.EVOSIM_PRICE_INTAKE = 0.0167
$s.EVOSIM_UPKEEP_CONSUMER = 1
$s
