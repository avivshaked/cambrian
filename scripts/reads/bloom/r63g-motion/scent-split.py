"""Grazers that read the leaf smell against those that do not: leafscent.jsonl's gread and gctl, 6,000-24,000 s.
Usage: python -I scripts/reads/bloom/r63g-motion/scent-split.py RUN LABEL"""
import json, os, sys
run, label = sys.argv[1], sys.argv[2]
acc = {'gread': [0, 0.0, 0.0], 'gctl': [0, 0.0, 0.0]}
for line in open(os.path.join(run, 'leafscent.jsonl'), encoding='utf-8'):
    r = json.loads(line)
    if not (6000 <= r['t'] <= 24000):
        continue
    for k in acc:
        n, c, v = r[k]
        if n:
            acc[k][0] += n; acc[k][1] += n * c; acc[k][2] += n * v
print(label, ' '.join(f'{k}: n {n} mean cos {c/n:+.3f} mean speed {100*v/n:.1f} cm/s' for k, (n, c, v) in acc.items() if n))
