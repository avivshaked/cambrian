# W1's misses: do the failing matter-only founders carry an absorptive node in their genome
# (an adult mixotroph whose newborn lost the stomach), where the passing ones do not?
import glob, gzip, json, os, sys
sys.path.insert(0, 'scratch/wt-r49/scripts/reads')
run = sorted(glob.glob('runs/r49-s1/2026*'))[-1]
founders = {}
with open(run + '/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try: r = json.loads(line)
        except ValueError: continue
        if 'fmat' in r or 'fsnow' in r:
            founders[r['id']] = r
def bad(r):
    out = []
    for a, b in (('fsnow', 'fcol'), ('fmat', 'fmcol')):
        x, y = r.get(a), r.get(b)
        if isinstance(x, (int, float)) and isinstance(y, (int, float)) and x < y * (1 - 1e-6):
            out.append(a)
    return out
ids = set(founders)
types = {}
with gzip.open(run + '/genomes.jsonl.gz', 'rt', encoding='utf-8') as f:
    for line in f:
        try: g = json.loads(line)
        except ValueError: continue
        gid = g.get('id')
        if gid in ids:
            s = json.dumps(g)
            types[gid] = sorted(set(t for t in ('Absorptive', 'Photosynthetic') if ('"' + t + '"') in s or ('"' + t.lower() + '"') in s))
rows = {'fail': [], 'pass': []}
for i, r in founders.items():
    if r.get('abs') == 0 and r.get('pho') == 1 and isinstance(r.get('fmat'), (int, float)):
        rows['fail' if bad(r) else 'pass'].append((i, types.get(i)))
    if r.get('abs') == 1 and r.get('pho') == 0 and isinstance(r.get('fsnow'), (int, float)) and bad(r):
        rows['fail'].append((i, types.get(i), 'snow-only row'))
for k in ('fail', 'pass'):
    v = rows[k]
    both = sum(1 for x in v if x[1] and len(x[1]) == 2)
    print(k, len(v), 'with both types in the genome:', both)
print('failures:', rows['fail'])
print('sample genome keys:', list(json.loads(gzip.open(run + '/genomes.jsonl.gz', 'rt').readline()).keys())[:12])
