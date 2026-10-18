"""Round 56 groundwork: leaves' lifetimes and the dominant leaf lineage of a run.

usage: python leaf_lineage.py <run dir> <snapshot second> <out prefix>

Reads lineage.jsonl (birth rows e=b with pho flag, death rows e=d), the snapshot at the given
second and genomes.jsonl.gz. Writes <out prefix>-ages.tsv, <out prefix>-clades.tsv and the
modal genome of the dominant clade as <out prefix>-genome.json (one line). Read-only on the run.
A live run's last line may be half-written: unparsable lines are skipped and counted.
"""
import gzip, json, sys, hashlib, statistics, collections, os, zlib

run, snap_t, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]

births, deaths, parent, pho, kind, src = {}, {}, {}, {}, {}, {}
children = collections.defaultdict(list)
bad = 0
with open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except Exception:
            bad += 1
            continue
        if r.get('e') == 'b':
            i = r['id']
            births[i] = r['t']; parent[i] = r['p']; pho[i] = r.get('pho', 0); kind[i] = r.get('k'); src[i] = r.get('src')
            if r['p'] >= 0:
                children[r['p']].append(r['t'])
        elif r.get('e') == 'd':
            deaths[r['id']] = r['t']

t_last = max(max(births.values()), max(deaths.values()) if deaths else 0)

def q(xs, p):
    xs = sorted(xs)
    if not xs: return float('nan')
    k = (len(xs) - 1) * p
    lo = int(k); hi = min(lo + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)

rows = []
def summary(label, ids, window=None):
    ids = [i for i in ids if window is None or (window[0] <= births[i] < window[1])]
    dead = [deaths[i] - births[i] for i in ids if i in deaths]
    bred = [min(children[i]) - births[i] for i in ids if children.get(i)]
    nkids = [len(children.get(i, [])) for i in ids if i in deaths]
    rows.append((label, len(ids), len(dead),
                 q(dead, .25), q(dead, .5), q(dead, .75), q(dead, .9),
                 len(bred), q(bred, .25), q(bred, .5), q(bred, .75),
                 (sum(1 for i in ids if i in deaths and children.get(i)) / len(dead)) if dead else float('nan'),
                 (statistics.mean(nkids) if nkids else float('nan'))))

photo = [i for i in births if pho[i] == 1]
summary('pho all', photo)
summary('pho inherited (k=r)', [i for i in photo if kind[i] == 'r'])
# Bodies born early enough that censoring by the run's end is small: the death must be seen.
summary('pho inherited, born < t_last-6000', [i for i in photo if kind[i] == 'r'], (0, t_last - 6000))
for a, b in [(0, 5000), (5000, 10000), (10000, 15000), (15000, 20000), (20000, 25000)]:
    if a < t_last - 3000:
        summary(f'pho inherited, born {a}-{b}', [i for i in photo if kind[i] == 'r'], (a, b))
summary('non-pho all', [i for i in births if pho[i] == 0])

with open(out + '-ages.tsv', 'w', encoding='utf-8') as f:
    f.write('# run %s, lineage rows unparsed %d, last event t=%.1f\n' % (run, bad, t_last))
    f.write('# age at death = death t - birth t; first birth = first child birth t - own birth t;\n')
    f.write('# bred share = share of the dead that had at least one child; kids = mean children per dead body\n')
    f.write('set\tn born\tn dead\tdeath age p25\tp50\tp75\tp90\tn bred\tfirst birth p25\tp50\tp75\tbred share\tkids per dead\n')
    for r in rows:
        f.write('\t'.join(str(round(x, 3)) if isinstance(x, float) else str(x) for x in r) + '\n')

# The snapshot: who lives at snap_t.
snap = os.path.join(run, 'snapshots', '%09d.jsonl.gz' % snap_t)
living = []
with gzip.open(snap, 'rt', encoding='utf-8') as g:
    for line in g:
        try:
            living.append(json.loads(line)['id'])
        except Exception:
            pass
