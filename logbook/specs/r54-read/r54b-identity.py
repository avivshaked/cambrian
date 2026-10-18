# Identity check for D141: r54b-s1 against the stopped first launch r54-s1, every stats row up to the
# stopped run's last second, wall-clock fields aside. Prints the first row that differs, if any.
import json, re, sys, glob
a = sorted(glob.glob('D:/Projects/experiments/evolution-simulator/runs/r54-s1/2026-09-30-*/stats.jsonl'))[-1]
b = sorted(glob.glob('D:/Projects/experiments/evolution-simulator/runs/r54b-s1/2026-10-01-*/stats.jsonl'))[-1]
skip = re.compile(r'wall|pace|clock|elapsed|realtime|Ms$|ms$', re.I)
def rows(p):
    out = {}
    with open(p, encoding='utf-8') as f:
        for line in f:
            if not line.startswith('{"t":'): continue
            try: r = json.loads(line)
            except ValueError: continue
            out[r['t']] = {k: v for k, v in r.items() if not skip.search(k)}
    return out
A, B = rows(a), rows(b)
common = sorted(set(A) & set(B))
diff = [t for t in common if A[t] != B[t]]
print('first launch rows %d (to %s s), relaunch rows %d (to %s s), compared %d' % (len(A), max(A), len(B), max(B) if B else 0, len(common)))
if diff:
    t = diff[0]; keys = [k for k in A[t] if A[t].get(k) != B[t].get(k)]
    print('PARTED at t=%s in %d rows; first keys: %s' % (t, len(diff), keys[:8])); sys.exit(1)
print('IDENTICAL through t=%s' % (common[-1] if common else None))
