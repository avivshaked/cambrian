$cols = @('alive','cols','x sd','p0','p1','p2','p3','jnt inh','mean m/s','diverged','wraps','crowded','audit','mat resid','depth m','depth sd','jointed','mat blk','food %','absorpt')
foreach ($a in @('r37-s1','r37-s2','r37-s3','r37-s4','r37-s5','r36-s1','r36-s2','r36-s3','r36-s4','r36-s5')) {
  & "$PSScriptRoot/../../../scripts/analyse-arm.ps1" $a -Timeline -Every 1000 -From 1000 -To 30000 -Columns $cols
}