living_pho = [i for i in living if pho.get(i) == 1]

def root(i):
    seen = 0
    while parent.get(i, -1) >= 0 and seen < 100000:
        i = parent[i]; seen += 1
    return i, seen

clade = collections.Counter()
depth = {}
for i in living_pho:
    r, d = root(i); clade[r] += 1; depth[i] = d

with open(out + '-clades.tsv', 'w', encoding='utf-8') as f:
    f.write('# living at %d: %d, photosynthetic %d; clades by founder\n' % (snap_t, len(living), len(living_pho)))
    f.write('founder id\tfounder src\tfounder birth t\tliving pho members\tshare\n')
    for r, n in clade.most_common(15):
        f.write('%d\t%s\t%.1f\t%d\t%.3f\n' % (r, src.get(r), births.get(r, -1), n, n / len(living_pho)))

top = clade.most_common(1)[0][0]
members = set(i for i in living_pho if root(i)[0] == top)

# Genomes of the members; hash without id (and without lineage-only fields).
genomes = {}
def read_genomes(path):
    # Record-format members; a live file's tail may be incomplete, so decompress member by member.
    data = open(path, 'rb').read()
    parts = []
    rest = data
    while rest:
        d = zlib.decompressobj(16 + zlib.MAX_WBITS)
        try:
            chunk = d.decompress(rest)
        except Exception:
            break
        parts.append(chunk)
        if not d.eof:
            break
        rest = d.unused_data
    buf = b''.join(parts)
    for line in buf.split(b'\n'):
        if not line.strip(): continue
        try:
            r = json.loads(line)
        except Exception:
            continue
        if r.get('id') in members:
            genomes[r['id']] = r
read_genomes(os.path.join(run, 'genomes.jsonl.gz'))

def key(gm):
    g = dict(gm); g.pop('id', None)
    return hashlib.sha1(json.dumps(g, sort_keys=True).encode()).hexdigest()[:12]

hashes = collections.Counter()
by_hash = {}
for i, gm in genomes.items():
    h = key(gm); hashes[h] += 1; by_hash.setdefault(h, i)

def shape(gm):
    cells = collections.Counter(n['cell'] for n in gm['nodes'])
    dims = [(round(n['dimensions']['x'], 2), round(n['dimensions']['y'], 2), round(n['dimensions']['z'], 2)) for n in gm['nodes']]
    rep = gm['reproduction']
    return dict(nodes=len(gm['nodes']), cells=dict(cells), dims=dims, adultScale=gm.get('adultScale'),
                lifespan=gm.get('lifespan'), investment=rep['investment'], margin=rep['margin'],
                brood=rep['brood'], mode=rep['mode'])

with open(out + '-clades.tsv', 'a', encoding='utf-8') as f:
    f.write('\n# dominant clade founder %d: %d living pho members, %d genomes found; generations from founder median %s\n'
            % (top, len(members), len(genomes), statistics.median(depth[i] for i in members)))
    f.write('genome hash\tcount\texample id\tshape\n')
    for h, n in hashes.most_common(8):
        f.write('%s\t%d\t%d\t%s\n' % (h, n, by_hash[h], json.dumps(shape(genomes[by_hash[h]]))))
    # trait medians across members
    if genomes:
        ms = [shape(g) for g in genomes.values()]
        f.write('\n# member trait medians: investment %.3f, margin %.1f s, lifespan %.3f, adultScale %.3f, nodes %s\n' % (
            statistics.median(m['investment'] for m in ms), statistics.median(m['margin'] for m in ms),
            statistics.median(m['lifespan'] for m in ms), statistics.median(m['adultScale'] for m in ms),
            collections.Counter(m['nodes'] for m in ms).most_common(3)))

modal = by_hash[hashes.most_common(1)[0][0]]
with open(out + '-genome.json', 'w', encoding='utf-8') as f:
    f.write(json.dumps(genomes[modal], separators=(',', ':')) + '\n')
print('done', out, 'modal id', modal, 'unparsed lineage rows', bad)
