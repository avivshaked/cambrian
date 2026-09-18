"""Where are round 40's leaves born, where do they breed, and who survives? For each body:
birth time and parent from lineage.jsonl ('k' r = child, f = founder), death time from its
'd' row, depth at first sighting in positions.jsonl (every 100 s), and the parent's depth at
that sample. Then per window: newborn depth, parent depth, and by birth-depth band the
lifespan and the share that breed."""
import json, sys, glob, os
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
arm = sys.argv[1] if len(sys.argv) > 1 else 'r40-s1'
d = sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*' + os.sep)))[-1]
birth, death, parent, pho, kids = {}, {}, {}, {}, {}
with open(os.path.join(d, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        try: r = json.loads(line)
        except Exception: continue
        if r.get('e') == 'b':
            birth[r['id']] = r['t']; parent[r['id']] = r['p']; pho[r['id']] = r.get('pho', 0)
            if r['p'] >= 0: kids[r['p']] = kids.get(r['p'], 0) + 1
        elif r.get('e') == 'd': death[r['id']] = r['t']
first, samples = {}, []
with open(os.path.join(d, 'positions.jsonl'), encoding='utf-8') as f:
    for line in f:
        try: r = json.loads(line)
        except Exception: continue
        t = r['t']; ys = {}
        for b in r['b']:
            ys[b[0]] = b[2]
            if b[0] not in first: first[b[0]] = (t, b[2])
        samples.append((t, ys))
last_t = samples[-1][0]
def q(v, p):
    v = sorted(v); return v[int(p * (len(v) - 1))] if v else float('nan')
print(f'{arm}: {len(birth)} births ({sum(1 for i in parent if parent[i] >= 0)} children), {len(death)} deaths, {len(samples)} samples to {last_t} s')
# newborn depth and parent depth per window
print('\nnewborns (children first seen within 100 s of birth): depth at first sighting, and the parent depth at that sample')
print(f"{'window s':>13} {'n':>6} {'newborn med':>11} {'q1':>6} {'q3':>6} {'parent med':>10} {'newborn-parent med':>18}")
by_t = dict(samples)
for lo in range(0, int(last_t), 2000):
    hi = lo + 2000
    nb, pa, diff = [], [], []
    for bid, (t, y) in first.items():
        if bid not in birth or parent.get(bid, -1) < 0: continue
        if not (lo <= birth[bid] < hi) or t - birth[bid] > 100: continue
        nb.append(y)
        py = by_t.get(t, {}).get(parent[bid])
        if py is not None: pa.append(py); diff.append(y - py)
    if nb: print(f"{lo:>6}-{hi:<6} {len(nb):>6} {q(nb,.5):>11.1f} {q(nb,.25):>6.1f} {q(nb,.75):>6.1f} {q(pa,.5):>10.1f} {q(diff,.5):>18.1f}")
# by birth-depth band: lifespan and breeding, for children born before last_t - 3000 (so a life could complete)
print('\nby birth depth (children born before', last_t - 3000, 's, so a full life fits): lifespan and breeding')
print(f"{'band':>10} {'n':>6} {'lifespan med s':>14} {'died<500s %':>11} {'alive>1500s %':>13} {'bred %':>7} {'children/parent':>15}")
bands = [(0, 4), (4, 8), (8, 12), (12, 16), (16, 22), (22, 60)]
for lo, hi in bands:
    ids = [bid for bid, (t, y) in first.items() if bid in birth and parent.get(bid, -1) >= 0 and t - birth[bid] <= 100 and lo <= -y < hi and birth[bid] < last_t - 3000]
    if not ids: continue
    life = [(death.get(b, last_t) - birth[b]) for b in ids]
    d500 = 100 * sum(1 for b in ids if b in death and death[b] - birth[b] < 500) / len(ids)
    a1500 = 100 * sum(1 for b in ids if death.get(b, last_t) - birth[b] >= 1500) / len(ids)
    bred = 100 * sum(1 for b in ids if kids.get(b, 0) > 0) / len(ids)
    kp = sum(kids.get(b, 0) for b in ids) / len(ids)
    print(f"{lo:>3}-{hi:<3} m {len(ids):>6} {q(life,.5):>14.0f} {d500:>11.0f} {a1500:>13.0f} {bred:>7.0f} {kp:>15.2f}")
# residents by age in the last 2,000 s
print('\nresidents in the last 2,000 s by age: median depth')
a = {'<500': [], '500-1500': [], '1500-3000': [], '>3000': []}
for t, ys in samples:
    if t < last_t - 2000: continue
    for bid, y in ys.items():
        if bid not in birth: continue
        age = t - birth[bid]
        a['<500' if age < 500 else '500-1500' if age < 1500 else '1500-3000' if age < 3000 else '>3000'].append(y)
for k, v in a.items(): print(f'  age {k:>9} s: n {len(v):>6} median {q(v,.5):>6.1f} m  q1 {q(v,.25):>6.1f}  q3 {q(v,.75):>6.1f}')
