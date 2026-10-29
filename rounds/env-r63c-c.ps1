# The bloom rerun's arm C (logbook/specs/r63c-prereg/predictions.md, D256): this rerun's arm B with r63b's far smell,
# G = 0.005, a grove band of 10 m and a reach of 30 m beyond it (logbook/specs/bloom-census/).
$s = . (Join-Path $PSScriptRoot 'env-r63c-b.ps1')
$s.EVOSIM_FAR_SMELL = 'leaves'
$s.EVOSIM_FAR_SMELL_GAIN = 0.005
$s.EVOSIM_FAR_SMELL_BAND = 10
$s.EVOSIM_FAR_SMELL_REACH = 30
$s
