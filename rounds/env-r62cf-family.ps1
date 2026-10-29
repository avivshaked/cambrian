# D233's shape family in the confirmation round's world under D229's fin law (rounds/env-r62cf-fin.ps1): the
# superellipsoid joins the world's shape list, so a node that mutates its shape can become a family part, and its
# three form genes walk at a chance of 0.05. The founders are r62cf's, all of the standard three shapes. The family
# arm of the shape build's card sitting (D255, G4): groups 32 and 64 for identity, and group 32 timed beside the
# fin-law arm for G0's 15 % pace gate. The form chance is the sitting's, not a ruled value for G5.
$s = . (Join-Path $PSScriptRoot 'env-r62cf-fin.ps1')
$s.EVOSIM_SHAPES = 'box,sphere,capsule,superellipsoid'
$s.EVOSIM_FORM_CHANCE = 0.05
$s
