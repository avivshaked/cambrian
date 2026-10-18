# Round 59's world for the leaf smell's card timing (D174; logbook/specs/r59-leaf-timing-plan.md): round 57's
# world (rounds/env-r57.ps1), which round 58 ran, with round 59's pieces as D174 sets them: the drag on turning in
# its spin form, the touch of the rock and its sense, the leaf smell in its direction form, and no anchoring cell
# (D171). The reach is 6 m, D174's choice; the timing sets 3 m on the command line. The smell's cell and half
# scale are the farm's defaults, as r59smoke-t16 ran them, until the pre-registration names its own.
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r57.ps1')
$s.EVOSIM_TURNING_DRAG = 'spin'
$s.EVOSIM_SPIN_DRAG = 0.5
$s.EVOSIM_SENSE_BED_TOUCH = 1
$s.EVOSIM_LEAF_SCENT = 'direction'
$s.EVOSIM_LEAF_SCENT_RANGE = 6
$s.EVOSIM_LEAF_SCENT_CELL = 2
$s.EVOSIM_LEAF_SCENT_HALF = 1
$s
