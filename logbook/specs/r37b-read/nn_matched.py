# -*- coding: utf-8 -*-
"""M2 and M3 for round 37b: the reader's cols and spreads, and the nearest-neighbour
medians raw and at matched abundance.

nn_matched.py of logbook/specs/r37-read, pointed at round 37b's summaries in this
directory and at round 36's and round 37's, which that directory already holds and which
are taken from there rather than recomputed (the brief's instruction). Seed 5's anchor at
30,000 s falls on its last sample, 29,200 s, and the table prints the time it used.

A median the reader marked ~ was taken from a thinned query set (--nn-max, 1200 bodies);
the mark is carried through.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
R37DIR = os.path.abspath(os.path.join(HERE, '..', 'r37-read'))
TIMES = [5000, 15000, 30000]

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
    d = HERE if arm.startswith('r37b') else R37DIR
    path = os.path.join(d, 'positions-%s.txt' % arm)
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


B = {i: read_summary('r37b-s%d' % i) for i in range(1, 6)}
R36 = {i: read_summary('r36-s%d' % i) for i in range(1, 6)}
R37 = {i: read_summary('r37-s%d' % i) for i in range(1, 6)}

out = ['## M2  cols and spreads from the positions reader (tank: the columns whose centres are'
       ' inside the circle; 100 live columns), beside the run report\'s own cols']
out.append('seed\tt\treader n\treader cols\treader x sd\treader z sd\tr37 cols\tr37 x sd'
           '\tr36 cols\tr36 x sd')
for i in range(1, 6):
    for t in TIMES:
        a, b, c = at(B[i], t), at(R37[i], t), at(R36[i], t)
        out.append('%d\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
            i, a['t'], a['n'], a['cols'], a['x sd'], a['z sd'],
            b['cols'], b['x sd'], c['cols'], c['x sd']))

out += ['', '## M3  nearest-neighbour median, raw (same time, whatever the abundance)',
        'seed\tt\tr37b n\tr37b nn h\tr37b nn 3d\tr36 n\tr36 nn h\tr36 nn 3d'
        '\tratio nn h\tratio nn 3d\tn ratio\tr37 nn 3d\tr37b/r37 nn 3d']
for i in range(1, 6):
    for t in (5000, 30000):
        a, b, c = at(B[i], t), at(R36[i], t), at(R37[i], t)
        h7, _ = val(a['nn h']); h6, _ = val(b['nn h'])
        d7, _ = val(a['nn 3d']); d6, _ = val(b['nn 3d']); dc, _ = val(c['nn 3d'])
        out.append('%d\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f\t%s\t%.3f' % (
            i, a['t'], a['n'], a['nn h'], a['nn 3d'], b['n'], b['nn h'], b['nn 3d'],
            h7 / h6, d7 / d6, float(a['n']) / float(b['n']), c['nn 3d'], d7 / dc))

out += ['', '## M3  nearest-neighbour median at MATCHED abundance: round 37b anchored at the'
        ' named time, and the round 36 sample of the same seed nearest in time whose n is'
        ' within 10% of round 37b\'s n at that time',
        'seed\tanchor t\tr37b t\tr37b n\tr37b nn h\tr37b nn 3d\tr36 t\tr36 n\tr36 nn h'
        '\tr36 nn 3d\tn gap\tratio nn h\tratio nn 3d\tin 0.8-1.25\tr37b depth\tr37b depth sd'
        '\tr36 depth\tr36 depth sd']
import json, glob, math


def depths(arm, t):
    """Mean and sd of y at the sample nearest t, from positions.jsonl itself."""
    root = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
    ds = [d for d in sorted(glob.glob(os.path.join(root, 'runs', arm, '*'))) if os.path.isdir(d)]
    best = None
    with open(os.path.join(ds[-1], 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{"t":'):
                continue
            try:
                row = json.loads(line)
            except ValueError:
                continue
            if best is None or abs(row['t'] - t) < abs(best['t'] - t):
                best = row
            if row['t'] > t + 200:
                break
    ys = [b[2] for b in best['b']]
    m = sum(ys) / len(ys)
    return m, math.sqrt(sum((y - m) ** 2 for y in ys) / len(ys))


hits = 0
anchors = 0
for i in range(1, 6):
    for t in TIMES:
        a = at(B[i], t)
        n7 = float(a['n'])
        cand = [r for r in R36[i] if abs(float(r['n']) - n7) <= 0.10 * n7]
        if not cand:
            out.append('%d\t%d\t%d\t%s\t%s\t%s\tno round 36 sample within 10%% of n=%s'
                       % (i, t, a['t'], a['n'], a['nn h'], a['nn 3d'], a['n']))
            continue
        b = min(cand, key=lambda r: abs(r['t'] - t))
        anchors += 1
        h7, _ = val(a['nn h']); h6, _ = val(b['nn h'])
        d7, _ = val(a['nn 3d']); d6, _ = val(b['nn 3d'])
        ok = 0.8 <= d7 / d6 <= 1.25
        hits += 1 if ok else 0
        m7, s7 = depths('r37b-s%d' % i, a['t'])
        m6, s6 = depths('r36-s%d' % i, b['t'])
        out.append('%d\t%d\t%d\t%s\t%s\t%s\t%d\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f\t%s'
                   '\t%.2f\t%.2f\t%.2f\t%.2f' % (
                       i, t, a['t'], a['n'], a['nn h'], a['nn 3d'],
                       b['t'], b['n'], b['nn h'], b['nn 3d'],
                       (float(b['n']) - n7) / n7, h7 / h6, d7 / d6,
                       'yes' if ok else 'no', m7, s7, m6, s6))
out.append('')
out.append('anchors that found a match\t%d of 15\tin 0.8-1.25\t%d' % (anchors, hits))

open(os.path.join(HERE, 'nn-matched.tsv'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
