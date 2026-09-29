# D129's landings in round 51 seed 1 at 12,000 s, from the film window winB-12000: a mouth founder's
# column drawn by corpse joules (the acceptance), set just above the richest corpse there and
# lifted by its own radius R, then what lies within its reach and how fast new corpses arrive.
import gzip, json, math, random, sys
from collections import defaultdict
WIN = 'D:/Projects/experiments/evolution-simulator/scratch/r51-build/husk/winB-12000/husks.jsonl.gz'
RHO, REACH = 500.0, 0.5
facts, first_step, last_pos, span = {}, None, {}, None
with gzip.open(WIN, 'rt') as f:
    for line in f:
        r = json.loads(line)
        if r['k'] == 'husk': facts[r['id']] = r
        elif r['k'] == 'step':
            h = r['h']
            rows = [(int(h[i]), h[i+1], h[i+2], h[i+3], h[i+4], h[i+5]) for i in range(0, len(h) - 5, 6)]
            if first_step is None: first_step = (r['t'], rows)
            for row in rows: last_pos[row[0]] = (r['t'],) + row[1:]
            span = r['t']
t0, rows = first_step
C = []  # (x, y, z, J, settled)
for cid, x, y, z, frac, st in rows:
    J = frac * facts[cid]['first']
    if J > 0: C.append((x, y, z, J, st))
tot = sum(c[3] for c in C)
print(f't {t0}: {len(C)} corpses, {tot/1000:.1f} kJ, mean {tot/len(C):.1f} J, settled {sum(1 for c in C if c[4])/len(C):.0%}')
col = defaultdict(float); rich = {}
for c in C:
    k = (math.floor(c[0]), math.floor(c[2])); col[k] += c[3]
    if k not in rich or c[3] > rich[k][3]: rich[k] = c
Jmax = max(col.values())
keys = list(col); w = [col[k] for k in keys]
print(f'columns holding corpses: {len(col)} of about 22,000; richest {Jmax:.0f} J; '
      f'expected joules in a D129 landing column {sum(v*v for v in w)/sum(w):.0f} J (uniform landing: {tot/22000:.2f} J)')
# New corpses in the window, where they came to rest, as power per 1 m column.
born = [(cid, f) for cid, f in facts.items() if f.get('born') is not None and f['born'] >= t0]
dur = span - t0
flux = defaultdict(float)
for cid, f in born:
    p = last_pos.get(cid)
    if p: flux[(math.floor(p[1]), math.floor(p[3]))] += f['first'] / dur
print(f'new corpses {len(born)} in {dur:.0f} s: {sum(f["first"] for _, f in born)/dur:.1f} W in all')
grid = defaultdict(list)
for c in C: grid[(math.floor(c[0]), math.floor(c[2]))].append(c)
def near(x, z):
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            yield from grid.get((math.floor(x) + dx, math.floor(z) + dz), [])
random.seed(1)
N = 20000
picks = random.choices(keys, weights=w, k=N)
print('R (m) | landings with any corpse in reach | joules in reach: median, mean | new-corpse power into the 3x3 m round it: median, mean (W)')
for R in (0.15, 0.3, 0.5, 0.8, 1.2):
    got, anyc, fl = [], 0, []
    for k in picks:
        x, z = k[0] + random.random(), k[1] + random.random()
        rc = rich[k]
        y = rc[1] + max(REACH * random.random(), R if rc[4] else 0.0)
        s = 0.0
        for c in near(x, z):
            r = (3 * c[3] / (4 * math.pi * RHO)) ** (1 / 3)
            if (c[0]-x)**2 + (c[1]-y)**2 + (c[2]-z)**2 <= (r + REACH)**2: s += c[3]
        got.append(s); anyc += s > 0
        fl.append(sum(flux.get((k[0]+dx, k[1]+dz), 0.0) for dx in (-1,0,1) for dz in (-1,0,1)))
    got.sort(); fl.sort()
    print(f'{R:5} | {anyc/N:6.1%} | {got[N//2]:7.1f} {sum(got)/N:7.1f} | {fl[N//2]:.3f} {sum(fl)/N:.3f}')
