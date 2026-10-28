# The bloom screen's arm D (logbook/specs/r63b-prereg/predictions.md): arm C with D247's fat in the cell table at its
# specification's room of 50,000 J/m3, and the reserve cap in D247's form, P + R + s x W + room, at s = 300 s.
$s = . (Join-Path $PSScriptRoot 'env-r63b-c.ps1')
$s.EVOSIM_FAT = 1
$s.EVOSIM_FAT_ROOM = 50000
$s.EVOSIM_RESERVE_CAP_FORM = 'bodyandstore'
$s.EVOSIM_RESERVE_CAP = 300
$s