# How many founders the flat-bed clamp lifted: each founder's first positions row (within one
# sample of its birth) against the bed under it and the bed at the tank's centre.
import glob, json, struct, sys
sys.path.insert(0, 'scratch/wt-r49/scripts/reads')
import runrec
arm = sys.argv[1] if len(sys.argv) > 1 else 'r49-s1'
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
lay = json.load(open(run + '/fields/layout.json'))
BZ = lay['bed']['cellsZ']
b = open(run + '/fields/bed.f32', 'rb').read(); bed = struct.unpack('<%df' % (len(b) // 4), b)
R = 83.68
centre = bed[int(R) * BZ + int(R)]
print(f'{arm}: bed at the tank centre {centre:.2f} m')
founders = {}
for line in open(run + '/lineage.jsonl', encoding='utf-8'):
    try: r = json.loads(line)
    except ValueError: continue
    if r.get('e') == 'b' and r.get('k') == 'f' and ('fsnow' in r or 'fmat' in r):
        founders[r['id']] = r
first = {}
want = set(founders)
for row in runrec.positions(run):
    for body in row['b']:
        i = body[0]
        if i in want and i not in first:
            first[i] = (row['t'], body[1], body[2], body[3])
at_clamp = []   # first y within 0.6 m above the centre's bed, over a local bed deeper than 1 m below it
deep_side = 0
for i, r in founders.items():
    if i not in first: continue
    t, x, y, z = first[i]
    if t - r['t'] > 10: continue
    local = bed[min(int(x), BZ - 1) * BZ + min(int(z), BZ - 1)]
    if local < centre - 1: deep_side += 1
    if centre < y < centre + 0.6 and local < centre - 1:
        at_clamp.append((i, r.get('src'), 'snow' if 'fsnow' in r else 'matter', round(y, 2), round(local, 1)))
n = sum(1 for i in founders if i in first and first[i][0] - founders[i]['t'] <= 10)
print(f'founders with a landing reading and a positions row within 10 s: {n}')
print(f'  over a bed more than 1 m deeper than the centre\'s: {deep_side}')
print(f'  of those, sitting just above the centre\'s bed height (the clamp): {len(at_clamp)}')
from collections import Counter
print('  by source and food:', dict(Counter((s, f) for _, s, f, _, _ in at_clamp)))
print('  e.g.', at_clamp[:6])
