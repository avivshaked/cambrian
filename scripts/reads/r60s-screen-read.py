"""Round 60's fed-island screens, the numbers field-map.py and crowd_vs_bed.py do not print.

python -I scripts/reads/r60s-screen-read.py <run dir> [seconds,...]

Per asked second (default 5000,10000): alive, cols (occupiedColumns over totalColumns), x sd, matterStanding,
burial against influx over the 2,000 s before it, and the desert's lit-layer matter: the live 5 m columns that
were not islands at 100 s (field-map.py's --footprint 100 rule over the top 3 layers), their top-12 m density
in units/m3 as matter_profile.py takes it, against the leaf's break-even of 0.00025 (scratch/r60-patchiness).
"""
import json
import os
import struct
import sys

run = sys.argv[1]
asked = [int(t) for t in sys.argv[2].split(',')] if len(sys.argv) > 2 else [5000, 10000]
BREAK_EVEN = 0.00025

rows = {}
with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
    for line in f:
        if line.strip():
            r = json.loads(line)
            rows[int(round(r['t']))] = r


def row(t):
    return rows[min(rows, key=lambda k: abs(k - t))]


lay = json.load(open(os.path.join(run, 'fields', 'layout.json')))['matter']
nx, ny, nz, cm = lay['cellsX'], lay['cellsY'], lay['cellsZ'], lay['cellMetres']


def load(t):
    p = os.path.join(run, 'fields', '%09d.matter.f32' % t)
    n = os.path.getsize(p) // 4
    return struct.unpack('<%df' % n, open(p, 'rb').read())


def cell(v, ix, iy, iz):
    return v[(iy * nx + ix) * nz + iz]


def live(v, ix, iz):
    return any(cell(v, ix, iy, iz) > 0 for iy in range(ny))


# The islands as seeded: live columns whose top-3-layer density is above the live mean at 100 s.
v0 = load(100)
top3 = {(ix, iz): sum(cell(v0, ix, iy, iz) for iy in range(3)) for ix in range(nx) for iz in range(nz) if live(v0, ix, iz)}
mean3 = sum(top3.values()) / len(top3)
island = {k for k, x in top3.items() if x > mean3}
print('footprint: %d of %d live columns (%.2f)' % (len(island), len(top3), len(island) / len(top3)))

for t in asked:
    r = row(t)
    r0 = row(t - 2000)
    influx = r['matterInfluxedTotal'] - r0['matterInfluxedTotal']
    buried = r['matterBuriedTotal'] - r0['matterBuriedTotal']
    v = load(t)
    desert, isle = [], []
    for ix in range(nx):
        for iz in range(nz):
            if (ix, iz) not in top3:
                continue
            d = (cell(v, ix, 0, iz) + cell(v, ix, 1, iz) + 0.4 * cell(v, ix, 2, iz)) / (12 * cm * cm)
            (isle if (ix, iz) in island else desert).append(d)
    desert.sort()
    dm = sum(desert) / len(desert)
    im = sum(isle) / len(isle) if isle else float('nan')
    below = sum(1 for d in desert if d < BREAK_EVEN) / len(desert)
    print('t %5d  alive %5d  cols %.3f  x sd %.1f  standing %8.1f  influx/2000s %7.1f  buried %7.1f (%s)'
          '  desert top12 mean %.5f median %.5f below-break-even %.2f  islands top12 mean %.5f'
          % (r['t'], r['alive'], r['occupiedColumns'] / r['totalColumns'], r['xSpreadMetres'], r['matterStanding'],
             influx, buried, ('%.2f of influx' % (buried / influx)) if influx else 'no influx',
             dm, desert[len(desert) // 2], below, im))
