# Where one line's stomachs stand at a second: the living bodies with a stomach (the positions
# row's absorptive flag), how many of them are the line rooted at a named founder, the extent of
# the line's members in x and z, their median depth, their centroid's distance from the tank's
# axis (the disc of worldAreaSquareMetres, centred at (R, R) as positions-read.py has it) and its
# ring of four equal areas, and how far each stomach outside the line is from its nearest stomach.
# The theatre section of 0122 read its clumps from this. Run from the main tree.
# usage: clump.py <arm> <second> <root id>
import glob, json, math, statistics, sys
sys.path.insert(0, 'scripts/reads')
import runrec
arm, second, root = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
ABSORPTIVE = 1
with open(f'{run}/config.json', encoding='utf-8') as f:
    R = math.sqrt(json.load(f)['world']['worldAreaSquareMetres'] / math.pi)
parent = {}
with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r['e'] == 'b':
            parent[r['id']] = r['p']

def rooted_at(i):
    while i >= 0:
        if i == root: return True
        i = parent.get(i, -1)
    return False

head = '{"t":%d,' % second
row = None
for line in runrec.positions_lines(run):
    if line.startswith(head):
        row = json.loads(line); break
if row is None:
    sys.exit(f'{arm}: no positions row at {second} s')
bodies = row['b']
stomachs = [b for b in bodies if b[4] & ABSORPTIVE]
line_ = [b for b in stomachs if rooted_at(b[0])]
others = [b for b in stomachs if not rooted_at(b[0])]
print(f'{arm} at {second} s: {len(bodies)} living, {len(stomachs)} with a stomach, {len(line_)} of them the line of {root}')
if line_:
    xs = [b[1] for b in line_]; ys = [b[2] for b in line_]; zs = [b[3] for b in line_]
    cx, cz = statistics.mean(xs), statistics.mean(zs)
    off = math.hypot(cx - R, cz - R)
    ring = min(3, int(off * off / (R * R) * 4))
    bearing = math.degrees(math.atan2(cz - R, cx - R)) % 360
    print(f'  the line: x {min(xs):.1f} to {max(xs):.1f} ({max(xs) - min(xs):.1f} m), '
          f'z {min(zs):.1f} to {max(zs):.1f} ({max(zs) - min(zs):.1f} m), median height {statistics.median(ys):.2f} m')
    print(f'  its centroid ({cx:.1f}, {cz:.1f}): {off:.1f} m from the axis of a {R:.2f} m tank, ring {ring} of 0 to 3 (equal areas), bearing {bearing:.0f} deg from +x toward +z')
if others:
    near = []
    for b in others:
        d = min((math.dist((b[1], b[2], b[3]), (o[1], o[2], o[3])) for o in stomachs if o is not b), default=float('nan'))
        near.append(d)
    near.sort()
    print(f'  the {len(others)} stomachs outside the line: nearest other stomach at a median {statistics.median(near):.1f} m, '
          f'min {near[0]:.1f} m; within 2 m of another: {sum(1 for d in near if d <= 2.0)}')
