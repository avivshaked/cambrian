# -*- coding: utf-8 -*-
"""E2's reading, continued: whether the detritus patchiness that does appear is the bed's
or the crowd's. Pearson r between `det cv` and `inherit` over t >= 6,000 s (after the field
has filled), with the two maxima and their times beside it."""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

out = ['## det cv against the inherited eater count, t >= 6,000 s (the field has filled by'
       ' then: det cv starts at 1.6 to 2.4 and falls through the first 5,000 s).',
       'arm\tdet cv max after 6000\tat\tinherit max\tat\tseconds apart\tPearson r']
for s in range(1, 6):
    a = 'r39-s%d' % s
    r, _ = rows(a)
    post = [x for x in r if x['t'] >= 6000]
    cv = [num(x['det cv']) for x in post]
    inh = [num(x['inherit']) for x in post]
    n = len(cv)
    mx = max(range(n), key=lambda i: cv[i])
    mi = max(range(n), key=lambda i: inh[i])
    mc, mn = sum(cv) / n, sum(inh) / n
    cov = sum((cv[i] - mc) * (inh[i] - mn) for i in range(n))
    den = (sum((c - mc) ** 2 for c in cv) * sum((i - mn) ** 2 for i in inh)) ** 0.5
    out.append('%s\t%.3f\t%d\t%d\t%d\t%d\t%.2f' % (
        a, cv[mx], post[mx]['t'], inh[mi], post[mi]['t'],
        abs(post[mx]['t'] - post[mi]['t']), cov / den))
with open(os.path.join(HERE, 'e2-cv-vs-eaters.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('wrote e2-cv-vs-eaters.tsv')
