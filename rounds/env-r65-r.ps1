# The smell screen's realistic arm R (logbook/specs/r65-prereg/predictions.md; logbook/specs/smell-plume-design.md): the
# carried leaf smell as a strength alone and the current sense, on C's world under true-shape contact. The plume's
# half-scale is the founding bench's (rounds/bench-r65.ps1, 2026-11-01): the median of plume-bench.py's downstream figure
# over r65bench-r-6202's dumps from 4,500 to 6,000 s (logbook/specs/r65-prereg/bench/plume-bench.txt),
# 1.31 m2 s/m3; r65bench-r-6203 gave 1.61.
$s = . (Join-Path $PSScriptRoot 'env-r65-c.ps1')
$s.EVOSIM_LEAF_PLUME = 'strength'
$s.EVOSIM_CURRENT_SENSE = 1
$s.EVOSIM_LEAF_PLUME_HALF = 1.31
$s
