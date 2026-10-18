"""Where a tank's D109 islands lie against its glass: placement or the current's drift (round 60, 2026-10-15).

    python -I scripts/reads/r60s-islands-glass.py <out dir> <run dir> [<run dir> ...]

For each run, from its matter dumps at 100 s and 1,000 s (fields/*.matter.f32, the grid's own index order,
layer by layer from the surface, then x, then z; fields/layout.json names the grid):
- island columns: the live columns holding matter in the top three layers (15 m, the islands' 12 m depth
  rounded up to whole cells) at 100 s. The islands are seeded with matter and the rest of the water with none,
  so at 0 s these are the columns of weight above 0. By 100 s the current (0.1 m/s) can have carried matter
  about 10 m and the mixing about 2 m, so the set is the seeding's islands widened by a cell or two at most.
  The columns above the live mean (field-map.py's footprint rule) are given beside them.
- for 20, 30 and 40 m from the glass: the share of island columns within that distance, the share of the
  island matter within it, and the share of all live columns within it, which is what a placement that
  knew nothing of the glass would give on average. The same for the matter at 1,000 s.
- a picture: the 100 s and 1,000 s maps side by side, the glass drawn and the 20, 30 and 40 m bands dashed.
A column's distance from the glass is the tank's radius less its centre's distance from the tank's centre,
the live rule field-map.py uses. Two runs on one seed, wavelength, cover and area should give one island set;
the script says whether they do.
"""
import json
import math
import os
import re
import struct
import sys

BANDS = (20.0, 30.0, 40.0)
LAYERS = 3


def floats(path):
    data = open(path, 'rb').read()
    n = len(data) // 4
    return struct.unpack('<%df' % n, data[:n * 4])


def columns(run, second, grid):
    nx, ny, nz, cell = grid['cellsX'], grid['cellsY'], grid['cellsZ'], float(grid['cellMetres'])
    v = floats(os.path.join(run, 'fields', '%09d.matter.f32' % second))
    if len(v) != nx * ny * nz:
        sys.exit('%s at %d s: %d floats, the layout names %d' % (run, second, len(v), nx * ny * nz))
    top = min(LAYERS, ny)
    total = [[0.0] * nz for _ in range(nx)]
    for iy in range(top):
        base = iy * nx * nz
        for ix in range(nx):
            row = base + ix * nz
            for iz in range(nz):
                total[ix][iz] += v[row + iz]
    volume = top * cell ** 3
    return [[t / volume for t in col] for col in total]


def read(run):
    grid = json.load(open(os.path.join(run, 'fields', 'layout.json'), encoding='utf-8'))['matter']
    text = open(os.path.join(run, 'config.json'), encoding='utf-8').read()
    area = float(re.search(r'"worldAreaSquareMetres"\s*:\s*([0-9.eE+-]+)', text).group(1))
    radius = math.sqrt(area / math.pi)
    nx, nz, cell = grid['cellsX'], grid['cellsZ'], float(grid['cellMetres'])
    gap = {}
    for ix in range(nx):
        for iz in range(nz):
            d = math.hypot((ix + 0.5) * cell - radius, (iz + 0.5) * cell - radius)
            if d <= radius:
                gap[(ix, iz)] = radius - d
    return grid, radius, gap


def shares(gap, weight):
    """{band: (share of weighted columns, share of weight)} within each band of the glass."""
    cols = [k for k, w in weight.items() if w > 0]
    total = sum(weight[k] for k in cols)
    out = {}
    for b in BANDS:
        near = [k for k in cols if gap[k] <= b]
        out[b] = (len(near) / len(cols) if cols else float('nan'),
                  sum(weight[k] for k in near) / total if total else float('nan'))
    return out, len(cols)


