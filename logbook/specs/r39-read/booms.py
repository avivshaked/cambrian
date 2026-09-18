# -*- coding: utf-8 -*-
"""E5 read boom by boom, and the eaters' whole course.

E5 asks whether the inherited absorptive count "peaks above 550 and falls below a sixth of
its peak within 10,000 s of the peak".  A seed with two booms has two peaks, and taking the
run's global maximum answers about whichever boom happened to be larger — in seed 2 that is
the second, which was still rising at the budget, so the global reading calls a seed that
boomed and busted at 13,000 s a seed that never busted.  This finds every boom instead: a
run of samples above 550 separated by a fall below a sixth of the preceding peak, or by the
end of the run.

Writes booms.tsv beside this file.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

A = ['r39-s%d' % i for i in range(1, 6)]
BAR = 550.0

out = ['## E5 boom by boom. A boom is a local maximum of `inherit` above 550 with no larger'
       ' sample within 3,000 s; its bust is the first later sample under a sixth of that'
       ' peak. "-" in "bust at" means the count never fell that far before the budget.',
       'arm\tboom\tpeak inherit\tat\tshare of the living\tpeak/6\tbust at\ts from peak'
       '\tinherit at the bust\twithin 10,000 s\tfell to (min after the peak)\tat']
counts = {}
for a in A:
    r, _ = rows(a)
    v = [(x['t'], num(x['inherit']), num(x['alive'])) for x in r]
    peaks = []
    for i, (t, c, al) in enumerate(v):
        if c < BAR:
            continue
        win = [y for y in v if abs(y[0] - t) <= 3000]
        if c == max(y[1] for y in win) and (not peaks or t - peaks[-1][0] > 3000):
            peaks.append((t, c, al))
        elif peaks and c > peaks[-1][1] and t - peaks[-1][0] <= 3000:
            peaks[-1] = (t, c, al)
    held = 0
    for k, (t, c, al) in enumerate(peaks, 1):
        after = [y for y in v if y[0] > t]
        bust = next((y for y in after if y[1] < c / 6.0), None)
        lo = min(after, key=lambda y: y[1]) if after else (t, c, al)
        ok = bust is not None and (bust[0] - t) <= 10000
        held += ok
        out.append('%s\t%d\t%d\t%d\t%.3f\t%.1f\t%s\t%s\t%s\t%s\t%d\t%d' % (
            a, k, c, t, c / al, c / 6.0,
            bust[0] if bust else '-', (bust[0] - t) if bust else '-',
            int(bust[1]) if bust else '-', 'yes' if ok else 'NO', lo[1], lo[0]))
    counts[a] = (len(peaks), held)

out.append('')
out.append('## per seed: booms above 550, and how many of them busted within 10,000 s')
out.append('arm\tbooms above 550\tbooms that busted within 10,000 s\tE5 on the FIRST such'
           ' boom\tE5 on the run\'s LARGEST peak')
n_first = n_largest = 0
for a in A:
    r, _ = rows(a)
    v = [(x['t'], num(x['inherit']), num(x['alive'])) for x in r]
    nb, held = counts[a]
    first = held > 0 and nb > 0
    # the first boom's own verdict
    firstok = 'no boom' if nb == 0 else ('yes' if held > 0 else 'NO')
    big = max(v, key=lambda y: y[1])
    after = [y for y in v if y[0] > big[0]]
    b = next((y for y in after if y[1] < big[1] / 6.0), None)
    bigok = big[1] > BAR and b is not None and (b[0] - big[0]) <= 10000
    n_first += (nb > 0 and held > 0)
    n_largest += bigok
    out.append('%s\t%d\t%d\t%s\t%s' % (a, nb, held, firstok, 'yes' if bigok else 'NO'))
out.append('count\t-\t-\t%d of 5 (bar 3)\t%d of 5 (bar 3)' % (n_first, n_largest))

with open(os.path.join(HERE, 'booms.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('wrote booms.tsv')
