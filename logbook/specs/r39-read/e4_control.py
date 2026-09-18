# -*- coding: utf-8 -*-
"""E4's control: the same reading on round 38's flat floor.

E4 asks whether the eaters sit lower above their own column's floor than the leaves do, and
reads it as "the eaters find the low ground". Round 38's floor is flat, so the same
statistic there is height above -depth, and whatever it says cannot be the bed's doing.
"""
import glob, json, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
out = ['## E4\'s statistic on round 38 (flat floor at -60 m), at 5,000, 15,000 and'
       ' 30,000 s. A negative last column is E4 holding.',
       'arm\tt\tleaves\tstomachs\tleaf median above floor m\tstomach median above floor m'
       '\tstomach - leaf m\tE4 would hold']
for s in range(1, 6):
    p = sorted(glob.glob(os.path.join(ROOT, 'runs', 'r38-s%d' % s, '*', 'positions.jsonl')))[-1]
    depth = json.load(open(os.path.join(os.path.dirname(p), 'config.json'),
                           encoding='utf-8'))['world']['worldDepthMetres']
    left = {5000, 15000, 30000}
    for line in open(p, encoding='utf-8'):
        if not line.startswith('{"t":'):
            continue
        t = int(line[5:line.index(',', 5)])
        if t not in left:
            continue
        left.discard(t)
        r = json.loads(line)
        a = np.array([[x[2], x[4]] for x in r['b']], dtype=float)
        y, fl = a[:, 0] + depth, a[:, 1].astype(int)
        lf, st = (fl & 4) != 0, (fl & 1) != 0
        ml = float(np.median(y[lf])) if lf.any() else float('nan')
        ms = float(np.median(y[st])) if st.any() else float('nan')
        out.append('r38-s%d\t%d\t%d\t%d\t%.3f\t%.3f\t%.3f\t%s' % (
            s, t, int(lf.sum()), int(st.sum()), ml, ms, ms - ml,
            'yes' if ms < ml else ('no stomachs' if not st.any() else 'NO')))
        if not left:
            break
with open(os.path.join(HERE, 'e4-control-r38.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out))
