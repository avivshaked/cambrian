"""Round 58's hollows screens against D155's bar for a material change.

    python scripts/reads/r58-hollows-read.py --h0 <run dir> --h0-corpses <film dir>
        --screen H1=<run dir>,<film dir> [--screen H2=...] --spread <run dir> <run dir> <run dir>
        [--at 10000] [--window 1000]

H0 is the same seed without pits (round 57 seed 2, which the hollows-off replay shows round 58's
build reproduces). A film dir is `Evosim.Farm --film-window <run dir> <at> <at+1> <film dir> --fps 1`,
whose `corpses.jsonl.gz` is the only record of where the corpses lie. The spread runs are round 57's
three seeds. Everything below was fixed before H1 and H2 were read (logbook/specs/r58-build/
hollows-design.md, D155); a reading that changes a definition after the fact says so in the record.

The pits. They are found from the beds, not from the config: the floor outside the pits is the same
bits with pits on or off, so a cell where a screen's `bed.f32` lies below H0's is in a pit. Cells are
grouped into pits by adjacency and the count must equal the config's `bedHollowCount`. A pit's floor
is its cells at least 95% of its deepest drop below H0. H0 is read on the same cells.

D155's three tests, each at `--at` seconds:
  1. The catch: the settled corpses' joules per square metre inside the pits (every pit cell, rim to
     floor) against the rest of the bed. Met at 5 or more.
  2. The dwellers: bodies born in the run, founders apart, within 2 m (in three dimensions) of a pit
     floor's cell centre, against the share of the bed within 2 m across of a floor cell, pooled over
     every positions row in the window (`--window` seconds up to `--at`). Read by guild (leaf, stomach,
     mixotroph, jointed, plain, as positions-read.py). Met when any guild with at least 10
     body-samples in the zone is there at twice its share or more.
  3. The world: the living count and the shares of photosynthetic, absorptive and jointed bodies,
     each the mean of the stats rows in the window. Met when any of them falls outside the range of
     round 57's three seeds over the same window.
Read beside them, deciding nothing: the floor's snow stock inside and outside, the same zone among
bodies within 2 m of the bed anywhere, the founders' own ratio, births by pit dwellers per
body-sample against births elsewhere, and the pace.
"""
import argparse
import gzip
import json
import os
import sys

import numpy as np

GUILDS = ['leaf', 'stom', 'mixo', 'jnt', 'plain']
ZONE_METRES = 2.0
FLOOR_SHARE = 0.95
MIN_ZONE_SAMPLES = 10
CATCH_BAR = 5.0
DWELL_BAR = 2.0


def fail(msg):
    print('r58-hollows-read: ' + msg, file=sys.stderr)
    sys.exit(2)


def guild_of(flags):
    a, p = flags & 1, flags & 4
    if a and p:
        return 'mixo'
    if a:
        return 'stom'
    if p:
        return 'leaf'
    if flags & 2:
        return 'jnt'
    return 'plain'


def opener(path):
    if os.path.exists(path):
        return open(path, 'rt', encoding='utf-8')
    if os.path.exists(path + '.gz'):
        return gzip.open(path + '.gz', 'rt', encoding='utf-8')
    fail(f'neither {path} nor {path}.gz')


