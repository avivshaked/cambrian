"""D134's measurements, read: the hit rate (m2-*) and the positive control (m3-*).

Usage: python scripts/nursery-read-d134.py [measure dir]  (default scratch/nursery/measure)
m2: every ancestor's 32 episodes (ancestors.jsonl): the pooled mean, standard deviation, share of
episodes over 5 J (a corpse or more beside the snow's few joules) and the per-body means.
m3: the grafted brain against its knockout, episode by episode (final-per.jsonl): the mean paired
difference and its standard error per body, and how many bodies clear twice the error.
"""
import json, math, os, sys

root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..', '..', 'scratch', 'nursery', 'measure')
root = os.path.abspath(root)

def stats(xs):
    n = len(xs); m = sum(xs) / n
    sd = math.sqrt(sum((x - m) ** 2 for x in xs) / (n - 1)) if n > 1 else 0.0
    return m, sd

for name in sorted(os.listdir(root)):
    d = os.path.join(root, name)
    if not os.path.isdir(d):
        continue
    if name.startswith('m2-'):
        rows = [json.loads(l) for l in open(os.path.join(d, 'ancestors.jsonl'))]
        allx = [x for r in rows for x in r['per']]
        m, sd = stats(allx)
        hits = sum(1 for x in allx if x > 5) / len(allx)
        per = ' '.join('%.1f' % (sum(r['per']) / len(r['per'])) for r in rows)
        print('%-22s episodes %4d  mean %6.2f J  sd %6.2f  over 5 J %4.0f%%  per body: %s' % (name, len(allx), m, sd, 100 * hits, per))
    elif name.startswith('m3-'):
        rows = [json.loads(l) for l in open(os.path.join(d, 'final-per.jsonl'))]
        clear = 0; parts = []
        for r in rows:
            if r['best'] is None or r['knockout'] is None:
                parts.append('-'); continue
            diffs = [a - b for a, b in zip(r['best'], r['knockout'])]
            m, sd = stats(diffs); se = sd / math.sqrt(len(diffs))
            if m > 0 and m >= 2 * se: clear += 1
            parts.append('%+.1f±%.1f' % (m, se))
        wired = [x for r in rows if r['best'] for x in r['best']]
        ko = [x for r in rows if r['knockout'] for x in r['knockout']]
        print('%-22s wired mean %6.2f J, knockout %6.2f J; bodies clearing 2 SE: %d of %d  per body: %s' % (
            name, stats(wired)[0], stats(ko)[0], clear, len(rows), ' '.join(parts)))