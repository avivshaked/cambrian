$cols = @('alive','cols','cols abs','x sd','p0','p1','p2','p3','absorpt','inherit','jnt inh','mean m/s','diverged','wraps','crowded','stillb','births','mat blk','mat short','audit','mat resid','det cv','mat cv','det deep','corpses','floor low %','floor J','% on floor','floor con','refuge J','depth m')
foreach ($a in @('r39-s1','r39-s2','r39-s3','r39-s4','r39-s5')) {
  & "$PSScriptRoot/../../../scripts/analyse-arm.ps1" $a -Timeline -Every 1000 -From 1000 -To 30000 -Columns $cols
}
