"""How even is a crowd across the tank, and does it follow the bed's depth or the matter?

python -I crowd_vs_bed.py <run dir> <seconds,...>

For each asked second (nearest positions row): bodies per 1 m bed-depth class against that class's
share of the tank's area; the cv of bodies over 10 m x 10 m squares inside the disc against a
Poisson crowd's cv at the same mean; the correlation of 5 m-column body counts with the matter
grid's column stock (whole column and top 12 m); and the crowd's depth quartiles.
"""
import gzip
import json
import math
import os
import struct
import sys

run = sys.argv[1]
asked = [int(t) for t in sys.argv[2].split(',')]
lay = json.load(open(os.path.join(run, 'fields', 'layout.json')))
bl = lay['bed']
bed = struct.unpack('<%df' % (bl['cellsX'] * bl['cellsZ']), open(os.path.join(run, 'fields', 'bed.f32'), 'rb').read())
R = bl['cellsX'] / 2.0  # disc radius in 1 m cells, centred


def inside(ix, iz):
    return (ix + 0.5 - R) ** 2 + (iz + 0.5 - R) ** 2 <= R * R


classes = [(0, 5), (5, 10), (10, 15), (15, 20), (20, 30), (30, 60)]
area = [0] * len(classes)
for ix in range(bl['cellsX']):
    for iz in range(bl['cellsZ']):
        if not inside(ix, iz):
            continue
        d = -bed[ix * bl['cellsZ'] + iz]
        for k, (a, b) in enumerate(classes):
            if a <= d < b:
                area[k] += 1
tot_area = sum(area)

rows = {}
with gzip.open(os.path.join(run, 'positions.jsonl.gz'), 'rt') as f:
    want = sorted(asked)
    for line in f:
        r = json.loads(line)
        t = r['t']
        for w in want:
            if w not in rows or abs(t - w) < abs(rows[w]['t'] - w):
                if abs(t - w) <= 100:
                    rows[w] = r
        if t > want[-1] + 100:
            break

ml = lay['matter']
for w in asked:
    r = rows.get(w)
    if r is None:
        print('no positions near', w)
        continue
    bodies = r['b']
    n = len(bodies)
    cnt = [0] * len(classes)
    sq = {}
    col5 = {}
    ys = sorted(b[2] for b in bodies)
    for b in bodies:
        x, y, z = b[1], b[2], b[3]
        ix, iz = int(x), int(z)
        if 0 <= ix < bl['cellsX'] and 0 <= iz < bl['cellsZ']:
            d = -bed[ix * bl['cellsZ'] + iz]
            for k, (a, c) in enumerate(classes):
                if a <= d < c:
                    cnt[k] += 1
        key = (int(x // 10), int(z // 10))
        sq[key] = sq.get(key, 0) + 1
        k5 = (int(x // 5), int(z // 5))
        col5[k5] = col5.get(k5, 0) + 1
    # squares fully inside the disc
    cells = []
    for i in range(int(2 * R // 10)):
        for j in range(int(2 * R // 10)):
            if all(inside(i * 10 + a, j * 10 + c) for a in (0, 9) for c in (0, 9)):
                cells.append(sq.get((i, j), 0))
    m = sum(cells) / len(cells)
    sd = math.sqrt(sum((c - m) ** 2 for c in cells) / len(cells))
    print('t %d (row %g) alive %d   depth quartiles %.1f %.1f %.1f m' % (
        w, r['t'], n, ys[n // 4], ys[n // 2], ys[3 * n // 4]))
    print('  bed class (m)  area share  body share  ratio')
    for k, (a, c) in enumerate(classes):
        if area[k]:
            print('  %2d-%2d          %.3f       %.3f       %.2f' % (a, c, area[k] / tot_area, cnt[k] / n, (cnt[k] / n) / (area[k] / tot_area)))
    print('  10 m squares inside the disc: %d, mean %.1f bodies, cv %.2f (Poisson %.2f), empty share %.2f'
          % (len(cells), m, sd / m, 1 / math.sqrt(m) if m else 0, sum(1 for c in cells if c == 0) / len(cells)))
    p = os.path.join(run, 'fields', '%09d.matter.f32' % int(r['t'] // 100 * 100))
    if os.path.exists(p):
        nx, ny, nz = ml['cellsX'], ml['cellsY'], ml['cellsZ']
        v = struct.unpack('<%df' % (nx * ny * nz), open(p, 'rb').read())
        xs, ys2, ts = [], [], []
        for ix in range(nx):
            for iz in range(nz):
                col = [v[(iy * nx + ix) * nz + iz] for iy in range(ny)]
                if sum(col) <= 0:
                    continue
                xs.append(col5.get((ix, iz), 0))
                ys2.append(sum(col))
                ts.append(col[0] + col[1] + 0.4 * col[2])

        def corr(a, b):
            ma, mb = sum(a) / len(a), sum(b) / len(b)
            sa = math.sqrt(sum((q - ma) ** 2 for q in a))
            sb = math.sqrt(sum((q - mb) ** 2 for q in b))
            return sum((p1 - ma) * (q - mb) for p1, q in zip(a, b)) / (sa * sb) if sa and sb else float('nan')
        print('  5 m columns: corr(bodies, matter whole column) %.2f, corr(bodies, matter top 12 m) %.2f'
              % (corr(xs, ys2), corr(xs, ts)))
