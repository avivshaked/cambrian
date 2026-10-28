"""Where a recorded world's leaves stand against D251's islands: the distance of every living photosynthetic body from
the nearest island's edge (level distance to its centroid less its effective radius), from an --island-leaf-census.

    python -I scripts/reads/bloom/leaf-reach.py <census prefix> [<census prefix> ...]

Prints, per census second, how many leaves stand inside an island's columns, and the share of all leaves within
0, 5, 10, 15, 20 and 30 m of an island's edge, and the share of each island's nearest leaves inside its own columns.
Exits 2 on a missing file.
"""
import csv
import math
import os
import sys

BANDS = [0, 5, 10, 15, 20, 30]
SIDE = 300.0


def fail(m):
    print(m, file=sys.stderr)
    sys.exit(2)


def tsv(path):
    if not os.path.isfile(path):
        fail(path + ': missing')
    with open(path) as f:
        return list(csv.DictReader(f, delimiter='\t'))


def ring(d):
    d = abs(d) % SIDE
    return min(d, SIDE - d)


if len(sys.argv) < 2:
    fail(__doc__)
for prefix in sys.argv[1:]:
    islands = tsv(prefix + '-islands.tsv')
    bodies = tsv(prefix + '-bodies.tsv')
    for t in sorted({r['second'] for r in islands}, key=float):
        isl = [(int(r['island']), float(r['cx']), float(r['cz']), float(r['r_eff'])) for r in islands if r['second'] == t]
        leaves = [b for b in bodies if b['second'] == t and b['photosynthetic'] == '1']
        inside = sum(1 for b in leaves if int(b['island']) >= 0)
        gaps = []
        nearest = {}
        for b in leaves:
            x, z = float(b['x']), float(b['z'])
            g, k = min((math.hypot(ring(x - cx), ring(z - cz)) - r, k) for k, cx, cz, r in isl)
            gaps.append(g)
            nearest.setdefault(k, [0, 0])
            nearest[k][0] += 1
            if int(b['island']) == k:
                nearest[k][1] += 1
        n = max(1, len(leaves))
        print('== %s at %s s: %d leaves, %d (%.0f%%) inside an island\'s columns' % (os.path.basename(prefix), t, len(leaves), inside, 100.0 * inside / n))
        print('  within m of the nearest edge: ' + '  '.join('%d m %.0f%%' % (d, 100.0 * sum(1 for g in gaps if g <= d) / n) for d in BANDS))
        print('  per island, leaves nearest it (inside its columns): ' + '  '.join('%d:%d(%d)' % (k, v[0], v[1]) for k, v in sorted(nearest.items())))
