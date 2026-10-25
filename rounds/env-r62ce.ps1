# D219-D221's contact-eating screen (logbook/specs/r62ce-prereg/predictions.md): rounds/env-r62-h.ps1's world, round
# 62's at a 3 m reach with a 500 s corpse half-life, with contact eating on and the remains' window at its default of
# 60 s. With the dial on no corpse is made, so the reach and the half-life are read by nothing and stay as round 62's,
# and the config differs from the controls' only in these two lines. Worlds r62ce-6201, r62ce-6202 and r62ce-6203.
$s = . (Join-Path $PSScriptRoot 'env-r62-h.ps1')
$s.EVOSIM_CONTACT_EATING = 1
$s.EVOSIM_REMAINS_WINDOW = 60
$s