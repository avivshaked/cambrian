"""Share of absorptive-log bodies whose reserve ever passed a cap, and share of logged rows above it (fat spec,
2026-10-25). Caps in seconds of upkeep and in J per m3 of body. Usage: python -I bind_rate.py RUNDIR"""
import json, sys
run = sys.argv[1]
secs = (600, 1000, 2000, 3000)
vols = (5000, 10000, 15000)
mx = {}
rows = 0; above_s = dict.fromkeys(secs, 0); above_v = dict.fromkeys(vols, 0)
for line in open(f'{run}/absorptive.jsonl'):
    if '"dead":true' in line: continue
    r = json.loads(line)
    if r['t'] < 6000 or r.get('upkeepW', 0) <= 0 or r.get('volume', 0) <= 0: continue
    s = r['energy'] / r['upkeepW']; v = r['energy'] / r['volume']
    rows += 1
    for c in secs: above_s[c] += s > c
    for c in vols: above_v[c] += v > c
    a, b = mx.get(r['id'], (0, 0)); mx[r['id']] = (max(a, s), max(b, v))
n = len(mx)
print(f'bodies {n}, rows {rows}')
for c in secs:
    print(f'cap {c} s: bodies ever above {sum(1 for a, _ in mx.values() if a > c) / n:.2%}, rows above {above_s[c] / rows:.3%}')
for c in vols:
    print(f'cap {c} J/m3: bodies ever above {sum(1 for _, b in mx.values() if b > c) / n:.2%}, rows above {above_v[c] / rows:.3%}')
