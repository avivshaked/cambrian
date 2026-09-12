# -*- coding: utf-8 -*-
"""M1's first candidate cause: is the dispersal disc clipped at the glass?

A child is placed within OffspringDispersalMetres of its parent, and the tank clips that
disc at the wall, so a rim parent's children can only be drawn inward. If that were what
piles bodies at the glass it would have to show up as births being at least as far out as
the standing crowd; if instead births sit further IN than the crowd, the placer is a source
of inward flux and cannot be what makes the crust.

The ring a child is born into is exact in lineage.jsonl (`pt`, TankGeometry.RingOf, four
rings of equal area). The standing crowd's rings are the report's p0..p3 at the samples in
the same window. Founders (k = f) are excluded: they are placed by the founding rule, not
by dispersal.
"""
import glob, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
from report_read import rows, num

WINDOW = 2000


def run_dir(arm):
    ds = [d for d in sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*'))) if os.path.isdir(d)]
    return ds[-1]


def main():
    out = ['## M1 candidate 1  the ring a child is born into (lineage `pt`, founders excluded)'
           ' against the ring the standing crowd is in (report p0..p3), in windows of %d s'
           % WINDOW]
    out.append('arm\twindow\tbirths\tb0\tb1\tb2\tb3\tbirth s3\tcrowd s3\tbirth s0+s1'
               '\tcrowd s0+s1\tbirth mean ring\tcrowd mean ring')
    summary = []
    for arm in ['r37-s%d' % i for i in range(1, 6)]:
        r, _ = rows(arm)
        live = {}
        for x in r:
            p = [num(x['p%d' % k]) for k in range(4)]
            live[x['t']] = p
        counts = {}
        for line in open(os.path.join(run_dir(arm), 'lineage.jsonl'), encoding='utf-8'):
            d = json.loads(line)
            if d['e'] != 'b' or d['k'] == 'f':
                continue
            wnd = int(d['t'] // WINDOW) * WINDOW
            c = counts.setdefault(wnd, [0, 0, 0, 0])
            c[d['pt']] += 1
        tot_b = [0, 0, 0, 0]
        tot_c = [0.0] * 4
        for wnd in sorted(counts):
            b = counts[wnd]
            nb = sum(b)
            cs = [0.0] * 4
            ns = 0
            for t, p in live.items():
                if wnd < t <= wnd + WINDOW:
                    ns += 1
                    for k in range(4):
                        cs[k] += p[k]
            if ns == 0 or nb == 0:
                continue
            ctot = sum(cs)
            for k in range(4):
                tot_b[k] += b[k]
                tot_c[k] += cs[k]
            bm = sum(k * b[k] for k in range(4)) / nb
            cm = sum(k * cs[k] for k in range(4)) / ctot
            out.append('%s\t%d-%d\t%d\t%d\t%d\t%d\t%d\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f' % (
                arm, wnd, wnd + WINDOW, nb, b[0], b[1], b[2], b[3],
                b[3] / nb, cs[3] / ctot, (b[0] + b[1]) / nb, (cs[0] + cs[1]) / ctot, bm, cm))
        nb, ctot = sum(tot_b), sum(tot_c)
        summary.append('%s\t%d\t%.3f\t%.3f\t%.3f\t%.3f' % (
            arm, nb, tot_b[3] / nb, tot_c[3] / ctot,
            sum(k * tot_b[k] for k in range(4)) / nb,
            sum(k * tot_c[k] for k in range(4)) / ctot))
    out.append('')
    out.append('## M1 candidate 1  whole run')
    out.append('arm\tbirths\tbirth rim share\tcrowd rim share\tbirth mean ring\tcrowd mean ring')
    out += summary
    text = '\n'.join(out) + '\n'
    open(os.path.join(HERE, 'dispersal.tsv'), 'w', encoding='utf-8').write(text)
    print('\n'.join(out[-8:]))


if __name__ == '__main__':
    main()
