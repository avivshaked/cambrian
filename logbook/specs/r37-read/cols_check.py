# -*- coding: utf-8 -*-
"""How far apart the two `cols` readings are, and why both can exceed 100 in a tank.

The report's `cols` and the branch positions reader's `cols` both count the 1 m columns a
body stands in, over a bounding square of 12 x 12 columns; only the denominator is the 100
columns whose own centres are inside the circle. A rim body can stand in a column whose
centre is outside the circle but whose near corner is inside, so a numerator above 100 is
geometry, not a bug -- and `cols/100` is therefore not a share of the live footprint.
"""
import math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from report_read import rows, num
from nn_matched import read_summary

R = math.sqrt(100.0 / math.pi)
cols_x = int(math.ceil(2 * R))  # the bounding square in 1 m columns


def inside(x, z):
    return (x - R) ** 2 + (z - R) ** 2 <= R * R


live = sum(1 for ix in range(cols_x) for iz in range(cols_x)
           if inside(ix + 0.5, iz + 0.5))
reachable = 0
for ix in range(cols_x):
    for iz in range(cols_x):
        # a column is reachable if any of its corners or its centre is in the water
        pts = [(ix, iz), (ix + 1, iz), (ix, iz + 1), (ix + 1, iz + 1), (ix + .5, iz + .5)]
        if any(inside(x, z) for x, z in pts):
            reachable += 1

out = ['## M2  the tank footprint in 1 m columns',
       'bounding square columns\t%d x %d = %d' % (cols_x, cols_x, cols_x * cols_x),
       'columns whose centre is inside the circle (the report\'s and the reader\'s denominator)'
       '\t%d' % live,
       'columns a body can stand in (any part inside the circle)\t%d' % reachable,
       '',
       '## M2  report cols against branch-reader cols, all 300 samples']
out.append('arm\tsamples\tidentical\tmean |difference|\tmax |difference|'
           '\tsamples over 100 (report)\tsamples over 100 (reader)')
for i in range(1, 6):
    arm = 'r37-s%d' % i
    r, _ = rows(arm)
    s = {int(x['t']): x for x in read_summary(arm)}
    pairs = []
    for x in r:
        if x['t'] in s:
            pairs.append((num(x['cols']), float(s[x['t']]['cols'].split('/')[0])))
    diffs = [abs(a - b) for a, b in pairs]
    out.append('%s\t%d\t%d\t%.3f\t%d\t%d\t%d' % (
        arm, len(pairs), sum(1 for d in diffs if d == 0),
        sum(diffs) / len(diffs), max(diffs),
        sum(1 for a, _ in pairs if a > 100), sum(1 for _, b in pairs if b > 100)))

text = '\n'.join(out) + '\n'
open(os.path.join(HERE, 'cols-check.tsv'), 'w', encoding='utf-8').write(text)
print(text)
