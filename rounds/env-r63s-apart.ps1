# G5's arm "on" (env-r63s-on.ps1 beside this file) with the coincident-contact switch on: sphere contact whose coincident
# pairs are pushed apart along X by organism id (ContactShapeForm.SpheresApart, D270). The smoke of
# rounds/coincident-smoke.ps1 runs it.
$s = . (Join-Path $PSScriptRoot 'env-r63s-on.ps1')
$s.EVOSIM_CONTACT_SHAPE = 'spheresapart'
$s
