# The structural mutation screen (logbook/specs/r62sm-prereg/predictions.md): rounds/env-r62p-t16.ps1's world (the limb
# push at 4.8, the work price at 0.03125, the newborn reserve at 0.5, the kill gut) with growth as a climb in 60 world
# seconds, the juvenile-strength switch off, the growth-reserve gene starting at 30 s in [1, 300] s mutating at 0.08,
# and every structural mutation chance at 1 times today's. World r62sm-s100-6201.
$s = . (Join-Path $PSScriptRoot 'env-r62p-t16.ps1')
$s.EVOSIM_GROWTH_ADULT = 60
$s.EVOSIM_GROWTH_RESERVE_START = 30
$s.EVOSIM_GROWTH_RESERVE_MIN = 1
$s.EVOSIM_GROWTH_RESERVE_MAX = 300
$s.EVOSIM_GROWTH_RESERVE_MUT = 0.08
$s.EVOSIM_ADD_NODE_CHANCE = 0.04
$s.EVOSIM_ADD_EDGE_CHANCE = 0.06
$s.EVOSIM_REMOVE_EDGE_CHANCE = 0.05
$s.EVOSIM_ADD_NEURON_CHANCE = 0.05
$s.EVOSIM_REMOVE_NEURON_CHANCE = 0.04
$s.EVOSIM_RECURSIVE_LIMIT_CHANCE = 0.03
$s.EVOSIM_JOINT_TYPE_CHANCE = 0.04
$s
