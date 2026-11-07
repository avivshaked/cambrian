# The ages at death of gripping plants by season (logbook/specs/plants-hold/forest-run.md, "B, rest"), for each world
# PREFIX-6202 and PREFIX-6203 under scratch/plants-work/runs: every body seen gripping (positions flag 16 without 8) at
# any 100 s sample, and of those that died, in each season of death, the count, the median age and the causes. A
# resting plant ages at the rest factor, so the rest should lengthen these against each arm's control.
# Seasons follow D151's two-season shape. Usage: python -I grip-age.py PREFIX [PREFIX ...], from the repository root.
import collections, gzip, json, os, statistics, sys
ROOT = 'scratch/plants-work/runs'
SPANS = [(0, 2750, 'summer 1'), (2750, 5750, 'winter 1'), (5750, 8750, 'summer 2'), (8750, 11750, 'winter 2'),
         (11750, 12000.5, 'summer 3')]
for prefix in sys.argv[1:]:
    for seed in ('6202', '6203'):
        arm = os.path.join(ROOT, '%s-%s' % (prefix, seed))
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        born, died, cause = {}, {}, {}
        for l in open(d + '/lineage.jsonl', encoding='utf-8'):
            r = json.loads(l)
            if r.get('e') == 'b': born[r['id']] = r['t']
            elif r.get('e') == 'd': died[r['id']] = r['t']; cause[r['id']] = r.get('c')
        gripped = set()
        for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
            if not l.startswith('{"t":'): continue
            t = float(l[5:l.index(',')])
            if abs(t / 100 - round(t / 100)) > 1e-6: continue
            gripped.update(b[0] for b in json.loads(l)['b'] if (b[4] & 16) and not (b[4] & 8))
        print('%s-%s: %d bodies seen gripping' % (prefix, seed, len(gripped)))
        for lo, hi, name in SPANS:
            dead = [i for i in gripped if lo <= died.get(i, 1e9) < hi and i in born]
            ages = [died[i] - born[i] for i in dead]
            print('  %-8s died %3d, median age %s s, %s' % (
                name, len(dead), '%.0f' % statistics.median(ages) if ages else '-',
                dict(collections.Counter(cause[i] for i in dead))))
