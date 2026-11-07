# Where the remains lie against where the animals are, in a forest world (propose-forest-pays.md, step 2's read).
# Reads positions.jsonl.gz: flag 4 photosynthetic, 8 remains. Plant remains are 4|8, animal remains 8 alone, living
# plants 4 without 8, animals neither. For one sample in every --every, it measures each body's distance to the nearest
# animal (3D, plain distance in the 300 m box) and its depth, and follows each remains id from first to last sample.
# Usage: python -I remains-where.py <run dir> [--every 10]
import gzip, json, math, sys, os, statistics as st

def nearest(points, others):
    out = []
    for (x, y, z) in points:
        best = math.inf
        for (a, b, c) in others:
            d = (x - a) ** 2 + (y - b) ** 2 + (z - c) ** 2
            if d < best: best = d
        out.append(math.sqrt(best))
    return out

def q(v, p):
    if not v: return float('nan')
    v = sorted(v); return v[min(len(v) - 1, int(p * len(v)))]

run = sys.argv[1]
every = int(sys.argv[sys.argv.index('--every') + 1]) if '--every' in sys.argv else 10
groups = {'plant remains': [], 'animal remains': [], 'living plants': [], 'animals': []}
depth = {k: [] for k in groups}
counts = {k: [] for k in groups}
track = {}
with gzip.open(os.path.join(run, 'positions.jsonl.gz'), 'rt') as g:
    for i, line in enumerate(g):
        r = json.loads(line)
        t = r['t']
        if t < 3000: continue
        rows = r['b']
        for (bid, x, y, z, f) in rows:
            if f & 8:
                tr = track.get(bid)
                if tr is None: track[bid] = [t, x, y, z, t, x, y, z, bool(f & 4)]
                else: tr[4:8] = [t, x, y, z]
        if i % every: continue
        sets = {'plant remains': [], 'animal remains': [], 'living plants': [], 'animals': []}
        for (bid, x, y, z, f) in rows:
            if f & 8: sets['plant remains' if f & 4 else 'animal remains'].append((x, y, z))
            elif f & 4: sets['living plants'].append((x, y, z))
            else: sets['animals'].append((x, y, z))
        for k, pts in sets.items():
            counts[k].append(len(pts))
            depth[k].extend(p[1] for p in pts)
            if k != 'animals' and sets['animals']:
                groups[k].extend(nearest(pts, sets['animals']))

print(f'run {run}, samples from 3,000 s, one in {every}')
print('group | mean count a sample | depth y median (q10, q90) | nearest animal median m (q10) | within 3 m | within 10 m')
for k in groups:
    d = groups[k]
    n = len(d)
    w3 = sum(1 for v in d if v <= 3) / n if n else float('nan')
    w10 = sum(1 for v in d if v <= 10) / n if n else float('nan')
    print(f'{k} | {st.mean(counts[k]) if counts[k] else 0:.1f} | {q(depth[k], .5):.1f} ({q(depth[k], .1):.1f}, {q(depth[k], .9):.1f}) | '
          f'{q(d, .5):.1f} ({q(d, .1):.1f}) | {w3:.1%} | {w10:.1%}')
for plant in (True, False):
    tr = [v for v in track.values() if v[8] == plant and v[4] > v[0]]
    if not tr: continue
    life = [v[4] - v[0] for v in tr]
    sink = [v[6] - v[2] for v in tr]
    move = [math.hypot(v[5] - v[1], v[7] - v[3]) for v in tr]
    print(f"{'plant' if plant else 'animal'} remains followed: {len(tr)}, seen for median {q(life, .5):.0f} s, "
          f"depth change median {q(sink, .5):+.2f} m (q10 {q(sink, .1):+.2f}), horizontal drift median {q(move, .5):.1f} m (q90 {q(move, .9):.1f})")
