"""Photosynthetic bodies' ages at death by cause, and breeding by cause.

usage: python ages_by_cause.py <run dir> <out tsv>
Inherited (k=r) photosynthetic bodies born before the last event minus 6,000 s, so that almost
every one has died. Read-only on the run; a half-written last line is skipped.
"""
import json, sys, os, collections

run, out = sys.argv[1], sys.argv[2]
b, pho, kind, cause, dt, kids, first = {}, {}, {}, {}, {}, collections.Counter(), {}
last = 0
with open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except Exception:
            continue
        last = max(last, r['t'])
        if r['e'] == 'b':
            b[r['id']] = r['t']; pho[r['id']] = r.get('pho', 0); kind[r['id']] = r.get('k')
            p = r['p']
            if p >= 0:
                kids[p] += 1
                first.setdefault(p, r['t'])
        else:
            cause[r['id']] = r.get('c'); dt[r['id']] = r['t']

def q(xs, p):
    xs = sorted(xs)
    if not xs: return float('nan')
    k = (len(xs) - 1) * p; lo = int(k); hi = min(lo + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)

ids = [i for i in b if pho[i] == 1 and kind[i] == 'r' and b[i] < last - 6000 and i in dt]
rows = []
for c in ('aged', 'starved', None):
    s = [i for i in ids if c is None or cause[i] == c]
    ages = [dt[i] - b[i] for i in s]
    bred = [i for i in s if kids[i]]
    fb = [first[i] - b[i] for i in bred]
    rows.append((c or 'all', len(s), q(ages, .1), q(ages, .25), q(ages, .5), q(ages, .75), q(ages, .9),
                 len(bred) / len(s) if s else float('nan'), q(fb, .5),
                 sum(kids[i] for i in s) / len(s) if s else float('nan')))
# Starvation deaths of bodies that never bred, by age band: the juveniles.
st = [dt[i] - b[i] for i in ids if cause[i] == 'starved' and not kids[i]]
bands = [(0, 30), (30, 100), (100, 300), (300, 1000), (1000, 3000), (3000, 1e9)]
with open(out, 'w', encoding='utf-8') as f:
    f.write('# %s: inherited photosynthetic bodies born before t=%.0f (last event %.0f) and dead\n' % (run, last - 6000, last))
    f.write('cause\tn\tage p10\tp25\tp50\tp75\tp90\tbred share\tfirst birth p50\tkids per body\n')
    for r in rows:
        f.write('\t'.join(('%.3f' % x) if isinstance(x, float) else str(x) for x in r) + '\n')
    f.write('\n# starved without breeding, by age at death (s): n and share of all such deaths\n')
    for lo, hi in bands:
        n = sum(1 for a in st if lo <= a < hi)
        f.write('%g-%g\t%d\t%.3f\n' % (lo, hi, n, n / len(st) if st else 0))
print(open(out, encoding='utf-8').read())
