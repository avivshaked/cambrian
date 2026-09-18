# -*- coding: utf-8 -*-
"""Round 39's positions read against the rebuilt bed: E3's disc share, E4, E9, E10, E11,
and the recorded-not-predicted readings that need a map (the leaves' height above their
own floor, the hollows' occupancy, the whole crowd's centre along the tilt's diameter).

The map is `bed.py`, a port of `BedShape` checked against every bed field the manifest
records.  The along-tilt coordinate is the signed distance from the tank's axis along the
unit vector pointing UP the ramp, so a positive number is toward the shallow arc.

Writes the TSVs beside this file.
"""
import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
import bed as bedmod

RUNS = {
    'r39-s1': '2026-09-15-213428-8eab1085',
    'r39-s2': '2026-09-15-213503-8eab1085',
    'r39-s3': '2026-09-17-085758-8eab1085',
    'r39-s4': '2026-09-17-085833-8eab1085',
    'r39-s5': '2026-09-17-085303-8eab1085',
}
ARMS = ['r39-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]
ABSORPTIVE, JOINTED, PHOTOSYNTHETIC = 1, 2, 4
NEAR_FLOOR = 2.0            # E9's and E10's "within 2 m of the floor"
CELL = 1.0                  # the detritus grid's cell, so its columns are the disc's


def run_dir(a):
    return os.path.join(ROOT, 'runs', a, RUNS[a])


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def sample_at(a, t):
    """The positions row at time t: x, z, y and flags as arrays."""
    with open(os.path.join(run_dir(a), 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            if not line.startswith('{'):
                continue
            # Cheap pre-filter: the row's own t is the first field.
            head = line[:24]
            if '"t":%d,' % t not in head:
                continue
            row = json.loads(line)
            if row['t'] != t:
                continue
            b = row['b']
            arr = np.array([[x[1], x[2], x[3], x[4]] for x in b], dtype=float)
            return arr[:, 0], arr[:, 2], arr[:, 1], arr[:, 3].astype(int)
    raise SystemExit('%s: no positions row at t=%d' % (a, t))


BEDS, MANS, CFGS = {}, {}, {}
for a in ARMS:
    CFGS[a] = json.load(open(os.path.join(run_dir(a), 'config.json'), encoding='utf-8'))
    MANS[a] = json.load(open(os.path.join(run_dir(a), 'run.json'), encoding='utf-8'))
    BEDS[a] = bedmod.from_config(CFGS[a], MANS[a]['seed'])

# ------------------------------------------------------------------ the map itself
mp = ['## The bed rebuilt from each seed\'s config.json and seed (bed.py, a port of'
      ' BedShape), checked field by field against run.json. The tilt bearing is not'
      ' recorded anywhere and is the rebuild\'s.',
      'arm\tseed\thollows (rebuilt/recorded)\tridges\trange m\tsteepest deg\tscale m'
      '\ttilt bearing deg\tup-ramp unit (x,z)\thollow radius m\tfloor lowest m'
      '\tfloor highest m\tlive 1m columns\tlowest-quarter columns\tlowest-quarter share'
      '\thollows share of disc']
DISC = {}
for a in ARMS:
    b = BEDS[a]
    m = MANS[a]
    R = float(b.RadiusMetres)
    n = int(math.ceil(2 * R / CELL))
    g = (np.arange(n) + 0.5) * CELL
    X, Z = np.meshgrid(g, g, indexing='ij')
    inside = ((X - R) ** 2 + (Z - R) ** 2) <= R * R
    cx, cz = X[inside], Z[inside]
    floors = np.float32(b.floor_y(cx, cz)).astype(float)
    lo, hi = floors.min(), floors.max()
    thr = lo + 0.25 * (hi - lo)
    low_cols = int((floors <= thr).sum())
    # The hollows' own share of the disc, for the occupancy reading.
    hshare = 0.0
    if b.HollowCentres:
        r = b.HollowRadiusMetres
        inh = np.zeros(cx.size, dtype=bool)
        for (hx, hz) in b.HollowCentres:
            inh |= ((cx - hx) ** 2 + (cz - hz) ** 2) <= r * r
        hshare = inh.mean()
    ux, uz = b.tilt_unit()
    # The null E9 is read against: where the centre along the tilt sits when bodies are
    # spread evenly through the WATER, which is deeper on the deep side by construction.
    s_col = (cx - R) * ux + (cz - R) * uz
    water = -floors                       # the column's water depth, m
    DISC[a] = dict(R=R, floors=floors, cx=cx, cz=cz, thr=thr, lo=lo, hi=hi,
                   cols=cx.size, low_share=low_cols / cx.size, hshare=hshare,
                   s_null_volume=float((water * s_col).sum() / water.sum()),
                   s_null_area=float(s_col.mean()))
    mp.append('%s\t%d\t%d/%d\t%d/%d\t%.4f\t%.4f\t%.4f\t%.2f\t(%.4f, %.4f)\t%.3f\t%.3f\t%.3f'
              '\t%d\t%d\t%.4f\t%.4f' % (
                  a, m['seed'], b.Hollows, m['bedHollows'], b.Ridges, m['bedRidges'],
                  b.RangeMetres, math.degrees(b.SteepestTotalSlopeRadians),
                  float(b.ScaleMetres), math.degrees(float(b.TiltDirectionRadians)),
                  ux, uz, b.HollowRadiusMetres, lo, hi, cx.size, low_cols,
                  low_cols / cx.size, hshare) + '	%.4f	%.4f' % (
                      DISC[a]['s_null_area'], DISC[a]['s_null_volume']))
write('bed-map.tsv', mp)

# ------------------------------------------------------------------ the bodies
rows = []
for a in ARMS:
    b = BEDS[a]
    R = float(b.RadiusMetres)
    ux, uz = b.tilt_unit()
    for t in TIMES:
        x, z, y, fl = sample_at(a, t)
        floor = b.floor_y(x, z)
        above = y - floor
        s = (x - R) * ux + (z - R) * uz          # + toward the shallow arc
        leaf = (fl & PHOTOSYNTHETIC) != 0
        stom = (fl & ABSORPTIVE) != 0
        mixo = leaf & stom
        jnt = (fl & JOINTED) != 0
        near = above <= NEAR_FLOOR
        rows.append(dict(arm=a, t=t, n=x.size, x=x, z=z, y=y, s=s, above=above,
                         leaf=leaf, stom=stom, mixo=mixo, jnt=jnt, near=near,
                         ux=ux, uz=uz, R=R, bed=b))


def med(v):
    return float(np.median(v)) if v.size else float('nan')


def find(a, t):
    for r in rows:
        if r['arm'] == a and r['t'] == t:
            return r
    raise KeyError((a, t))


# ------------------------------------------------------------------ E4 and the heights
e4 = ['## E4  the eaters find the low ground: the absorptive bodies\' median height above'
      ' their own column\'s floor against the leaves\', at 15,000 s (the prediction) and'
      ' at the other two times as a reading. Heights are y - BedShape.FloorY(x, z).',
      'arm\tt\tliving\tleaves\tstomachs\tmixo\tleaf median above floor m'
      '\tstomach median above floor m\tstomach - leaf m\tE4 (stomach below leaf)'
      '\tleaf median depth m\tstomach median depth m\tall median above floor m']
n4 = 0
for a in ARMS:
    for t in TIMES:
        r = find(a, t)
        lh = med(r['above'][r['leaf']])
        sh = med(r['above'][r['stom']])
        ok = (r['stom'].sum() > 0) and (sh < lh)
        if t == 15000:
            n4 += bool(ok)
        e4.append('%s\t%d\t%d\t%d\t%d\t%d\t%.3f\t%.3f\t%.3f\t%s\t%.2f\t%.2f\t%.3f' % (
            a, t, r['n'], int(r['leaf'].sum()), int(r['stom'].sum()), int(r['mixo'].sum()),
            lh, sh, sh - lh, 'yes' if ok else ('no stomachs' if r['stom'].sum() == 0 else 'NO'),
            med(r['y'][r['leaf']]), med(r['y'][r['stom']]), med(r['above'])))
e4.append('## E4 at 15,000 s: %d of 5 (the bar is 4 of 5)' % n4)
write('e4-height-above-floor.tsv', e4)

# ------------------------------------------------------------------ E9, E10, E11
e9 = ['## E9, E10 and E11 along the tilt\'s diameter. s is the signed distance from the'
      ' axis along the up-ramp direction, so s > 0 is toward the SHALLOW arc. "near floor"'
      ' is within 2.0 m of BedShape.FloorY under the body.',
      'arm\tt\tliving\tleaf centre s m\tstomach centre s m\tall centre s m'
      '\tleaf mean s m\tE9a leaf centre >= +4 m\tnear-floor leaves shallow (s>0)'
      '\tnear-floor leaves deep (s<0)\tE9b shallow >= 2x deep\tnear floor n'
      '\tnear floor share of living\tE10 share >= 5.6%\t|stomach centre - leaf centre| m'
      '\tE11 within 3 m\tnear-floor stomachs\tnear-floor share of stomachs']
c9a = c9b = c10 = c11 = 0
for a in ARMS:
    for t in TIMES:
        r = find(a, t)
        ls = med(r['s'][r['leaf']])
        ss = med(r['s'][r['stom']]) if r['stom'].sum() else float('nan')
        alls = med(r['s'])
        shallow = int((r['leaf'] & r['near'] & (r['s'] > 0)).sum())
        deep = int((r['leaf'] & r['near'] & (r['s'] < 0)).sum())
        nearn = int(r['near'].sum())
        share = nearn / r['n']
        gap = abs(ss - ls) if r['stom'].sum() else float('nan')
        a9 = ls >= 4.0
        b9 = shallow >= 2 * deep and (shallow + deep) > 0
        a10 = share >= 0.056
        a11 = (r['stom'].sum() > 0) and gap <= 3.0
        if t in (15000, 30000):
            c9a += bool(a9); c9b += bool(b9)
        if t == 15000:
            c10 += bool(a10); c11 += bool(a11)
        e9.append('%s\t%d\t%d\t%.3f\t%s\t%.3f\t%.3f\t%s\t%d\t%d\t%s\t%d\t%.4f\t%s\t%s\t%s'
                  '\t%d\t%.4f' % (
                      a, t, r['n'], ls, ('%.3f' % ss) if r['stom'].sum() else '-', alls,
                      float(np.mean(r['s'][r['leaf']])) if r['leaf'].sum() else float('nan'),
                      'yes' if a9 else 'NO', shallow, deep, 'yes' if b9 else 'NO',
                      nearn, share, 'yes' if a10 else 'NO',
                      ('%.3f' % gap) if r['stom'].sum() else '-',
                      'yes' if a11 else ('no stomachs' if r['stom'].sum() == 0 else 'NO'),
                      int((r['stom'] & r['near']).sum()),
                      float((r['stom'] & r['near']).sum() / max(r['stom'].sum(), 1)))
                  + '	%.4f	%.4f' % (DISC[a]['s_null_volume'],
                                      ls - DISC[a]['s_null_volume']))
e9.append('## E9a (leaf centre >= +4 m) at 15,000 and 30,000 s: %d of 10 seed-times' % c9a)
e9.append('## E9b (near-floor leaves shallow >= 2x deep) at 15,000 and 30,000 s: %d of 10' % c9b)
e9.append('## E10 at 15,000 s: %d of 5 (the bar is 4 of 5)' % c10)
e9.append('## E11 at 15,000 s: %d of 5 (the bar is 3 of 5)' % c11)
write('e9-e10-e11-tilt.tsv', e9)

# ------------------------------------------------------------------ the hollows
ho = ['## Recorded, not predicted: the hollows\' occupancy. A body is in a hollow when it'
      ' is within HollowRadiusMetres (0.25 x the largest band\'s scale) of a hollow\'s'
      ' lattice centre, horizontally. The share to beat is the hollows\' own share of the'
      ' disc\'s columns. Seeds 1 and 5 drew no hollow, so they have none to occupy.',
      'arm\thollows\thollow radius m\thollows share of disc\tt\tliving\tin a hollow'
      '\tshare of living\tratio to the disc share\tleaves in a hollow\tstomachs in a hollow'
      '\tnear-floor bodies in a hollow']
for a in ARMS:
    b = BEDS[a]
    d = DISC[a]
    for t in TIMES:
        r = find(a, t)
        if not b.HollowCentres:
            ho.append('%s\t0\t%.3f\t0.0000\t%d\t%d\t-\t-\t-\t-\t-\t-'
                      % (a, b.HollowRadiusMetres, t, r['n']))
            continue
        inh = np.zeros(r['n'], dtype=bool)
        for (hx, hz) in b.HollowCentres:
            inh |= ((r['x'] - hx) ** 2 + (r['z'] - hz) ** 2) <= b.HollowRadiusMetres ** 2
        sh = inh.sum() / r['n']
        ho.append('%s\t%d\t%.3f\t%.4f\t%d\t%d\t%d\t%.4f\t%.3f\t%d\t%d\t%d' % (
            a, b.Hollows, b.HollowRadiusMetres, d['hshare'], t, r['n'], int(inh.sum()), sh,
            sh / d['hshare'] if d['hshare'] else float('nan'),
            int((inh & r['leaf']).sum()), int((inh & r['stom']).sum()),
            int((inh & r['near']).sum())))
write('hollow-occupancy.tsv', ho)

# ------------------------------------------------------------------ the depth band
db = ['## Recorded, not predicted: the crowd\'s depth band, its height above its own'
      ' floor, and its centre along the tilt (s > 0 toward the shallow arc), per guild.',
      'arm\tt\tguild\tn\tmedian y m\tq1 y\tq3 y\tmedian above floor m\tq1 above\tq3 above'
      '\tmedian s m\tq1 s\tq3 s']
for a in ARMS:
    for t in TIMES:
        r = find(a, t)
        for name, mask in (('all', np.ones(r['n'], dtype=bool)), ('leaf', r['leaf']),
                           ('stomach', r['stom']), ('jointed', r['jnt'])):
            k = int(mask.sum())
            if k == 0:
                db.append('%s\t%d\t%s\t0\t-\t-\t-\t-\t-\t-\t-\t-\t-' % (a, t, name))
                continue
            yy, ab, ss = r['y'][mask], r['above'][mask], r['s'][mask]
            db.append('%s\t%d\t%s\t%d\t%.2f\t%.2f\t%.2f\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f'
                      % (a, t, name, k, np.median(yy), np.percentile(yy, 25),
                         np.percentile(yy, 75), np.median(ab), np.percentile(ab, 25),
                         np.percentile(ab, 75), np.median(ss), np.percentile(ss, 25),
                         np.percentile(ss, 75)))
write('depth-band.tsv', db)
print('wrote bed-map.tsv, e4-height-above-floor.tsv, e9-e10-e11-tilt.tsv,'
      ' hollow-occupancy.tsv, depth-band.tsv')
