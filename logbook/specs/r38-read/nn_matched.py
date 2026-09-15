# -*- coding: utf-8 -*-
"""D3 and D4 from the positions reader: round 38's cols, spreads and nearest-neighbour
medians, and the nearest neighbour at MATCHED abundance against round 37b's ten seeds.

nn_matched.py of logbook/specs/r37b-fresh-read, with round 38's arms and D3's comparison:
for each anchor the control is whichever of round 37b's TEN seeds has the `alive` nearest
round 38's at that time; an anchor counts as matched only when the two abundances are
within 15% (0100's wording), and the nearest is named either way. D3's band on the ratio
is 1.3 to 1.9 (a quarter of the density in three dimensions reads 1.59).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
B37 = os.path.abspath(os.path.join(HERE, '..', 'r37b-read'))
F37 = os.path.abspath(os.path.join(HERE, '..', 'r37b-fresh-read'))
TIMES = [5000, 15000, 30000]
ARMS = ['r38-s%d' % i for i in range(1, 6)]
COMP = ['r37b-s%d' % i for i in range(1, 11)]

WIDTHS = [('t', 9), ('n', 6), ('cols', 9), ('x sd', 7), ('z sd', 7), ('nn h', 8), ('nn 3d', 8),
          ('leaf', 6), ('leaf y', 8), ('stom', 6), ('stom y', 8), ('mixo', 6), ('mixo y', 8),
          ('jnt', 6), ('jnt y', 8), ('plain', 6), ('plain y', 8), ('clade 1m', 9),
          ('jointed', 8), ('joint y', 8), ('rigid y', 8)]


def split_fixed(line):
    out, i = [], 0
    for _, w in WIDTHS:
        out.append(line[i:i + w].strip())
        i += w + 1
    return out


def read_summary(arm):
    for d in (HERE, B37, F37):
        p = os.path.join(d, 'positions-%s.txt' % arm)
        if os.path.exists(p):
            path = p
            break
    else:
        raise SystemExit('no summary for ' + arm)
    names = [n for n, _ in WIDTHS]
    seen, rows = False, []
    for line in open(path, encoding='utf-8'):
        s = line.rstrip('\n')
        if not s.strip():
            continue
        if not seen:
            if s.strip().startswith('t ') and 'nn h' in s:
                seen = True
            continue
        f = split_fixed(s)
        if not f[0].replace('.', '', 1).isdigit():
            continue
        d2 = dict(zip(names, f))
        d2['t'] = float(d2['t'])
        rows.append(d2)
    if not seen:
        raise SystemExit('no summary table in ' + path)
    return rows


def val(s):
    s = s.lstrip('~')
    return float(s) if s not in ('-', '') else None


def at(rows, t):
    return min(rows, key=lambda r: abs(r['t'] - t))


F = {a: read_summary(a) for a in ARMS}
C = {a: read_summary(a) for a in COMP}

out = ['## D4 from the positions reader: the columns whose centres are inside the circle'
       ' (402 live columns at 400 m2), the spreads, and the nearest-neighbour medians.'
       ' A "~" in the reader\'s own output means the median is estimated from a sample of'
       ' pairs; it is carried here as the reader printed it.',
       'arm\tt\treader n\treader cols\treader x sd\treader z sd\treader nn h\treader nn 3d'
       '\tleaf\tleaf y\tstom\tstom y\tmixo\tmixo y\tjointed\tjoint y']
for a in ARMS:
    for t in TIMES:
        r = at(F[a], t)
        out.append('\t'.join([a, '%d' % r['t'], r['n'], r['cols'], r['x sd'], r['z sd'],
                              r['nn h'], r['nn 3d'], r['leaf'], r['leaf y'], r['stom'],
                              r['stom y'], r['mixo'], r['mixo y'], r['jointed'],
                              r['joint y']]))

out += ['', '## D3  nearest-neighbour median at MATCHED abundance. The control at each anchor'
        " is whichever of round 37b's TEN seeds has the `alive` nearest round 38's; an anchor"
        ' counts as matched when the two are within 15%. D3 asks the ratio to sit between 1.3'
        ' and 1.9 in at least 3 of the anchors that match.',
        'arm\tanchor t\tn\tnn h\tnn 3d\tcontrol arm\tcontrol t\tcontrol n\tcontrol nn h'
        '\tcontrol nn 3d\tn gap\tmatched\tratio nn h\tratio nn 3d\tin 1.3-1.9']
hits = anchors = 0
for a in ARMS:
    for t in TIMES:
        r = at(F[a], t)
        n38 = float(r['n'])
        best = bestrow = bestarm = None
        for c in COMP:
            row = at(C[c], t)
            gap = abs(float(row['n']) - n38)
            if best is None or gap < best:
                best, bestrow, bestarm = gap, row, c
        gapfrac = (float(bestrow['n']) - n38) / n38
        matched = abs(gapfrac) <= 0.15
        h38, h37 = val(r['nn h']), val(bestrow['nn h'])
        d38, d37 = val(r['nn 3d']), val(bestrow['nn 3d'])
        ok = 1.3 <= d38 / d37 <= 1.9
        if matched:
            anchors += 1
            hits += 1 if ok else 0
        out.append('%s\t%d\t%s\t%s\t%s\t%s\t%d\t%s\t%s\t%s\t%+.3f\t%s\t%.3f\t%.3f\t%s' % (
            a, t, r['n'], r['nn h'], r['nn 3d'], bestarm, bestrow['t'], bestrow['n'],
            bestrow['nn h'], bestrow['nn 3d'], gapfrac, 'yes' if matched else 'no',
            h38 / h37, d38 / d37, 'yes' if ok else 'no'))
out.append('')
out.append('anchors matched within 15%%\t%d of 15\tin 1.3-1.9\t%d\t(D3 holds at 3 of the'
           ' matched anchors)' % (anchors, hits))

out += ['', '## the same ratio at every anchor against the 37b seed nearest in abundance,'
        ' ignoring the match test, so that the unmatched anchors can be read too. And the'
        ' raw reading: 37b\'s ten seeds read nn 3d 0.66 to 0.79 at these anchors.']

open(os.path.join(HERE, 'nn-matched.tsv'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
