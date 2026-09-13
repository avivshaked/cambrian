$cols = @('alive','cols','cols abs','x sd','p0','p1','p2','p3','jnt inh','mean m/s','diverged','wraps','crowded','stillb','audit','mat resid','det patch sd','patch max share','corpses','det deep','absorpt','inherit','depth m','depth sd','jointed','shade %')
foreach ($a in @('r37b-s1','r37b-s2','r37b-s3','r37b-s4','r37b-s5')) {
  & "$PSScriptRoot/../../../scripts/analyse-arm.ps1" $a -Timeline -Every 1000 -From 1000 -To 30000 -Columns $cols
}
