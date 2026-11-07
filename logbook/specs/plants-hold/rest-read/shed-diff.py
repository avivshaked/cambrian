# Whether the shedding switch reached the world (logbook/specs/plants-hold/forest-run.md, "B, rest"): for each seed, the
# first stats sample at which plr (sheds) and plrk (keeps) differ, the time of the first rest in each, and the stats
# columns that name rest, shed or blade. Usage: python -I shed-diff.py, from the repository root.
import json, os
ROOT = 'scratch/plants-work/runs'
def rows(prefix, seed):
    arm = os.path.join(ROOT, '%s-%s' % (prefix, seed))
    d = os.path.join(arm, sorted(os.listdir(arm))[-1])
    # wall-clock columns differ between any two runs and are left out
    return [{k: v for k, v in json.loads(l).items() if not k.startswith('wall')}
            for l in open(d + '/stats.jsonl', encoding='utf-8') if l.strip()]
for seed in ('6202', '6203'):
    a, b = rows('plr', seed), rows('plrk', seed)
    keys = sorted(k for k in a[0] if any(w in k.lower() for w in ('rest', 'shed', 'blade', 'wake')))
    first = next((i for i, (x, y) in enumerate(zip(a, b)) if x != y), None)
    rest_a = next((r['t'] for r in a if r.get('restsBegun', 0) > 0), None)
    rest_b = next((r['t'] for r in b if r.get('restsBegun', 0) > 0), None)
    print('seed %s: %d and %d rows; columns %s' % (seed, len(a), len(b), keys))
    print('  first rest: plr %s s, plrk %s s' % (rest_a, rest_b))
    if first is None:
        print('  no sample differs')
    else:
        diff = sorted(k for k in a[first] if a[first].get(k) != b[first].get(k))
        print('  first differing sample at %s s; columns differing there: %s' % (a[first]['t'], diff[:12]))
