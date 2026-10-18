"""Where round 57's free (spent) matter sits: by layer, by column, and how patchy.

python -I scratch/r60-patchiness/helpers/matter_profile.py <run dir> [seconds,...]

Reads fields/NNNNNNNNN.matter.f32 (the 5 m matter grid, J per cell, layout.json's order) and
fields/bed.f32. Prints per time: total J and units (100 J/unit), the share per 5 m layer,
the share in each column's lowest live cell (the floor), the share in the top 12 m, and the
column cv of the top-12 m density over live columns.
"""
import json
import math
import os
import struct
import sys

run = sys.argv[1]
times = [int(t) for t in sys.argv[2].split(',')] if len(sys.argv) > 2 else None
lay = json.load(open(os.path.join(run, 'fields', 'layout.json')))['matter']
nx, ny, nz, cm = lay['cellsX'], lay['cellsY'], lay['cellsZ'], lay['cellMetres']
JPU = 1.0  # the dump is in units (field-map.py divides by volume only)


def load(t):
    p = os.path.join(run, 'fields', '%09d.matter.f32' % t)
    n = os.path.getsize(p) // 4
    return struct.unpack('<%df' % n, open(p, 'rb').read())


def idx(ix, iy, iz):
    return (iy * nx + ix) * nz + iz


if times is None:
    names = sorted(f for f in os.listdir(os.path.join(run, 'fields')) if f.endswith('.matter.f32'))
    all_t = [int(f[:9]) for f in names]
    times = [all_t[0]] + [t for t in all_t if t % 6000 == 0]

vol = cm ** 3
print('grid %dx%dx%d cells of %g m' % (nx, ny, nz, cm))
for t in times:
    v = load(t)
    total = sum(v)
    layers = [sum(v[idx(ix, iy, iz)] for ix in range(nx) for iz in range(nz)) for iy in range(ny)]
    floor = 0.0
    top12 = []
    live_cols = 0
    for ix in range(nx):
        for iz in range(nz):
            col = [v[idx(ix, iy, iz)] for iy in range(ny)]
            # a cell that holds anything is treated as live; the lowest nonzero is the floor proxy
            nzs = [iy for iy in range(ny) if col[iy] > 0]
            if not nzs:
                continue
            live_cols += 1
            floor += col[max(nzs)]
            # top 12 m: layers 0,1 and 2/5 of layer 2
            top12.append((col[0] + col[1] + 0.4 * col[2]) / (12 * cm * cm))
    m = sum(top12) / len(top12)
    sd = math.sqrt(sum((x - m) ** 2 for x in top12) / len(top12))
    shares = ' '.join('%.2f' % (l / total) for l in layers)
    print('t %6d  free %8.0f units  layers(5 m) %s  floor-cell %.2f  top12 %.2f  top12 mean %.4f u/m3  col cv %.2f  cols %d'
          % (t, total / JPU, shares, floor / total, sum(top12) * 12 * cm * cm / total, m / JPU, sd / m if m else 0, live_cols))
