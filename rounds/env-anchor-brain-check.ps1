# D268's check world (logbook/specs/anchor-brain-build.md, "After the build"): r64's arm A with the anchoring cell in the
# table at bud weight 1 as env-r59g.ps1 sets it, the touch of the rock and its sense, and the brain's hold of the grip
# on (EVOSIM_ANCHOR_BRAIN). Run by the card-group launchers for the switch-on founded check on the CPU accelerator and
# for the card runs at groups 32 and 64.
# The shape scripts/run-farm.ps1 -Launcher wants; overrides go on the command line.
$s = . (Join-Path $PSScriptRoot 'env-r64-a.ps1')
$s.EVOSIM_TURNING_DRAG = 'spin'
$s.EVOSIM_SPIN_DRAG = 0.5
$s.EVOSIM_SENSE_BED_TOUCH = 1
$s.EVOSIM_ANCHORING = 1
$s.EVOSIM_ANCHOR_BUD_WEIGHT = 1
$s.EVOSIM_ANCHOR_BRAIN = 1
$s
