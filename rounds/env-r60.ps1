# Round 60's world (logbook/specs/r60-prereg/0142-draft.md until its entry lands): r60s-big3's world (rounds/env-r60s-big3.ps1,
# D185) at the round's dose and desert burial (D190, D191). Both are the pre-registration's to fill: until $influx,
# $budget and $desert below are set, the block refuses, so no world can be founded on settings nobody chose. $desert
# is the desert's burial rate per second (RunConfig.MatterBurialDesertPerSecond), or -1 for none of its own, the
# islands' 0.001/s everywhere (D191's last case). rounds/r60.ps1 reads all three back from each world's config.json.
# Seeds 6001, 6002 and 6003 (D180), given by the launcher.
$influx = 3.6   # units/s
$budget = 45000   # units
$desert = 0.02   # 1/s, or -1
if ($null -eq $influx -or $null -eq $budget -or $null -eq $desert) { throw 'rounds/env-r60.ps1: the dose or the desert burial is not set; fill $influx, $budget and $desert from the pre-registration' }
$s = . (Join-Path $PSScriptRoot 'env-r60s-big3.ps1')
$s.EVOSIM_MATTER_INFLUX = $influx
$s.EVOSIM_MATTER_BUDGET = $budget
$s.EVOSIM_MATTER_BURIAL_DESERT = $desert
$s
