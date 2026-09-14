# -*- coding: utf-8 -*-
"""Round 37b read, the positions side: the radial histogram, the azimuthal histogram,
the depth histogram and the radial drift, read straight from positions.jsonl.

pos_analyse.py of logbook/specs/r37-read, with round 37b's arms, seed 5's censoring
(its last sample is 29,200 s, so the 30,000 s anchor is read there) and two histograms
added that 0095's two-sided readings ask for (angle, for M1-holds-M2-fails; depth, for
the pockets question and for M3 read against depth).

Geometry is the tank's own (logbook/specs/tank-spec.md): the bounding square is
[0, 2R) x [0, 2R) with the axis at (R, R) and R = sqrt(area / pi).
"""
import glob, json, math, os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
HERE = os.path.dirname(os.path.abspath(__file__))
ARMS = ['r37b-s6', 'r37b-s7', 'r37b-s8', 'r37b-s9', 'r37b-s10']
TIMES = [5000, 15000, 30000]
LAST = {}


def run_dir(arm):
    ds = [d for d in sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*'))) if os.path.isdir(d)]
    return ds[-1]


def geometry(arm):
    cfg = json.load(open(os.path.join(run_dir(arm), 'config.json'), encoding='utf-8'))
    w = cfg['world']
    area = w['worldAreaSquareMetres']
    return w.get('worldShape', 'Box'), area, math.sqrt(area / math.pi), w['worldDepthMetres']


def samples(arm, wanted=None):
    path = os.path.join(run_dir(arm), 'positions.jsonl')
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{"t":'):
                continue
            try:
                row = json.loads(line)
            except ValueError:
                continue          # a truncated tail row on the stopped arm
            if wanted is not None and row['t'] not in wanted:
                continue
            yield row['t'], row['b']


def stats(vals):
    n = len(vals)
    if n == 0:
        return None, None
    m = sum(vals) / n
    return m, math.sqrt(sum((x - m) ** 2 for x in vals) / n)


def median(vals):
    if not vals:
        return None
    s = sorted(vals)
    n = len(s)
    return s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2])


hist = ['## Radial histogram, the fresh seeds (eight shells of EQUAL AREA, so a uniform disc reads'
        ' 0.125 in every one; r/R bounds sqrt(k/8))',
        'arm\tt\tn\t' + '\t'.join('shell%d' % k for k in range(8)) + '\t' +
        '\t'.join('f%d' % k for k in range(8)) +
        '\tmean r\tsd r\tmedian r\tmean R-r (m to glass)\tn within 0.5 m of glass'
        '\tshare within 0.5 m\tn within 1 m\tshare within 1 m\tmean depth\tsd depth']
ang = ['## Azimuthal histogram, the fresh seeds (twelve sectors of 30 degrees, equal area; a spread'
       ' disc reads 0.0833 in each). max/min and the resultant length R_bar say whether the'
       ' crowd is clumped in angle: R_bar near 0 is even, near 1 is one arc.',
       'arm\tt\tn\t' + '\t'.join('s%02d' % k for k in range(12)) +
       '\tmax share\tmin share\tmax/min\tR_bar']
dep = ['## Depth histogram, the fresh seeds (twelve bins of 5 m from the surface to the bed)',
       'arm\tt\tn\t' + '\t'.join('%d..%d' % (-5 * k, -5 * (k + 1)) for k in range(12)) +
       '\tmean depth\tsd depth\tmedian depth\tshare in the top 5 m\tshare in the bottom 5 m']
drift = ['## Radial drift, the fresh seeds (r37b build, seeds 6 to 10): for every body present in two consecutive samples (100 s'
         ' apart), the change in its distance from the axis, binned by the starting radius.'
         ' Positive is outward. Averaged over the whole run.',
         ' The fully-mixed bound is what a body starting in the bin would drift if its place'
         ' 100 s later were drawn from the whole crowd (the sample\'s own mean r, less r0): a'
         ' stationary disc CANNOT read zero in a bin away from the mean radius, so the bound,'
         ' not zero, is what a well-mixed world looks like in this statistic. "all radii pooled"'
         ' is the net flux and is zero at a stationary distribution.',
         'arm\tr bin (m)\tbody pairs\tmean dr (m per 100 s)\tmedian dr\tshare moving outward'
         '\tfully-mixed bound\tobserved / bound']

