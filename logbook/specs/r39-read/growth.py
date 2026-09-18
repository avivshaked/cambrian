# -*- coding: utf-8 -*-
"""Recorded and not predicted: the growth dials' course, which split the seeds in two.

D087's genome carries an adult scale and a birth investment, and the table reports the
living population's means. Round 39's five seeds part on them, and the parting lines up with
whether the eaters' line survived to the budget.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

COLS = ['adult scale', 'invest', 'brood', 'body frac', 'age s', 'gen max']
out = ['## The growth dials at 5,000, 15,000 and 30,000 s, with the inherited eater count'
       ' beside them.', 'arm\tt\t' + '\t'.join(COLS) + '\tinherit\talive']
for s in range(1, 6):
    a = 'r39-s%d' % s
    r, _ = rows(a)
    d = {x['t']: x for x in r}
    for t in (5000, 15000, 30000):
        x = d[t]
        out.append('%s\t%d\t%s\t%s\t%s' % (
            a, t, '\t'.join(x[c] for c in COLS), x['inherit'], x['alive']))
out += ['', '## round 38 at 30,000 s, for the comparison',
        'arm\t' + '\t'.join(COLS) + '\tinherit\talive']
for s in range(1, 6):
    a = 'r38-s%d' % s
    r, _ = rows(a)
    x = r[-1]
    out.append('%s\t%s\t%s\t%s' % (a, '\t'.join(x[c] for c in COLS), x['inherit'], x['alive']))
with open(os.path.join(HERE, 'growth.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out))
