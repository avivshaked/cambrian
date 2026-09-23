"""Reads a film's trace.tsv: per body and link, the frame-to-frame jumps in the solver's
position and rotation and in the view's transform and visual scale, and names the frames
where any of them jumps by more than its usual step.

    python scripts/film-trace.py scratch/films/r46-s1/5000/trace.tsv [--top 12]
"""
import csv, sys, math
from collections import defaultdict

path = sys.argv[1]
top = int(sys.argv[sys.argv.index('--top') + 1]) if '--top' in sys.argv else 12

rows = defaultdict(list)
with open(path, newline='') as f:
    for r in csv.DictReader(f, delimiter='\t'):
        rows[(int(r['id']), int(r['link']))].append(r)

def v(r, *keys):
    return [float(r[k]) for k in keys]

def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))

def qang(a, b):
    d = abs(sum(x * y for x, y in zip(a, b)))
    d = min(1.0, d)
    return 2 * math.degrees(math.acos(d))

report = []
for (id_, link), rs in rows.items():
    rs.sort(key=lambda r: int(r['frame']))
    for a, b in zip(rs, rs[1:]):
        fa, fb = int(a['frame']), int(b['frame'])
        dp = dist(v(a, 'px', 'py', 'pz'), v(b, 'px', 'py', 'pz'))
        dr = qang(v(a, 'rx', 'ry', 'rz', 'rw'), v(b, 'rx', 'ry', 'rz', 'rw'))
        dvp = dist(v(a, 'vx', 'vy', 'vz'), v(b, 'vx', 'vy', 'vz'))
        dvq = qang(v(a, 'qx', 'qy', 'qz', 'qw'), v(b, 'qx', 'qy', 'qz', 'qw'))
        ds = dist(v(a, 'sx', 'sy', 'sz'), v(b, 'sx', 'sy', 'sz'))
        dl = abs(float(a['lossy']) - float(b['lossy']))
        report.append((id_, link, fa, fb, dp, dr, dvp, dvq, ds, dl))

print('bodies traced:', len({k[0] for k in rows}), ' links:', len(rows), ' rows:', sum(len(r) for r in rows.values()))
for name, idx, unit in (('solver position', 4, 'm'), ('solver rotation', 5, 'deg'),
                        ('view part position', 6, 'm'), ('view part rotation', 7, 'deg'),
                        ('visual local scale', 8, ''), ('visual lossy scale', 9, '')):
    ranked = sorted(report, key=lambda t: -t[idx])[:top]
    vals = sorted(t[idx] for t in report)
    med = vals[len(vals) // 2] if vals else 0
    print('\n== %s: median frame step %.4g %s; largest steps:' % (name, med, unit))
    for t in ranked:
        if t[idx] <= 0: break
        print('  body %d link %d  frames %d->%d  %.4g %s' % (t[0], t[1], t[2], t[3], t[idx], unit))

# per-frame: how many links moved by more than 3x the median solver step, to see a cadence
med_p = sorted(t[4] for t in report)[len(report) // 2] if report else 0
by_frame = defaultdict(int)
for t in report:
    if t[4] > 3 * med_p + 1e-6: by_frame[t[3]] += 1
if by_frame:
    print('\nframes where a link moved > 3x the median step (%.4g m): ' % med_p +
          ', '.join('%d:%d' % (f, n) for f, n in sorted(by_frame.items())))