class Run:
    def __init__(self, name, run_dir, film_dir):
        self.name, self.dir, self.film = name, run_dir, film_dir
        with open(os.path.join(run_dir, 'fields', 'layout.json')) as f:
            bed = json.load(f)['bed']
        self.nx, self.nz, self.cell = bed['cellsX'], bed['cellsZ'], bed['cellMetres']
        self.bed = np.fromfile(os.path.join(run_dir, 'fields', 'bed.f32'), dtype='<f4').reshape(self.nx, self.nz)
        with open(os.path.join(run_dir, 'config.json')) as f:
            text = f.read()
        self.config = json.loads(text)
        with open(os.path.join(run_dir, 'run.json')) as f:
            self.manifest = json.load(f)

    def setting(self, name):
        def find(node):
            if isinstance(node, dict):
                for k, v in node.items():
                    if k == name:
                        return v
                    got = find(v)
                    if got is not None:
                        return got
            return None
        return find(self.config)

    def cells_of(self, x, z):
        ix = np.clip(np.floor(np.asarray(x) / self.cell).astype(int), 0, self.nx - 1)
        iz = np.clip(np.floor(np.asarray(z) / self.cell).astype(int), 0, self.nz - 1)
        return ix, iz

    def stats_window(self, lo, hi):
        rows = []
        with opener(os.path.join(self.dir, 'stats.jsonl')) as f:
            for line in f:
                r = json.loads(line)
                if r['t'] > hi:
                    break
                if r['t'] >= lo:
                    rows.append(r)
        if not rows:
            fail(f'{self.name}: no stats rows between {lo} and {hi} s')
        return rows

    def founders_and_births(self, lo, hi):
        founders, births = set(), []
        with opener(os.path.join(self.dir, 'lineage.jsonl')) as f:
            for line in f:
                r = json.loads(line)
                if r.get('e') != 'b':
                    continue
                if r['t'] > hi:
                    break
                if r.get('p', -1) < 0:
                    founders.add(r['id'])
                elif r['t'] > lo:
                    births.append((r['t'], r['p']))
        return founders, births

    def positions(self, lo, hi):
        with opener(os.path.join(self.dir, 'positions.jsonl')) as f:
            for line in f:
                r = json.loads(line)
                if r['t'] > hi:
                    break
                if r['t'] >= lo:
                    yield r['t'], np.array(r['b'], dtype=float).reshape(-1, 5)

    def corpses(self, at):
        path = os.path.join(self.film, 'corpses.jsonl.gz')
        first, step = {}, None
        with gzip.open(path, 'rt', encoding='utf-8') as f:
            for line in f:
                r = json.loads(line)
                if r['k'] == 'corpse':
                    first[r['id']] = r['first']
                elif r['k'] == 'step' and step is None:
                    step = r
        if step is None:
            fail(f'{self.name}: no step row in {path}')
        if abs(step['t'] - at) > 1:
            fail(f'{self.name}: the film\'s first step is at {step["t"]} s, not {at} s')
        h = np.array(step['h'], dtype=float).reshape(-1, 6)
        joules = np.array([first[int(i)] for i in h[:, 0]]) * h[:, 4]
        return h[:, 1], h[:, 2], h[:, 3], joules, h[:, 5] == 1, step['t']


def find_pits(screen, h0):
    drop = h0.bed - screen.bed
    inside = drop > 1e-3
    label = np.zeros(drop.shape, dtype=int)
    n = 0
    for i, j in zip(*np.nonzero(inside)):
        if label[i, j]:
            continue
        n += 1
        stack = [(i, j)]
        label[i, j] = n
        while stack:
            a, b = stack.pop()
            for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                c, d = a + da, b + db
                if 0 <= c < drop.shape[0] and 0 <= d < drop.shape[1] and inside[c, d] and not label[c, d]:
                    label[c, d] = n
                    stack.append((c, d))
    floor = np.zeros(drop.shape, dtype=bool)
    for k in range(1, n + 1):
        cells = label == k
        floor |= cells & (drop >= FLOOR_SHARE * drop[cells].max())
    return inside, floor, n, drop


def zone_cells(floor, nx, nz, cell):
    fi, fj = np.nonzero(floor)
    gi, gj = np.meshgrid(np.arange(nx), np.arange(nz), indexing='ij')
    zone = np.zeros((nx, nz), dtype=bool)
    for a, b in zip(fi, fj):
        zone |= ((gi - a) ** 2 + (gj - b) ** 2) * cell * cell <= ZONE_METRES ** 2
    return zone


def ratio(a, b):
    return a / b if b > 0 else float('nan')


def read_catch(run, inside, at, label):
    x, y, z, joules, settled, t = run.corpses(at)
    ix, iz = run.cells_of(x, z)
    pit = inside[ix, iz]
    a_in, a_out = inside.sum() * run.cell ** 2, (~inside).sum() * run.cell ** 2
    j_in, j_out = joules[settled & pit].sum(), joules[settled & ~pit].sum()
    stock = np.fromfile(os.path.join(run.dir, 'fields', f'{int(round(at)):09d}.snow-floor.f32'), dtype='<f4').reshape(run.nx, run.nz)
    s_in, s_out = stock[inside].sum() / a_in, stock[~inside].sum() / a_out
    r = ratio(j_in / a_in, j_out / a_out)
    print(f'  {label} catch at {t:g} s: {int((settled & pit).sum())} settled corpses in the pits, {j_in:.0f} J on {a_in:.0f} m2 '
          f'({j_in / a_in:.2f} J/m2); outside {int((settled & ~pit).sum())}, {j_out:.0f} J on {a_out:.0f} m2 ({j_out / a_out:.3f} J/m2); '
          f'ratio {r:.2f}; unsettled corpses {int((~settled).sum())}')
    print(f'  {label} floor snow stock: {s_in:.3f} J/m2 inside, {s_out:.3f} outside, ratio {ratio(s_in, s_out):.2f}')
    return r


