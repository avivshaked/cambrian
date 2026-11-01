# The mouth screen's arm M (logbook/specs/r66-prereg/predictions.md): r65's control C, D's world under true-shape contact
# with the smell off (rounds/env-r65-c.ps1), with D275's rule on. A mouth's blow wounds and feeds only where its mouth
# point lies within one mouth radius of the bitten part's true surface. Every other dial is C's.
$s = . (Join-Path $PSScriptRoot 'env-r65-c.ps1')
$s.EVOSIM_BITE_NEEDS_MOUTH = 1
$s
