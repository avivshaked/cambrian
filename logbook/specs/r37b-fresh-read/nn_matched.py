# -*- coding: utf-8 -*-
"""F3 and F7 for the fresh seeds: the reader's cols and spreads, and the
nearest-neighbour medians raw and at matched abundance.

nn_matched.py of logbook/specs/r37b-read, with one change the brief names. The fresh
seeds have no same-numbered round 36 seed to match against (round 36 ran seeds 1 to 5
only), so the comparison at each anchor is **round 37b's seed whose `alive` at that
anchor is nearest the fresh seed's**, taken from logbook/specs/r37b-read's own summaries
and not recomputed. That is a matched-abundance comparison against the first draw of the
same world, not against round 36; F7's band (0.8 to 1.35) is applied to it and the table
says which comparison seed each anchor used and how far apart the abundances were.
Round 36's raw numbers are also carried, seed for seed by position (fresh seed 6 against
round 36 seed 1, and so on), purely as a scale and never as a matched pair.
"""
import os, json, glob, math

HERE = os.path.dirname(os.path.abspath(__file__))
B37 = os.path.abspath(os.path.join(HERE, '..', 'r37b-read'))
R37DIR = os.path.abspath(os.path.join(HERE, '..', 'r37-read'))
TIMES = [5000, 15000, 30000]
FRESH = ['r37b-s6', 'r37b-s7', 'r37b-s8', 'r37b-s9', 'r37b-s10']
COMP = ['r37b-s1', 'r37b-s2', 'r37b-s3', 'r37b-s4', 'r37b-s5']
R36A = ['r36-s1', 'r36-s2', 'r36-s3', 'r36-s4', 'r36-s5']

WIDTHS = [('t', 9), ('n', 6), ('cols', 9), ('x sd', 7), ('z sd', 7), ('nn h', 8), ('nn 3d', 8),
          ('leaf', 6), ('leaf y', 8), ('stom', 6), ('stom y', 8), ('mixo', 6), ('mixo y', 8),
          ('jnt', 6), ('jnt y', 8), ('plain', 6), ('plain y', 8), ('clade 1m', 9),
          ('jointed', 8), ('joint y', 8), ('rigid y', 8)]


def split_fixed(line):
    out, i = [], 0
    for _, wdt in WIDTHS:
        out.append(line[i:i + wdt].strip())
        i += wdt + 1
    return out


def read_summary(arm):
    for d in (HERE, B37, R37DIR):
        p = os.path.join(d, 'positions-%s.txt' % arm)
        if os.path.exists(p):
            path = p
            break
    else:
        raise SystemExit('no summary for ' + arm)
    names = [n for n, _ in WIDTHS]
    seen_header, rows = False, []
    for line in open(path, encoding='utf-8'):
        s = line.rstrip('\n')
        if not s.strip():
            continue
        if not seen_header:
            if s.strip().startswith('t ') and 'nn h' in s:
                seen_header = True
            continue
        f = split_fixed(s)
        if not f[0].replace('.', '', 1).isdigit():
            continue
        d2 = dict(zip(names, f))
        d2['t'] = float(d2['t'])
        rows.append(d2)
    if not seen_header:
        raise SystemExit('no summary table in ' + path)
    return rows


def val(s):
    return (float(s.lstrip('~')) if s.lstrip('~') != '-' else None), s.startswith('~')


def at(rows, t):
    return min(rows, key=lambda r: abs(r['t'] - t))


F = {a: read_summary(a) for a in FRESH}
C = {a: read_summary(a) for a in COMP}
R36 = {a: read_summary(a) for a in R36A}

out = ['## F3  cols and spreads from the positions reader (tank: the columns whose centres'
       ' are inside the circle; 100 live columns), beside round 37b seeds 1 to 5']
out.append('arm\tt\treader n\treader cols\treader x sd\treader z sd\t37b s1-s5 cols at t'
           '\t37b s1-s5 x sd at t')
for a in FRESH:
    for t in TIMES:
        r = at(F[a], t)
        cs = [at(C[c], t)['cols'] for c in COMP]
        xs = [at(C[c], t)['x sd'] for c in COMP]
        out.append('%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s' % (
            a, r['t'], r['n'], r['cols'], r['x sd'], r['z sd'],
            ' '.join(cs), ' '.join(xs)))

out += ['', '## F7  nearest-neighbour median, raw (the fresh seed at the anchor, beside round'
        ' 36 seed by position -- a scale, NOT a matched pair)',
        'arm\tt\tn\tnn h\tnn 3d\tr36 arm\tr36 n\tr36 nn h\tr36 nn 3d\tn ratio\tratio nn 3d']
for i, a in enumerate(FRESH):
    for t in (5000, 30000):
        r = at(F[a], t)
        b = at(R36[R36A[i]], t)
        d7, _ = val(r['nn 3d']); d6, _ = val(b['nn 3d'])
        out.append('%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%.3f\t%.3f' % (
            a, r['t'], r['n'], r['nn h'], r['nn 3d'], R36A[i], b['n'], b['nn h'], b['nn 3d'],
            float(r['n']) / float(b['n']), d7 / d6))

out += ['', '## F7  nearest-neighbour median at MATCHED abundance. The comparison is round'
        " 37b's seed whose `alive` at the anchor is nearest the fresh seed's (no round 36"
        ' seed 6 to 10 exists). An anchor counts as matched only when the two abundances are'
        ' within 10%; the nearest is named either way. Round 37b seed 5 ends at 29,200 s, so'
        " its 30,000 s row is that sample (taken from r37b-read's summary, not recomputed).",
        'arm\tanchor t\tn\tnn h\tnn 3d\tcomparison arm\tcomp t\tcomp n\tcomp nn h\tcomp nn 3d'
        '\tn gap\tmatched\tratio nn h\tratio nn 3d\tin 0.8-1.35']

hits = anchors = 0
for a in FRESH:
    for t in TIMES:
        r = at(F[a], t)
        n7 = float(r['n'])
        best, bestrow = None, None
        for c in COMP:
            row = at(C[c], t)
            gap = abs(float(row['n']) - n7)
            if best is None or gap < best:
                best, bestrow, bestarm = gap, row, c
        gapfrac = (float(bestrow['n']) - n7) / n7
        matched = abs(gapfrac) <= 0.10
        h7, _ = val(r['nn h']); h6, _ = val(bestrow['nn h'])
        d7, _ = val(r['nn 3d']); d6, _ = val(bestrow['nn 3d'])
        ok = 0.8 <= d7 / d6 <= 1.35
        if matched:
            anchors += 1
            hits += 1 if ok else 0
        out.append('%s\t%d\t%s\t%s\t%s\t%s\t%d\t%s\t%s\t%s\t%+.3f\t%s\t%.3f\t%.3f\t%s' % (
            a, t, r['n'], r['nn h'], r['nn 3d'], bestarm, bestrow['t'], bestrow['n'],
            bestrow['nn h'], bestrow['nn 3d'], gapfrac, 'yes' if matched else 'no',
            h7 / h6, d7 / d6, 'yes' if ok else 'no'))
out.append('')
out.append('anchors matched within 10%%\t%d of 15\tin 0.8-1.35\t%d' % (anchors, hits))

open(os.path.join(HERE, 'nn-matched.tsv'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
