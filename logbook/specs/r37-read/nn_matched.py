# -*- coding: utf-8 -*-
"""M2 and M3: the branch reader's cols and spreads, and the nearest-neighbour medians
raw and at matched abundance.

Source: the summary tables written by
scratch/wt-streams/scripts/positions-read.py <arm> --summary, one file per arm in this
directory. A median marked ~ in that file was taken from a thinned query set (the
reader's --nn-max, 1200 bodies); the mark is carried through here.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
TIMES = [5000, 15000, 30000]
COLS = ['t', 'n', 'cols', 'x sd', 'z sd', 'nn h', 'nn 3d']


# positions-read.py's own COLUMNS: names carry spaces, so the table is fixed width and is
# read by those widths rather than by splitting on whitespace.
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
    """Rows of the summary table, keyed by the header's own names."""
    path = os.path.join(HERE, 'positions-%s.txt' % arm)
    names = [n for n, _ in WIDTHS]
    seen_header = False
    rows = []
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
        d = dict(zip(names, f))
        d['t'] = float(d['t'])
        rows.append(d)
    if not seen_header:
        raise SystemExit('no summary table in ' + path)
    return rows


def val(s):
    """A reading, and whether the reader marked it thinned."""
    t = s.startswith('~')
    return (float(s.lstrip('~')) if s.lstrip('~') not in ('-',) else None), t


def at(rows, t):
    return min(rows, key=lambda r: abs(r['t'] - t))


def main():
    r37 = {i: read_summary('r37-s%d' % i) for i in range(1, 6)}
    r36 = {i: read_summary('r36-s%d' % i) for i in range(1, 6)}

    out = []
    out.append('## M2  cols and spreads from the branch positions reader (tank: columns whose'
               ' centres are inside the circle; 100 live columns)')
    out.append('seed\tt\tr37 n\tr37 cols\tr37 x sd\tr37 z sd\tr36 n\tr36 cols\tr36 x sd\tr36 z sd')
    for i in range(1, 6):
        for t in TIMES:
            a, b = at(r37[i], t), at(r36[i], t)
            out.append('%d\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
                i, t, a['n'], a['cols'], a['x sd'], a['z sd'],
                b['n'], b['cols'], b['x sd'], b['z sd']))

    out.append('')
    out.append('## M2  branch cols, every 1000 s, r37')
    out.append('t\t' + '\t'.join('s%d' % i for i in range(1, 6)))
    for t in range(1000, 30001, 1000):
        out.append('%d\t' % t + '\t'.join(at(r37[i], t)['cols'] for i in range(1, 6)))

    out.append('')
    out.append('## M3  nearest-neighbour median, raw (same time, whatever the abundance)')
    out.append('seed\tt\tr37 n\tr37 nn h\tr37 nn 3d\tr36 n\tr36 nn h\tr36 nn 3d'
               '\tratio nn h\tratio nn 3d\tn ratio')
    for i in range(1, 6):
        for t in (5000, 30000):
            a, b = at(r37[i], t), at(r36[i], t)
            h7, th7 = val(a['nn h']); h6, th6 = val(b['nn h'])
            d7, _ = val(a['nn 3d']); d6, _ = val(b['nn 3d'])
            out.append('%d\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f' % (
                i, t, a['n'], a['nn h'], a['nn 3d'], b['n'], b['nn h'], b['nn 3d'],
                h7 / h6, d7 / d6, float(a['n']) / float(b['n'])))

    out.append('')
    out.append('## M3  nearest-neighbour median at MATCHED abundance: r37 anchored at the named'
               ' time, and the r36 sample of the same seed nearest in time whose n is within 10%'
               ' of r37\'s n at that time')
    out.append('seed\tanchor t\tr37 t\tr37 n\tr37 nn h\tr37 nn 3d\tr36 t\tr36 n\tr36 nn h'
               '\tr36 nn 3d\tn gap\tratio nn h\tratio nn 3d')
    for i in range(1, 6):
        for t in TIMES:
            a = at(r37[i], t)
            n7 = float(a['n'])
            cand = [r for r in r36[i] if abs(float(r['n']) - n7) <= 0.10 * n7]
            if not cand:
                out.append('%d\t%d\t%d\t%s\t%s\t%s\tno r36 sample within 10%% of n=%s'
                           % (i, t, a['t'], a['n'], a['nn h'], a['nn 3d'], a['n']))
                continue
            b = min(cand, key=lambda r: abs(r['t'] - t))
            h7, _ = val(a['nn h']); h6, _ = val(b['nn h'])
            d7, _ = val(a['nn 3d']); d6, _ = val(b['nn 3d'])
            out.append('%d\t%d\t%d\t%s\t%s\t%s\t%d\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f' % (
                i, t, a['t'], a['n'], a['nn h'], a['nn 3d'],
                b['t'], b['n'], b['nn h'], b['nn 3d'],
                (float(b['n']) - n7) / n7, h7 / h6, d7 / d6))

    text = '\n'.join(out) + '\n'
    open(os.path.join(HERE, 'nn-matched.tsv'), 'w', encoding='utf-8').write(text)
    print(text)


if __name__ == '__main__':
    main()
