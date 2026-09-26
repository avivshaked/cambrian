# Leaf founders (matter eaters) under the depth rule: first recorded height, age at death, bred.
# Usage: leafdepth.py <arm> [runs root, default runs]
import glob, json, sys, statistics as st
sys.path.insert(0, 'scripts/reads')
import runrec
arm = sys.argv[1]
root = sys.argv[2] if len(sys.argv) > 2 else 'runs'
run = sorted(glob.glob(f'{root}/{arm}/2026*'))[-1]
born, died, parents = {}, {}, set()
for line in open(run + '/lineage.jsonl', encoding='utf-8'):
    try: r = json.loads(line)
    except ValueError: continue
    if r.get('e') == 'b':
        if r.get('p', -1) >= 0: parents.add(r['p'])
        if r.get('k') == 'f' and r.get('pho') == 1 and r.get('abs') == 0 and 'fmat' in r:
            born[r['id']] = r
    elif r.get('e') == 'd' and r.get('id') in born:
        died[r['id']] = r
first = {}
for row in runrec.positions(run):
    for b in row['b']:
        if b[0] in born and b[0] not in first: first[b[0]] = (row['t'], b[2])
for src in ('floor', 'trickle'):
    ids = [i for i, r in born.items() if r.get('src') == src]
    ys = [first[i][1] for i in ids if i in first and first[i][0] - born[i]['t'] <= 10]
    ages = [died[i]['t'] - born[i]['t'] for i in ids if i in died]
    bred = sum(1 for i in ids if i in parents)
    if not ids: continue
    deep = sum(1 for y in ys if y < -20)
    print(f'{arm} {src:7s} leaf founders {len(ids):4d}: first y median {st.median(ys):6.1f} m, below -20 m {deep}/{len(ys)}; '
          f'median age at death {st.median(ages):6.1f} s ({len(ages)} dead); bred {bred}')
