# -*- coding: utf-8 -*-
"""The reader item 0102's early look flagged: the positions plot clips at the configured
depth (45 m), and the tilt puts the deep arc's floor at about -60 m, so any body below
-45 m is drawn outside the box or not at all. This counts them.
"""
import json, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RUNS = {'r39-s1': '2026-09-15-213428-8eab1085', 'r39-s2': '2026-09-15-213503-8eab1085',
        'r39-s3': '2026-09-17-085758-8eab1085', 'r39-s4': '2026-09-17-085833-8eab1085',
        'r39-s5': '2026-09-17-085303-8eab1085'}
TIMES = [5000, 15000, 30000]
out = ['## Bodies below the configured depth of 45 m, which the positions reader\'s plot'
       ' clips away. The floor reaches about -60 m on the deep arc, so this water exists.',
       'arm\tt\tliving\tbelow -45 m\tshare\tdeepest body m\tleaves below -45\tstomachs below -45']
for a in sorted(RUNS):
    p = os.path.join(ROOT, 'runs', a, RUNS[a], 'positions.jsonl')
    left = set(TIMES)
    for line in open(p, encoding='utf-8'):
        if not line.startswith('{"t":'):
            continue
        t = int(line[5:line.index(',', 5)])
        if t not in left:
            continue
        left.discard(t)
        r = json.loads(line)
        arr = np.array([[x[2], x[4]] for x in r['b']], dtype=float)
        y, fl = arr[:, 0], arr[:, 1].astype(int)
        deep = y < -45.0
        out.append('%s\t%d\t%d\t%d\t%.4f\t%.2f\t%d\t%d' % (
            a, t, y.size, int(deep.sum()), deep.mean(), y.min(),
            int((deep & ((fl & 4) != 0)).sum()), int((deep & ((fl & 1) != 0)).sum())))
        if not left:
            break
with open(os.path.join(HERE, 'below45.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out))
