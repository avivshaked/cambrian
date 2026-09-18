# -*- coding: utf-8 -*-
"""Round 40's positions read: where each guild floats, in metres of depth.

L1 asks whether the leaves rise into a band (their median depth and their third quartile of
depth); L3 asks whether the eaters take the dark column under it (the absorptive median at
least 10 m below the leaf median).  Both are read from positions.jsonl, which carries every
living body's id, x, y, z and its three guild flags at every 100 s.

The quartiles are quartiles of DEPTH, so "q3 depth" is the deep end of the crowd and equals
minus the 25th percentile of y — the same convention r39-read/depth-band.tsv prints as `q1 y`.

Writes depth-band.tsv and, for the round's own record, above3.tsv beside this file.
Run from anywhere: the repository root is found from this file's own path.
"""
import json, os, sys, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))

ARMS = ['r40-s1', 'r40-s2', 'r40-s3']
ABSORPTIVE, JOINTED, PHOTOSYNTHETIC = 1, 2, 4


def run_dir(arm):
    return sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*' + os.sep)))[-1]


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def q(v, p):
    """The p-quantile of a sorted-able list, by the same nearest-rank rule birth-depth.py uses."""
    if not v:
        return None
    v = sorted(v)
    return v[int(p * (len(v) - 1))]


def rows_at(arm, times):
    """{t: [(y, flags), ...]} for each wanted time, in one pass over the file."""
    want = set(times)
    out = {}
    with open(os.path.join(run_dir(arm), 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            if not line.startswith('{'):
                continue
            head = line[:32]
            if not any('"t":%d,' % t in head or '"t":%d.' % t in head for t in want):
                continue
            r = json.loads(line)
            t = int(r['t'])
            if t not in want:
                continue
            out[t] = [(b[2], int(b[4])) for b in r['b']]
    return out


def last_time(arm):
    t = None
    with open(os.path.join(run_dir(arm), 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            if line.startswith('{'):
                i = line.find('"t":')
                t = int(float(line[i + 4:line.find(',', i)]))
    return t


def main():
    hdr = ('## Depth by guild (positions.jsonl, one row per living body at every 100 s).'
           ' Depth is positive metres below the waterline. q1 is the shallow quartile and q3'
           ' the deep one, so L1\'s "third quartile shallower than 15 m" is the q3 depth'
           ' column. A guild is the flag the harness wrote: leaf = photosynthetic, stomach ='
           ' absorptive, mixo = both.')
    lines = [hdr, 'arm\tt\tguild\tn\tmedian depth m\tq1 depth\tq3 depth\tmean depth m'
                  '\tmin depth\tmax depth']
    a3 = ['## Recorded, not predicted: the share of the living above 3 m, the surface film.',
          'arm\tt\tliving\tabove 3 m\tshare\tleaves above 3 m\tleaf share\tstomachs above 3 m']
    for arm in ARMS:
        lt = last_time(arm)
        times = sorted({t for t in (1000, 5000, 10000, 15000) if t <= lt} | {lt})
        data = rows_at(arm, times)
        for t in times:
            bodies = data.get(t)
            if bodies is None:
                lines.append('%s\t%d\t-\t-\tno positions row\t-\t-\t-\t-\t-' % (arm, t))
                continue
            groups = [
                ('all', [y for y, fl in bodies]),
                ('leaf', [y for y, fl in bodies if fl & PHOTOSYNTHETIC]),
                ('stomach', [y for y, fl in bodies if fl & ABSORPTIVE]),
                ('mixo', [y for y, fl in bodies if (fl & ABSORPTIVE) and (fl & PHOTOSYNTHETIC)]),
                ('jointed', [y for y, fl in bodies if fl & JOINTED]),
            ]
            for name, ys in groups:
                if not ys:
                    lines.append('%s\t%d\t%s\t0\t-\t-\t-\t-\t-\t-' % (arm, t, name))
                    continue
                d = sorted(-y for y in ys)
                lines.append('%s\t%d\t%s\t%d\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f' % (
                    arm, t, name, len(d), q(d, .5), q(d, .25), q(d, .75),
                    sum(d) / len(d), d[0], d[-1]))
            n = len(bodies)
            lf = [y for y, fl in bodies if fl & PHOTOSYNTHETIC]
            st = [y for y, fl in bodies if fl & ABSORPTIVE]
            a3.append('%s\t%d\t%d\t%d\t%.4f\t%d\t%s\t%d' % (
                arm, t, n, sum(1 for y, fl in bodies if -y < 3),
                sum(1 for y, fl in bodies if -y < 3) / float(n),
                sum(1 for y in lf if -y < 3),
                ('%.4f' % (sum(1 for y in lf if -y < 3) / float(len(lf)))) if lf else '-',
                sum(1 for y in st if -y < 3)))
    write('depth-band.tsv', lines)
    write('above3.tsv', a3)
    print('wrote depth-band.tsv, above3.tsv')


if __name__ == '__main__':
    main()
