"""What holds round 61's crowd 15 to 60 m inside the glass: the reader D204's first measurement asks for.

The question (DECISIONS.md D204, logbook/specs/open-ocean-survey.md section 5): round 61's tank has
its bodies piled 30 to 60 m inside the glass, with the outer 15 m under-filled. Is the glass a trap,
is it the tilted floor, or is it the current's slow outer band? Three readings tell them apart.

  1. The radial profile, at each report time. The share of bodies, of dissolved matter and of snow in
     bands 0-5, 5-15, 15-30, 30-60 and over 60 m from the glass, against each band's share of the
     floor area; then the same split by compass sector (8), by floor depth under the column (three
     bins) and, for bodies, by kind (the positions flags). Every ratio is "share of the stock in this
     cell / share of the floor area in this cell", so 1.00 is the tank's mean density and 0.50 is
     half of it.
  2. Dwell. A body that crosses into the outer 15 m (it was outside that band at the sample before)
     is followed until it leaves the band or is gone from the next sample (it died). The same is done
     for a control band of the same floor area further in, which starts at --control-from metres from
     the glass (30 by default; the survey's "30 to 45 m" is 30 to 48.7 m at this tank's size when the
     areas are made equal, and the exact edge is printed). Bodies still in the band when the data ends
     are censored. The tables give the median and quartiles of the finished dwells and the same three
     numbers from a Kaplan-Meier fit, which counts the censored ones and is the figure to trust
     when many are.
  3. Radial speeds. A body's speed toward (negative) or away from (positive) the centre of the tank
     between two consecutive samples, by band of its mid-step radius: the mean, the spread and the
     mean absolute value. In a window of the report interval ending at each report time.

Usage:

    python -I scripts/reads/r61-rim.py RUN [RUN ...] [--every 6000] [--start T] [--end T] [--limit-rows N]

RUN is a run directory, or an arm name under runs/ (the newest run directory of it). --start and
--end are simulated seconds that clip the positions read; --limit-rows stops after N rows (a proof
that the parse works; it does not give a result worth reading). Report times are the multiples of
--every inside the rows read, each taken from the sample nearest it; a slice holding none reports
its last row and says so.

What the tables cannot say. Dwell and speeds are read at the positions interval (10 s), so a body that
crosses a band edge and comes back inside one interval is not seen, and a step shorter than the
interval's resolution is not a speed. A body born inside the outer band is not an entry, and a body
already inside it when the data begins is not followed, since neither crossed. A dwell belongs to the
window its entry fell in, and a window's last entries are censored by the end of the data rather than
by the end of the window. A field stamp is read when one lies within --field-gap seconds of the
sample (they are written every 100 s, and only for part of a run). Memory is one row of bodies and a
few dictionaries; a run reads in one pass.

Standard library only. Reads positions through reads/runrec.py and the tank's geometry through
positions-read.py, and writes nothing. Exits 0 on a run it could read, 2 on a usage error or a run it
could not find or read.
"""
import argparse
import bisect
import importlib.util
import json
import math
import os
import re
import sys
import time
from array import array

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'scripts', 'reads'))
import runrec  # noqa: E402

_spec = importlib.util.spec_from_file_location('positions_read', os.path.join(ROOT, 'scripts', 'positions-read.py'))
_pr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_pr)

BAND_LABELS = ['0-5', '5-15', '15-30', '30-60', '>60']
NB = len(BAND_LABELS)
SECTORS = ['+x', '+x+z', '+z', '-x+z', '-x', '-x-z', '-z', '+x-z']
DEPTH_LABELS = ['shallow', 'middle', 'deep']
KINDS = ['absorptive', 'jointed', 'photosynthetic', 'none of three']
RIM_M = 15.0
FIELD_RE = re.compile(r'^(\d{9})\.matter\.f32$')


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def band_of(d):
    if d < 5.0:
        return 0
    if d < 15.0:
        return 1
    if d < 30.0:
        return 2
    if d < 60.0:
        return 3
    return 4