def read_dwellers(run, floor_bed, floor_pts, zone, lo, hi, label):
    a_zone, a_bed = zone.sum(), zone.size
    share = a_zone / a_bed
    founders, births = run.founders_and_births(lo, hi)
    n = {g: 0 for g in GUILDS}
    nz = {g: 0 for g in GUILDS}
    nbed = {g: 0 for g in GUILDS}
    nzbed = {g: 0 for g in GUILDS}
    f_all = f_zone = 0
    rows = 0
    where = {}  # body id -> (sample second, in zone) at the latest sample, for the births
    zone_samples = other_samples = 0
    pit_births = other_births = 0
    births = sorted(births)
    bi = 0
    last_in = {}
    for t, b in run.positions(lo, hi):
        while bi < len(births) and births[bi][0] < t:
            parent = births[bi][1]
            if parent in last_in:
                if last_in[parent]:
                    pit_births += 1
                else:
                    other_births += 1
            bi += 1
        rows += 1
        ids, x, y, z, flags = b[:, 0].astype(int), b[:, 1], b[:, 2], b[:, 3], b[:, 4].astype(int)
        ix, iz = run.cells_of(x, z)
        near_bed = (y - run.bed[ix, iz]) <= ZONE_METRES
        p = np.stack([x, y, z], axis=1)
        d2 = ((p[:, None, :] - floor_pts[None, :, :]) ** 2).sum(axis=2).min(axis=1)
        in_zone = d2 <= ZONE_METRES ** 2
        last_in = dict(zip(ids.tolist(), in_zone.tolist()))
        zone_samples += int(in_zone.sum())
        other_samples += int((~in_zone).sum())
        is_founder = np.array([i in founders for i in ids.tolist()], dtype=bool)
        f_all += int(is_founder.sum())
        f_zone += int((is_founder & in_zone).sum())
        for k in np.nonzero(~is_founder)[0]:
            g = guild_of(int(flags[k]))
            n[g] += 1
            nz[g] += int(in_zone[k])
            nbed[g] += int(near_bed[k])
            nzbed[g] += int(in_zone[k] and near_bed[k])
    if rows == 0:
        fail(f'{run.name}: no positions rows between {lo} and {hi} s')
    print(f'  {label} dwellers, {rows} positions rows {lo:g} to {hi:g} s; the zone is {a_zone} of {a_bed} m2 '
          f'({share:.4f} of the bed)')
    met = False
    for g in GUILDS:
        r = ratio(nz[g] / n[g], share) if n[g] else float('nan')
        rb = ratio(nzbed[g] / nbed[g], share) if nbed[g] else float('nan')
        flag = ''
        if nz[g] >= MIN_ZONE_SAMPLES and r >= DWELL_BAR:
            met = True
            flag = '  <- at the bar'
        print(f'    {g:5s} born body-samples {n[g]:7d}, in the zone {nz[g]:6d}, ratio {r:6.2f}; '
              f'within 2 m of the bed {nbed[g]:6d}, of those in the zone {nzbed[g]:5d}, ratio {rb:6.2f}{flag}')
    print(f'    founders (apart) body-samples {f_all}, in the zone {f_zone}, ratio {ratio(f_zone / f_all, share) if f_all else float("nan"):.2f}')
    per_zone = ratio(pit_births, zone_samples)
    per_other = ratio(other_births, other_samples)
    print(f'    births by parents last seen in the zone {pit_births} over {zone_samples} body-samples, elsewhere '
          f'{other_births} over {other_samples}; per body-sample ratio {ratio(per_zone, per_other):.2f}')
    return met


