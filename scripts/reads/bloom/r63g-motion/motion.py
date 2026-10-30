"""Do grazers swim toward leaves, or drift? A read of r63g's arms against the control, 6,000-24,000 s.

Usage: python -I scripts/reads/bloom/r63g-motion/motion.py RUN LABEL [--from 6000] [--end 24000] [--newborn 80]

Definitions (lineage birth rows, as scripts/reads/bloom/g2-read.py and grazer-motion-read.py read them):
  grazer   born with atk > 0 and ink > 0.
  food     a living body whose positions flags carry photosynthetic (4), not remains (8), not a grazer.
  drifter  a living body whose positions flags are exactly 4 (photosynthetic, no joint, no absorptive) and which was
           born with atk 0 and ink 0: a leaf that cannot swim, the drift baseline.
Positions rows are 10 s apart (positions.jsonl.gz, record format 2, read through scripts/reads/runrec.py). Every row
from --from to --end is read; consecutive rows make a move. x and z wrap (Box, side sqrt(worldAreaSquareMetres)).

Per move of a grazer (present in both rows):
  ground speed      |displacement| / 10 s, horizontal and 3-D.
  drift-relative    displacement less the mean displacement of drifters within 10 m (horizontal) of it at the
                    start, at least 3 of them; the speed of that difference.
  toward food       u = unit vector (3-D) from the grazer to its nearest food body at the start. a = (dg - df) . u,
                    dg and df the grazer's and that food body's displacements (the food body must be in both rows).
                    Efficiency = sum(a) / sum(|dg - df|): +1 a beeline, 0 random. The null of random headings with
                    the same step lengths has mean 0 and, for a 3-D isotropic heading, variance |step|^2 / 3 per move.
The same is done for one drifter in 10 (by id), toward its nearest other food body: what pure drift gives.
Near: the share of grazer fixes whose nearest food body (root to root, 3-D) is within 2 m.

Per grazer born in [--from, --end - 200]: its first fix, the nearest food there, its first kill row as attacker
("e":"k", by = it; a part or a whole body taken), where it was at the row nearest that kill, its death row.

Reads files only; prints to stdout. Standard library only.
"""
import argparse
import json
import math
import os
import statistics
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(REPO, 'scripts', 'reads'))
import runrec  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('run')
ap.add_argument('label')
ap.add_argument('--from', dest='t0', type=float, default=6000.0)
ap.add_argument('--end', type=float, default=24000.0)
ap.add_argument('--newborn', type=float, default=80.0)
ap.add_argument('--near', type=float, default=2.0)
ap.add_argument('--drift-r', type=float, default=10.0)
a = ap.parse_args()

run = a.run
cfg = json.load(open(os.path.join(run, 'config.json'), encoding='utf-8'))


def find(node, name):
    if isinstance(node, dict):
        if name in node:
            return node[name]
        for v in node.values():
            r = find(v, name)
            if r is not None:
                return r
    return None


