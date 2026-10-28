# The bloom screen's arm A (logbook/specs/r63b-prereg/predictions.md): r62cf's world (rounds/env-r62cf.ps1) with D245's
# energy sense as D249 requires of every round after fat's screen, HalfScale with k = 30 s. Every other new switch is
# off, set here explicitly so that no arm leans on a default.
$s = . (Join-Path $PSScriptRoot 'env-r62cf.ps1')
$s.EVOSIM_ENERGY_SQUASH = 'halfscale'
$s.EVOSIM_ENERGY_HALF_SCALE = 30
$s.EVOSIM_ISLAND_BLOOM = 'none'
$s.EVOSIM_FAR_SMELL = 'none'
$s.EVOSIM_FAT = 0
$s.EVOSIM_RESERVE_CAP_FORM = 'seconds'
$s.EVOSIM_RESERVE_CAP = 0
$s