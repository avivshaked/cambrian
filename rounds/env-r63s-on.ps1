# The shape screen's arm "on" (G5, logbook/specs/r63s-prereg/predictions.md, D252, D255): the film's world, r63c's
# arm B (rounds/env-r63c-b.ps1, D259), with D229's fin law on, the superellipsoid in the world's shape list, and its
# three form genes walking at 0.08 a node, ScalarChance's value. The arm "off" is r63c-b's own runs on these seeds.
$s = . (Join-Path $PSScriptRoot 'env-r63c-b.ps1')
$s.EVOSIM_SHAPED_ADDED_MASS = 1
$s.EVOSIM_SHAPES = 'box,sphere,capsule,superellipsoid'
$s.EVOSIM_FORM_CHANCE = 0.08
$s
