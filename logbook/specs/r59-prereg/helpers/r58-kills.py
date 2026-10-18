"""Baseline for round 59's draft: kills on leaves and mouthed bodies in round 58's isolated worlds, per year.

Usage: python -I r58-kills.py <first run dir> <resumed run dir> [<year s>]

Reads birth rows from both lineages (the first to 1,500 s, the resume after), kill rows ("e":"k") and the
stats rows' mouth counters at each year's end from the resumed run. A leaf is pho 1, jnt 0, abs 0 (round 58's
reader's rule). A mouthed body is atk 1 and ink 1. Writes nothing.
"""
import json
import os
import sys


def rows(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


first, resumed = sys.argv[1], sys.argv[2]
year = float(sys.argv[3]) if len(sys.argv) > 3 else 6000.0
born = {}
kills = []
for d, cut in ((first, 1500.0), (resumed, None)):
    for r in rows(os.path.join(d, 'lineage.jsonl')):
        if cut is not None and r.get('t', 0) > cut:
            continue
        if r.get('e') == 'b':
            born[r['id']] = r
        elif r.get('e') == 'k' and cut is None:
            kills.append(r)


def leaf(b):
    return b is not None and b.get('pho') == 1 and b.get('jnt') == 0 and b.get('abs') == 0


def mouthed(b):
    return b is not None and b.get('atk') == 1 and b.get('ink') == 1


print(f'{resumed}: births {len(born)}, kill rows after 1500 s {len(kills)}')
print('year  kills  on leaves  leaf roots  by mouthed  mouthed births  mouthed births w/o pho')
mb = {}
for b in born.values():
    y = int(b['t'] // year) + 1
    if mouthed(b):
        mb.setdefault(y, [0, 0])
        mb[y][0] += 1
        if b.get('pho') == 0:
            mb[y][1] += 1
per = {}
for k in kills:
    y = int(k['t'] // year) + 1
    v = born.get(k['id'])
    a = born.get(k.get('by'))
    p = per.setdefault(y, [0, 0, 0, 0])
    p[0] += 1
    if leaf(v):
        p[1] += 1
        if k.get('root') == 1:
            p[2] += 1
    if mouthed(a):
        p[3] += 1
for y in sorted(set(per) | set(mb)):
    p = per.get(y, [0, 0, 0, 0])
    m = mb.get(y, [0, 0])
    print(f'{y:4d}  {p[0]:5d}  {p[1]:9d}  {p[2]:10d}  {p[3]:10d}  {m[0]:14d}  {m[1]:22d}')

keys = ['alive', 'partsKilled', 'bodiesEaten', 'corpsesFromKills', 'unitsEaten', 'corpsesEaten']
marks = [year * i for i in range(1, 9)]
got = {}
for r in rows(os.path.join(resumed, 'stats.jsonl')):
    t = r.get('t') or r.get('time') or r.get('elapsedSeconds')
    if t is None:
        continue
    for m in marks:
        if m not in got and t >= m:
            got[m] = {k: r.get(k) for k in keys}
print('stats at each year end (running totals but alive):')
for m in marks:
    print(f'  {m:7.0f}  {got.get(m)}')
