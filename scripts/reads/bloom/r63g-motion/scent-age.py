"""Grazers' speed through the water by age, from leafscent.jsonl (every 100 s) joined to lineage birth rows, 6,000-24,000 s.
Only bodies moving over 1 mm/s through the water are in the file; the count of grazers alive at each row (lineage) is
printed beside, so the share missing from the file (still, or no gradient) can be read.
Usage: python -I scripts/reads/bloom/r63g-motion/scent-age.py RUN LABEL"""
import json, os, sys
run, label = sys.argv[1], sys.argv[2]
born, g, died = {}, set(), {}
for line in open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8'):
    if line.startswith('{"e":"b"'):
        r = json.loads(line)
        if r['t'] > 24000: break
        born[r['id']] = r['t']
        if r.get('atk', 0) > 0 and r.get('ink', 0) > 0: g.add(r['id'])
    elif line.startswith('{"e":"d"'):
        r = json.loads(line)
        if r['t'] > 24000: break
        died[r['id']] = r['t']
bands = [(0, 40), (40, 80), (80, 300), (300, 1e9)]
v = {b: [] for b in bands}; alive = {b: 0 for b in bands}
for line in open(os.path.join(run, 'leafscent.jsonl'), encoding='utf-8'):
    r = json.loads(line)
    t = r['t']
    if not (6000 <= t <= 24000): continue
    for i in g:
        if born[i] <= t < died.get(i, 1e12):
            age = t - born[i]
            for b in bands:
                if b[0] <= age < b[1]: alive[b] += 1
    for e in r['b']:
        if e[0] in g:
            age = t - born[e[0]]
            for b in bands:
                if b[0] <= age < b[1]: v[b].append(e[4] / 10.0)
def q(xs, p):
    xs = sorted(xs); return xs[min(len(xs) - 1, int(p * len(xs)))] if xs else float('nan')
for b in bands:
    print(f'{label} age {b[0]:>4.0f}-{b[1] if b[1] < 1e8 else "":<4} s: grazer-rows alive {alive[b]:6d}, in file {len(v[b]):6d}, '
          f'speed through water med {q(v[b], .5):5.1f} p90 {q(v[b], .9):5.1f} cm/s')
