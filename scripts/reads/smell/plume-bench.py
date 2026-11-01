"""The plume bench's reader: the median far leaf smell 20 to 40 m downstream of groves (D270).

Usage: python -I scripts/reads/smell/plume-bench.py RUN [--seconds 600,1200 | --seconds all] [--json]
                                                    [--lo 20] [--hi 40] [--step 2.5] [--min-columns 5]
                                                    [--report FILE] [--trace K]

The number it prints is what the smell screen's founding bench sets `LeafPlumeHalfScale` from
(logbook/specs/smell-plume-design.md, "A screen of five worlds"): the sense reads a density s as s / (s + H), so H set
to this median makes a grazer standing there read 0.5. Reads files only, steps nothing. Needs numpy.

WHAT IT READS. RUN is a run directory (or an arm folder, whose latest dated run is read) with plume on. From
fields/ it reads layout.json, `NNNNNNNNN.plume.f32` at each dumped second (the stock of each 5 m cell, m2 s, order
(iy * cellsX + ix) * cellsZ + iz) and bed.f32 (the floor's y on a 1 m lattice); from positions.jsonl.gz the bodies at
that second (id, x, y, z, flags; bit 4 is photosynthetic); from the report beside the arm folder (`<arm>.md`) or
report.md the `island bloom, N islands` line; from config.json the matter cell size.

THE DENSITY. Matched to src/Evosim.Dynamics/CreatureSenses.cs:395 -> src/Evosim.Core/Environment/LeafPlumeField.cs
Sample (line 239): the density is the cell's stock over the cell volume (cell^3), interpolated trilinearly between
the eight cell centres around the point (x and z wrap at the seams; y is measured down from the surface in cells,
less half a cell, and clamped to the first and last layer); a corner cell that is dead stands in as the cell the point
is in. A dead cell is one that holds exactly 0 at every dumped second (the bed's layer and the coral; the dump does
not carry the mask). The strength a part reads is then s / (s + H) with s > 0.

A GROVE is g2-read.py's (scripts/reads/bloom/g2-read.py, make_groves and island_radius, imported, not restated): the
disc of radius r_eff + 10 m round an island's centroid, r_eff = sqrt(columns x 5^2 / pi), each axis the shorter way
round the joined 300 m box. Islands come from the report's island line. As gate 2 judges islands, only islands of at
least --min-columns columns (5) count, and a grove needs at least one leaf at the second: a LEAF is a body with the
photosynthetic flag (4) and not the absorptive flag (1), as positions-read.py's guild_of reads it.

DOWNSTREAM, from the plume itself. For each grove the annulus 20 to 40 m past its edge is sampled at --step metres
along 36 bearings (every 10 degrees) and at the grove's own heights (the 10th, 50th and 90th percentile of its leaves'
y), skipping any point at or below the floor, above the surface or in a dead cell. Downstream is the bearing of the
density-weighted centroid of that annulus: the direction of the sum over its points of density x unit bearing, which is
the direction the smell has drifted. The BENCH FIGURE is the median density over the annulus points within +-45 degrees
of that bearing, pooled over groves, with p10, p90, the points and groves counted. Beside it the median over all
bearings of the annulus ("all"). Per grove, `drift` is the length of that resultant over the sum of densities: 0 is no
drift, 1 a single direction; --trace K prints one grove's rows. This replaces the design's "along the current at that
grove" because the ocean's current is not in the dump, turns faster than the plume ages (lifetime 3,000 s), and the
only port of it available here left out the bed and the coral fade.
Groves: only groves with at least one leaf at the second are read. Densities are in m2 s/m3.

Exits 2 on a missing file. One line a second read; --json prints one row each.
"""
import argparse
import glob
import gzip
import importlib.util
import json
import math
import os
import re
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
CURRENT_FIELD_INDEX = (1 << 64) - 1 - 5
M64 = (1 << 64) - 1


def fail(msg):
    print(msg, file=sys.stderr)
    sys.exit(2)


