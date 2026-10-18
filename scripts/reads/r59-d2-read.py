"""Round 59's clause D2 on a pool bench (r59-d2-s2, r59-d2-s3): the smell knockout's paired cost on each genome's eating against its own
brain, its mean and standard error over the genomes, and whether the cost is at least two standard errors.

    python scripts/reads/r59-d2-read.py <bench run folder>
"""
import json
import math
import sys

rows = [json.loads(l) for l in open(sys.argv[1] + '/tank-bench.jsonl', encoding='utf-8') if l.strip()]
brains = list(dict.fromkeys(r['brain'] for r in rows))
eaten = {b: {r['genome']: r['eaten'] for r in rows if r['brain'] == b} for b in brains}
own, ko = brains[0], brains[-1]
gs = sorted(eaten[own])
for b in brains:
    v = [eaten[b][g] for g in gs]
    print('%-40s eaten mean %.2f J over %d genomes' % (b, sum(v) / len(v), len(v)))
d = [eaten[own][g] - eaten[ko][g] for g in gs]
m = sum(d) / len(d)
se = math.sqrt(sum((x - m) ** 2 for x in d) / (len(d) - 1) / len(d))
print('knockout cost on eating: %.2f J, se %.2f, %.1f se; %s; genomes it costs %d, helps %d, equal %d' %
      (m, se, m / se if se else float('inf'), 'MET (>= 2 se)' if m >= 2 * se else 'NOT MET (< 2 se)',
       sum(x > 0 for x in d), sum(x < 0 for x in d), sum(x == 0 for x in d)))