def main():
    out_dir, runs = sys.argv[1], sys.argv[2:]
    os.makedirs(out_dir, exist_ok=True)
    island_sets = []
    for run in runs:
        run = run.rstrip('/\\')
        name = os.path.basename(os.path.dirname(run))
        grid, radius, gap = read(run)
        live_n = len(gap)
        m100 = columns(run, 100, grid)
        has1000 = os.path.isfile(os.path.join(run, 'fields', '%09d.matter.f32' % 1000))
        m1000 = columns(run, 1000, grid) if has1000 else [[0.0] * grid['cellsZ'] for _ in range(grid['cellsX'])]
        w100 = {k: m100[k[0]][k[1]] for k in gap}
        w1000 = {k: m1000[k[0]][k[1]] for k in gap}
        mean100 = sum(w100.values()) / live_n
        above = {k: (w if w > mean100 else 0.0) for k, w in w100.items()}
        island_sets.append((name, frozenset(k for k, w in w100.items() if w > 0)))

        print('== %s  %s' % (name, run))
        print('tank radius %.1f m, %d live 5 m columns' % (radius, live_n))
        all_share = {b: sum(1 for k in gap if gap[k] <= b) / live_n for b in BANDS}
        s_isl, n_isl = shares(gap, w100)
        s_abv, n_abv = shares(gap, above)
        s_1000, n_1000 = shares(gap, w1000)
        print('island columns at 100 s (matter > 0): %d (%.3f of the live columns); above the live mean: %d (%.3f)'
              % (n_isl, n_isl / live_n, n_abv, n_abv / live_n))
        if has1000:
            print('columns holding matter at 1,000 s: %d (%.3f)' % (n_1000, n_1000 / live_n))
        else:
            print('no dump at 1,000 s: the 1,000 s columns read 0')
        print('within  all live cols  | 100 s islands: cols  matter | 100 s above mean: cols  matter | 1,000 s: cols  matter')
        for b in BANDS:
            print('%4.0f m   %8.3f        | %18.3f  %6.3f | %21.3f  %6.3f | %13.3f  %6.3f'
                  % (b, all_share[b], s_isl[b][0], s_isl[b][1], s_abv[b][0], s_abv[b][1], s_1000[b][0], s_1000[b][1]))

        try:
            import matplotlib
            matplotlib.use('Agg')
            import matplotlib.pyplot as plt
        except ImportError:
            print('no matplotlib: no picture')
            continue
        cell = float(grid['cellMetres'])
        panels = ((100, m100), (1000, m1000)) if has1000 else ((100, m100),)
        fig, axes = plt.subplots(1, len(panels), figsize=(7 * len(panels), 7.2), squeeze=False)
        for ax, (second, field) in zip(axes[0], panels):
            img = [[(field[ix][iz] if (ix, iz) in gap else float('nan')) for ix in range(grid['cellsX'])]
                   for iz in range(grid['cellsZ'])]
            peak = max(v for col in field for v in col) or 1.0
            im = ax.imshow(img, origin='lower', extent=(0, grid['cellsX'] * cell, 0, grid['cellsZ'] * cell),
                           cmap='YlGn', vmin=0, vmax=peak)
            ax.add_patch(plt.Circle((radius, radius), radius, fill=False, edgecolor='#1f4e9c', linewidth=1.6))
            for b in BANDS:
                ax.add_patch(plt.Circle((radius, radius), radius - b, fill=False, edgecolor='#c0392b',
                                        linewidth=0.8, linestyle='--'))
            ax.set_title('%s | matter at %d s, top %d m\nglass solid; 20, 30 and 40 m from it dashed'
                         % (name, second, int(LAYERS * cell)), fontsize=10)
            ax.set_xlabel('x (m)')
            ax.set_ylabel('z (m)')
            fig.colorbar(im, ax=ax, fraction=0.046, pad=0.03, label='units / m3')
        fig.tight_layout()
        path = os.path.join(out_dir, '%s-islands-%s.png' % (name, '100-1000' if has1000 else '100'))
        fig.savefig(path, dpi=110)
        plt.close(fig)
        print('picture: %s' % path)

    if len(island_sets) > 1:
        first = island_sets[0]
        for name, s in island_sets[1:]:
            print('island columns of %s and %s: %s (%d and %d, %d shared)'
                  % (first[0], name, 'the same set' if s == first[1] else 'different sets',
                     len(first[1]), len(s), len(first[1] & s)))


if __name__ == '__main__':
    main()
