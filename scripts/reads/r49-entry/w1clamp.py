# W1's misses against the clamp. clamped.py counts founders within 0.6 m above the centre's bed,
# but the clamp holds a founder at the centre's placement floor for its own radius, so a larger
# body sits higher and that window is a lower bound. This prints, for the matter-only founders
# over a bed more than 1 m deeper than the centre's, their first height above the centre's bed in
# 0.25 m bins with W1's misses in each (a miss: fmat under fmcol, r49-read.py's test), and the
# misses elsewhere. The clamp shows as a pile-up just above 0. Usage: w1clamp.py <arm>
import glob, json, struct, sys
from collections import Counter
sys.path.insert(0, 'scripts/reads')
import runrec
arm = sys.argv[1]
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
lay = json.load(open(run + '/fields/layout.json'))
BZ = lay['bed']['cellsZ']
b = open(run + '/fields/bed.f32', 'rb').read(); bed = struct.unpack('<%df' % (len(b) // 4), b)
R = 83.68
centre = bed[int(R) * BZ + int(R)]
tol = 1e-6
founders = {}
for line in open(run + '/lineage.jsonl', encoding='utf-8'):
    try: r = json.loads(line)
    except ValueError: continue
    if (r.get('e') == 'b' and r.get('k') == 'f' and r.get('pho') == 1 and r.get('abs') != 1
            and 'fmat' in r and 'fmcol' in r):
        founders[r['id']] = r
first, want = {}, set(founders)
for row in runrec.positions(run):
    for body in row['b']:
        if body[0] in want and body[0] not in first:
            first[body[0]] = (row['t'], body[2], body[1], body[3])
    if len(first) == len(want): break
def miss(r):
    here, col = r['fmat'], r['fmcol']
    return not (here >= col or here >= col * (1.0 - tol))
bins, missbins = Counter(), Counter()
deep = deep_miss = other_miss = other = 0
for i, r in founders.items():
    if i not in first or first[i][0] - r['t'] > 10: continue
    t, y, x, z = first[i]
    local = bed[min(int(x), BZ - 1) * BZ + min(int(z), BZ - 1)]
    m = miss(r)
    if local < centre - 1:
        deep += 1; deep_miss += m
        k = int((y - centre) // 0.25)
        if -4 <= k < 16:
            bins[k] += 1; missbins[k] += m
    else:
        other += 1; other_miss += m
print(f'{arm}: bed at the tank centre {centre:.2f} m; matter-only founders placed within 10 s: {deep + other}')
print(f'  over a bed more than 1 m deeper than the centre\'s: {deep}, W1 misses {deep_miss}')
print(f'  elsewhere: {other}, W1 misses {other_miss}')
print('  first height above the centre\'s bed, 0.25 m bins: founders (misses)')
for k in range(-4, 16):
    if bins[k]: print(f'    {k * 0.25:+5.2f} to {(k + 1) * 0.25:+5.2f} m: {bins[k]:4d} ({missbins[k]})')