def window_means(run, lo, hi):
    rows = run.stats_window(lo, hi)
    alive = np.array([r['alive'] for r in rows], dtype=float)
    out = {'alive': alive.mean()}
    for k in ('photosynthetic', 'absorptive', 'jointed'):
        out[k + ' share'] = np.mean([r[k] / r['alive'] if r['alive'] else 0 for r in rows])
    last = rows[-1]
    out['_pace'] = (last['wallTotalMs'] / 1000.0, last['t'])
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--h0', required=True)
    ap.add_argument('--h0-corpses', required=True)
    ap.add_argument('--screen', action='append', required=True, help='NAME=<run dir>,<film dir>')
    ap.add_argument('--spread', nargs=3, required=True)
    ap.add_argument('--at', type=float, default=10000)
    ap.add_argument('--window', type=float, default=1000)
    args = ap.parse_args()
    lo, hi = args.at - args.window, args.at

    h0 = Run('H0', args.h0, args.h0_corpses)
    spread = [window_means(Run(f'r57 {i}', d, None), lo, hi) for i, d in enumerate(args.spread)]
    h0_means = window_means(h0, lo, hi)
    keys = [k for k in h0_means if not k.startswith('_')]
    print(f'H0 {args.h0}')
    print('round 57 seed range over the window: ' + '; '.join(
        f'{k} {min(s[k] for s in spread):.4g} to {max(s[k] for s in spread):.4g}' for k in keys))

    for spec in args.screen:
        name, rest = spec.split('=', 1)
        run_dir, film_dir = rest.split(',', 1)
        run = Run(name, run_dir, film_dir)
        print(f'\n== {name} {run_dir}')
        if (run.nx, run.nz, run.cell) != (h0.nx, h0.nz, h0.cell):
            fail(f'{name}: its bed grid differs from H0\'s')
        inside, floor, count, drop = find_pits(run, h0)
        want = run.setting('bedHollowCount')
        print(f'  pits found {count} (config bedHollowCount {want}), {inside.sum()} m2 in all, floor {floor.sum()} m2, '
              f'deepest drop {drop.max():.2f} m; repose {run.setting("corpseReposeDegrees")} deg, slide '
              f'{run.setting("corpseSlideMetresPerSecond")} m/s')
        if count == 0 or count != want:
            fail(f'{name}: found {count} pits where the config asks for {want}')
        fi, fj = np.nonzero(floor)
        centre = lambda i: (i + 0.5) * run.cell
        pts_screen = np.stack([centre(fi), run.bed[fi, fj], centre(fj)], axis=1)
        pts_h0 = np.stack([centre(fi), h0.bed[fi, fj], centre(fj)], axis=1)
        zone = zone_cells(floor, run.nx, run.nz, run.cell)

        print('  -- test 1, the catch (bar: 5 or more)')
        r1 = read_catch(run, inside, args.at, name)
        r1_h0 = read_catch(h0, inside, args.at, 'H0 on the same cells,')
        print('  -- test 2, the dwellers (bar: 2 or more in a guild with 10 body-samples in the zone)')
        m2 = read_dwellers(run, run.bed, pts_screen, zone, lo, hi, name)
        read_dwellers(h0, h0.bed, pts_h0, zone, lo, hi, 'H0 on the same cells,')
        print('  -- test 3, the world (bar: outside round 57\'s three-seed range)')
        means = window_means(run, lo, hi)
        m3 = False
        for k in keys:
            lo_k, hi_k = min(s[k] for s in spread), max(s[k] for s in spread)
            out = not (lo_k <= means[k] <= hi_k)
            m3 |= out
            print(f'    {k:22s} {name} {means[k]:.4g}, H0 {h0_means[k]:.4g}, range {lo_k:.4g} to {hi_k:.4g}'
                  f'{"  <- outside" if out else ""}')
        (w, t), (w0, t0) = means['_pace'], h0_means['_pace']
        print(f'  pace: {name} {1000 * w / t:.0f} ms of wall per simulated second to {t:g} s at {run.manifest.get("threads")} threads; '
              f'H0 {1000 * w0 / t0:.0f} at {h0.manifest.get("threads")} threads (not a timing read: neither ran alone)')
        print(f'  ** {name}: catch {"MET" if r1 >= CATCH_BAR else "not met"} ({r1:.2f}, H0 {r1_h0:.2f}); dwellers '
              f'{"MET" if m2 else "not met"}; world {"MET" if m3 else "not met"}')


if __name__ == '__main__':
    main()
