# The r62 grazer screen's eating arm (the owner's, 2026-10-19): rounds/env-r62g.ps1 with every mouth taking at four
# times the rate its Intake gene and area grant (EVOSIM_INTAKE_SCALE, RunConfig.IntakeScale). The mouth's price stays
# on the gene.
$s = . (Join-Path $PSScriptRoot 'env-r62g.ps1')
$s.EVOSIM_INTAKE_SCALE = 4
$s
