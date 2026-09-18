# -*- coding: utf-8 -*-
"""Where each boom's eaters came from: the foundings of the absorptive trait, streamed from
lineage.jsonl. A founding is an absorptive birth whose parent was not absorptive (a founder,
kind "f", counts as a founding when it is absorptive). 0101 found that each of round 38's
booms was a fresh line; this says whether round 39's are too.
"""
import json, glob, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RUNS = {'r39-s1': '2026-09-15-213428-8eab1085', 'r39-s2': '2026-09-15-213503-8eab1085',
        'r39-s3': '2026-09-17-085758-8eab1085', 'r39-s4': '2026-09-17-085833-8eab1085',
        'r39-s5': '2026-09-17-085303-8eab1085'}

out = ['## Foundings of the absorptive trait per seed, from lineage.jsonl birth rows only'
       ' (e == "b"). "in the first boom\'s window" is any founding before the first boom\'s'
       ' peak; the peaks are booms.tsv\'s.',
       'arm\tbirths\tabsorptive births\tfoundings of the trait\tfirst founding t'
       '\tfoundings before 5,000 s\tbefore 15,000 s\tafter 20,000 s\tlast founding t']
for a in sorted(RUNS):
    p = os.path.join(ROOT, 'runs', a, RUNS[a], 'lineage.jsonl')
    parent, absf, born = {}, {}, {}
    nb = na = 0
    for line in open(p, encoding='utf-8'):
        line = line.strip()
        if not line.startswith('{'):
            continue
        r = json.loads(line)
        if r.get('e') != 'b':
            continue
        nb += 1
        i = r['id']
        parent[i] = r.get('p', -1)
        absf[i] = r.get('abs', 0)
        born[i] = r['t']
        na += 1 if absf[i] else 0
    founds = []
    for i, v in absf.items():
        if not v:
            continue
        pa = parent.get(i, -1)
        if pa is None or pa < 0 or not absf.get(pa, 0):
            founds.append(born[i])
    founds.sort()
    out.append('%s\t%d\t%d\t%d\t%s\t%d\t%d\t%d\t%s' % (
        a, nb, na, len(founds), ('%.1f' % founds[0]) if founds else '-',
        sum(1 for t in founds if t < 5000), sum(1 for t in founds if t < 15000),
        sum(1 for t in founds if t >= 20000),
        ('%.1f' % founds[-1]) if founds else '-'))

with open(os.path.join(HERE, 'foundings.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out))
