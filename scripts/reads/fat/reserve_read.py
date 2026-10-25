"""Reserve readings for the fat specification (2026-10-25). Reads one run directory's lineage.jsonl and
absorptive.jsonl. Prints: aged-death reserve by guild and breeding mode (lineage), and for absorptive bodies the
reserve per tissue, per volume and in seconds of upkeep at a few sample times (absorptive log).
Usage: python -I reserve_read.py RUNDIR [times...]"""
import json, sys, statistics as st

run = sys.argv[1]
times = set(float(x) for x in sys.argv[2:]) or {12000.0, 18000.0, 24000.0}

def q(xs, ps=(0.5, 0.9, 0.99, 1.0)):
    if not xs: return 'n=0'
    xs = sorted(xs); n = len(xs)
    return f'n={n} ' + ' '.join(f'p{int(p*100)}={xs[min(n-1, int(p*n))]:.4g}' for p in ps)

born = {}
deaths = {}
for line in open(f'{run}/lineage.jsonl'):
    r = json.loads(line)
    e = r['e']
    if e == 'b':
        g = 'leaf' if r.get('pho') == 1 and r.get('abs') == 0 and r.get('jnt') == 0 and r.get('atk', 0) == 0 else \
            'mouthed' if r.get('atk', 0) > 0 and r.get('ink', 0) > 0 else \
            'stomach' if r.get('abs') == 1 else \
            'jointed' if r.get('jnt') == 1 else 'other'
        born[r['id']] = (r['t'], g, r.get('gm', 0), r.get('rm', 0.0))
    elif e == 'd':
        deaths[r['id']] = r

print('== aged and eaten deaths: reserve at death (res), J, by guild and mode (gm 1 = gestation)')
groups = {}
for i, d in deaths.items():
    if i not in born: continue
    t0, g, gm, rm = born[i]
    if d['t'] < 6000: continue
    key = (d['c'], g, gm)
    groups.setdefault(key, []).append((d.get('res', 0.0), d.get('ga', 0.0), d['t'] - t0, rm))
for key in sorted(groups):
    v = groups[key]
    if len(v) < 20: continue
    print(key, 'res', q([x[0] for x in v]), '| ga', q([x[1] for x in v], (0.5, 0.99)),
          '| age med', f'{st.median([x[2] for x in v]):.0f}', '| margin med', f'{st.median([x[3] for x in v]):.0f}')

print('== absorptive log: energy/tissue, energy/volume J/m3, energy/upkeepW s, at', sorted(times))
per = {}
maxratio = {}
for line in open(f'{run}/absorptive.jsonl'):
    if '"dead":true' in line: continue
    r = json.loads(line)
    if r.get('volume', 0) <= 0 or r.get('tissue', 0) <= 0 or r.get('upkeepW', 0) <= 0: continue
    if r['t'] < 6000: continue
    e = r['energy']
    ratio = e / r['tissue']
    mid = r['id']
    if ratio > maxratio.get(mid, (-1, 0))[0]:
        maxratio[mid] = (ratio, e / r['volume'], e / r['upkeepW'], r['mixotroph'])
    if r['t'] in times:
        per.setdefault((r['t'], r['mixotroph']), []).append((ratio, e / r['volume'], e / r['upkeepW'], r['tissue'], r['volume'], r['upkeepW'] / r['volume']))
for key in sorted(per):
    v = per[key]
    print(key, '\n  E/tissue', q([x[0] for x in v]), '\n  E/vol', q([x[1] for x in v]), '\n  E/W s', q([x[2] for x in v]),
          '\n  tissue J', q([x[3] for x in v], (0.5,)), 'vol', q([x[4] for x in v], (0.1, 0.5, 0.9)), 'W/m3', q([x[5] for x in v], (0.5,)))
for mixo in (False, True):
    v = [m for m in maxratio.values() if m[3] == mixo]
    print('lifetime max per body, mixotroph', mixo, '\n  E/tissue', q([x[0] for x in v]), '\n  E/vol', q([x[1] for x in v]), '\n  E/W s', q([x[2] for x in v]))
