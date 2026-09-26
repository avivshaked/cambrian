# One line's feeding over time: its members' absorptive.jsonl rows in 1,000 s windows (rows, median
# snow density at the mouth, food and net watts, depth), beside the line's living count at the
# window's end. Usage: eaterfeed.py <arm> <root id> [window s, default 1000]
import glob, json, statistics, sys
from collections import defaultdict

arm, rootid = sys.argv[1], int(sys.argv[2])
win = float(sys.argv[3]) if len(sys.argv) > 3 else 1000.0
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
birth, died = {}, {}
with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r['e'] == 'b':
            birth[r['id']] = r
        elif r['e'] == 'd':
            died[r['id']] = r['t']

members = set()
for i in birth:
    j = i
    while j != rootid and birth[j]['p'] >= 0:
        j = birth[j]['p']
    if j == rootid:
        members.add(i)
print(f'{arm}: line of {rootid}, {len(members)} members')

rows = defaultdict(list)
with open(f'{run}/absorptive.jsonl', encoding='utf-8') as f:
    for line in f:
        if f'"id":' not in line:
            continue
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r['id'] in members:
            rows[int(r['t'] // win)].append(r)

def med(xs):
    return statistics.median(xs) if xs else float('nan')

print('window  rows  living  density  foodW    lightW   netW     depth')
for k in sorted(rows):
    rs = rows[k]
    end = (k + 1) * win
    living = sum(1 for i in members if birth[i]['t'] <= end and died.get(i, float('inf')) > end)
    print(f'{k * win:6.0f} {len(rs):5d} {living:6d}  {med([r["densityHere"] for r in rs]):7.3f}  '
          f'{med([r["foodW"] for r in rs]):7.4f}  {med([r["lightW"] for r in rs]):7.4f}  '
          f'{med([r["netW"] for r in rs]):7.4f}  {med([r["y"] for r in rs]):6.1f}')
