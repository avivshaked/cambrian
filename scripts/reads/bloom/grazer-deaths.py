# Grazer deaths by cause, and grazer kill rows split into root (lethal) and part, per run span.
# A grazer is a body born with atk > 0 and ink > 0, g2-read.py's reading. Reads lineage.jsonl files in order.
import json, sys, collections
g = set(); born = {}
deaths = collections.Counter(); ages = collections.defaultdict(list)
kills = collections.Counter()
for p in sys.argv[1:]:
    with open(p, encoding='utf-8') as f:
        for l in f:
            e = l[6:8]
            if l.startswith('{"e":"b"'):
                r = json.loads(l)
                born[r['id']] = r['t']
                if r.get('atk', 0) > 0 and r.get('ink', 0) > 0: g.add(r['id'])
            elif l.startswith('{"e":"d"') or l.startswith('{"e":"k"'):
                r = json.loads(l)
                if r['id'] not in g: continue
                if r['e'] == 'd':
                    deaths[r['c']] += 1; ages[r['c']].append(r['t'] - born.get(r['id'], r['t']))
                else:
                    kills[('by grazer' if r['by'] in g else 'by other', 'root' if r.get('root') else 'part')] += 1
n = sum(deaths.values())
print('grazers born', len(g), 'grazer deaths', n)
for k, v in deaths.most_common():
    a = sorted(ages[k]); print(f'  {k:8s} {v:7d} {v/n:.3f}  median age {a[len(a)//2]:.0f} s')
print('kill rows with a grazer victim:', dict(kills))
