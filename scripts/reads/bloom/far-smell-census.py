"""D251's far smell, sized from a recorded world: each island's lit leaf area, and what the leaf-smell strength reads
with and without a far smell at a range of gains.

    python -I scripts/reads/bloom/far-smell-census.py <census prefix> <run dir> [<census prefix> <run dir> ...]

The census is `--island-leaf-census` (branch fat-step0), which writes <prefix>-islands.tsv, -bodies.tsv and
-grid.tsv. The far smell is the coordinator's form of 2026-10-28: island k adds
G x L_k x max(0, 1 - max(0, d - r_k) / R), with L_k its lit leaf area, d the level distance to its centroid, r_k its
effective radius and R = 30 m, summed into the leaf smell s; the strength a body reads is s / (s + h), with h the
run's leafScentHalfScaleSquareMetres. A grazer is a body born with atk 1 and ink 1, as r61-grazer-economy.py reads it.

For each census second it prints the islands, the gaps between them, and for each gain: the far smell at an island's
edge, the strength grazers read on and off the islands, its spread on the islands (p10 to p90, the saturation
check), and how far into the desert the far smell reaches. Exits 2 on a missing file.
"""
import csv
import json
import math
import os
import sys

GAINS = [0.0, 0.002, 0.003, 0.004, 0.005, 0.01, 0.02, 0.05]
REACH = 30.0


def fail(m):
    print(m, file=sys.stderr)
    sys.exit(2)


def tsv(path):
    if not os.path.isfile(path):
        fail(path + ': missing')
    with open(path) as f:
        return list(csv.DictReader(f, delimiter='\t'))


def grazers(run):
    ids = set()
    path = os.path.join(run, 'lineage.jsonl')
    if not os.path.isfile(path):
        fail(path + ': missing')
    with open(path) as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            if r.get('atk') == 1 and r.get('ink') == 1:
                ids.add(r['id'])
    return ids


def config(run):
    path = os.path.join(run, 'config.json')
    if not os.path.isfile(path):
        fail(path + ': missing')
    c = json.load(open(path))
    world = c['world']
    side = math.sqrt(float(world['worldAreaSquareMetres']))
    return float(c['sense']['leafScentHalfScaleSquareMetres']), side


def q(xs, p):
    if not xs:
        return float('nan')
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * (len(xs) - 1) + 0.5))]


def ring(d, side):
    d = abs(d) % side
    return min(d, side - d)


def far(x, z, isl, g, side):
    total = 0.0
    for k in isl:
        d = math.hypot(ring(x - k['cx'], side), ring(z - k['cz'], side))
        total += g * k['L'] * max(0.0, 1.0 - max(0.0, d - k['r']) / REACH)
    return total


args = sys.argv[1:]
BAND = 0.0
if len(args) >= 2 and args[0] == '--band':
    # The grove band, metres: the flat top reaches r_k + BAND, and L_k counts the leaves within BAND of the island's
    # edge (nearest island first). The census gives leaf area by column only, so the banded L_k is the column L_k
    # scaled by the ratio of leaf counts, banded to inside; that assumes the halo's leaves are the core's size.
    BAND = float(args[1])
    args = args[2:]
if not args or len(args) % 2:
    fail(__doc__)

