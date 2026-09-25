"""Compare a fresh short run on this build against a recorded run's opening rows.

    python logbook/specs/checkpoint-regress.py <fresh run dir> <recorded run dir>
    python logbook/specs/checkpoint-regress.py <resumed run dir> <unbroken run dir> --after 800

Every stats row the fresh run wrote is compared with the recorded row at the same second, with
the wall-clock fields stripped; positions and poses rows are compared byte for byte; lineage rows
born within the fresh run's span are compared as a sequence.

With --after S the fresh run is a resume from the checkpoint at S, and every file is compared
over the rows after S only (the checkpoint spec's acceptance): stats, positions and poses by
second as above, and lineage and absorptive rows as sequences over S < t <= the fresh run's last
sample. absorptive.jsonl is compared only under --after, as the spec's acceptance did.
"""
import json
import os
import sys

args = sys.argv[1:]
after = None
if '--after' in args:
    i = args.index('--after')
    after = float(args[i + 1])
    del args[i:i + 2]
fresh, rec = args[0], args[1]


def rows(p, name):
    f = os.path.join(p, name)
    if not os.path.exists(f):
        return None
    return [r for r in open(f, 'rb').read().split(b'\n') if r.strip()]


def t_of(r):
    d = json.loads(r)
    return d.get('birth', d.get('t', 0))


def later(rs):
    return rs if after is None else [r for r in rs if t_of(r) > after]


def strip(r):
    d = json.loads(r)
    for k in list(d):
        if k.startswith('wall') or k in ('harnessBodySteps', 'fluidLinkSteps'):
            d.pop(k)
    return json.dumps(d, sort_keys=True)


ok = True
fs = later(rows(fresh, 'stats.jsonl'))
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
    f = later(f)
    rm = {json.loads(x)['t']: x for x in r}
    bad = [json.loads(x)['t'] for x in f if rm.get(json.loads(x)['t']) != x]
    print('%s: %d rows, differing: %s' % (name, len(f), bad[:5] if bad else 'none'))
    ok &= not bad
for name in ('lineage.jsonl',) + (('absorptive.jsonl',) if after is not None else ()):
    f = rows(fresh, name)
    r = rows(rec, name)
    if f is None or r is None:
        print('%s: absent in %s' % (name, 'fresh' if f is None else 'recorded'))
        ok = False
        continue
    fl = f if after is None else [x for x in later(f) if t_of(x) <= t_end]
    rl = [x for x in later(r) if t_of(x) <= t_end]
    same = fl == rl
    print('%s: fresh %d rows, recorded %d rows in the span, equal: %s' % (name, len(fl), len(rl), same))
    ok &= same
print('IDENTICAL' if ok else 'DIFFERENT')
sys.exit(0 if ok else 1)
