# -*- coding: utf-8 -*-
"""Round 37 read, the positions side: the radial histogram the tank needs and the
radial drift of bodies, read straight from positions.jsonl.

The branch's positions-read.py prints n, cols, the spreads and the nearest-neighbour
medians; it does not print a radial histogram, so this does. Geometry is the tank's own
(logbook/specs/tank-spec.md): the bounding square is [0, 2R) x [0, 2R) with the axis at
(R, R) and R = sqrt(area / pi).
"""
import glob, json, math, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
HERE = os.path.dirname(os.path.abspath(__file__))
TIMES = [5000, 15000, 30000]


def run_dir(arm):
    ds = sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*')))
    ds = [d for d in ds if os.path.isdir(d)]
    return ds[-1]


def geometry(arm):
    cfg = json.load(open(os.path.join(run_dir(arm), 'config.json'), encoding='utf-8'))
    w = cfg['world']
    area = w['worldAreaSquareMetres']
    shape = w.get('worldShape', 'Box')
    return shape, area, math.sqrt(area / math.pi), w['worldDepthMetres']


def samples(arm, wanted=None):
    """Yield (t, [(id, x, y, z, flags)...]) for the wanted times, or every sample."""
    path = os.path.join(run_dir(arm), 'positions.jsonl')
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{"t":'):
                continue
            row = json.loads(line)
            t = row['t']
            if wanted is not None and t not in wanted:
                continue
            yield t, row['b']


def radial(bodies, R):
    out = []
    for b in bodies:
        dx, dz = b[1] - R, b[3] - R
        out.append(math.hypot(dx, dz))
    return out


def stats(vals):
    n = len(vals)
    if n == 0:
        return None, None
    m = sum(vals) / n
    v = sum((x - m) ** 2 for x in vals) / n
    return m, math.sqrt(v)


def median(vals):
    if not vals:
        return None
    s = sorted(vals)
    n = len(s)
    return s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2])


def main():
    lines_hist = ['## Radial histogram, r37 (eight shells of EQUAL AREA, so a uniform disc reads'
                  ' 0.125 in every one; r/R bounds sqrt(k/8))']
    lines_hist.append('arm\tt\tn\t' + '\t'.join('shell%d' % k for k in range(8)) +
                      '\t' + '\t'.join('f%d' % k for k in range(8)) +
                      '\tmean r\tsd r\tmedian r\tmean R-r (m to glass)\tn within 0.5 m of glass'
                      '\tshare within 0.5 m\tn within 1 m\tshare within 1 m'
                      '\tmean depth\tsd depth')
    lines_drift = ['## Radial drift, r37: for every body present in two consecutive samples'
                   ' (100 s apart), the change in its distance from the axis, binned by the'
                   ' starting radius. Positive is outward. Averaged over the whole run.']
    lines_drift.append('arm\tr bin (m)\tbody pairs\tmean dr (m per 100 s)\tmedian dr'
                       '\tshare moving outward')

    for arm in ['r37-s%d' % i for i in range(1, 6)]:
        shape, area, R, depth = geometry(arm)
        assert shape == 'Tank', (arm, shape)
        want = set(TIMES)
        for t, bodies in samples(arm, want):
            rs = radial(bodies, R)
            ys = [b[2] for b in bodies]
            n = len(rs)
            edges = [R * math.sqrt(k / 8.0) for k in range(9)]
            counts = [0] * 8
            for r in rs:
                k = min(7, int((r / R) ** 2 * 8)) if R > 0 else 0
                counts[k] += 1
            mr, sr = stats(rs)
            md, sd = stats(ys)
            near05 = sum(1 for r in rs if R - r <= 0.5)
            near1 = sum(1 for r in rs if R - r <= 1.0)
            lines_hist.append(
                '%s\t%d\t%d\t' % (arm, t, n) + '\t'.join(str(c) for c in counts) + '\t' +
                '\t'.join('%.3f' % (c / n) for c in counts) +
                '\t%.3f\t%.3f\t%.3f\t%.3f\t%d\t%.3f\t%d\t%.3f\t%.2f\t%.2f' % (
                    mr, sr, median(rs), sum(R - r for r in rs) / n,
                    near05, near05 / n, near1, near1 / n, md, sd))

        # the drift, over every consecutive pair of samples in the run
        bins = [(0.0, 1.0), (1.0, 2.0), (2.0, 3.0), (3.0, 4.0), (4.0, 5.0), (5.0, R)]
        acc = {b: [] for b in bins}
        prev = None
        for t, bodies in samples(arm):
            cur = {b[0]: (b[1], b[3]) for b in bodies}
            if prev is not None:
                for bid, (x, z) in cur.items():
                    if bid not in prev:
                        continue
                    px, pz = prev[bid]
                    r0 = math.hypot(px - R, pz - R)
                    r1 = math.hypot(x - R, z - R)
                    for b in bins:
                        if b[0] <= r0 < b[1]:
                            acc[b].append(r1 - r0)
                            break
            prev = cur
        for b in bins:
            v = acc[b]
            if not v:
                lines_drift.append('%s\t%.2f-%.2f\t0\t-\t-\t-' % (arm, b[0], b[1]))
                continue
            m, _ = stats(v)
            lines_drift.append('%s\t%.2f-%.2f\t%d\t%.4f\t%.4f\t%.3f' % (
                arm, b[0], b[1], len(v), m, median(v),
                sum(1 for x in v if x > 0) / len(v)))

    # depth for round 36 at the same times, for M3's depth column
    lines_depth = ['## Depth from positions.jsonl (mean and population sd of y), both rounds']
    lines_depth.append('arm\tt\tn\tmean depth\tsd depth')
    for arm in (['r37-s%d' % i for i in range(1, 6)] + ['r36-s%d' % i for i in range(1, 6)]):
        for t, bodies in samples(arm, set(TIMES)):
            ys = [b[2] for b in bodies]
            m, s = stats(ys)
            lines_depth.append('%s\t%d\t%d\t%.2f\t%.2f' % (arm, t, len(ys), m, s))

    text = '\n'.join(lines_hist) + '\n\n' + '\n'.join(lines_drift) + '\n\n' + \
        '\n'.join(lines_depth) + '\n'
    open(os.path.join(HERE, 'radial-hist.tsv'), 'w', encoding='utf-8').write(text)
    print(text)


if __name__ == '__main__':
    main()
