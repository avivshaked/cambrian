# Round 59's world (D174, D176, D177): round 59's leaf-timing world (rounds/env-r59l.ps1) with a mouth's equipment at
# a third of its price. That world is round 57's with the drag on turning in its spin form at 0.5, the touch of the
# rock and its sense, the leaf smell in its direction form at 6 m (cell 2 m, half scale 1 m2) and no anchoring cell.
# D177 adds the attack and intake prices at 0.033 W a unit and the consumer cell's upkeep at 2 W/m3.
# This is seed 1's block, the control: round 58's trickle pool (rounds/r55-pool-control.tsv, the four stomachs, at
# 0.1 of the trickle) and nothing from the pool at the floor, as env-r57.ps1 sets them. Seeds 2 and 3 are
# rounds/env-r59-planted.ps1. The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r59l.ps1')
$s.EVOSIM_PRICE_ATTACK = 0.033
$s.EVOSIM_PRICE_INTAKE = 0.033
$s.EVOSIM_UPKEEP_CONSUMER = 2
$s
