"""Bodies per 10 m of x, by guild, at the samples nearest the given times, for each r62cf world: whether the gap
between the groves fills in. Usage: python -I scripts/reads/r62cf-groves.py <run dir> [<run dir> ...]"""
import os
import sys

sys.path.insert(0, r'D:\Projects\experiments\evolution-simulator\scripts\reads')
import runrec  # noqa: E402

TIMES = [12000, 18000, 24000]
BIN = 10.0


def guild(f):
    a, p = f & 1, f & 4
    return 'mouth' if a else ('leaf' if p else 'other')


for run in sys.argv[1:]:
    want = list(TIMES)
    best = {}
    for row in runrec.positions(run):
        t = row['t']
        for w in want:
            if w not in best or abs(t - w) < abs(best[w]['t'] - w):
                best[w] = row
        if t > max(TIMES) + 200:
            break
    print('==', os.path.basename(os.path.dirname(run)))
    xs_all = [b[1] for r in best.values() for b in r['b']]
    lo = BIN * int(min(xs_all) // BIN)
    hi = BIN * (int(max(xs_all) // BIN) + 1)
    nb = int((hi - lo) / BIN)
    for w in TIMES:
        r = best[w]
        h = {g: [0] * nb for g in ('leaf', 'mouth', 'other')}
        for b in r['b']:
            i = min(nb - 1, int((b[1] - lo) // BIN))
            h[guild(b[4])][i] += 1
        print('  t=%d  (n=%d)' % (r['t'], r['n']))
        print('    x from    ' + ' '.join('%5d' % (lo + i * BIN) for i in range(nb)))
        for g in ('leaf', 'mouth', 'other'):
            print('    %-9s ' % g + ' '.join('%5d' % v for v in h[g]))
