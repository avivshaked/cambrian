# Round 60's screens, their common world (D175; proposals/propose-r60-patchy-matter.md §5): round 59's world as
# D174 sets it, on round 57's settings (rounds/env-r57.ps1). The drag on turning in its spin form, the touch of the
# rock and its sense, the leaf smell in its direction form at 6 m with the farm's 2 m cell and 1 m2 half scale, and
# no anchoring cell (D171). The same block as the leaf timing's rounds/env-r59l.ps1 on r59-leaftime.
# Each arm's block (env-r60s-c.ps1, -i1, -i2, -i3) starts from this one.
# The shape scripts/run-farm.ps1 -Launcher and scripts/card/card-group.ps1 -Launcher want.
$s = . (Join-Path $PSScriptRoot 'env-r57.ps1')
$s.EVOSIM_TURNING_DRAG = 'spin'
$s.EVOSIM_SPIN_DRAG = 0.5
$s.EVOSIM_SENSE_BED_TOUCH = 1
$s.EVOSIM_LEAF_SCENT = 'direction'
$s.EVOSIM_LEAF_SCENT_RANGE = 6
$s.EVOSIM_LEAF_SCENT_CELL = 2
$s.EVOSIM_LEAF_SCENT_HALF = 1
$s
