# -*- coding: utf-8 -*-
"""The crowd's spacing, exactly, at the three times, for round 39 and round 38.

`positions-read.py` estimates its nearest-neighbour median from a sample of pairs above
1,200 bodies and prints it with a leading "~". At three samples per seed the whole pair
matrix is affordable, so this computes it exactly, for both rounds, with the same rule:
the median over the living of the distance to the nearest other living body.

Writes nn-exact.tsv beside this file.
"""
import glob, json, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RUNS = {'r39-s1': '2026-09-15-213428-8eab1085', 'r39-s2': '2026-09-15-213503-8eab1085',
        'r39-s3': '2026-09-17-085758-8eab1085', 'r39-s4': '2026-09-17-085833-8eab1085',
        'r39-s5': '2026-09-17-085303-8eab1085'}
TIMES = [5000, 15000, 30000]


def nn(p):
    """The median nearest-neighbour distance, in blocks so the matrix never lands whole."""
    n = p.shape[0]
    if n < 2:
        return float('nan')
    best = np.full(n, np.inf)
    step = 512
    for i in range(0, n, step):
        d = p[i:i + step, None, :] - p[None, :, :]
        d = np.sqrt((d * d).sum(axis=2))
        d[np.arange(d.shape[0]), np.arange(i, min(i + step, n))] = np.inf
        best[i:i + step] = d.min(axis=1)
    return float(np.median(best))


def sample(path, t):
    with open(path, encoding='utf-8') as f:
        for line in f:
            if not line.startswith('{"t":'):
                continue
            if int(line[5:line.index(',', 5)]) != t:
                continue
            r = json.loads(line)
            return np.array([[x[1], x[2], x[3]] for x in r['b']], dtype=float)
    return None


out = ['## The exact three-dimensional and horizontal nearest-neighbour medians at the'
       ' three times, both rounds, computed here rather than sampled.',
       'round\tarm\tt\tliving\tnn 3d m\tnn horizontal m\twater m3\tbodies per 1000 m3']
rows39 = []
for a in sorted(RUNS):
    p = os.path.join(ROOT, 'runs', a, RUNS[a], 'positions.jsonl')
    cfg = json.load(open(os.path.join(os.path.dirname(p), 'config.json'), encoding='utf-8'))
    vol = cfg['world']['worldAreaSquareMetres'] * cfg['world']['worldDepthMetres']
    for t in TIMES:
        q = sample(p, t)
        v3, vh = nn(q), nn(q[:, [0, 2]])
        rows39.append((a, t, q.shape[0], v3))
        out.append('r39\t%s\t%d\t%d\t%.4f\t%.4f\t%d\t%.2f' % (
            a, t, q.shape[0], v3, vh, vol, 1000.0 * q.shape[0] / vol))
rows38 = []
for s in range(1, 6):
    p = sorted(glob.glob(os.path.join(ROOT, 'runs', 'r38-s%d' % s, '*', 'positions.jsonl')))[-1]
    cfg = json.load(open(os.path.join(os.path.dirname(p), 'config.json'), encoding='utf-8'))
    vol = cfg['world']['worldAreaSquareMetres'] * cfg['world']['worldDepthMetres']
    for t in TIMES:
        q = sample(p, t)
        v3, vh = nn(q), nn(q[:, [0, 2]])
        rows38.append(('r38-s%d' % s, t, q.shape[0], v3))
        out.append('r38\tr38-s%d\t%d\t%d\t%.4f\t%.4f\t%d\t%.2f' % (
            s, t, q.shape[0], v3, vh, vol, 1000.0 * q.shape[0] / vol))

out += ['', '## matched abundance: for each round 39 anchor, the round 38 anchor whose'
        ' living count is nearest. The water is 99,000 m3 against 24,000, so at equal'
        ' counts the spacing would go as the cube root of 4.125, 1.60.',
        'arm\tt\tr39 n\tcontrol\tcontrol t\tcontrol n\tabundance gap\twithin 15%'
        '\tr39 nn 3d\tcontrol nn 3d\tratio']
rs = []
for a, t, n, v in rows39:
    b = min(rows38, key=lambda r: abs(r[2] - n))
    gap = abs(b[2] - n) / n
    rs.append(v / b[3])
    out.append('%s\t%d\t%d\t%s\t%d\t%d\t%.3f\t%s\t%.4f\t%.4f\t%.3f' % (
        a, t, n, b[0], b[1], b[2], gap, 'yes' if gap <= 0.15 else 'no', v, b[3], v / b[3]))
out.append('## the ratio over the fifteen anchors: %.3f to %.3f, mean %.3f'
           % (min(rs), max(rs), sum(rs) / len(rs)))

with open(os.path.join(HERE, 'nn-exact.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out[-20:]))
