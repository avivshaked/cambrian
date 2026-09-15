# -*- coding: utf-8 -*-
"""The depth band of round 37b's ten seeds at 5,000, 15,000 and 30,000 s, computed the same
way pos_analyse.py computes round 38's, so that D3's two-sided reading has a control. Only
the three anchors are read, so each positions.jsonl is streamed once and nothing else.
Seed 5 of the first draw ends at 29,200 s and is read there for the 30,000 s anchor.
"""
import glob, json, math, os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
HERE = os.path.dirname(os.path.abspath(__file__))
ARMS = ['r37b-s%d' % i for i in range(1, 11)]
TIMES = [5000, 15000, 30000]
ABSORPTIVE, PHOTOSYNTHETIC = 1, 4


def run_dir(arm):
    ds = [d for d in sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*'))) if os.path.isdir(d)]
    return ds[-1]


def q(v, p):
    if not v:
        return None
    s = sorted(v)
    i = p * (len(s) - 1)
    lo = int(math.floor(i))
    hi = min(lo + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (i - lo)


out = ['## The depth band of round 37b\'s ten seeds at the three anchors, computed exactly as'
       ' round 38\'s is in pos_analyse.py. IQR is |q75 - q25| in metres.',
       'arm\tt\tn\tmedian y all\tIQR all\tsd y all\tn absorptive\tmedian y abs\tIQR abs'
       '\tn leaf\tmedian y leaf\tIQR leaf\tabs median - leaf median']
for arm in ARMS:
    p = os.path.join(run_dir(arm), 'positions.jsonl')
    if not os.path.exists(p):
        out.append('%s\t-\t(no positions.jsonl)' % arm)
        continue
    best = {t: (None, None) for t in TIMES}
    with open(p, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{"t":'):
                continue
            row = json.loads(line)
            for t in TIMES:
                d = abs(row['t'] - t)
                if best[t][0] is None or d < best[t][0]:
                    best[t] = (d, row)
    for t in TIMES:
        row = best[t][1]
        if row is None:
            continue
        bodies = row['b']
        ys = [b[2] for b in bodies]
        ya = [b[2] for b in bodies if b[4] & ABSORPTIVE]
        yl = [b[2] for b in bodies if (b[4] & PHOTOSYNTHETIC) and not (b[4] & ABSORPTIVE)]
        m = sum(ys) / len(ys)
        sd = math.sqrt(sum((x - m) ** 2 for x in ys) / len(ys))
        out.append('%s\t%d\t%d\t%.2f\t%.2f\t%.2f\t%d\t%s\t%s\t%d\t%s\t%s\t%s' % (
            arm, row['t'], len(ys), q(ys, 0.5), abs(q(ys, 0.75) - q(ys, 0.25)), sd,
            len(ya), '%.2f' % q(ya, 0.5) if ya else '-',
            '%.2f' % abs(q(ya, 0.75) - q(ya, 0.25)) if ya else '-',
            len(yl), '%.2f' % q(yl, 0.5) if yl else '-',
            '%.2f' % abs(q(yl, 0.75) - q(yl, 0.25)) if yl else '-',
            '%.2f' % (q(ya, 0.5) - q(yl, 0.5)) if (ya and yl) else '-'))
    print('done', arm)

open(os.path.join(HERE, 'depth-band-37b.tsv'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
