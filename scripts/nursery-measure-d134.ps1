# D134's measurements (logbook/specs/fast-nursery-proposal.md §5, logbook/specs/fast-nursery-measurements.md),
# run once nursery run 2 has ended, at four threads, one job at a time. Writes under scratch/nursery/measure/.
param([int]$Threads = 4)
$main = 'D:\Projects\experiments\evolution-simulator'
$exe = "$main\scratch\wt-nursery\artifacts\Evosim.Nursery\bin\Release-b\Evosim.Nursery.exe"
$out = "$main\scratch\nursery\measure"
New-Item -ItemType Directory -Force $out | Out-Null
$cfg = @{
  small = (Get-ChildItem "$main\scratch\nursery\runs\nursery-small\*\config.json").FullName
  still = (Get-ChildItem "$main\scratch\nursery\runs\nursery-small-still\*\config.json").FullName
  nomix = (Get-ChildItem "$main\scratch\nursery\runs\nursery-small-nomix\*\config.json").FullName
  big   = (Get-ChildItem "$main\scratch\nursery\runs\nursery-cfg-s\*\config.json").FullName
}
$srcs = @('r52-s1','r52-s2') | ForEach-Object { (Get-ChildItem "$main\runs\$_\*\run.json" | Sort-Object LastWriteTime | Select-Object -Last 1).DirectoryName }
$srcs += (Get-ChildItem "$main\runs\r52-s3j" -Directory | Select-Object -First 1).FullName
$common = @('--source', ($srcs -join ','), '--at', ((2..30 | ForEach-Object { $_ * 1000 }) -join ','), '--bodies', '10', '--threads', $Threads)
$fine = @('--corpse-total', '200', '--corpse-count', '25,100')
$coarse = @('--corpses', '5', '--corpse-joules', '40')
function Run($name, [string[]]$a) {
  $t = Get-Date
  "$(Get-Date -Format HH:mm:ss) == $name" | Tee-Object -Append "$out\measure.log"
  & $exe @common @a --out "$out\$name" 2>&1 | Select-Object -Last 14 | Tee-Object -Append "$out\measure.log"
  "  ($([int]((Get-Date) - $t).TotalSeconds) s)" | Tee-Object -Append "$out\measure.log"
}
# 1. Where the wall goes: one episode on one thread in each config.
foreach ($k in 'small','still','nomix') { Run "m1-$k" (@('--config', $cfg[$k], '--seconds', '300', '--above-bed', '0.5', '--threads', '1', '--time-one') + $fine) }
Run 'm1-big-300' (@('--config', $cfg.big, '--seconds', '300', '--above-bed', '0.5', '--threads', '1', '--time-one') + $fine)
Run 'm1-big-900' (@('--config', $cfg.big, '--seconds', '900', '--threads', '1', '--time-one') + $coarse)
# 2. The hit rate: every ancestor on 32 episodes (ancestors.jsonl), in five conditions.
$m2 = @('--generations', '0', '--episodes', '32', '--final-episodes', '2')
Run 'm2-small-fine-bed'    (@('--config', $cfg.small, '--seconds', '300', '--above-bed', '0.5') + $fine + $m2)
Run 'm2-small-coarse-bed'  (@('--config', $cfg.small, '--seconds', '300', '--above-bed', '0.5') + $coarse + $m2)
Run 'm2-small-fine-high'   (@('--config', $cfg.small, '--seconds', '300', '--above-bed', '1.5') + $fine + $m2)
Run 'm2-still-fine-bed'    (@('--config', $cfg.still, '--seconds', '300', '--above-bed', '0.5') + $fine + $m2)
Run 'm2-big-coarse-d133'   (@('--config', $cfg.big, '--seconds', '900', '--above-bed', '1.5') + $coarse + $m2)
# 3. The positive control: the kinesis graft against its own knockout on 32 held-out episodes.
$m3 = @('--generations', '0', '--episodes', '2', '--final-episodes', '32')
foreach ($g in '0.8,0.8', '0.8,3') {
  $tag = $g -replace ',', '-'
  Run "m3-small-$tag" (@('--config', $cfg.small, '--seconds', '300', '--above-bed', '0.5', '--graft-kinesis', $g) + $fine + $m3)
  Run "m3-still-$tag" (@('--config', $cfg.still, '--seconds', '300', '--above-bed', '0.5', '--graft-kinesis', $g) + $fine + $m3)
}
"$(Get-Date -Format HH:mm:ss) == done" | Tee-Object -Append "$out\measure.log"