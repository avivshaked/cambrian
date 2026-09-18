# -*- coding: utf-8 -*-
"""The crowd's spacing at matched abundance, round 39 against round 38.

`nn_matched.py` of logbook/specs/r38-read, with round 39's arms and round 38's five seeds
as the control.  Two matchings, because round 39 holds half again as many bodies as round
38 ever did and the first matching often cannot be made:

  * AT THE SAME SECOND, which is round 38's own method: the control is whichever of round
    38's five seeds has the `alive` nearest at that second, and the anchor counts as
    matched only when the two are within 15%.
  * AT MATCHED ABUNDANCE ANYWHERE IN ROUND 38'S RUN: the control is whichever (seed,
    second) of round 38's five seeds has the `alive` nearest, over the whole 30,000 s.
    This is the comparison the name promises, and it is the one to read when the first
    cannot be matched.

The expectation, for a reading rather than a bar: the water is 99,000 m3 (2,200 m2 by 45 m)
against round 38's 24,000 m3, so at the same body count the spacing goes as the cube root of
4.125, about 1.60.

Writes nn-matched.tsv beside this file.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
R38 = os.path.abspath(os.path.join(HERE, '..', 'r38-read'))
TIMES = [5000, 15000, 30000]
ARMS = ['r39-s%d' % i for i in range(1, 6)]
COMP = ['r38-s%d' % i for i in range(1, 6)]
VOLUME_RATIO = (2200.0 * 45.0) / (400.0 * 60.0)

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
    for d in (HERE, R38):
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

out = ['## Round 39 from the positions reader: the columns (2,211 live at 2,200 m2), the'
       ' spreads and the nearest-neighbour medians. A "~" in the reader\'s own output means'
       ' the median is estimated from a sample of pairs; it is carried here as printed.',
       'arm\tt\treader n\treader cols\treader x sd\treader z sd\treader nn h\treader nn 3d'
       '\tleaf\tleaf y\tstom\tstom y\tjointed\tjoint y']
for a in ARMS:
    for t in TIMES:
        r = at(F[a], t)
        out.append('%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
            a, t, int(float(r['n'])), r['cols'], r['x sd'], r['z sd'], r['nn h'],
            r['nn 3d'], r['leaf'], r['leaf y'], r['stom'], r['stom y'], r['jointed'],
            r['joint y']))

out += ['', '## Matching 1, round 38\'s own method: the control is the round 38 seed whose'
        ' `alive` is nearest AT THE SAME SECOND; matched when within 15 per cent. The water'
        ' is ' + ('%.3f' % VOLUME_RATIO) + ' times round 38\'s, so at equal counts the'
        ' spacing would go as its cube root, '
        + ('%.2f' % (VOLUME_RATIO ** (1.0 / 3.0))) + '.',
        'arm\tt\tr39 n\tcontrol\tcontrol n\tabundance gap\tmatched within 15%'
        '\tr39 nn 3d\tcontrol nn 3d\tratio']
for a in ARMS:
    for t in TIMES:
        r = at(F[a], t)
        n = float(r['n'])
        best = min(COMP, key=lambda c: abs(float(at(C[c], t)['n']) - n))
        cr = at(C[best], t)
        cn = float(cr['n'])
        gap = abs(cn - n) / n
        out.append('%s\t%d\t%d\t%s\t%d\t%.3f\t%s\t%s\t%s\t%.3f' % (
            a, t, int(n), best, int(cn), gap, 'yes' if gap <= 0.15 else 'no',
            r['nn 3d'], cr['nn 3d'], val(r['nn 3d']) / val(cr['nn 3d'])))

out += ['', '## Matching 2, matched abundance anywhere in round 38\'s run: the control is'
        ' the (seed, second) of round 38 whose `alive` is nearest, over its whole 30,000 s.',
        'arm\tt\tr39 n\tcontrol\tcontrol t\tcontrol n\tabundance gap'
        '\tmatched within 15%\tr39 nn 3d\tcontrol nn 3d\tratio']
ratios = []
for a in ARMS:
    for t in TIMES:
        r = at(F[a], t)
        n = float(r['n'])
        best = None
        for c in COMP:
            for cr in C[c]:
                if cr['t'] < 2000:
                    continue
                g = abs(float(cr['n']) - n)
                if best is None or g < best[0]:
                    best = (g, c, cr)
        g, c, cr = best
        cn = float(cr['n'])
        gap = abs(cn - n) / n
        ratio = val(r['nn 3d']) / val(cr['nn 3d'])
        ratios.append(ratio)
        out.append('%s\t%d\t%d\t%s\t%d\t%d\t%.3f\t%s\t%s\t%s\t%.3f' % (
            a, t, int(n), c, int(cr['t']), int(cn), gap,
            'yes' if gap <= 0.15 else 'no', r['nn 3d'], cr['nn 3d'], ratio))
out.append('## the ratio over the fifteen anchors: '
           + ('%.3f to %.3f, mean %.3f' % (min(ratios), max(ratios), sum(ratios) / len(ratios)))
           + (' (the volume expectation is %.2f)' % (VOLUME_RATIO ** (1 / 3.0))))

out += ['', '## round 38\'s own anchors, for reference (0101\'s D3 read 1.11 to 1.41 m'
        ' three-dimensional against round 37b\'s 0.66 to 0.79 m)',
        'arm\tt\tn\tnn 3d\tx sd\tcols']
for c in COMP:
    for t in TIMES:
        cr = at(C[c], t)
        out.append('%s\t%d\t%d\t%s\t%s\t%s' % (
            c, t, int(float(cr['n'])), cr['nn 3d'], cr['x sd'], cr['cols']))

with open(os.path.join(HERE, 'nn-matched.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('wrote nn-matched.tsv')
