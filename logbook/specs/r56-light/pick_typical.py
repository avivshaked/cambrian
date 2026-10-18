"""Pick the living photosynthetic member of the dominant clade nearest the clade's trait medians.

usage: python pick_typical.py <run dir> <snapshot second> <founder id> <out genome.json>

Distance is over log adultScale, investment, margin/600, lifespan and node count, among members
whose modal node count and reproduction mode match the clade's. Writes the genome as one line.
"""
import gzip, json, sys, os, zlib, statistics, collections, math

run, snap_t, founder, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]

parent, pho = {}, {}
with open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except Exception:
            continue
        if r.get('e') == 'b':
            parent[r['id']] = r['p']; pho[r['id']] = r.get('pho', 0)

living = []
with gzip.open(os.path.join(run, 'snapshots', '%09d.jsonl.gz' % snap_t), 'rt', encoding='utf-8') as g:
    for line in g:
        try:
            living.append(json.loads(line)['id'])
        except Exception:
            pass

def root(i):
    while parent.get(i, -1) >= 0:
        i = parent[i]
    return i

members = set(i for i in living if pho.get(i) == 1 and root(i) == founder)

data = open(os.path.join(run, 'genomes.jsonl.gz'), 'rb').read()
parts, rest = [], data
while rest:
    d = zlib.decompressobj(16 + zlib.MAX_WBITS)
    try:
        parts.append(d.decompress(rest))
    except Exception:
        break
    if not d.eof:
        break
    rest = d.unused_data
genomes = {}
for line in b''.join(parts).split(b'\n'):
    if not line.strip():
        continue
    try:
        r = json.loads(line)
    except Exception:
        continue
    if r.get('id') in members:
        genomes[r['id']] = r

def traits(g):
    rep = g['reproduction']
    return dict(nodes=len(g['nodes']), mode=rep['mode'], brood=rep['brood'], scale=g['adultScale'],
                inv=rep['investment'], margin=rep['margin'], life=g['lifespan'],
                cells=tuple(sorted(n['cell'] for n in g['nodes'])))

T = {i: traits(g) for i, g in genomes.items()}
modes = collections.Counter(t['mode'] for t in T.values())
nodes = collections.Counter(t['nodes'] for t in T.values())
broods = collections.Counter(t['brood'] for t in T.values())
cells = collections.Counter(t['cells'] for t in T.values())
mode, n, brood = modes.most_common(1)[0][0], nodes.most_common(1)[0][0], broods.most_common(1)[0][0]
pool = [i for i, t in T.items() if t['mode'] == mode and t['nodes'] == n and t['brood'] == brood]
med = {k: statistics.median(T[i][k] for i in pool) for k in ('scale', 'inv', 'margin', 'life')}

def dist(t):
    return ((math.log(t['scale']) - math.log(med['scale'])) ** 2 + ((t['inv'] - med['inv']) / 0.02) ** 2 +
            ((t['margin'] - med['margin']) / 60) ** 2 + ((t['life'] - med['life']) / 0.1) ** 2)

best = min(pool, key=lambda i: dist(T[i]))
print('members', len(members), 'genomes', len(genomes))
print('modes', dict(modes), 'nodes', dict(nodes), 'broods', dict(broods))
print('cells', cells.most_common(4))
print('medians among', len(pool), med)
print('typical', best, T[best])
with open(out, 'w', encoding='utf-8') as f:
    f.write(json.dumps(genomes[best], separators=(',', ':')) + '\n')
