# The emptied cell: for each pure stomach on the bed (under 1.5 m up) at a second the farm dumped
# the bed's snow (every 100 s), the stock of its own bed cell against its eight neighbours', its
# clearance (foodW / densityHere, m3/s) and its upkeep. Usage: python scripts/reads/r52-larder-cell.py [arm ...]
import gzip, json, glob, os, sys
import numpy as np
ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'runs')
for arm in sys.argv[1:] or ['r51-s1', 'r51-s2', 'r51-s3']:
    d = sorted(glob.glob(os.path.join(ROOT, arm, '*', 'run.json')))[-1].rsplit(os.sep, 1)[0]
    lay = json.load(open(os.path.join(d, 'fields', 'layout.json')))
    nx, nz = lay['bed']['cellsX'], lay['bed']['cellsZ']
    bed = np.fromfile(os.path.join(d, 'fields', 'bed.f32'), '<f4').reshape(nx, nz)
    want, rows = {}, []
    for line in open(os.path.join(d, 'absorptive.jsonl')):
        r = json.loads(line)
        if r['mixotroph'] or r['dead'] or r['t'] % 100:
            continue
        rows.append(r); want.setdefault(r['t'], set()).add(r['id'])
    place = {}
    with gzip.open(os.path.join(d, 'positions.jsonl.gz'), 'rt') as f:
        for line in f:
            if not want: break
            t = json.loads(line[:line.index(',')] + '}')['t']
            if t not in want: continue
            ids = want.pop(t)
            for b in json.loads(line)['b']:
                if b[0] in ids: place[(t, b[0])] = (b[1], b[2], b[3])
    own, nb, tank, clr, upk, absv = [], [], [], [], [], []
    cache = {}
    for r in rows:
        p = place.get((r['t'], r['id']))
        if p is None: continue
        ix, iz = int(p[0]), int(p[2])
        if not (1 <= ix < nx - 1 and 1 <= iz < nz - 1) or p[1] - bed[ix, iz] >= 1.5: continue
        t = r['t']
        if t not in cache:
            fn = os.path.join(d, 'fields', f'{t:09d}.snow-floor.f32')
            if not os.path.exists(fn): continue
            s = np.fromfile(fn, '<f4').reshape(nx, nz); cache = {t: s}
        s = cache[t]
        ring = [s[ix + a, iz + b] for a in (-1, 0, 1) for b in (-1, 0, 1) if (a or b) and s[ix + a, iz + b] > 0]
        if not ring: continue
        own.append(s[ix, iz]); nb.append(np.mean(ring)); tank.append(s[s > 0].mean())
        if r['densityHere'] > 0: clr.append(r['foodW'] / r['densityHere'])
        upk.append(r['upkeepW']); absv.append(r['absVolume'])
    own, nb, tank = map(np.array, (own, nb, tank))
    print(f'== {arm}: {len(own)} bed-stomach samples at a dump second')
    print(f'  own cell J/m3 median {np.median(own):.3f}; eight neighbours {np.median(nb):.3f}; tank bed mean {np.median(tank):.3f}; own / neighbours median {np.median(own/np.maximum(nb,1e-9)):.2f}')
    print(f'  clearance m3/s median {np.median(clr):.2f} (p10 {np.percentile(clr,10):.2f}, p90 {np.percentile(clr,90):.2f}); absorptive volume m3 median {np.median(absv):.3f}; upkeep W median {np.median(upk):.3f} (p10 {np.percentile(upk,10):.3f})')
