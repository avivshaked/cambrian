# Which founders' lines held the joints: at each named second, the living bodies grouped by the
# founder at the root of their parent chain, with the jointed share of each of the largest lines.
# Reads lineage.jsonl alone (birth rows' p and jnt, death rows). Usage: jointclades.py <arm> s1,s2,...
import glob, json, sys
from collections import Counter, defaultdict

arm = sys.argv[1]
seconds = [float(x) for x in sys.argv[2].split(',')]
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]

parent, born, jnt, pho, abs_, src = {}, {}, {}, {}, {}, {}
died = {}
with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue  # a live run's last row, half written
        if r['e'] == 'b':
            i = r['id']
            parent[i] = r['p']; born[i] = r['t']; jnt[i] = r['jnt']; pho[i] = r['pho']; abs_[i] = r['abs']
            src[i] = r.get('src')
        elif r['e'] == 'd':
            died[r['id']] = r['t']

root = {}
def root_of(i):
    chain = []
    while i not in root and parent.get(i, -1) >= 0:
        chain.append(i)
        i = parent[i]
    r = root.get(i, i)
    for c in chain:
        root[c] = r
    root[i] = r
    return r

for s in seconds:
    alive = [i for i in born if born[i] <= s and died.get(i, float('inf')) > s]
    by = defaultdict(list)
    for i in alive:
        by[root_of(i)].append(i)
    total_j = sum(jnt[i] for i in alive)
    print(f'{arm} at {s:.0f} s: {len(alive)} alive, {total_j} jointed, {len(by)} lines')
    for r, members in sorted(by.items(), key=lambda kv: -len(kv[1]))[:5]:
        j = sum(jnt[i] for i in members)
        print(f'  line of {r} (founded {born[r]:.1f} s, {src[r]}, founder jnt {jnt[r]} pho {pho[r]} abs {abs_[r]}): '
              f'{len(members)} alive, {j} jointed ({100.0 * j / len(members):.0f}%)')