for arm in ARMS:
    shape, area, R, depth = geometry(arm)
    assert shape == 'Tank', (arm, shape)
    want = set(TIMES)
    if arm in LAST:
        want = {min(t, LAST[arm]) for t in TIMES}
    for t, bodies in samples(arm, want):
        rs = [math.hypot(b[1] - R, b[3] - R) for b in bodies]
        th = [math.atan2(b[3] - R, b[1] - R) for b in bodies]
        ys = [b[2] for b in bodies]
        n = len(rs)
        counts = [0] * 8
        for r in rs:
            counts[min(7, int((r / R) ** 2 * 8)) if R > 0 else 0] += 1
        mr, sr = stats(rs)
        md, sd = stats(ys)
        n05 = sum(1 for r in rs if R - r <= 0.5)
        n1 = sum(1 for r in rs if R - r <= 1.0)
        hist.append('%s\t%d\t%d\t' % (arm, t, n) + '\t'.join(str(c) for c in counts) + '\t' +
                    '\t'.join('%.3f' % (c / n) for c in counts) +
                    '\t%.3f\t%.3f\t%.3f\t%.3f\t%d\t%.3f\t%d\t%.3f\t%.2f\t%.2f' % (
                        mr, sr, median(rs), sum(R - r for r in rs) / n,
                        n05, n05 / n, n1, n1 / n, md, sd))
        sectors = [0] * 12
        for a in th:
            sectors[min(11, int((a + math.pi) / (2 * math.pi) * 12))] += 1
        sh = [c / n for c in sectors]
        rb = math.hypot(sum(math.cos(a) for a in th), sum(math.sin(a) for a in th)) / n
        ang.append('%s\t%d\t%d\t' % (arm, t, n) + '\t'.join('%.3f' % s for s in sh) +
                   '\t%.3f\t%.3f\t%.2f\t%.3f' % (max(sh), min(sh),
                   (max(sh) / min(sh)) if min(sh) > 0 else float('inf'), rb))
        dbins = [0] * 12
        for y in ys:
            dbins[min(11, max(0, int(-y / 5.0)))] += 1
        dep.append('%s\t%d\t%d\t' % (arm, t, n) + '\t'.join('%.3f' % (c / n) for c in dbins) +
                   '\t%.2f\t%.2f\t%.2f\t%.3f\t%.3f' % (md, sd, median(ys),
                   dbins[0] / n, dbins[11] / n))

    bins = [(0.0, 1.0), (1.0, 2.0), (2.0, 3.0), (3.0, 4.0), (4.0, 5.0), (5.0, R)]
    acc = {b: [] for b in bins}
    mixed = {b: [] for b in bins}     # the fully-mixed bound, per pair
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
        m, _ = stats(v)
        mm, _ = stats(mixed[b])
        drift.append('%s\t%.2f-%.2f\t%d\t%.4f\t%.4f\t%.3f\t%.4f\t%s' % (
            arm, b[0], b[1], len(v), m, median(v), sum(1 for x in v if x > 0) / len(v),
            mm, ('%.3f' % (m / mm)) if mm else '-'))
    pm, _ = stats(pooled)
    drift.append('%s\tall radii pooled\t%d\t%.4f\t%.4f\t%.3f\t0.0000\t-' % (
        arm, len(pooled), pm, median(pooled), sum(1 for x in pooled if x > 0) / len(pooled)))


drift.append('')
drift.append('## Comparison, NOT recomputed: round 37b seeds 1 to 5 and round 37 seeds 1 to 5')
drift.append('## are in logbook/specs/r37b-read/drift.tsv. 37b pooled net flux +0.0025 to +0.0032 m')
drift.append('## per 100 s; round 37 +0.0202 to +0.0486.')

open(os.path.join(HERE, 'radial-hist.tsv'), 'w', encoding='utf-8').write(
    '\n'.join(hist) + '\n\n' + '\n'.join(ang) + '\n\n' + '\n'.join(dep) + '\n')
open(os.path.join(HERE, 'drift.tsv'), 'w', encoding='utf-8').write('\n'.join(drift) + '\n')
print('\n'.join(drift))
print()
print('\n'.join(hist[:2] + [l for l in hist[2:]]))
