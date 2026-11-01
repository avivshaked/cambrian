# The smell screen's realistic arm R (logbook/specs/r65-prereg/predictions.md; logbook/specs/smell-plume-design.md): the
# carried leaf smell as a strength alone and the current sense, on C's world under true-shape contact. The plume's
# half-scale is the build's default (1) until the founding bench sets it; that line is then added here, with the bench's
# run named beside it.
$s = . (Join-Path $PSScriptRoot 'env-r65-c.ps1')
$s.EVOSIM_LEAF_PLUME = 'strength'
$s.EVOSIM_CURRENT_SENSE = 1
$s
