# The gripping grazer control's world (logbook/specs/grazer-grip-control-plan.md): round 57's world
# (rounds/env-r57.ps1), which round 58 ran, with round 59's pieces on as r59-build/smoke.ps1 sets them: the drag
# on turning in its spin form, the touch of the rock and its sense, and the anchoring cell in the table at bud
# weight 1, where D165 starts it. The leaf smell stays off: the owner rules its form after the nursery's arms
# (D167), the card's fill of the leaf field waits for its timing, and a brainless grazer cannot smell.
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r57.ps1')
$s.EVOSIM_TURNING_DRAG = 'spin'
$s.EVOSIM_SPIN_DRAG = 0.5
$s.EVOSIM_SENSE_BED_TOUCH = 1
$s.EVOSIM_ANCHORING = 1
$s.EVOSIM_ANCHOR_BUD_WEIGHT = 1
$s