def sector_of(dx, dz):
    return int(((math.degrees(math.atan2(dz, dx)) + 360.0 + 22.5) % 360.0) // 45.0)


# ---------------------------------------------------------------- the run

def find_run(arg, runs_root):
    path = arg if os.path.isdir(arg) else os.path.join(runs_root, arg)
    try:
        return runrec.run_directory(path)
    except runrec.Refusal as e:
        fail(f'{arg}: {e}')


class Run:
    """One run's tank, its bed, its floor-depth bins and the per-cell maps the field tallies use."""

    def __init__(self, path):
        self.path = path
        cfg_path = os.path.join(path, 'config.json')
        if not os.path.isfile(cfg_path):
            fail(f'{path}: no config.json, so the tank has no size')
        with open(cfg_path, encoding='utf-8') as f:
            config = json.load(f)
        self.config_hash = config.get('configHash', '?')
        water = _pr.world_of(config)
        if not isinstance(water, _pr.Tank):
            fail(f'{path}: config.json describes {water}, and this reader is for a tank')
        self.water = water
        self.R = water.radius
        self.area = float(_pr.find_field(config, 'worldAreaSquareMetres'))
        self.run_json = {}
        rj = os.path.join(path, 'run.json')
        if os.path.isfile(rj):
            with open(rj, encoding='utf-8') as f:
                self.run_json = json.load(f)

        if not runrec.has_positions(path):
            fail(f'{path}: no positions file')

        fields = os.path.join(path, 'fields')
        self.fields_dir = fields if os.path.isdir(fields) else None
        self.stamps = []
        self.bed = None
        self.n = None
        self.matter_dims = (67, 10, 67, 5.0)
        if self.fields_dir:
            layout = {}
            lp = os.path.join(fields, 'layout.json')
            if os.path.isfile(lp):
                with open(lp, encoding='utf-8') as f:
                    layout = json.load(f)
            bedl = layout.get('bed', {'cellsX': math.ceil(2 * self.R), 'cellsZ': math.ceil(2 * self.R)})
            self.n = int(bedl['cellsX'])
            if int(bedl['cellsZ']) != self.n or float(bedl.get('cellMetres', 1)) != 1.0:
                fail(f'{path}: fields/layout.json describes a bed this reader does not read: {bedl}')
            m = layout.get('matter')
            if m:
                self.matter_dims = (int(m['cellsX']), int(m['cellsY']), int(m['cellsZ']), float(m['cellMetres']))
            bp = os.path.join(fields, 'bed.f32')
            if os.path.isfile(bp):
                self.bed = array('f')
                with open(bp, 'rb') as f:
                    self.bed.fromfile(f, self.n * self.n)
                if sys.byteorder == 'big':
                    self.bed.byteswap()
            self.stamps = sorted(int(m.group(1)) for m in
                                 (FIELD_RE.match(x) for x in os.listdir(fields)) if m)

        self.dep_edges = None
        self.build_cells()

    # -- bins by floor depth, tallies by cell
    def build_cells(self):
        R = self.R
        self.cells1 = None
        self.cells5 = None
        self.area1 = None
        self.area5 = None
        if self.bed is None:
            return
        n = self.n
        floors = []
        raw1 = []
        for ix in range(n):
            for iz in range(n):
                dx = ix + 0.5 - R
                dz = iz + 0.5 - R
                r = math.hypot(dx, dz)
                if r > R:
                    continue
                fl = -self.bed[ix * n + iz]
                floors.append(fl)
                raw1.append((ix * n + iz, band_of(R - r), sector_of(dx, dz), fl))
        floors.sort()
        k = len(floors)
        self.dep_edges = (floors[k // 3], floors[(2 * k) // 3])
        self.cells1 = [(i, b, s, self.dep_bin(fl)) for i, b, s, fl in raw1]

        cx, cy, cz, cm = self.matter_dims
        raw5 = []
        for ix in range(cx):
            for iz in range(cz):
                x = (ix + 0.5) * cm
                z = (iz + 0.5) * cm
                dx = x - R
                dz = z - R
                r = math.hypot(dx, dz)
                if r > R:
                    continue
                bx = min(n - 1, max(0, int(x)))
                bz = min(n - 1, max(0, int(z)))
                fl = -self.bed[bx * n + bz]
                raw5.append((ix * cz + iz, band_of(R - r), sector_of(dx, dz), self.dep_bin(fl)))
        self.cells5 = raw5
        self.area1 = area_tally(self.cells1)
        self.area5 = area_tally(self.cells5)

    def dep_bin(self, floor):
        lo, hi = self.dep_edges
        return 0 if floor < lo else (1 if floor < hi else 2)

    def floor_at(self, x, z):
        n = self.n
        ix = min(n - 1, max(0, int(x)))
        iz = min(n - 1, max(0, int(z)))
        return -self.bed[ix * n + iz]

    def nearest_stamp(self, t, gap):
        if not self.stamps:
            return None
        i = bisect.bisect_left(self.stamps, t)
        best = None
        for j in (i - 1, i):
            if 0 <= j < len(self.stamps):
                if best is None or abs(self.stamps[j] - t) < abs(best - t):
                    best = self.stamps[j]
        return best if best is not None and abs(best - t) <= gap else None


def zeros():
    return [0.0] * NB


def new_split():
    """band totals, sector x band, depth x band."""
    return [0.0] * NB, [zeros() for _ in SECTORS], [zeros() for _ in DEPTH_LABELS]


def area_tally(cells):
    tb, ts, td = new_split()
    for _, b, s, d in cells:
        tb[b] += 1
        ts[s][b] += 1
        td[d][b] += 1
    return tb, ts, td


def stock_tally(cells, vals):
    tb, ts, td = new_split()
    for i, b, s, d in cells:
        v = vals[i]
        tb[b] += v
        ts[s][b] += v
        td[d][b] += v
    return tb, ts, td


def read_f32(path, count):
    a = array('f')
    with open(path, 'rb') as f:
        a.fromfile(f, count)
    if sys.byteorder == 'big':
        a.byteswap()
    return a


def matter_columns(run, path):
    cx, cy, cz, _ = run.matter_dims
    m = read_f32(path, cx * cy * cz)
    col = [0.0] * (cx * cz)
    plane = cx * cz
    for iy in range(cy):
        base = iy * plane
        for j in range(plane):
            col[j] += m[base + j]
    return col


# ---------------------------------------------------------------- statistics

def quantile(sorted_values, q):
    if not sorted_values:
        return None
    k = (len(sorted_values) - 1) * q
    lo = int(math.floor(k))
    hi = min(len(sorted_values) - 1, lo + 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (k - lo)


def km_quantiles(pairs, qs):
    """Kaplan-Meier quantiles of (duration, event) pairs, event 1 or 0 (censored). None where the
    curve never falls that far. Durations tie in steps of the sampling interval."""
    pairs = sorted(pairs)
    at_risk = len(pairs)
    surv = 1.0
    out = [None] * len(qs)
    i = 0
    while i < len(pairs):
        u = pairs[i][0]
        d = c = 0
        while i < len(pairs) and pairs[i][0] == u:
            if pairs[i][1]:
                d += 1
            else:
                c += 1
            i += 1
        if d:
            surv *= 1.0 - d / at_risk
        at_risk -= d + c
        for k, q in enumerate(qs):
            if out[k] is None and surv <= 1.0 - q + 1e-12:
                out[k] = u
    return out


def fnum(v, places=1):
    return '-' if v is None else f'{v:.{places}f}'


def ratio_cell(obs_share, area_share):
    if area_share <= 0:
        return None
    return obs_share / area_share


# ---------------------------------------------------------------- the profile, printed

def print_profile(run, t_target, row_t, bodies, args, out):
    """The radial profile at one sample. `bodies` is the row's list [id, x, y, z, flags]."""
    R = run.R
    have_bed = run.bed is not None
    tb, ts, td = new_split()
    kinds = [zeros() for _ in KINDS]
    kind_n = [0] * len(KINDS)
    N = len(bodies)

    for _, x, _y, z, fl in bodies:
        dx = x - R
        dz = z - R
        b = band_of(R - math.sqrt(dx * dx + dz * dz))
        tb[b] += 1
        if have_bed:
            ts[sector_of(dx, dz)][b] += 1
            td[run.dep_bin(run.floor_at(x, z))][b] += 1
        for k, bit in ((0, 1), (1, 2), (2, 4)):
            if fl & bit:
                kinds[k][b] += 1
                kind_n[k] += 1
        if not fl & 7:
            kinds[3][b] += 1
            kind_n[3] += 1
    if not have_bed:
        out('  (no fields/bed.f32: the bands are given, the splits by sector and floor depth are not)')

    stamp = run.nearest_stamp(row_t, args.field_gap) if run.fields_dir else None
    matter = snow = None
    if stamp is not None:
        mp = os.path.join(run.fields_dir, '%09d.matter.f32' % stamp)
        sp = os.path.join(run.fields_dir, '%09d.snow-columns.f32' % stamp)
        if os.path.isfile(mp) and run.cells5:
            matter = stock_tally(run.cells5, matter_columns(run, mp))
        if os.path.isfile(sp) and run.cells1:
            snow = stock_tally(run.cells1, read_f32(sp, run.n * run.n))
    out('')
    why = '' if abs(row_t - t_target) < 0.5 else f' (asked for {t_target:g})'
    fnote = (f'fields stamp {stamp} ({stamp - row_t:+g} s)' if stamp is not None else
             ('no fields stamp within %g s' % args.field_gap if run.fields_dir else 'no fields folder'))
    out(f'-- profile at t = {row_t:g} s{why}: {N} bodies; {fnote}')

    area_b = run.area1[0] if run.area1 else None
    area_tot = sum(area_b) if area_b else 0.0
    area5 = run.area5[0] if run.area5 else None
    area5_tot = sum(area5) if area5 else 0.0
    mt = sum(matter[0]) if matter else 0.0
    st = sum(snow[0]) if snow else 0.0

    out('  band m     area   bodies     x    matter     x     snow     x   [x = share / area share; area5 = matter grid]')
    for b in range(NB):
        a1 = area_b[b] / area_tot if area_b else analytic_area_share(run, b)
        bs = tb[b] / N if N else 0.0
        cells = [f'{BAND_LABELS[b]:>8}', f'{a1:8.3f}', f'{bs:8.3f}', fnum(ratio_cell(bs, a1), 2).rjust(6)]
        if matter and area5_tot:
            a5 = area5[b] / area5_tot
            ms = matter[0][b] / mt if mt else 0.0
            cells += [f'{ms:8.3f}', fnum(ratio_cell(ms, a5), 2).rjust(6)]
        else:
            cells += ['       -', '     -']
        if snow and area_tot:
            ss = snow[0][b] / st if st else 0.0
            cells += [f'{ss:8.3f}', fnum(ratio_cell(ss, a1), 2).rjust(6)]
        else:
            cells += ['       -', '     -']
        if matter and area5_tot:
            cells.append(f'  area5 {area5[b] / area5_tot:.3f}')
        out('  ' + ' '.join(cells))

    if not have_bed:
        return

    def split_table(title, labels, body_rows, matter_rows, snow_rows, area_rows, area5_rows, total_n):
        out(f'  {title}: stock share in the cell / area share in the cell (1.00 = the tank mean); '
            f'bodies carry their count')
        out('  ' + ' ' * 14 + ''.join(f'{lab:>14}' for lab in BAND_LABELS))
        for i, lab in enumerate(labels):
            line = f'  {lab:<14}'
            for b in range(NB):
                a1 = area_rows[i][b] / area_tot
                v = ratio_cell(body_rows[i][b] / N, a1) if N else None
                line += (f'{fnum(v, 2):>6}({int(body_rows[i][b]):>5})' if v is not None else f'{"-":>14}')[-14:].rjust(14)
            out(line + '   bodies')
            if matter:
                line = f'  {"":<14}'
                for b in range(NB):
                    a5 = area5_rows[i][b] / area5_tot if area5_tot else 0
                    v = ratio_cell(matter_rows[i][b] / mt, a5) if mt else None
                    line += f'{fnum(v, 2):>14}'
                out(line + '   matter')
            if snow:
                line = f'  {"":<14}'
                for b in range(NB):
                    a1 = area_rows[i][b] / area_tot
                    v = ratio_cell(snow_rows[i][b] / st, a1) if st else None
                    line += f'{fnum(v, 2):>14}'
                out(line + '   snow')

    split_table('by compass sector', SECTORS, ts, matter[1] if matter else None, snow[1] if snow else None,
                run.area1[1], run.area5[1], N)
    lo, hi = run.dep_edges
    split_table(f'by floor depth under the column (terciles of the floor area: shallow < {lo:.1f} m, '
                f'middle < {hi:.1f} m, deep above)', DEPTH_LABELS, td, matter[2] if matter else None,
                snow[2] if snow else None, run.area1[2], run.area5[2], N)

    out('  by kind of body (flags overlap; share of that kind in the band / area share of the band)')
    out('  ' + ' ' * 14 + ''.join(f'{lab:>10}' for lab in BAND_LABELS) + '      n')
    for k, lab in enumerate(KINDS):
        line = f'  {lab:<14}'
        for b in range(NB):
            a1 = area_b[b] / area_tot
            v = ratio_cell(kinds[k][b] / kind_n[k], a1) if kind_n[k] else None
            line += f'{fnum(v, 2):>10}'
        out(line + f'{kind_n[k]:>7}')


def analytic_area_share(run, b):
    R = run.R
    edges = [0.0, 5.0, 15.0, 30.0, 60.0, 1e18]
    ro = R - edges[b]
    ri = max(0.0, R - edges[b + 1])
    return (ro * ro - ri * ri) / (R * R)


# ---------------------------------------------------------------- the pass

class Pass:
    """Dwell and speed accumulation over a run's rows, in one pass."""

    def __init__(self, run, args):
        self.run = run
        self.args = args
        R = run.R
        self.R = R
        self.every = args.every
        # radii (from the axis) at the band edges; a band is "r above e" for the rim.
        self.e_rim = R - RIM_M
        # the control band has the rim's floor area: pi (ro^2 - ri^2) equal, outer edge --control-from in
        rim_area = R * R - (R - RIM_M) ** 2
        self.c_out = R - args.control_from
        inner_sq = self.c_out ** 2 - rim_area
        if inner_sq <= 0:
            fail(f'{run.path}: no control band of the rim\'s area fits {args.control_from:g} m in from the glass')
        self.c_in = math.sqrt(inner_sq)
        self.ctl_from = args.control_from
        self.ctl_to = R - self.c_in
        self.edges_r = [R - 5.0, R - 15.0, R - 30.0, R - 60.0]
        self.prev = {}
        self.prev_t = None
        self.dt0 = None
        self.open = {'rim': {}, 'ctl': {}}
        # dwell[kind][window] = [array durations, array codes]  code 1 left, 2 died, 0 censored
        self.dwell = {'rim': {}, 'ctl': {}}
        # speeds[window] = list of 7 accumulators [n, sum, sumsq, sumabs]
        self.speeds = {}
        self.rows = 0
        self.bodies = 0
        self.reports = []
        self.t_first = None
        self.t_last = None
        self.next_target = None
        self.prev_bodies = None

    def window(self, t):
        return -int(-t // self.every)

    def close(self, kind, bid, t_end, code):
        t0 = self.open[kind].pop(bid)
        w = self.window(t0)
        d = self.dwell[kind].get(w)
        if d is None:
            d = self.dwell[kind][w] = [array('f'), array('b')]
        d[0].append(t_end - t0)
        d[1].append(code)

    def band_idx(self, r):
        e = self.edges_r
        if r > e[0]:
            return 0
        if r > e[1]:
            return 1
        if r > e[2]:
            return 2
        if r > e[3]:
            return 3
        return 4

    def step(self, t, bodies):
        R = self.R
        sqrt = math.sqrt
        cur = {}
        for b in bodies:
            dx = b[1] - R
            dz = b[3] - R
            cur[b[0]] = sqrt(dx * dx + dz * dz)

        prev = self.prev
        if self.prev_t is not None:
            dt = t - self.prev_t
            if self.dt0 is None:
                self.dt0 = dt
            if dt <= 0 or dt > 1.5 * self.dt0:
                # a gap in the record: follow nothing across it
                for kind in ('rim', 'ctl'):
                    for bid in list(self.open[kind]):
                        self.close(kind, bid, self.prev_t, 0)
                prev = {}
                dt = None
        else:
            dt = None

        e_rim = self.e_rim
        c_in = self.c_in
        c_out = self.c_out
        open_rim = self.open['rim']
        open_ctl = self.open['ctl']
        w = self.window(t)
        acc = None
        if dt:
            acc = self.speeds.get(w)
            if acc is None:
                acc = self.speeds[w] = [[0, 0.0, 0.0, 0.0] for _ in range(7)]
        band_idx = self.band_idx

        for bid, r in cur.items():
            p = prev.get(bid)
            in_rim = r > e_rim
            in_ctl = c_in < r <= c_out
            if in_rim:
                if p is not None and p <= e_rim and bid not in open_rim:
                    open_rim[bid] = t
            elif bid in open_rim:
                self.close('rim', bid, t, 1)
            if in_ctl:
                if p is not None and not (c_in < p <= c_out) and bid not in open_ctl:
                    open_ctl[bid] = t
            elif bid in open_ctl:
                self.close('ctl', bid, t, 1)
            if p is not None and dt:
                v = (r - p) / dt
                m = 0.5 * (r + p)
                a = acc[band_idx(m)]
                a[0] += 1
                a[1] += v
                a[2] += v * v
                a[3] += abs(v)
                if m > e_rim:
                    a = acc[5]
                    a[0] += 1
                    a[1] += v
                    a[2] += v * v
                    a[3] += abs(v)
                if c_in < m <= c_out:
                    a = acc[6]
                    a[0] += 1
                    a[1] += v
                    a[2] += v * v
                    a[3] += abs(v)

        if open_rim:
            for bid in [i for i in open_rim if i not in cur]:
                self.close('rim', bid, t, 2)
        if open_ctl:
            for bid in [i for i in open_ctl if i not in cur]:
                self.close('ctl', bid, t, 2)

        self.prev = cur
        self.prev_t = t
        self.rows += 1
        self.bodies += len(bodies)

    def finish(self):
        """Whatever is still inside a band is censored at the last sample."""
        for kind in ('rim', 'ctl'):
            for bid in list(self.open[kind]):
                self.close(kind, bid, self.prev_t, 0)


def stream_run(run, args, out):
    ps = Pass(run, args)
    every = args.every
    rows_iter = runrec.positions_lines(run.path)
    prev_row = None  # (t, bodies) of the row before
    next_target = None
    t0 = time.time()

    for line in rows_iter:
        t = _pr.sample_time(line)
        if t is None:
            continue
        if args.start is not None and t < args.start:
            continue
        if args.end is not None and t > args.end:
            break
        if args.limit_rows is not None and ps.rows >= args.limit_rows:
            break
        row = json.loads(line)
        bodies = row['b']
        if ps.t_first is None:
            ps.t_first = t
            next_target = math.ceil(t / every - 1e-9) * every
        ps.t_last = t

        # a report: the row nearest each multiple of --every
        while next_target is not None and t >= next_target - 1e-9:
            if prev_row is not None and (next_target - prev_row[0]) < (t - next_target):
                chosen_t, chosen = prev_row
            else:
                chosen_t, chosen = t, bodies
            if chosen_t - next_target <= 2 * (ps.dt0 or every) and next_target - chosen_t <= 2 * (ps.dt0 or every):
                print_profile(run, next_target, chosen_t, chosen, args, out)
                ps.reports.append(next_target)
            next_target += every

        ps.step(t, bodies)
        prev_row = (t, bodies)

    ps.finish()
    if not ps.reports and prev_row is not None:
        out('')
        out(f'(no multiple of {every:g} s inside the rows read; the profile is the last row, t = {prev_row[0]:g})')
        print_profile(run, prev_row[0], prev_row[0], prev_row[1], args, out)
        ps.reports.append(prev_row[0])
    ps.seconds = time.time() - t0
    ps.torn = rows_iter.notes
    return ps


# ---------------------------------------------------------------- dwell and speeds, printed

def window_label(w, every, t_first):
    lo = (w - 1) * every
    mark = '*' if t_first is not None and lo < t_first else ' '
    return f'{lo:g}-{w * every:g}{mark}'


def print_dwell(ps, out):
    every = ps.every
    for kind, title in (('rim', f'the outer {RIM_M:g} m (0-{RIM_M:g} m from the glass)'),
                        ('ctl', f'the control band, {ps.ctl_from:g}-{ps.ctl_to:.1f} m from the glass, '
                                f'the same floor area')):
        out('')
        out(f'== dwell in {title}: seconds from entering to leaving it or to the sample it was gone from')
        out('   window (entry)      entered  left  died  cens |  finished: median     q1     q3 | '
            'Kaplan-Meier: median     q1     q3')
        data = ps.dwell[kind]
        allp = []
        for w in sorted(data):
            durs, codes = data[w]
            pairs = [(durs[i], 1 if codes[i] else 0) for i in range(len(durs))]
            allp.extend(pairs)
            out(dwell_line(window_label(w, ps.every, ps.t_first), durs, codes, pairs))
        if len(data) > 1:
            durs = array('f', [p[0] for p in allp])
            codes = array('b')
            for w in sorted(data):
                codes.extend(data[w][1])
            out(dwell_line('all windows', durs, codes, allp))
        if not data:
            out('   no entries')


def dwell_line(label, durs, codes, pairs):
    left = sum(1 for c in codes if c == 1)
    died = sum(1 for c in codes if c == 2)
    cens = sum(1 for c in codes if c == 0)
    fin = sorted(d for d, c in zip(durs, codes) if c)
    q = [quantile(fin, x) for x in (0.5, 0.25, 0.75)]
    maxd = max(durs) if len(durs) else 0
    km = km_quantiles(pairs, (0.5, 0.25, 0.75))
    kmtxt = [(f'>{maxd:.0f}' if v is None else f'{v:.0f}') for v in km]
    return (f'   {label:<18}{len(durs):>9}{left:>6}{died:>6}{cens:>6} |  '
            f'{fnum(q[0], 0):>16}{fnum(q[1], 0):>7}{fnum(q[2], 0):>7} | '
            f'{kmtxt[0]:>20}{kmtxt[1]:>7}{kmtxt[2]:>7}')


def print_speeds(ps, out):
    out('')
    out('== radial speeds, m/s, positive away from the centre (toward the glass): between consecutive samples, '
        'by band of the step\'s mid radius')
    out('   window (ending)    band            steps     mean      sd   mean|v|')
    names = BAND_LABELS + [f'0-{RIM_M:g} (rim)', f'{ps.ctl_from:g}-{ps.ctl_to:.1f} (ctl)']
    for w in sorted(ps.speeds):
        acc = ps.speeds[w]
        lab = window_label(w, ps.every, ps.t_first)
        for k, name in enumerate(names):
            n, s, sq, sa = acc[k]
            if n == 0:
                out(f'   {lab:<18} {name:<14}{0:>8}        -       -         -')
                continue
            mean = s / n
            sd = math.sqrt(max(0.0, sq / n - mean * mean))
            out(f'   {lab:<18} {name:<14}{n:>8}{mean:>9.4f}{sd:>8.4f}{sa / n:>10.4f}')
    out('   (* = the window opens before the first row read, so it is partial)')


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(
        description='Round 61 rim reader: radial profile, dwell and radial speeds against the glass.')
    ap.add_argument('runs', nargs='+', help='run directories, or arm names under runs/')
    ap.add_argument('--every', type=float, default=6000.0, help='report interval, seconds (default 6000)')
    ap.add_argument('--start', type=float, default=None, help='first second to read')
    ap.add_argument('--end', type=float, default=None, help='last second to read')
    ap.add_argument('--limit-rows', type=int, default=None, help='stop after this many rows (a parse check)')
    ap.add_argument('--control-from', type=float, default=30.0,
                    help='metres from the glass where the control band starts (default 30); '
                         'its far edge is set so its floor area equals the outer 15 m')
    ap.add_argument('--field-gap', type=float, default=150.0,
                    help='largest gap, seconds, between a sample and the fields stamp read for it (default 150)')
    ap.add_argument('--runs-root', default=None, help='the runs/ directory (default: the repository\'s own)')
    args = ap.parse_args()
    if args.every <= 0:
        ap.error('--every must be positive')
    runs_root = args.runs_root or os.path.join(ROOT, 'runs')

    def out(text=''):
        print(text, flush=True)

    status = 0
    for arg in args.runs:
        path = find_run(arg, runs_root)
        run = Run(path)
        rj = run.run_json
        arm = rj.get('arm') or arg
        out('=' * 100)
        win = f'{args.start:g}' if args.start is not None else 'first row'
        win += ' to ' + (f'{args.end:g}' if args.end is not None else 'last row')
        if args.limit_rows is not None:
            win += f' (at most {args.limit_rows} rows)'
        out(f'run {arm} | {os.path.basename(path)} | configHash {run.config_hash} | window {win} | '
            f'report every {args.every:g} s')
        out(f'{run.water} | R {run.R:.2f} m; axis ({run.R:.2f}, {run.R:.2f}); seed {rj.get("seed")}; '
            f'run.json: {rj.get("status")} ({rj.get("ending")}) at {rj.get("simulatedSeconds", "?")} s')
        out(f'fields: {len(run.stamps)} stamps' + (f' ({run.stamps[0]} to {run.stamps[-1]} s)' if run.stamps else '')
            + (f'; bed {run.n} x {run.n}, floor depth terciles {run.dep_edges[0]:.1f} / {run.dep_edges[1]:.1f} m'
               if run.bed is not None else '; no bed'))
        ps = stream_run(run, args, out)
        out('')
        out(f'-- read {ps.rows} rows, t = {ps.t_first} to {ps.t_last}, {ps.bodies} body-rows, '
            f'{ps.seconds:.1f} s; sampling step {ps.dt0} s')
        for note in ps.torn:
            out('   ' + note)
        if ps.rows < 2:
            out('   fewer than two rows: no dwell and no speeds')
            continue
        print_dwell(ps, out)
        print_speeds(ps, out)
    return status


if __name__ == '__main__':
    sys.exit(main())
