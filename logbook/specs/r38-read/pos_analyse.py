# -*- coding: utf-8 -*-
"""Round 38's positions side, read straight from positions.jsonl: the depth band's shape
(D3's two-sided reading), the eaters' depth against the leaves' at the boom's peak and at
its trough (D5), the radial drift against the fully-mixed bound (D4's reading) and the
radial histogram.

pos_analyse.py of logbook/specs/r37b-fresh-read, with round 38's arms and its geometry
(a disc of radius 11.2838 m, 400 m2), and with the depth band split by guild.
"""
import glob, json, math, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

ARMS = ['r38-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]
ABSORPTIVE, JOINTED, PHOTOSYNTHETIC = 1, 2, 4


def run_dir(arm):
    ds = [d for d in sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*'))) if os.path.isdir(d)]
    return ds[-1]


def geometry(arm):
    cfg = json.load(open(os.path.join(run_dir(arm), 'config.json'), encoding='utf-8'))
    w = cfg['world']
    area = w['worldAreaSquareMetres']
    return w.get('worldShape', 'Box'), area, math.sqrt(area / math.pi), w['worldDepthMetres']


def samples(arm, wanted=None):
    with open(os.path.join(run_dir(arm), 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{"t":'):
                continue
            row = json.loads(line)
            if wanted is not None and row['t'] not in wanted:
                continue
            yield row['t'], row['b']


def stat(v):
    n = len(v)
    if n == 0:
        return None, None
    m = sum(v) / n
    return m, math.sqrt(sum((x - m) ** 2 for x in v) / n)


def q(v, p):
    if not v:
        return None
    s = sorted(v)
    i = p * (len(s) - 1)
    lo = int(math.floor(i))
    hi = min(lo + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (i - lo)


def guild(f):
    if f & ABSORPTIVE and f & PHOTOSYNTHETIC:
        return 'mixo'
    if f & ABSORPTIVE:
        return 'stom'
    if f & PHOTOSYNTHETIC:
        return 'leaf'
    if f & JOINTED:
        return 'jnt'
    return 'plain'


# the eaters' boom peak and trough per seed, from the report
PEAKS = {}
for a in ARMS:
    r, _ = rows(a)
    inh = [(x['t'], num(x['inherit'])) for x in r]
    pk = max(inh, key=lambda z: z[1])
    after = [z for z in inh if z[0] > pk[0]]
    tr = min(after, key=lambda z: z[1]) if after else pk
    PEAKS[a] = (pk[0], tr[0])

band = ['## D3\'s two-sided reading: the depth band\'s shape. The median depth and the'
        ' interquartile range of the living, and of the absorptive bodies alone (stomachs'
        ' plus mixotrophs), at the three anchors and at the eaters\' peak and trough.',
        'arm\twindow\tt\tn\tmedian y all\tq25 all\tq75 all\tIQR all\tmean y all\tsd y all'
        '\tn absorptive\tmedian y abs\tq25 abs\tq75 abs\tIQR abs\tn leaf\tmedian y leaf'
        '\tIQR leaf\tabs median - leaf median']
dep = ['## Depth histogram (twelve bins of 5 m from the surface to the bed), all bodies',
       'arm\tt\tn\t' + '\t'.join('%d..%d' % (-5 * k, -5 * (k + 1)) for k in range(12)) +
       '\tmedian y\tshare in the top 5 m\tshare in the bottom 5 m']
hist = ['## Radial histogram (eight shells of EQUAL AREA, so a uniform disc reads 0.125 in'
        ' every one; r/R bounds sqrt(k/8)); R = 11.2838 m',
        'arm\tt\tn\t' + '\t'.join('f%d' % k for k in range(8)) +
        '\tmean r\tmedian r\tn within 0.5 m of glass\tshare\tn within 1 m\tshare']
drift = ['## Radial drift: for every body present in two consecutive samples (100 s apart),'
         ' the change in its distance from the axis, binned by the starting radius. Positive'
         ' is outward. Averaged over the whole run.',
         ' The fully-mixed bound is what a body starting in the bin would drift if its place'
         ' 100 s later were drawn from the whole crowd (the sample\'s own mean r, less r0):'
         ' a stationary disc CANNOT read zero in a bin away from the mean radius, so the'
         ' bound, not zero, is what a well-mixed world looks like in this statistic.'
         ' "all radii pooled" is the net flux and is zero at a stationary distribution.',
         'arm\tr bin (m)\tbody pairs\tmean dr (m per 100 s)\tmedian dr\tshare moving outward'
         '\tfully-mixed bound\tobserved / bound']

for arm in ARMS:
    shape, area, R, depth = geometry(arm)
    assert shape == 'Tank', (arm, shape)
    pk, tr = PEAKS[arm]
    want = {('t=%d' % t): t for t in TIMES}
    marks = [('t=%d' % t, t) for t in TIMES] + [('eaters peak', pk), ('eaters trough', tr)]
    wanted = {t for _, t in marks}
    got = {}
    for t, bodies in samples(arm, wanted):
        got[t] = bodies
    for name, t in marks:
        bodies = got.get(t)
        if bodies is None:
            continue
        ys = [b[2] for b in bodies]
        gs = [guild(b[4]) for b in bodies]
        ya = [b[2] for b, g in zip(bodies, gs) if g in ('stom', 'mixo')]
        yl = [b[2] for b, g in zip(bodies, gs) if g == 'leaf']
        m, sd = stat(ys)
        band.append('%s\t%s\t%d\t%d\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f\t%d\t%s\t%s\t%s\t%s'
                    '\t%d\t%s\t%s\t%s' % (
            arm, name, t, len(ys), q(ys, 0.5), q(ys, 0.25), q(ys, 0.75),
            q(ys, 0.25) - q(ys, 0.75), m, sd, len(ya),
            '%.2f' % q(ya, 0.5) if ya else '-', '%.2f' % q(ya, 0.25) if ya else '-',
            '%.2f' % q(ya, 0.75) if ya else '-',
            '%.2f' % (q(ya, 0.25) - q(ya, 0.75)) if ya else '-',
            len(yl), '%.2f' % q(yl, 0.5) if yl else '-',
            '%.2f' % (q(yl, 0.25) - q(yl, 0.75)) if yl else '-',
            '%.2f' % (q(ya, 0.5) - q(yl, 0.5)) if (ya and yl) else '-'))
        if name.startswith('t='):
            n = len(ys)
            dbins = [0] * 12
            for y in ys:
                dbins[min(11, max(0, int(-y / 5.0)))] += 1
            dep.append('%s\t%d\t%d\t' % (arm, t, n) +
                       '\t'.join('%.3f' % (c / n) for c in dbins) +
                       '\t%.2f\t%.3f\t%.3f' % (q(ys, 0.5), dbins[0] / n, dbins[11] / n))
            rs = [math.hypot(b[1] - R, b[3] - R) for b in bodies]
            counts = [0] * 8
            for rr in rs:
                counts[min(7, int((rr / R) ** 2 * 8))] += 1
            n05 = sum(1 for rr in rs if R - rr <= 0.5)
            n1 = sum(1 for rr in rs if R - rr <= 1.0)
            hist.append('%s\t%d\t%d\t' % (arm, t, n) +
                        '\t'.join('%.3f' % (c / n) for c in counts) +
                        '\t%.3f\t%.3f\t%d\t%.3f\t%d\t%.3f' % (
                            sum(rs) / n, q(rs, 0.5), n05, n05 / n, n1, n1 / n))

    bins = [(0.0, 2.0), (2.0, 4.0), (4.0, 6.0), (6.0, 8.0), (8.0, 10.0), (10.0, R)]
    acc = {b: [] for b in bins}
    mixed = {b: [] for b in bins}
    pooled = []
    prev = None
    for t, bodies in samples(arm):
        cur = {b[0]: (b[1], b[3]) for b in bodies}
        rbar = (sum(math.hypot(b[1] - R, b[3] - R) for b in bodies) / len(bodies)
                if bodies else None)
        if prev is not None and rbar is not None:
            for bid, (x, z) in cur.items():
                if bid not in prev:
                    continue
                px, pz = prev[bid]
                r0 = math.hypot(px - R, pz - R)
                r1 = math.hypot(x - R, z - R)
                pooled.append(r1 - r0)
                for b in bins:
                    if b[0] <= r0 < b[1]:
                        acc[b].append(r1 - r0)
                        mixed[b].append(rbar - r0)
                        break
        prev = cur
    for b in bins:
        v = acc[b]
        if not v:
            drift.append('%s\t%.2f-%.2f\t0\t-\t-\t-\t-\t-' % (arm, b[0], b[1]))
            continue
        m, _ = stat(v)
        mm, _ = stat(mixed[b])
        drift.append('%s\t%.2f-%.2f\t%d\t%.4f\t%.4f\t%.3f\t%.4f\t%s' % (
            arm, b[0], b[1], len(v), m, q(v, 0.5), sum(1 for x in v if x > 0) / len(v),
            mm, ('%.3f' % (m / mm)) if mm else '-'))
    pm, _ = stat(pooled)
    drift.append('%s\tall radii pooled\t%d\t%.4f\t%.4f\t%.3f\t0.0000\t-' % (
        arm, len(pooled), pm, q(pooled, 0.5), sum(1 for x in pooled if x > 0) / len(pooled)))
    print('done', arm)

drift += ['', '## Comparison, NOT recomputed (logbook/specs/r37b-read/drift.tsv and'
          ' r37b-fresh-read/drift.tsv): 37b seeds 1 to 5 pooled net flux +0.0025 to +0.0032 m'
          ' per 100 s, seeds 6 to 10 +0.0023 to +0.0027; round 37 (no acceleration force)'
          ' +0.0202 to +0.0486. Note the bins here are 2 m wide on an 11.28 m disc where'
          ' 37b\'s were 1 m wide on a 5.64 m disc, so the bins are the same fraction of the'
          ' radius and the bounds are comparable in shape, not in metres.']
band += ['', '## Comparison, round 37b: the report\'s own mean depth at the three times ran'
         ' -13.5 to -23.7 m (s6) and -15.6 to -18.5 (s7); the fresh read\'s depth histogram'
         ' is in r37b-fresh-read/radial-hist.tsv. Round 38\'s report depth m column reads'
         ' -16.1 to -27.8 at the three anchors.']

open(os.path.join(HERE, 'depth-band.tsv'), 'w', encoding='utf-8').write(
    '\n'.join(band) + '\n\n' + '\n'.join(dep) + '\n')
open(os.path.join(HERE, 'drift.tsv'), 'w', encoding='utf-8').write('\n'.join(drift) + '\n')
open(os.path.join(HERE, 'radial-hist.tsv'), 'w', encoding='utf-8').write('\n'.join(hist) + '\n')
print('wrote depth-band.tsv, drift.tsv, radial-hist.tsv')
