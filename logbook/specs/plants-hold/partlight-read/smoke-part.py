# Where a D297 smoke world first parts from its pldr twin, by the stats rows at the same t.
import json, sys, glob, os
def rows(arm):
    f = sorted(glob.glob(os.path.join(arm, '*', 'stats.jsonl')))[-1]
    return {r['t']: r for r in (json.loads(l) for l in open(f))}
a, b = rows(sys.argv[1]), rows(sys.argv[2])
keys = ('population', 'alive', 'standingJoules', 'energyIn', 'lightCaptured', 'births', 'deaths')
first = None
for t in sorted(set(a) & set(b)):
    ra, rb = a[t], b[t]
    diff = [k for k in ra if k in rb and ra[k] != rb[k] and k not in ('wall', 'wallSeconds', 'pace', 'stepMs')]
    if diff and first is None:
        first = t; print('first parts at t', t, 'on', len(diff), 'columns, e.g.', diff[:6])
last = max(set(a) & set(b))
print('at t', last, {k: (a[last].get(k), b[last].get(k)) for k in ('births', 'deaths', 'partFeedFallbacks') if k in a[last] or k in b[last]})
if first is None: print('NO column differs at any shared t')
