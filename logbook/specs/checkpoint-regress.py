"""Compare a fresh short run on this build against a recorded run's opening rows.

    python logbook/specs/checkpoint-regress.py <fresh run dir> <recorded run dir>

Every stats row the fresh run wrote is compared with the recorded row at the same second, with
the wall-clock fields stripped; positions and poses rows are compared byte for byte; lineage rows
born within the fresh run's span are compared as a sequence.
"""
import json
import os
import sys

fresh, rec = sys.argv[1], sys.argv[2]


def rows(p, name):
    f = os.path.join(p, name)
    if not os.path.exists(f):
        return None
    return [r for r in open(f, 'rb').read().split(b'\n') if r.strip()]


def strip(r):
    d = json.loads(r)
    for k in list(d):
        if k.startswith('wall') or k in ('harnessBodySteps', 'fluidLinkSteps'):
            d.pop(k)
    return json.dumps(d, sort_keys=True)


ok = True
fs = rows(fresh, 'stats.jsonl')
rs = {json.loads(r)['t']: r for r in rows(rec, 'stats.jsonl')}
t_end = json.loads(fs[-1])['t']
bad = [json.loads(r)['t'] for r in fs if strip(rs.get(json.loads(r)['t'], b'{}')) != strip(r)]
print('stats: %d rows to t=%s, differing: %s' % (len(fs), t_end, bad[:5] if bad else 'none'))
ok &= not bad
for name in ('positions.jsonl', 'poses.jsonl'):
    f = rows(fresh, name)
    r = rows(rec, name)
    if f is None or r is None:
        print('%s: absent in %s' % (name, 'fresh' if f is None else 'recorded'))
        continue
    rm = {json.loads(x)['t']: x for x in r}
    bad = [json.loads(x)['t'] for x in f if rm.get(json.loads(x)['t']) != x]
    print('%s: %d rows, differing: %s' % (name, len(f), bad[:5] if bad else 'none'))
    ok &= not bad
fl = rows(fresh, 'lineage.jsonl')
rl = [x for x in rows(rec, 'lineage.jsonl') if json.loads(x).get('birth', json.loads(x).get('t', 0)) <= t_end]
print('lineage: fresh %d rows, recorded to t_end %d rows, equal: %s' % (len(fl), len(rl), fl == rl[:len(fl)] and len(fl) == len(rl)))
ok &= fl == rl
print('IDENTICAL' if ok else 'DIFFERENT')
sys.exit(0 if ok else 1)
