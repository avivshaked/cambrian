# What mouths took from remains against living leaves, and what closed uneaten, per world (for propose-forest-pays.md).
import json, sys, glob, os
for arm in sys.argv[1:]:
    runs = sorted(glob.glob(os.path.join(arm, '*', 'stats.jsonl')))
    if not runs: print(arm, 'no stats'); continue
    f = runs[-1]; first = None; last = None; mono = True; prev = None
    for line in open(f):
        r = json.loads(line)
        if first is None: first = r
        v = r.get('biteKeptRemains')
        if prev is not None and v is not None and v < prev - 1e-6: mono = False
        prev = v; last = r
    keys = ('t', 'biteKeptLeaves', 'biteKeptRemains', 'biteKeptOthers', 'remainsClosedJoules', 'remainsClosed', 'moduleDrops', 'deaths', 'bodiesEaten', 'burntTotal')
    print(os.path.basename(arm.rstrip('/')), {k: (round(last.get(k), 1) if isinstance(last.get(k), float) else last.get(k)) for k in keys}, 'cumulative' if mono else 'NOT monotonic')
