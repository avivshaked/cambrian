# How much of a stomach's income is its link's light: over the feeding log's rows from bodies
# whose birth row carries a stomach and no leaf (abs 1, pho 0), where every watt of light comes
# from a link (a link catches light at the launcher's EVOSIM_LINK_PHOTO share of a leaf's rate,
# D043 and D046), by window, and then for every line with ten or more such bodies (round 49's
# E2 lines). The instrument HANDOFF's first decision asks for, as a read and not a build: a body
# with a leaf mixes the two lights and is left out. Run from the main tree.
# usage: linklight.py <arm> [window s, default 5000] [runs root, default runs]
import glob, json, sys
from collections import defaultdict

arm = sys.argv[1]
win = float(sys.argv[2]) if len(sys.argv) > 2 else 5000.0
root_dir = sys.argv[3] if len(sys.argv) > 3 else 'runs'
run = sorted(glob.glob(f'{root_dir}/{arm}/2026*'))[-1]

birth = {}
with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r.get('e') == 'b':
            birth[r['id']] = (r['p'], r.get('abs', 0), r.get('pho', 0))

def stomach_only(i):
    b = birth.get(i)
    return b is not None and b[1] == 1 and b[2] == 0

root = {}
def root_of(i):
    chain = []
    while i not in root and birth[i][0] >= 0 and birth[i][0] in birth:
        chain.append(i)
        i = birth[i][0]
    r = root.get(i, i)
    for c in chain + [i]:
        root[c] = r
    return r

pure_by_line = defaultdict(int)
for i in birth:
    if stomach_only(i):
        pure_by_line[root_of(i)] += 1
lines = {r for r, n in pure_by_line.items() if n >= 10}

def tally():
    return {'rows': 0, 'lit': 0, 'food': 0.0, 'light': 0.0}

by_win = defaultdict(tally)
by_line = defaultdict(tally)
with open(f'{run}/absorptive.jsonl', encoding='utf-8') as f:
    for line in f:
        if '"id":' not in line:
            continue
        try:
            r = json.loads(line)
        except ValueError:
            continue
        i = r.get('id')
        if not stomach_only(i):
            continue
        food, light = max(0.0, r.get('foodW', 0.0)), max(0.0, r.get('lightW', 0.0))
        for t in (by_win[int(r['t'] // win)], by_line[root_of(i)] if root_of(i) in lines else None):
            if t is None:
                continue
            t['rows'] += 1
            t['lit'] += light > 0
            t['food'] += food
            t['light'] += light

def show(label, t):
    inc = t['food'] + t['light']
    lit = 100.0 * t['lit'] / t['rows'] if t['rows'] else float('nan')
    share = 100.0 * t['light'] / inc if inc > 0 else float('nan')
    print(f'  {label:<28} rows {t["rows"]:7d}  earning light {lit:5.1f}%  light over income {share:5.1f}%')

print(f'{arm}: rows from a body with a stomach and no leaf, the light all a link\'s')
for k in sorted(by_win):
    show(f'{k * win:.0f} to {(k + 1) * win:.0f} s', by_win[k])
print(f'{arm}: lines with ten or more such bodies ({len(lines)})')
for r in sorted(lines, key=lambda r: -pure_by_line[r]):
    show(f'line of {r} ({pure_by_line[r]} bodies)', by_line[r])
