# The lines rooted in a pool founder, and then every line of any source with ten or more pure
# stomachs (abs 1, pho 0): each line's births, its pure stomachs, its generations, its peak
# living count and when, and when its last member died. Reads lineage.jsonl alone.
# Usage: eaterlines.py <arm> [min births for a pool line, default 5]
import glob, json, sys
from collections import defaultdict

arm = sys.argv[1]
least = int(sys.argv[2]) if len(sys.argv) > 2 else 5
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

root = {}
def root_of(i):
    chain = []
    while i not in root and birth[i]['p'] >= 0:
        chain.append(i)
        i = birth[i]['p']
    r = root.get(i, i)
    for c in chain + [i]:
        root[c] = r
    return r

lines = defaultdict(list)
for i in birth:
    lines[root_of(i)].append(i)

def pure(i):
    return birth[i]['abs'] == 1 and birth[i]['pho'] == 0

def describe(r, members):
    events = []
    for i in members:
        events.append((birth[i]['t'], 1))
        if i in died: events.append((died[i], -1))
    events.sort()
    n = peak = 0; peak_t = 0.0
    for t, d in events:
        n += d
        if n > peak: peak, peak_t = n, t
    end = max(died.get(i, float('inf')) for i in members)
    stomachs = sum(1 for i in members if pure(i))
    gen = max(birth[i]['g'] for i in members) - birth[r]['g']
    print(f'  line of {r} ({birth[r].get("src")}, pool {birth[r].get("pool")}, founder abs {birth[r]["abs"]} pho {birth[r]["pho"]} jnt {birth[r]["jnt"]}, landed {birth[r]["t"]:.1f} s): {len(members)} born, '
          f'{stomachs} pure stomachs, {gen} generations, peak {peak} living at {peak_t:.0f} s, '
          + ('alive at the end' if end == float('inf') else f'gone at {end:.0f} s'))

pool_lines = [(r, m) for r, m in lines.items() if birth[r].get('src') == 'pool' and len(m) >= least]
print(f'{arm}: {sum(1 for r in lines if birth[r].get("src") == "pool")} pool founders, '
      f'{len(pool_lines)} of their lines with {least} or more members')
for r, members in sorted(pool_lines, key=lambda kv: -len(kv[1])):
    describe(r, members)

eaters = [(r, m) for r, m in lines.items() if sum(1 for i in m if pure(i)) >= 10]
print(f'{arm}: {len(eaters)} lines of any source with ten or more pure stomachs')
for r, members in sorted(eaters, key=lambda kv: -sum(1 for i in kv[1] if pure(i))):
    describe(r, members)
