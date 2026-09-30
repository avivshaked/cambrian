# Horizontal speed of jointed against rigid bodies over 10 s, from a run's positions.jsonl.gz, at five times.
# Rigid bodies only drift with the water, so they are the baseline a swimmer has to beat.
# Usage: python scripts/reads/swim-speed.py <arm>
import gzip, json, math, sys, glob, os
arm = sys.argv[1]
f = sorted(glob.glob(f'runs/{arm}/*/positions.jsonl.gz'), key=os.path.getmtime)[-1]
Ts = [1000, 5000, 10000, 20000, 29000]
want = set(Ts) | {t + 10 for t in Ts}
rows = {}
with gzip.open(f, 'rt') as g:
    for line in g:
        t = int(float(line[5:line.index(',')]))
        if t in want:
            rows[t] = json.loads(line)
        if t > max(want): break
def med(a):
    a = sorted(a); return a[len(a)//2] if a else float('nan')
def pct(a, q):
    a = sorted(a); return a[int(q*(len(a)-1))] if a else float('nan')
print(arm)
for T in Ts:
    if T not in rows or T+10 not in rows: continue
    a = {b[0]: b for b in rows[T]['b']}; c = {b[0]: b for b in rows[T+10]['b']}
    sp = {True: [], False: []}
    for i, b in a.items():
        if i in c:
            e = c[i]; v = math.hypot(e[1]-b[1], e[3]-b[3]) / 10 * 100  # cm/s horizontal
            sp[bool(b[4] & 2)].append(v)
    j, r = sp[True], sp[False]
    print(f'  t={T:>5}  jointed n={len(j):>4} median {med(j):5.1f} cm/s  90th {pct(j,.9):5.1f} | rigid n={len(r):>5} median {med(r):5.1f} 90th {pct(r,.9):5.1f}')