SIDE = math.sqrt(find(cfg, 'worldAreaSquareMetres'))
HALF = SIDE / 2
CELL = 4.0
NC = int(SIDE // CELL)
CELLW = SIDE / NC


def wrap(d):
    if d > HALF:
        d -= SIDE
    elif d < -HALF:
        d += SIDE
    return d


def q(xs, p):
    if not xs:
        return float('nan')
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


# ------------------------------------------------------------------ lineage
born = {}
grazer = set()
mouthless = set()
death = {}
first_kill = {}
kills_by = defaultdict(int)
leaf_born = set()
with open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        if line.startswith('{"e":"b"'):
            r = json.loads(line)
            if r['t'] > a.end:
                break
            born[r['id']] = r['t']
            if r.get('atk', 0) > 0 and r.get('ink', 0) > 0:
                grazer.add(r['id'])
            if r.get('atk', 0) == 0 and r.get('ink', 0) == 0:
                mouthless.add(r['id'])
        elif line.startswith('{"e":"k"'):
            r = json.loads(line)
            if r['t'] > a.end:
                break
            b = r['by']
            if b in grazer:
                kills_by[b] += 1
                if b not in first_kill:
                    first_kill[b] = (r['t'], r['id'], r.get('root', 0))
        elif line.startswith('{"e":"d"'):
            r = json.loads(line)
            if r['t'] > a.end:
                break
            death[r['id']] = (r['t'], r['c'])


# ------------------------------------------------------------------ grids
def grid_of(points):
    g = defaultdict(list)
    for p in points:
        g[(int(p[1] // CELLW) % NC, int(p[3] // CELLW) % NC)].append(p)
    return g


def nearest(g, x, y, z, skip=None, rmax=20):
    """Nearest point (3-D, wrapped) in grid g to (x, y, z), excluding id skip. (dist, point) or (inf, None)."""
    cx, cz = int(x // CELLW) % NC, int(z // CELLW) % NC
    best, bp = float('inf'), None
    for r in range(0, rmax + 1):
        if bp is not None and (r - 1) * CELLW > best:
            break
        for i in range(-r, r + 1):
            for k in range(-r, r + 1):
                if max(abs(i), abs(k)) != r:
                    continue
                for p in g.get(((cx + i) % NC, (cz + k) % NC), ()):
                    if p[0] == skip:
                        continue
                    dx, dy, dz = wrap(p[1] - x), p[2] - y, wrap(p[3] - z)
                    d = dx * dx + dy * dy + dz * dz
                    if d < best * best:
                        best, bp = math.sqrt(d), p
    return best, bp


def within(g, x, z, rad):
    out = []
    n = int(math.ceil(rad / CELLW))
    cx, cz = int(x // CELLW) % NC, int(z // CELLW) % NC
    r2 = rad * rad
    for i in range(-n, n + 1):
        for k in range(-n, n + 1):
            for p in g.get(((cx + i) % NC, (cz + k) % NC), ()):
                dx, dz = wrap(p[1] - x), wrap(p[3] - z)
                if dx * dx + dz * dz <= r2:
                    out.append(p)
    return out


# ------------------------------------------------------------------ accumulators
def acc():
    return dict(n=0, vh=[], v3=[], vrel=[], near=0, fixes=0, dnear=[],
                tow_sum=0.0, step_sum=0.0, step2_sum=0.0, tow_pos=0, tow_n=0,
                band=defaultdict(lambda: [0.0, 0.0, 0.0, 0]))


A = {'grazer': acc(), 'grazer_new': acc(), 'grazer_old': acc(), 'drifter': acc()}
W = defaultdict(acc)  # per 6,000 s window, grazers
JG = {}  # second -> (jointed grazers, grazers) in the positions row

# leafscent: speed through the water (root's velocity relative to the water) and cos to the leaf-smell gradient
scent = {}
with open(os.path.join(run, 'leafscent.jsonl'), encoding='utf-8') as f:
    for line in f:
        r = json.loads(line)
        if a.t0 <= r['t'] <= a.end:
            scent[round(r['t'])] = r
LS = defaultdict(lambda: dict(v=[], cos=[], n=0))

jnt_counts = defaultdict(int)
gz = {}  # per grazer born in window: first fix etc.
kill_pos_need = {}  # grazer -> kill time
gpath = defaultdict(float)
glast = {}
everclose = set()


def band_of(d):
    if d < 2:
        return '<2'
    if d < 5:
        return '2-5'
    if d < 20:
        return '5-20'
    return '>=20'


def move(slot, ident, p0, p1, food_now, food_next, drift_grid, disp, skip=None):
    dx, dy, dz = wrap(p1[1] - p0[1]), p1[2] - p0[2], wrap(p1[3] - p0[3])
    slot['n'] += 1
    slot['vh'].append(math.hypot(dx, dz) / 10.0)
    slot['v3'].append(math.sqrt(dx * dx + dy * dy + dz * dz) / 10.0)
    near = within(drift_grid, p0[1], p0[3], a.drift_r)
    near = [p for p in near if p[0] != ident and p[0] in disp]
    if len(near) >= 3:
        mx = sum(disp[p[0]][0] for p in near) / len(near)
        my = sum(disp[p[0]][1] for p in near) / len(near)
        mz = sum(disp[p[0]][2] for p in near) / len(near)
        slot['vrel'].append(math.sqrt((dx - mx) ** 2 + (dy - my) ** 2 + (dz - mz) ** 2) / 10.0)
    d, fp = nearest(food_now, p0[1], p0[2], p0[3], skip=skip if skip is not None else ident)
    if fp is None:
        return d
    f1 = food_next.get(fp[0])
    if f1 is None:
        return d
    fdx, fdy, fdz = wrap(f1[1] - fp[1]), f1[2] - fp[2], wrap(f1[3] - fp[3])
    ux, uy, uz = wrap(fp[1] - p0[1]), fp[2] - p0[2], wrap(fp[3] - p0[3])
    un = math.sqrt(ux * ux + uy * uy + uz * uz)
    if un < 1e-9:
        return d
    rx, ry, rz = dx - fdx, dy - fdy, dz - fdz
    step = math.sqrt(rx * rx + ry * ry + rz * rz)
    tow = (rx * ux + ry * uy + rz * uz) / un
    slot['tow_sum'] += tow
    slot['step_sum'] += step
    slot['step2_sum'] += step * step
    slot['tow_n'] += 1
    if tow > 0:
        slot['tow_pos'] += 1
    b = slot['band'][band_of(d)]
    b[0] += tow
    b[1] += step
    b[2] += step * step
    b[3] += 1
    return d


prev = None
rows_read = 0
t_lo = a.t0 - 10.0
for line in runrec.positions_lines(run):
    t = float(line[5:line.index(',')])
    if t < t_lo:
        continue
    if t > a.end:
        break
    row = json.loads(line)
    rows_read += 1
    bodies = {}
    food = []
    drift = []
    for b in row['b']:
        fl = b[4]
        if fl & 8:
            continue
        bodies[b[0]] = b
        if fl & 4 and b[0] not in grazer:
            food.append(b)
            if fl == 4 and b[0] in mouthless:
                drift.append(b)
    # first fixes and near-food for grazers
    food_grid = grid_of(food)
    gnow = [b for i, b in bodies.items() if i in grazer]
    JG[round(t)] = (sum(1 for b in gnow if b[4] & 2), len(gnow))
    for b in gnow:
        i = b[0]
        if b[4] & 2:
            jnt_counts['grazer_jnt'] += 1
        else:
            jnt_counts['grazer_rigid'] += 1
        bt = born.get(i, -1)
        if a.t0 <= bt <= a.end - 200 and i not in gz:
            d, fp = nearest(food_grid, b[1], b[2], b[3], skip=i)
            gz[i] = dict(t_fix=t, pos=(b[1], b[2], b[3]), d_food=d)
        if i in gz:
            if i in glast:
                lp = glast[i]
                gpath[i] += math.sqrt(wrap(b[1] - lp[0]) ** 2 + (b[2] - lp[1]) ** 2 + wrap(b[3] - lp[2]) ** 2)
            glast[i] = (b[1], b[2], b[3])
            gz[i]['last'] = (t, b[1], b[2], b[3])
            fk = first_kill.get(i)
            if fk is not None and 'kill_pos' not in gz[i] and t >= fk[0] - 5:
                gz[i]['kill_pos'] = (b[1], b[2], b[3])
    for b in bodies.values():
        if b[4] & 2 and b[0] not in grazer:
            jnt_counts['other_jnt'] += 1
    if prev is not None and prev[0] >= a.t0 and abs(t - prev[0] - 10) < 1e-6:
        pt, pbodies, pfood_grid, pdrift_grid = prev
        # drifter displacements over the move
        disp = {}
        for i, p in pdrift_grid_ids.items():
            n1 = bodies.get(i)
            if n1 is not None and n1[4] == 4:
                disp[i] = (wrap(n1[1] - p[1]), n1[2] - p[2], wrap(n1[3] - p[3]))
        food_next = {b[0]: b for b in food}
        for i, p0 in pbodies.items():
            if i not in grazer:
                continue
            p1 = bodies.get(i)
            if p1 is None:
                continue
            age = pt - born.get(i, 0)
            d = move(A['grazer'], i, p0, p1, pfood_grid, food_next, pdrift_grid, disp)
            move(W[int(pt // 6000)], i, p0, p1, pfood_grid, food_next, pdrift_grid, disp)
            sub = A['grazer_new'] if age < a.newborn else A['grazer_old']
            move(sub, i, p0, p1, pfood_grid, food_next, pdrift_grid, disp)
            for s in (A['grazer'], sub):
                s['fixes'] += 1
                s['dnear'].append(d)
                if d < a.near:
                    s['near'] += 1
            if d < a.near:
                everclose.add(i)
        for i, p0 in pdrift_grid_ids.items():
            if i % 10:
                continue
            p1 = bodies.get(i)
            if p1 is None or p1[4] != 4:
                continue
            d = move(A['drifter'], i, p0, p1, pfood_grid, food_next, pdrift_grid, disp, skip=i)
            A['drifter']['fixes'] += 1
            A['drifter']['dnear'].append(d)
            if d < a.near:
                A['drifter']['near'] += 1
    sr = scent.get(round(t))
    if sr is not None:
        for e in sr['b']:
            i, c1000, reads, isg, vmm = e[0], e[1], e[2], e[3], e[4]
            b = bodies.get(i)
            if b is None:
                continue
            if i in grazer:
                k = 'grazer jnt' if b[4] & 2 else 'grazer rigid'
            elif b[4] & 2:
                k = 'other jnt'
            elif b[4] == 4 and i in mouthless:
                k = 'drifter'
            else:
                continue
            LS[k]['v'].append(vmm / 1000.0)
            LS[k]['cos'].append(c1000 / 1000.0)
            if k.startswith('grazer'):
                LS['grazer all']['v'].append(vmm / 1000.0)
                LS['grazer all']['cos'].append(c1000 / 1000.0)
        LS['_rows']['n'] += 1
        LS['_skip']['v'].append(sum(sr['skip']) if isinstance(sr['skip'], list) else sr['skip'])
    prev = (t, bodies, food_grid, grid_of(drift))
    pdrift_grid_ids = {b[0]: b for b in drift}

# ------------------------------------------------------------------ print
print(f'== {a.label}  {run}')
print(f'   positions rows read {rows_read} ({a.t0:.0f}-{a.end:.0f} s, every row, 10 s apart); side {SIDE:.0f} m')
print(f'   grazers born to {a.end:.0f} s: {len(grazer)}; grazer fixes jointed {jnt_counts["grazer_jnt"]}, rigid '
      f'{jnt_counts["grazer_rigid"]}; non-grazer jointed fixes {jnt_counts["other_jnt"]}')
print('\n Q1/Q2 per move (10 s). speeds in cm/s.')
print(f'{"class":>11} {"moves":>8} | {"vh med":>7} {"vh p90":>7} | {"v3 med":>7} {"v3 p90":>7} | {"rel n":>7} '
      f'{"rel med":>7} {"rel p90":>7} | {"eff":>7} {"null95":>7} {"pos%":>6} | {"<2m %":>6} {"dnear med":>9}')
for k in ('grazer', 'grazer_new', 'grazer_old', 'drifter'):
    s = A[k]
    eff = s['tow_sum'] / s['step_sum'] if s['step_sum'] else float('nan')
    null = 1.96 * math.sqrt(s['step2_sum'] / 3) / s['step_sum'] if s['step_sum'] else float('nan')
    pos = s['tow_pos'] / s['tow_n'] if s['tow_n'] else float('nan')
    near = s['near'] / s['fixes'] if s['fixes'] else float('nan')
    print(f'{k:>11} {s["n"]:8d} | {100*q(s["vh"],.5):7.2f} {100*q(s["vh"],.9):7.2f} | {100*q(s["v3"],.5):7.2f} '
          f'{100*q(s["v3"],.9):7.2f} | {len(s["vrel"]):7d} {100*q(s["vrel"],.5):7.2f} {100*q(s["vrel"],.9):7.2f} | '
          f'{eff:7.3f} {null:7.3f} {100*pos:6.1f} | {100*near:6.1f} {q(s["dnear"],.5):9.2f}')
print('\n grazers by 6,000 s window')
for w in sorted(W):
    s = W[w]
    eff = s['tow_sum'] / s['step_sum'] if s['step_sum'] else float('nan')
    null = 1.96 * math.sqrt(s['step2_sum'] / 3) / s['step_sum'] if s['step_sum'] else float('nan')
    print(f'{w*6000:>6.0f}-{(w+1)*6000:<6.0f} moves {s["n"]:7d} vh med {100*q(s["vh"],.5):6.2f} p90 {100*q(s["vh"],.9):6.2f} '
          f'rel med {100*q(s["vrel"],.5):6.2f} eff {eff:+.3f} (null {null:.3f})')
print(f'\n leafscent ({LS["_rows"]["n"]} rows, every 100 s): speed through the water, cm/s, and cos to the leaf-smell '
      f'gradient (bodies under 1 mm/s or with no gradient are not in the file)')
for k in ('grazer all', 'grazer jnt', 'grazer rigid', 'other jnt', 'drifter'):
    v, c = LS[k]['v'], LS[k]['cos']
    if not v:
        continue
    print(f'{k:>13} n {len(v):7d} v med {100*q(v,.5):6.2f} p90 {100*q(v,.9):6.2f} mean {100*statistics.fmean(v):6.2f} | '
          f'cos mean {statistics.fmean(c):+.3f} share>0 {100*sum(1 for x in c if x > 0)/len(c):5.1f}%')

# Q3: the world's joint work against the jointed count, stats.jsonl
mult = find(cfg, 'workCostMultiplier')
srows = []
with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
    for line in f:
        r = json.loads(line)
        if a.t0 <= r['t'] <= a.end and r.get('jointed'):
            srows.append(r)
raw = statistics.fmean(r['workJoulesPerSecond'] / r['jointed'] for r in srows)
spj = statistics.fmean(r['speedJointed'] for r in srows)
vj = LS['other jnt']['v'] + LS['grazer jnt']['v']
vjm = statistics.fmean(vj) if vj else float('nan')
print(f'\n Q3 stats ({len(srows)} rows): raw joint work per jointed body {raw:.4f} W; billed at x{mult} {raw*mult:.5f} W; '
      f'mean jointed root ground speed (speedJointed) {100*spj:.2f} cm/s; mean jointed through-water speed (leafscent) '
      f'{100*vjm:.2f} cm/s; billed J per metre through water {raw*mult/vjm:.4f}; raw J per metre {raw/vjm:.3f}; '
      f'jointed bodies mean {statistics.fmean(r["jointed"] for r in srows):.0f}')
m = [(r['workJoulesPerSecond'], JG[round(r['t'])][0], JG[round(r['t'])][1]) for r in srows if round(r['t']) in JG]
wsum = sum(x[0] for x in m)
jg = sum(x[1] for x in m)
ga = sum(x[2] for x in m)
vg = statistics.fmean(LS['grazer jnt']['v']) if LS['grazer jnt']['v'] else float('nan')
up = wsum / jg * mult if jg else float('nan')
print(f'    rows joined to positions {len(m)}; jointed grazers mean {jg/len(m):.1f} of grazers {ga/len(m):.1f}; '
      f'all the world joint work laid on the jointed grazers (an upper bound): raw {wsum/jg:.3f} W, billed {up:.4f} W '
      f'a jointed grazer; mean through-water speed of jointed grazers {100*vg:.2f} cm/s; billed J/m {up/vg:.4f}, raw J/m '
      f'{wsum/jg/vg:.3f}')

print('\n toward-food efficiency by distance to the nearest food body (sum a / sum |step|; null95 = random headings)')
for k in ('grazer', 'grazer_new', 'grazer_old', 'drifter'):
    s = A[k]
    parts = []
    for bnd in ('<2', '2-5', '5-20', '>=20'):
        t_, st, s2, n = s['band'][bnd]
        if n:
            parts.append(f'{bnd}: n {n} eff {t_/st:+.3f} (null {1.96*math.sqrt(s2/3)/st:.3f})')
    print(f'{k:>11}  ' + '; '.join(parts))

# Q4
print(f'\n Q4: grazers born {a.t0:.0f}-{a.end-200:.0f} s with a fix: {len(gz)}')
starved = [i for i in gz if death.get(i, (0, ''))[1] == 'starved']
for name, ids in (('all', list(gz)), ('starved', starved)):
    if not ids:
        continue
    df = [gz[i]['d_food'] for i in ids]
    life = [death[i][0] - born[i] for i in ids if i in death]
    killers = [i for i in ids if i in first_kill]
    tk = [first_kill[i][0] - born[i] for i in killers]
    dk = []
    for i in killers:
        if 'kill_pos' in gz[i]:
            p0, p1 = gz[i]['pos'], gz[i]['kill_pos']
            dk.append(math.sqrt(wrap(p1[0] - p0[0]) ** 2 + (p1[1] - p0[1]) ** 2 + wrap(p1[2] - p0[2]) ** 2))
    net = []
    path = []
    for i in ids:
        if 'last' in gz[i]:
            p0 = gz[i]['pos']
            _, x, y, z = gz[i]['last']
            net.append(math.sqrt(wrap(x - p0[0]) ** 2 + (y - p0[1]) ** 2 + wrap(z - p0[2]) ** 2))
            path.append(gpath[i])
    close = sum(1 for i in ids if i in everclose)
    print(f'  {name:>8}: n {len(ids)}; nearest food at first fix med {q(df,.5):.2f} m, q1 {q(df,.25):.2f}, p90 '
          f'{q(df,.9):.2f}, within 2 m {100*sum(1 for d in df if d < 2)/len(df):.1f}%; ever within 2 m {100*close/len(ids):.1f}%')
    print(f'            life med {q(life,.5):.0f} s; made a kill row {100*len(killers)/len(ids):.1f}% '
          f'(first at age med {q(tk,.5):.0f} s, from first fix med {q(dk,.5):.2f} m); net travel first-last fix med '
          f'{q(net,.5):.2f} m p90 {q(net,.9):.2f}; path (10 s fixes) med {q(path,.5):.2f} m p90 {q(path,.9):.2f}')
