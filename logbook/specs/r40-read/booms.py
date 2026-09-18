# -*- coding: utf-8 -*-
"""L4 read boom by boom, adapted from logbook/specs/r39-read/booms.py.

0106's L4: "the bust slows" — the first boom above 550 inherited eaters falls under a sixth
of its peak LATER than 2,600 s after it, or not before the budget, in 4 of 5.  Round 39's
five first booms all busted 1,900 to 2,600 s after their peaks, which is the bar's origin.

Round 40 was stopped at 16,400 / 12,200 / 14,300 s, so "not before the budget" cannot be
distinguished from "not before the stop": a seed whose eaters are still climbing at its last
sample has no peak to measure from at all.  This file says which, and never converts a
censored seed into a holding one.

A boom is a local maximum of `inherit` above 550 with no larger sample within 3,000 s, the
same rule round 39 used; a peak inside 2,600 s of the last sample cannot have its bar
tested even in principle and is marked so.

Writes booms.tsv beside this file.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

ARMS = ['r40-s1', 'r40-s2', 'r40-s3']
BAR = 550.0
SLOW = 2600.0      # round 39's slowest first bust, the bar L4 asks the round to beat

out = ['## L4 boom by boom. A boom is a local maximum of `inherit` above 550 with no larger'
       ' sample within 3,000 s; its bust is the first later sample under a sixth of that'
       ' peak. L4 holds for a boom whose bust comes later than 2,600 s after the peak, or'
       ' not at all before the budget — but this round has no budget, only a stop, so a'
       ' boom with no bust and less than 2,600 s of run left after it is NOT READABLE.',
       'arm\tlast sample\tboom\tpeak inherit\tat\tshare of the living\tpeak/6\tbust at'
       '\ts from peak\tinherit at the bust\ts of run after the peak\tL4 verdict']
per = []
for a in ARMS:
    r, _ = rows(a)
    v = [(x['t'], num(x['inherit']), num(x['alive'])) for x in r]
    last = v[-1][0]
    peaks = []
    for i, (t, c, al) in enumerate(v):
        if c < BAR:
            continue
        win = [y for y in v if abs(y[0] - t) <= 3000]
        if c == max(y[1] for y in win) and (not peaks or t - peaks[-1][0] > 3000):
            peaks.append((t, c, al))
        elif peaks and c > peaks[-1][1] and t - peaks[-1][0] <= 3000:
            peaks[-1] = (t, c, al)
    if not peaks:
        top = max(v, key=lambda y: y[1])
        out.append('%s\t%d\t-\t-\t-\t-\t-\t-\t-\t-\t-\tnot readable: `inherit` never reached'
                   ' 550 (run maximum %d at %d s)' % (a, last, top[1], top[0]))
        per.append((a, 'not readable: no boom above 550 before the stop'))
        continue
    for k, (t, c, al) in enumerate(peaks, 1):
        after = [y for y in v if y[0] > t]
        bust = next((y for y in after if y[1] < c / 6.0), None)
        left = last - t
        if bust is not None:
            ok = (bust[0] - t) > SLOW
            vd = 'HOLDS (bust %d s after the peak, later than 2,600)' % (bust[0] - t) if ok \
                else 'FAILS (bust %d s after the peak, 2,600 or sooner)' % (bust[0] - t)
        elif left > SLOW:
            vd = 'HOLDS (no bust in the %d s of run after the peak, which is past 2,600)' % left
        else:
            vd = ('not readable: no bust, but only %d s of run after the peak, under the'
                  ' 2,600 s the bar needs' % left)
        out.append('%s\t%d\t%d\t%d\t%d\t%.3f\t%.1f\t%s\t%s\t%s\t%d\t%s' % (
            a, last, k, c, t, c / al, c / 6.0, bust[0] if bust else '-',
            (bust[0] - t) if bust else '-', int(bust[1]) if bust else '-', left, vd))
        if k == 1:
            per.append((a, vd))

out.append('')
out.append('## per seed, on the FIRST boom above 550 (L4\'s own wording)')
out.append('arm\tL4 on the first boom')
for a, vd in per:
    out.append('%s\t%s' % (a, vd))

with open(os.path.join(HERE, 'booms.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('wrote booms.tsv')
