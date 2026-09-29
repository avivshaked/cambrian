# Stomachs on the larder: did round 51's stomachs sit on the bed's corpse piles, and how rich is
# the bed's water for them? Reads each seed's feeding log (absorptive.jsonl), joins every pure
# stomach's row to its place in positions.jsonl.gz at the same second, and reads its height above
# the bed from fields/bed.f32; then reads the bed cells' snow (fields/*.snow-floor.f32).
# Usage: python scripts/reads/r52-larder.py [arm ...]   (default r51-s1 r51-s2 r51-s3)
import gzip, json, glob, os, sys, statistics as st
import numpy as np
ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'runs')
BREAK_EVEN = 0.44   # J/m3 at the mouth, line 4292's founder and the pool stomach (r51-husk-ledger.md)
R0_ONE = 1.0        # J/m3 at which line 4292's founder first has R0 above 1

def run_dir(arm):
    return sorted(glob.glob(os.path.join(ROOT, arm, '*', 'run.json')))[-1].rsplit(os.sep, 1)[0]

def q(v, p):
    return float(np.percentile(v, p)) if len(v) else float('nan')

for arm in sys.argv[1:] or ['r51-s1', 'r51-s2', 'r51-s3']:
    d = run_dir(arm)
    lay = json.load(open(os.path.join(d, 'fields', 'layout.json')))
    nx, nz = lay['bed']['cellsX'], lay['bed']['cellsZ']
    bed = np.fromfile(os.path.join(d, 'fields', 'bed.f32'), '<f4').reshape(nx, nz)
    # Pure stomachs: an absorptive body the log does not mark a mixotroph (photoArea is its surface, not a leaf's).
    want, rows = {}, []
    for line in open(os.path.join(d, 'absorptive.jsonl')):
        r = json.loads(line)
        if r['mixotroph'] or r['dead']:
            continue
        rows.append(r); want.setdefault(r['t'], set()).add(r['id'])
    place = {}
    with gzip.open(os.path.join(d, 'positions.jsonl.gz'), 'rt') as f:
        for line in f:
            if not want:
                break
            t = json.loads(line[:line.index(',')] + '}')['t']
            if t not in want:
                continue
            ids = want.pop(t)
            for b in json.loads(line)['b']:
                if b[0] in ids:
                    place[(t, b[0])] = (b[1], b[2], b[3])
    bins = {'on the bed (<1.5 m)': [], '1.5 to 5 m up': [], 'over 5 m up': []}
    for r in rows:
        p = place.get((r['t'], r['id']))
        if p is None:
            continue
        ix, iz = min(nx - 1, max(0, int(p[0]))), min(nz - 1, max(0, int(p[2])))
        h = p[1] - bed[ix, iz]
        key = 'on the bed (<1.5 m)' if h < 1.5 else '1.5 to 5 m up' if h < 5 else 'over 5 m up'
        bins[key].append(r)
    print(f'== {arm}: {len(rows)} live pure-stomach rows in the feeding log, {len(place)} placed')
    print('  where        | rows | bodies | density here J/m3 (median, p90) | food W median | net W median | share net>0 | bodies with a child')
    for k, v in bins.items():
        if not v:
            print(f'  {k:22s} | 0'); continue
        dens = [r['densityHere'] for r in v]; food = [r['foodW'] for r in v]; net = [r['netW'] for r in v]
        ids = {r['id'] for r in v}; kids = {r['id'] for r in v if r['children'] > 0}
        print(f'  {k:22s} | {len(v):5d} | {len(ids):5d} | {q(dens,50):.3f}, {q(dens,90):.3f} | {q(food,50):.4f} | {q(net,50):+.4f} | {sum(1 for x in net if x > 0)/len(v):.0%} | {len(kids)}')
    # The bed's snow, cell by cell (the stock of each column's lowest live cell, J in 1 m3).
    print('  t (s)  | bed cells live | mean J/m3 | p99 | max | cells >= 0.44 | cells >= 1 | cells >= 2')
    for t in (3000, 6000, 12000, 18000, 24000, 30000):
        fn = os.path.join(d, 'fields', f'{t:09d}.snow-floor.f32')
        if not os.path.exists(fn):
            continue
        s = np.fromfile(fn, '<f4'); live = s[bed.reshape(-1) > -1e9]
        live = live[live > 0] if (live > 0).any() else live
        print(f'  {t:6d} | {live.size:6d} | {live.mean():.3f} | {q(live,99):.2f} | {live.max():.2f} | {(live >= BREAK_EVEN).sum():5d} | {(live >= R0_ONE).sum():5d} | {(live >= 2).sum():4d}')
