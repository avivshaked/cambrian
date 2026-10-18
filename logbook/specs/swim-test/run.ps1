$m = 'D:\Projects\experiments\evolution-simulator'
$exe = "$m\scratch\wt-nursery\artifacts\Evosim.Nursery\bin\Release-b\Evosim.Nursery.exe"
$cfg = (Get-ChildItem "$m\scratch\nursery\runs\nursery-small-still\*\config.json").FullName
$at = (2..30 | ForEach-Object { $_ * 1000 }) -join ','
$r52 = @('r52-s1','r52-s2') | ForEach-Object { (Get-ChildItem "$m\runs\$_\*\run.json" | Sort-Object LastWriteTime | Select-Object -Last 1).DirectoryName }
$r52 += (Get-ChildItem "$m\runs\r52-s3j" -Directory | Select-Object -First 1).FullName
$r53 = @('r53-s1','r53-s2','r53-s3') | ForEach-Object { (Get-ChildItem "$m\runs\$_" -Directory | Select-Object -First 1).FullName }
foreach ($job in @(@{n='r52'; s=$r52}, @{n='r53'; s=$r53})) {
  $out = "$m\scratch\swimtest\$($job.n)"
  & $exe --source ($job.s -join ',') --at $at --diet jointed --config $cfg --out $out --bodies 30 --seconds 300 --swim-test --swim-seeds 3 --swim-freqs '0.5,1,2' --threads 4 *> "$out.log"
}
'done' | Set-Content "$m\scratch\swimtest\DONE"
