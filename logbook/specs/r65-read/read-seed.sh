#!/bin/bash
# r65's reads on one seed's three worlds, each into scratch/r65/read-<seed>/, pinned to logical 30-31, BelowNormal.
# Usage, from the repository root: bash scratch/r65/read-seed.sh 6202
seed=$1
root=/d/Projects/experiments/evolution-simulator
out=$root/scratch/r65/read-$seed
mkdir -p $out
cd $root
py() { cmd //c start "" //b //wait //belownormal //affinity C0000000 python -I "$@"; }
C=runs/r65-c-$seed; R=runs/r65-r-$seed; O=runs/r65-o-$seed
echo "start $(date +%H:%M:%S)" > $out/done.txt
py scripts/reads/bloom/income-split.py $C $R $O --from 21000 --end 24000 > $out/mark1-income-split.txt 2>&1
py scripts/reads/bloom/g2-read.py $C $R $O --survive-from 12000 --crossing-gap 10 > $out/mark2-g2-read.txt 2>&1
for w in c r o; do
  py scripts/reads/smell/far-share.py runs/r65-$w-$seed --from 12000 --to 24000 > $out/far-share-$w.txt 2>&1
  py scripts/reads/smell/gather-lag.py runs/r65-$w-$seed --from 12000 --to 24000 > $out/mark3-gather-lag-$w.txt 2>&1
  py scripts/reads/smell/first-bite.py runs/r65-$w-$seed --from 21000 --to 24000 > $out/first-bite-$w.txt 2>&1
done
py scripts/reads/smell/throughput.py $C $R $O --last 6000 > $out/mark4-throughput.txt 2>&1
py scripts/reads/pace-calc.py runs r65-c-$seed r65-r-$seed r65-o-$seed > $out/mark4-pace.txt 2>&1
for w in r o; do
  py scripts/reads/smell/plume-bench.py runs/r65-$w-$seed --seconds 21000,22500,24000 > $out/plume-reading-$w.txt 2>&1
  W=$(ls -d runs/r65-$w-$seed/*/ | tail -1); CR=$(ls -d $C/*/ | tail -1)
  py scripts/reads/overshoot-read.py ${W%/} ${CR%/} --end 24000 > $out/leaves-$w.txt 2>&1
done
echo "end $(date +%H:%M:%S)" >> $out/done.txt
