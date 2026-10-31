# D270's realistic arm R (logbook/specs/smell-plume-design.md, "A screen of five worlds"): the carried leaf smell as a strength
# alone, and the current sense, on the base world below. The base is r64's arm D, which the identity sitting founds; the
# screen's own world is named when r64's read chooses it, and then this line is the one to change. Cell, lifetime and the
# half-scale of the current sense are the build's defaults (5 m, 3,000 s, 0.01 m/s). The plume's half-scale (1 m2 s/m3) is
# the default until the founding bench sets it from the 1,200 s dump of this world.
$s = . (Join-Path $PSScriptRoot 'env-r64-d.ps1')
$s.EVOSIM_LEAF_PLUME = 'strength'
$s.EVOSIM_CURRENT_SENSE = 1
$s