def load_g2():
    path = os.path.join(REPO, 'scripts', 'reads', 'bloom', 'g2-read.py')
    if not os.path.isfile(path):
        fail(path + ': missing')
    spec = importlib.util.spec_from_file_location('g2_read', path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# ------------------------------------------------------------------------------------------ the dump

class Plume:
    def __init__(self, fields_dir, files):
        lay = json.load(open(os.path.join(fields_dir, 'layout.json')))['plume']
        self.nx, self.ny, self.nz = lay['cellsX'], lay['cellsY'], lay['cellsZ']
        self.cell = float(lay['cellMetres'])
        self.length, self.width = self.nx * self.cell, self.nz * self.cell
        self.vol = self.cell ** 3
        self.stock = {}
        for sec, path in files.items():
            a = np.fromfile(path, dtype='<f4')
            if a.size != self.nx * self.ny * self.nz:
                fail('%s: %d values, layout says %d' % (path, a.size, self.nx * self.ny * self.nz))
            self.stock[sec] = a.astype(np.float64).reshape(self.ny, self.nx, self.nz)
        allfiles = sorted(glob.glob(os.path.join(fields_dir, '*.plume.f32')))
        dead = np.ones((self.ny, self.nx, self.nz), dtype=bool)
        for p in allfiles:
            a = np.fromfile(p, dtype='<f4').reshape(self.ny, self.nx, self.nz)
            dead &= (a == 0)
        self.dead = dead

    def home(self, x, y, z):
        ix = int(math.floor((x % self.length) / self.cell)) % self.nx
        iz = int(math.floor((z % self.width) / self.cell)) % self.nz
        iy = min(self.ny - 1, int(math.floor(-y / self.cell))) if y < 0 else 0
        return ix, iy, iz

    def density(self, stock, x, y, z):
        """LeafPlumeField.Sample's density: trilinear between the eight cell centres, dead corners as the home cell."""
        hx, hy, hz = self.home(x, y, z)
        ux = (x % self.length) / self.cell - 0.5
        uz = (z % self.width) / self.cell - 0.5
        uy = (0.0 if y >= 0 else -y) / self.cell - 0.5
        x0, fx = int(math.floor(ux)), ux - math.floor(ux)
        z0, fz = int(math.floor(uz)), uz - math.floor(uz)
        if uy <= 0:
            y0 = y1 = 0
            fy = 0.0
        elif uy >= self.ny - 1:
            y0 = y1 = self.ny - 1
            fy = 0.0
        else:
            y0 = int(math.floor(uy))
            y1 = y0 + 1
            fy = uy - y0
        xs = (x0 % self.nx, (x0 + 1) % self.nx)
        zs = (z0 % self.nz, (z0 + 1) % self.nz)
        ys = (y0, y1)
        d = 0.0
        for a in range(2):
            for b in range(2):
                for c in range(2):
                    w = (1 - fx if a == 0 else fx) * (1 - fy if b == 0 else fy) * (1 - fz if c == 0 else fz)
                    if w == 0.0:
                        continue
                    ix, iy, iz = xs[a], ys[b], zs[c]
                    if self.dead[iy, ix, iz]:
                        ix, iy, iz = hx, hy, hz
                    d += w * stock[iy, ix, iz] / self.vol
        return d

    def in_dead(self, x, y, z):
        ix, iy, iz = self.home(x, y, z)
        return bool(self.dead[iy, ix, iz])


def resolve_run(path):
    path = os.path.normpath(path)
    if not os.path.isdir(path):
        fail(path + ': not a directory')
    if os.path.isdir(os.path.join(path, 'fields')):
        return path
    subs = sorted(d for d in glob.glob(os.path.join(path, '*')) if os.path.isdir(os.path.join(d, 'fields')))
    if not subs:
        fail(path + ': no run with a fields/ folder')
    return subs[-1]


def find_islands(g2, run, explicit):
    cands = [explicit] if explicit else []
    cands.append(os.path.normpath(os.path.dirname(run)) + '.md')
    cands.append(os.path.join(run, 'report.md'))
    for c in cands:
        if c and os.path.isfile(c):
            text = open(c, encoding='utf-8', errors='replace').read()
            isl = g2.parse_islands(text)
            if isl:
                return isl, c
    fail('no report with an island bloom line (looked for %s); pass --report FILE' % ', '.join(str(c) for c in cands))


def find(d, key):
    if isinstance(d, dict):
        if key in d:
            return d[key]
        for v in d.values():
            r = find(v, key)
            if r is not None:
                return r
    return None


def read_positions(run):
    path = os.path.join(run, 'positions.jsonl.gz')
    if not os.path.isfile(path):
        fail(path + ': missing')
    out = {}
    with gzip.open(path, 'rt') as f:
        for line in f:
            m = re.match(r'\{"t":([0-9.eE+-]+)', line)
            if m and float(m.group(1)).is_integer():
                out[int(float(m.group(1)))] = line
    return out


def pct(xs, p):
    return float(np.percentile(xs, p)) if len(xs) else float('nan')


def read_second(sec, plume, groves, bodies_line, bed, a, trace=None):
    row = json.loads(bodies_line)
    leaves = [(b[1], b[2], b[3]) for b in row['b'] if (b[4] & 4) and not (b[4] & 1)]
    side = plume.length
    g2 = a.g2
    down, allp, per, skipped = [], [], [], []
    nb = 36
    for gr in groves:
        inside = [(x, y, z) for x, y, z in leaves
                  if math.hypot(g2.gap(x, gr['cx'], side), g2.gap(z, gr['cz'], side)) <= gr['r']]
        if not inside:
            skipped.append((gr['k'], 'no leaf'))
            continue
        ys = [p[1] for p in inside]
        heights = sorted({pct(ys, 10), pct(ys, 50), pct(ys, 90)})
        pts = []
        d = a.lo
        while d <= a.hi + 1e-9:
            for i in range(nb):
                ang = 2 * math.pi * i / nb
                px = gr['cx'] + math.cos(ang) * (gr['r'] + d)
                pz = gr['cz'] + math.sin(ang) * (gr['r'] + d)
                for y in heights:
                    floor = bed[(int(math.floor(px % side)) % 300) * 300 + (int(math.floor(pz % side)) % 300)]
                    if y >= 0 or y <= floor or plume.in_dead(px, y, pz):
                        continue
                    pts.append((ang, d, y, plume.density(plume.stock[sec], px, y, pz)))
            d += a.step
        tot = sum(p[3] for p in pts)
        if not pts or tot <= 0:
            skipped.append((gr['k'], 'no smell in the annulus'))
            continue
        sx = sum(p[3] * math.cos(p[0]) for p in pts)
        sz = sum(p[3] * math.sin(p[0]) for p in pts)
        drift = math.hypot(sx, sz) / tot
        bearing = math.atan2(sz, sx)
        dn = [p for p in pts if abs((p[0] - bearing + math.pi) % (2 * math.pi) - math.pi) <= math.pi / 4 + 1e-9]
        down += [p[3] for p in dn]
        allp += [p[3] for p in pts]
        per.append(dict(grove=gr['k'], drift=drift, bearing_deg=math.degrees(bearing) % 360, points=len(pts),
                        down_points=len(dn), down_median=pct([p[3] for p in dn], 50), all_median=pct([p[3] for p in pts], 50)))
        if trace is not None and gr['k'] == trace:
            print('trace grove %d at %d s: centre (%.1f, %.1f) radius %.2f, %d leaves, heights %s, bearing %.0f deg, drift %.3f'
                  % (gr['k'], sec, gr['cx'], gr['cz'], gr['r'], len(inside), ['%.2f' % h for h in heights],
                     math.degrees(bearing) % 360, drift))
            for p in dn:
                print('  bearing %5.1f  d %5.1f  y %6.2f  density %.6g' % (math.degrees(p[0]), p[1], p[2], p[3]))
    return down, allp, per, skipped


def main():
    ap = argparse.ArgumentParser(description='Median far leaf smell 20 to 40 m downstream of groves (D270).')
    ap.add_argument('run')
    ap.add_argument('--seconds', default='all', help='comma list of dumped seconds, or all')
    ap.add_argument('--json', action='store_true')
    ap.add_argument('--lo', type=float, default=20.0)
    ap.add_argument('--hi', type=float, default=40.0)
    ap.add_argument('--step', type=float, default=2.5)
    ap.add_argument('--min-columns', type=int, default=5)
    ap.add_argument('--report')
    ap.add_argument('--trace', type=int, default=None, help='print one grove\'s points')
    a = ap.parse_args()

    run = resolve_run(a.run)
    fields = os.path.join(run, 'fields')
    for f in ('layout.json', 'bed.f32'):
        if not os.path.isfile(os.path.join(fields, f)):
            fail(os.path.join(fields, f) + ': missing')
    files = {}
    for p in sorted(glob.glob(os.path.join(fields, '*.plume.f32'))):
        files[int(os.path.basename(p).split('.')[0])] = p
    if not files:
        fail(fields + ': no plume dump (was the plume on?)')
    want = sorted(files) if a.seconds == 'all' else [int(s) for s in a.seconds.split(',')]
    for s in want:
        if s not in files:
            fail('no plume dump at %d s; have %s' % (s, sorted(files)))

    a.g2 = load_g2()
    cfg = json.load(open(os.path.join(run, 'config.json')))
    islands, report = find_islands(a.g2, run, a.report)
    cell = float(find(cfg, 'fieldMatterCellMetres') or a.g2.CELL_DEFAULT)
    groves = [g for g in a.g2.make_groves(islands, cell) if g['columns'] >= a.min_columns]
    plume = Plume(fields, {s: files[s] for s in want})
    bed = np.fromfile(os.path.join(fields, 'bed.f32'), dtype='<f4')
    positions = read_positions(run)

    rows = []
    for sec in want:
        if sec not in positions:
            fail('positions.jsonl.gz has no row at %d s' % sec)
        down, allp, per, skipped = read_second(sec, plume, groves, positions[sec], bed, a, a.trace)
        row = dict(second=sec, median=pct(down, 50), p10=pct(down, 10), p90=pct(down, 90), points=len(down),
                   groves=len(per), median_all_bearings=pct(allp, 50), points_all=len(allp),
                   p10_all=pct(allp, 10), p90_all=pct(allp, 90), per_grove=per, skipped=[list(x) for x in skipped],
                   lo=a.lo, hi=a.hi, run=os.path.basename(os.path.dirname(run)))
        rows.append(row)
        if a.json:
            print(json.dumps(row))
        else:
            print('%s  %5d s  downstream median %.4g  p10 %.4g  p90 %.4g (%d points, %d groves)  all bearings median %.4g (%d points)  m2 s/m3'
                  % (row['run'], sec, row['median'], row['p10'], row['p90'], row['points'], row['groves'],
                     row['median_all_bearings'], row['points_all']))
            print('    drift by grove: ' + '  '.join('%d:%.2f' % (g['grove'], g['drift']) for g in per))
    return 0


if __name__ == '__main__':
    sys.exit(main())
