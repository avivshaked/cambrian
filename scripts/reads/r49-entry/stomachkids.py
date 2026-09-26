# S2's stomach children (birth rows with abs 1 and pho 0, not founders): age at death by what the
# parent was (a pure stomach, a mixotroph or a leaf), by generation band, and by birth fraction,
# with the living at the last row. Reads lineage.jsonl alone. Usage: stomachkids.py <arm>
import glob, json, statistics, sys
from collections import defaultdict

arm = sys.argv[1]
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
birth, died = {}, {}
last = 0.0
with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        last = max(last, r['t'])
        if r['e'] == 'b':
            birth[r['id']] = r
        elif r['e'] == 'd':
            died[r['id']] = r['t']

def kind(r):
    if r is None: return 'none'
    if r['abs'] and not r['pho']: return 'stomach'
    if r['abs'] and r['pho']: return 'mixotroph'
    if r['pho']: return 'leaf'
    return 'other'

kids = [r for r in birth.values() if r['k'] != 'f' and r['abs'] == 1 and r['pho'] == 0]
dead = [r for r in kids if r['id'] in died]
print(f'{arm}: {len(kids)} stomach children born, {len(dead)} dead, {len(kids) - len(dead)} alive at {last:.0f} s')
if dead:
    ages = [died[r['id']] - r['t'] for r in dead]
    print(f'  all: median age at death {statistics.median(ages):.0f} s')

def table(label, key):
    groups = defaultdict(list)
    for r in dead:
        groups[key(r)].append(died[r['id']] - r['t'])
    for k in sorted(groups, key=lambda k: -len(groups[k])):
        a = groups[k]
        print(f'  {label} {k}: {len(a)} dead, median {statistics.median(a):.0f} s, '
              f'quartiles {statistics.quantiles(a, n=4)[0]:.0f} to {statistics.quantiles(a, n=4)[2]:.0f} s' if len(a) > 3
              else f'  {label} {k}: {len(a)} dead, median {statistics.median(a):.0f} s')

table('parent', lambda r: kind(birth.get(r['p'])))
table('parent founder', lambda r: 'yes' if birth.get(r['p'], {}).get('k') == 'f' else 'no')
table('generation', lambda r: '1' if r['g'] <= 1 else ('2-4' if r['g'] <= 4 else '5+'))
table('birth fraction', lambda r: '<0.1' if r['bf'] < 0.1 else ('0.1-0.3' if r['bf'] < 0.3 else '>=0.3'))
table('gestation', lambda r: 'gm1' if r.get('gm') else 'lump')
