# The books of each world PREFIX-6202 and PREFIX-6203 under scratch/plants-work/runs: the largest absolute value of
# every stats column whose name ends in "Residual", and the sample where it falls. The matter line is 1e-5
# (logbook/specs/plants-hold/forest-run.md). Usage: python -I books.py PREFIX [PREFIX ...], from the repository root.
import json, os, sys
ROOT = 'scratch/plants-work/runs'
for prefix in sys.argv[1:]:
    for seed in ('6202', '6203'):
        arm = os.path.join(ROOT, '%s-%s' % (prefix, seed))
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        most = {}
        for l in open(d + '/stats.jsonl', encoding='utf-8'):
            if not l.strip():
                continue
            r = json.loads(l)
            for k, v in r.items():
                if k.endswith('Residual') and isinstance(v, (int, float)) and abs(v) >= most.get(k, (0, None))[0]:
                    most[k] = (abs(v), r['t'])
        print('%s-%s: %s' % (prefix, seed, ', '.join('%s %.2g at %s s' % (k, v, t) for k, (v, t) in sorted(most.items()))))
