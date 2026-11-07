# Whether the rest carried the gripping plants it reached (logbook/specs/plants-hold/forest-run.md, "B, rest"), for each
# world PREFIX-6202 and PREFIX-6203 under scratch/plants-work/runs. For each winter: the bodies seen gripping
# (positions flag 16 without 8) at any 100 s sample in it, split by whether any sample in it showed them resting
# (flag 32), and of each part how many were alive at the winter's end and how many died in it, by cause. A rested body
# that survives where an unrested one dies says the rest works where it reaches; few rested says its reach is short.
# Usage: python -I rest-fate.py PREFIX [PREFIX ...], from the repository root.
import collections, gzip, json, os, sys
ROOT = 'scratch/plants-work/runs'
WINTERS = [(2750, 5750, 'winter 1'), (8750, 11750, 'winter 2')]
for prefix in sys.argv[1:]:
    for seed in ('6202', '6203'):
        arm = os.path.join(ROOT, '%s-%s' % (prefix, seed))
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        died, cause = {}, {}
        for l in open(d + '/lineage.jsonl', encoding='utf-8'):
            r = json.loads(l)
            if r.get('e') == 'd': died[r['id']] = r['t']; cause[r['id']] = r.get('c')
        grip = {w[2]: set() for w in WINTERS}
        rest = {w[2]: set() for w in WINTERS}
        for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
            if not l.startswith('{"t":'): continue
            t = float(l[5:l.index(',')])
            if abs(t / 100 - round(t / 100)) > 1e-6: continue
            w = next((n for lo, hi, n in WINTERS if lo <= t < hi), None)
            if w is None: continue
            for b in json.loads(l)['b']:
                if (b[4] & 16) and not (b[4] & 8):
                    grip[w].add(b[0])
                    if b[4] & 32: rest[w].add(b[0])
        print('%s-%s' % (prefix, seed))
        for lo, hi, w in WINTERS:
            for label, ids in (('rested', grip[w] & rest[w]), ('never rested', grip[w] - rest[w])):
                dead = [i for i in ids if lo <= died.get(i, 1e9) < hi]
                alive = [i for i in ids if died.get(i, 1e9) >= hi]
                print('  %s, %-12s %3d: alive at its end %3d, died in it %3d %s' % (
                    w, label, len(ids), len(alive), len(dead), dict(collections.Counter(cause[i] for i in dead))))
