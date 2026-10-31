# Who kills grazers: for each grazer killed by a grazer, the victim's age, whether the killer is its parent,
# its child or a sibling, and the victim's reserve-share at birth. Reads lineage.jsonl files in order.
import json, sys, collections
born = {}      # id -> (t, parent, grazer, rm)
grazer = lambda r: r.get('atk', 0) > 0 and r.get('ink', 0) > 0
kills = []     # (t, victim, killer)
for p in sys.argv[1:]:
    with open(p, encoding='utf-8') as f:
        for l in f:
            if '"e":"b"' in l[:12] or '"e":"k"' in l[:12]:
                r = json.loads(l)
                if r['e'] == 'b':
                    born[r['id']] = (r['t'], r['p'], grazer(r), r.get('k'))
                else:
                    kills.append((r['t'], r['id'], r['by'], r.get('root'), r.get('tj', 0), r.get('rj', 0)))
ng = sum(1 for v in born.values() if v[2])
print('bodies born', len(born), 'grazers born', ng, 'kill rows', len(kills))
by = collections.Counter()
ages = collections.Counter()
gk_total = 0; g_kills_any = 0
victims_by_grazer = collections.Counter()
for t, v, k, root, tj, rj in kills:
    bv = born.get(v); bk = born.get(k)
    if bk and bk[2]:
        g_kills_any += 1
        victims_by_grazer['grazer' if bv and bv[2] else 'other'] += 1
    if not (bv and bv[2] and bk and bk[2]):
        continue
    gk_total += 1
    rel = 'unrelated'
    if bv[1] == k: rel = 'killer is parent'
    elif bk[1] == v: rel = 'killer is child'
    elif bv[1] == bk[1] and bv[1] >= 0: rel = 'sibling'
    by[rel] += 1
    a = t - bv[0]
    b = '<10s' if a < 10 else '<30s' if a < 30 else '<60s' if a < 60 else '<120s' if a < 120 else '<300s' if a < 300 else '>=300s'
    ages[b] += 1
print('kills by grazers', g_kills_any, dict(victims_by_grazer))
print('grazer-on-grazer kills', gk_total)
for k, v in by.most_common(): print(f'  {k:18s} {v:7d} {v/max(gk_total,1):.3f}')
for k in ['<10s', '<30s', '<60s', '<120s', '<300s', '>=300s']: print(f'  victim age {k:7s} {ages[k]:7d} {ages[k]/max(gk_total,1):.3f}')
