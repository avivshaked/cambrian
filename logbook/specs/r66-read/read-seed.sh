#!/bin/bash
# r66's reads on one seed (logbook/specs/r66-prereg/predictions.md, "Readouts"): arms M, Mk and M4k against r65's C, each
# into scratch/r66/read-<seed>/, pinned to logical 30-31, BelowNormal.
# Usage, from the repository root: bash scratch/r66/read-seed.sh 6202
seed=$1
root=/d/Projects/experiments/evolution-simulator
out=$root/scratch/r66/read-$seed
mkdir -p $out
cd $root
py() { cmd //c start "" //b //wait //belownormal //affinity C0000000 python -I "$@"; }
C=runs/r65-c-$seed; M=runs/r66-m-$seed; K=runs/r66-mk-$seed; Q=runs/r66-m4k-$seed
echo "start $(date +%H:%M:%S)" > $out/done.txt
# Mark 1, the grazer floor from 12,000 s, with crossings beside it.
py scripts/reads/bloom/g2-read.py $C $M $K $Q --survive-from 12000 --crossing-gap 10 > $out/mark1-g2-read.txt 2>&1
# Mark 2, the leaf share of bite income over the last window.
py scripts/reads/bloom/income-split.py $C $M $K $Q --from 21000 --end 24000 > $out/mark2-income-split.txt 2>&1
# Mark 3, leaves and grazers against C.
CR=$(ls -d $C/*/ | tail -1)
for w in m mk m4k; do
  W=$(ls -d runs/r66-$w-$seed/*/ | tail -1)
  py scripts/reads/overshoot-read.py ${W%/} ${CR%/} --end 24000 > $out/mark3-leaves-$w.txt 2>&1
done
# Mark 4, the landed share of mouth blows, early and late.
py scratch/wt-mouth/scripts/reads/mouth/landed-share.py $(ls -d $M/*/ $K/*/ $Q/*/) --windows 12000-15000,21000-24000,0-24000 > $out/mark4-landed-share.txt 2>&1
# Mark 5, pace and throughput.
py scripts/reads/pace-calc.py runs r65-c-$seed r66-m-$seed r66-mk-$seed r66-m4k-$seed > $out/mark5-pace.txt 2>&1
py scripts/reads/smell/throughput.py $C $M $K $Q --last 6000 > $out/mark5-throughput.txt 2>&1
# Reported beside the marks.
for w in m mk m4k; do
  py scripts/reads/smell/first-bite.py runs/r66-$w-$seed --from 21000 --to 24000 > $out/first-bite-$w.txt 2>&1
done
py scripts/reads/smell/first-bite.py $C --from 21000 --to 24000 > $out/first-bite-c.txt 2>&1
py scripts/reads/bloom/grazer-deaths.py $C $M $K $Q > $out/grazer-deaths.txt 2>&1
py scripts/reads/bloom/cannibal.py $C $M $K $Q > $out/cannibal.txt 2>&1
echo "end $(date +%H:%M:%S)" >> $out/done.txt
