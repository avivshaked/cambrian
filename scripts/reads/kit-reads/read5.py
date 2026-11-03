# Read 5: how much of the grazer guild sits at its breeding gate. Grazer = birth row with atk>0 and ink>0 (as read1.py).
# Input is the gate census's JSONL (Evosim.Farm --gate-census): one row per living body at each named second.
# Lump breeders: reserve / gate, in the bands <0.5, 0.5-0.9, 0.9-1.0 and >=1.0. Gestating: account / gate, quartiles.
# Usage: python -I read5.py <run dir or arm dir> <census jsonl>
import json, sys, glob, os
import os as _os
REPO = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), '..', '..', '..')).replace(chr(92), '/')

def q(a, p):
    if not a: return None
    a = sorted(a); i = (len(a) - 1) * p; lo = int(i); hi = min(lo + 1, len(a) - 1)
    return round(a[lo] + (a[hi] - a[lo]) * (i - lo), 4)

def quart(a): return {'q25': q(a, .25), 'med': q(a, .5), 'q75': q(a, .75)}

def lineage_path(run):
    run = run.replace(chr(92), '/')
    if os.path.isfile(run + '/lineage.jsonl'): return run + '/lineage.jsonl'
    found = sorted(glob.glob(run + '/*/lineage.jsonl'))
    if not found: sys.exit('read5: no lineage.jsonl under ' + run)
    return found[-1]

if len(sys.argv) != 3: sys.exit('Usage: python -I read5.py <run dir or arm dir> <census jsonl>')
run, census = sys.argv[1], sys.argv[2]

# Line by line; only the grazer ids are kept.
graz = set()
with open(lineage_path(run), encoding='utf-8') as f:
    for l in f:
        if l.startswith('{"e":"b"'):
            r = json.loads(l)
            if r.get('atk', 0) > 0 and r.get('ink', 0) > 0: graz.add(r['id'])

by_t = {}
with open(census, encoding='utf-8') as f:
    for l in f:
        if not l.strip(): continue
        r = json.loads(l)
        if r['plant'] or r['id'] not in graz or not r['adult'] or r['gate'] <= 0: continue
        b = by_t.setdefault(r['t'], {'lump': [], 'gest': []})
        if r['mode'] == 'lump': b['lump'].append(r['reserve'] / r['gate'])
        else: b['gest'].append(r['account'] / r['gate'])

def share(a, lo, hi):
    return round(sum(1 for x in a if lo <= x < hi) / len(a), 4) if a else None

res = {'census': census, 'grazers_with_birth_row': len(graz), 'seconds': {}}
for t in sorted(by_t):
    lump, gest = by_t[t]['lump'], by_t[t]['gest']
    res['seconds'][str(t)] = {
        'lump_adult_grazers': len(lump),
        'lump_share_below_0.5': share(lump, float('-inf'), 0.5),
        'lump_share_0.5_to_0.9': share(lump, 0.5, 0.9),
        'lump_share_0.9_to_1.0': share(lump, 0.9, 1.0),
        'lump_share_at_or_above_1.0': share(lump, 1.0, float('inf')),
        'lump_reserve_over_gate': quart(lump),
        'gestating_adult_grazers': len(gest),
        'gestating_account_over_gate': quart(gest),
    }
    print(t, json.dumps(res['seconds'][str(t)]))

_os.makedirs(REPO + '/scratch/kit-reads/out', exist_ok=True)
name = _os.path.splitext(_os.path.basename(census))[0]
json.dump(res, open(REPO + '/scratch/kit-reads/out/read5-' + name + '.json', 'w'), indent=1)
