"""The whole-window ate contact table under the onset - 1 measure (c8, 2026-11-01): one row per onset (eater, part, second) from a
film log's `[Theatre] ate contact` lines. A kill reads its onset - 1 line; a cut reads touch where either of its lines touches; a
wound reads its onset-frame line. Lines written within the edge ("not its nearest frame") count only where nothing nearer exists.
A mouth touch is a touch with gap/radius at most 1. Prints the counts by match and kind, and the eaters with several leaf bites.

    python -I -B ate_table2.py <film log> [--out table.tsv]
"""
import re, sys
from collections import defaultdict

log = sys.argv[1]
out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else None
head = re.compile(r'ate contact: t ([\d.]+) eater (\d+) part (\d+) prey (\S+) part (\S+) (.*)$')
rows = defaultdict(lambda: {'prev': None, 'onset': None, 'edge': None})

for line in open(log, encoding='utf-8', errors='replace'):
    m = head.search(line)
    if not m: continue
    t, eater, part, prey, ppart, rest = m.groups()
    key = (float(t), int(eater), int(part))
    match = 'kill' if '(kill) victim at onset - 1' in rest else 'cut' if '(cut) victim at onset - 1' in rest else None
    absent = ' absent: ' in rest
    gr = re.search(r'gap/radius (-?[\d.]+)', rest)
    kind = re.search(r'\((leaf|animal|corpse|remains)[;)]', rest)
    if match:
        v = re.search(r": (touch within a frame's motion|touch|far) frame \d+ to \d+", rest)
        rows[key]['match'] = match
        rows[key]['prev'] = {'absent': absent, 'verdict': v.group(1) if v else None, 'gr': float(gr.group(1)) if gr else None,
                             'kind': kind.group(1) if kind else None, 'prey': prey}
        continue
    v = re.search(r': (touch|far) frame \d+ \(window sec', rest)
    rec = {'absent': absent, 'verdict': v.group(1) if v else None, 'gr': float(gr.group(1)) if gr else None,
           'kind': kind.group(1) if kind else None, 'prey': prey, 'only': '[onset frame only]' in rest}
    if 'not its nearest frame' in rest:
        rows[key]['edge'] = rec
    else:
        rows[key]['onset'] = rec

def resolve(key, r):
    match = r.get('match', 'wound')
    p, o = r['prev'], r['onset'] or r['edge']
    if match == 'kill' and p and not p['absent'] and p['verdict']:
        src = p
    elif match == 'cut':
        cands = [x for x in (p, o) if x and not x['absent'] and x['verdict']]
        touching = [x for x in cands if x['verdict'].startswith('touch')]
        src = (touching or cands or [None])[0]
    else:
        src = o if o and not o['absent'] and o['verdict'] else None
    if src is None:
        return match, 'absent', None, None, None
    return match, src['verdict'], src['kind'], src['gr'], src['prey']

table = []
for key in sorted(rows):
    match, verdict, kind, gr, prey = resolve(key, rows[key])
    touch = verdict is not None and verdict.startswith('touch')
    mouth = touch and gr is not None and gr <= 1.0
    table.append((key, match, verdict, kind, gr, prey, touch, mouth))

counts = defaultdict(int)
for key, match, verdict, kind, gr, prey, touch, mouth in table:
    counts[(match, kind, 'absent' if verdict == 'absent' else 'touch' if touch else 'far')] += 1
    if verdict == "touch within a frame's motion": counts[('within',)] += 1
    if mouth: counts[('mouth', match, kind)] += 1
print(f'onsets {len(table)}')
for k in sorted(counts, key=str): print(' ', k, counts[k])

per = defaultdict(list)
for key, match, verdict, kind, gr, prey, touch, mouth in table:
    if kind == 'leaf': per[key[1]].append((key[0], key[2], match, verdict, gr, prey, mouth))
print('eaters with at least three leaf onsets (second, part, match, verdict, gap/radius, prey, mouth touch):')
for eater, bites in sorted(per.items(), key=lambda kv: -len(kv[1])):
    if len(bites) < 3: continue
    n = sum(1 for b in bites if b[6])
    print(f'  {eater}: {len(bites)} leaf onsets, {n} mouth touches')
    for b in bites: print('    ', b)

if out:
    with open(out, 'w', encoding='utf-8') as f:
        f.write('t\teater\tpart\tmatch\tverdict\tkind\tgap_radius\tprey\tmouth_touch\n')
        for key, match, verdict, kind, gr, prey, touch, mouth in table:
            f.write(f'{key[0]}\t{key[1]}\t{key[2]}\t{match}\t{verdict}\t{kind}\t{gr}\t{prey}\t{mouth}\n')
