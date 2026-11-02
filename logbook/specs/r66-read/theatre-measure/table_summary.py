"""The coordinator's breakdown of an onset - 1 table (ate_table2.py's --out TSV), c8 2026-11-01: for leaf kills, leaf cuts and wounds
on animals, how many onsets read touch (any part), mouth touch (a touch with gap/radius at most 1) and far; and among the part
touches that are not mouth touches, gap/radius at the median, p75 and p90.

    python -I -B table_summary.py <table.tsv>
"""
import csv, sys

rows = list(csv.DictReader(open(sys.argv[1], encoding='utf-8'), delimiter='\t'))


def q(values, p):
    s = sorted(values)
    return s[min(len(s) - 1, int(round(p * (len(s) - 1))))] if s else float('nan')


groups = [('leaf kills', 'kill', 'leaf'), ('leaf cuts', 'cut', 'leaf'), ('wounds on animals', 'wound', 'animal'),
          ('animal kills', 'kill', 'animal'), ('leaf wounds', 'wound', 'leaf')]
print('rows', len(rows))
for name, match, kind in groups:
    g = [r for r in rows if r['match'] == match and r['kind'] == kind]
    if not g: print('%-18s none' % name); continue
    touch = [r for r in g if r['verdict'].startswith('touch')]
    mouth = [r for r in touch if r['mouth_touch'] == 'True']
    far = [r for r in g if r['verdict'].startswith('far')]
    other = len(g) - len(touch) - len(far)
    gaps = [float(r['gap_radius']) for r in touch if r['mouth_touch'] != 'True' and r['gap_radius'] not in ('', 'None')]
    print('%-18s %4d onsets: touch %d (mouth touch %d, part only %d), far %d%s; part-only gap/radius median %.2f p75 %.2f p90 %.2f' % (
        name, len(g), len(touch), len(mouth), len(touch) - len(mouth), len(far), (', other %d' % other) if other else '',
        q(gaps, 0.5), q(gaps, 0.75), q(gaps, 0.9)))
