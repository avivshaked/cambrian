# Round 48 story v2: the few readings facts.py did not take, for the charts.
# Read-only over runs/; writes extras.json beside this file. Single-threaded.
import json, os
from runlib import Run, rows

HERE = os.path.dirname(os.path.abspath(__file__))
X = {}

s1 = Run('r48-s1', ['t', 'alive', 'bodiesEaten', 'corpsesEaten', 'photosynthetic', 'absorptive'])

# Body 201's whole recorded life: its reserve (absorptive.jsonl 'energy') and depth, every row.
life = s1.absrows(wanted_ids={201}, upto=3300)[201]
X['life_201'] = [[d['t'], round(d['energy'], 2), round(d['tissue'], 3), round(d['y'], 2),
                  round(d['lightW'], 4), round(d['upkeepW'], 4), d['children']] for d in life]
X['life_201_fields'] = ['t', 'energy_J', 'tissue_J', 'y_m', 'lightW', 'upkeepW', 'children']

# The line of 48048 (guide rule: same flags as the parent), alive every 250 s from 23,000 s.
times = list(range(23000, 30001, 250))
ser = s1.series(lambda i: s1.croot.get(i), [48048], times)[48048]
X['line_48048'] = [[t, n] for t, n in zip(times, ser)]

# Nothing eaten: the end rows of every run.
X['eaten_end'] = {}
for arm in ('r48-s1', 'r48-s2', 'r48-s3'):
    r = s1 if arm == 'r48-s1' else None
    path = os.path.join(Run.__init__.__globals__['ROOT'], Run.__init__.__globals__['RUNS'][arm][0], 'stats.jsonl')
    last = None
    for d in rows(path, Run.__init__.__globals__['RUNS'][arm][1]):
        last = d
    X['eaten_end'][arm] = {'t': last['t'], 'bodiesEaten': last.get('bodiesEaten'), 'corpsesEaten': last.get('corpsesEaten')}

json.dump(X, open(os.path.join(HERE, 'extras.json'), 'w'), indent=1)
print('life rows', len(X['life_201']), X['life_201'][0], X['life_201'][-1])
print('line 48048', X['line_48048'])
print('eaten', X['eaten_end'])