for prefix, run in zip(args[0::2], args[1::2]):
    half, side = config(run)
    keep = grazers(run)
    islands = tsv(prefix + '-islands.tsv')
    bodies = tsv(prefix + '-bodies.tsv')
    grid = tsv(prefix + '-grid.tsv')
    for t in sorted({r['second'] for r in islands}, key=float):
        isl = [dict(k=int(r['island']), n=int(r['columns']), cx=float(r['cx']), cz=float(r['cz']), r=float(r['r_eff']),
                    rfar=float(r['r_far']), L=float(r['leaf_area_m2']), leaves=int(r['leaf_sources']))
               for r in islands if r['second'] == t]
        if BAND > 0:
            near = {k['k']: 0 for k in isl}
            for b in bodies:
                if b['second'] != t or b['photosynthetic'] != '1':
                    continue
                x, z = float(b['x']), float(b['z'])
                g, kk = min((math.hypot(ring(x - k['cx'], side), ring(z - k['cz'], side)) - k['r'], k['k']) for k in isl)
                if g <= BAND:
                    near[kk] += 1
            for k in isl:
                k['L'] = k['L'] * near[k['k']] / k['leaves'] if k['leaves'] > 0 else 0.0
                k['r'] += BAND
        print('== %s at %s s: %d islands, half scale %g m2, box %g m, grove band %g m' % (os.path.basename(prefix), t, len(isl), half, side, BAND))
        for k in isl:
            print('  island %d  %3d columns  centre (%6.1f, %6.1f)  r_eff %5.1f m  r_far %5.1f m  L %8.1f m2  %5d leaves'
                  % (k['k'], k['n'], k['cx'], k['cz'], k['r'], k['rfar'], k['L'], k['leaves']))
        gaps = []
        for i in range(len(isl)):
            near = min((math.hypot(ring(isl[i]['cx'] - isl[j]['cx'], side), ring(isl[i]['cz'] - isl[j]['cz'], side))
                        - isl[i]['r'] - isl[j]['r'], j) for j in range(len(isl)) if j != i)
            gaps.append(near[0])
        print('  nearest neighbour edge gap (centroid distance - both r_eff): median %.1f m, min %.1f, max %.1f'
              % (q(gaps, .5), min(gaps), max(gaps)))
        Ls = [k['L'] for k in isl]
        print('  L_k median %.1f m2, min %.1f, max %.1f' % (q(Ls, .5), min(Ls), max(Ls)))

        def grove(r):
            # with a band, a point is in a grove when it lies within an island's banded radius; without, in its columns
            if BAND <= 0:
                return int(r['island']) >= 0
            x, z = float(r['x']), float(r['z'])
            return any(math.hypot(ring(x - k['cx'], side), ring(z - k['cz'], side)) <= k['r'] for k in isl)

        bt = [b for b in bodies if b['second'] == t and int(b['creature']) in keep]
        on_isl = [b for b in bt if grove(b)]
        off_isl = [b for b in bt if not grove(b)]
        g_isl = [r for r in grid if r['second'] == t and grove(r)]
        g_des = [r for r in grid if r['second'] == t and not grove(r)]
        s_isl = [float(r['s_off']) for r in g_isl]
        print('  grazers %d (%d on islands, %d in the desert); leaf smell s on island grid points: p10 %.2f median %.2f p90 %.2f m2'
              % (len(bt), len(on_isl), len(off_isl), q(s_isl, .1), q(s_isl, .5), q(s_isl, .9)))
        print('  %-6s %-22s %-34s %-34s %-26s %s' % ('G', 'edge far (median L)', 'grazers on islands: strength p10/50/90',
                                                   'island grid: strength p10/50/90', 'grazers in desert p50/90',
                                                   'desert grid with far > s_off / far > 0.1'))
        # the desert grid at one depth only (the first listed), to count columns rather than column-depths
        d0 = min(float(r['depth']) for r in g_des) if g_des else 0.0
        des_cols = [r for r in g_des if float(r['depth']) == d0]
        for g in GAINS:
            def st(s):
                return s / (s + half) if s > 0 else 0.0
            sb = [st(float(b['s_off']) + far(float(b['x']), float(b['z']), isl, g, side)) for b in on_isl]
            sd = [st(float(b['s_off']) + far(float(b['x']), float(b['z']), isl, g, side)) for b in off_isl]
            sg = [st(float(r['s_off']) + far(float(r['x']), float(r['z']), isl, g, side)) for r in g_isl]
            fd = [(far(float(r['x']), float(r['z']), isl, g, side), float(r['s_off'])) for r in des_cols]
            above = sum(1 for f, s in fd if f > s) / max(1, len(fd))
            reach = sum(1 for f, s in fd if f > 0.1) / max(1, len(fd))
            edge = g * q(Ls, .5)
            print('  %-6g %6.2f m2 (str %.2f)    %.2f / %.2f / %.2f                   %.2f / %.2f / %.2f                   %.2f / %.2f                %3.0f%% / %3.0f%%'
                  % (g, edge, st(edge), q(sb, .1), q(sb, .5), q(sb, .9), q(sg, .1), q(sg, .5), q(sg, .9),
                     q(sd, .5), q(sd, .9), 100 * above, 100 * reach))
