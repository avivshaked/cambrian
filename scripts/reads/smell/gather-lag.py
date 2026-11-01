"""The gather lag as one number: how long after an island's bloom its grazers have gathered (the smell screen's mark 3).

Usage: python -I scripts/reads/smell/gather-lag.py RUN --from 12000 --to 24000 [--json] [--report FILE]
                                                   [--min-columns 5] [--min-r 0.3]

RUN is a run directory or an arm folder (whose latest dated run is read; a run that resumed another is read as one world
with it, through g2-read's own loader). Reads files only, steps nothing.

DEFINITION. It is g2-read.py's crowd lag (scripts/reads/bloom/g2-read.py, section 5, "crowds at blooms"), imported rather
than restated: for each island, the number of grazers inside its grove (a grazer is a body born with attack and intake
both 1, g2-read's `mouthed`; the grove is the disc of r_eff + 10 m round the island; remains are left out) at each stats
sample is correlated (Pearson) with the island's bloomFactor at lags of 0 to 3,000 s, the crowd lagging the bloom, over
samples from --from to --to. The best lag is the one with the largest r. The number printed is the MEDIAN of the best
lags over the islands of at least --min-columns columns (5) whose best r reaches --min-r (0.3, g2-read's CROWD_R).
Islands under either limit are listed and left out. Needs positions.jsonl.gz, the stats' bloomFactor and the report's
`island bloom` line (found as g2-read finds it). Exits 2 on a missing input. Prints NaN when no island qualifies.

One line for the span; --json prints one JSON row with the per-island lags.
"""
import argparse
import importlib.util
import json
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))


def fail(msg):
    print(msg, file=sys.stderr)
    sys.exit(2)


def load_g2():
    path = os.path.join(REPO, 'scripts', 'reads', 'bloom', 'g2-read.py')
    if not os.path.isfile(path):
        fail(path + ': missing')
    spec = importlib.util.spec_from_file_location('g2_read', path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def resolve(g2, path):
    path = os.path.normpath(path)
    if not os.path.isdir(path):
        fail(path + ': not a directory')
    return g2.load_reads()['r61'].r59.resolve(path)


def main():
    ap = argparse.ArgumentParser(description='Median best lag between an island bloom and its grazer crowd.')
    ap.add_argument('run')
    ap.add_argument('--from', dest='t_from', type=float, required=True)
    ap.add_argument('--to', dest='t_to', type=float, required=True)
    ap.add_argument('--min-columns', type=int, default=5)
    ap.add_argument('--min-r', type=float, default=0.3)
    ap.add_argument('--report')
    ap.add_argument('--json', action='store_true')
    a = ap.parse_args()

    g2 = load_g2()
    R = g2.load_reads()
    path = resolve(g2, a.run)
    ns = argparse.Namespace(end=a.t_to, year=6000.0, every=6000.0, window=500.0, upkeep_w=0.908, t_from=a.t_from,
                            bin=a.t_to - a.t_from)
    g = argparse.Namespace(end=a.t_to, t_from=a.t_from, window=a.t_to - a.t_from)
    rr = g2.RunRead(path, g, ns)
    w = rr.w
    if not any('bloomFactor' in s for s in rr.stats):
        fail(w.dir + ': stats carry no bloomFactor column (no bloom)')
    if not w.segs or not R['r61'].r59.runrec.has_positions(w.segs[-1][0]):
        fail(w.dir + ': no positions file')
    islands, why = g2.read_islands_for(rr, a.report, None)
    if not islands:
        fail(why)
    cell = float(R['r61'].r59.find(w.config, 'fieldMatterCellMetres') or g2.CELL_DEFAULT)
    shape = R['r61'].r59.find(w.config, 'worldShape')
    area = R['r61'].r59.find(w.config, 'worldAreaSquareMetres')
    side = math.sqrt(float(area)) if (shape == 'Box' and area) else None
    all_groves = g2.make_groves(islands, cell)
    groves = [x for x in all_groves if x['columns'] >= a.min_columns]
    if not groves:
        fail('no island of at least %d columns' % a.min_columns)

    # the crowd in each grove at each positions row, as g2-read's section 5 counts it (nearest centroid wins a point
    # inside two groves, so every grove is passed to assign_grove, and the small ones' counts are dropped afterwards)
    index = {x['k']: n for n, x in enumerate(all_groves)}
    crowd = {}
    for t, line in w.seg_positions(lambda t: t <= rr.end + 1e-9):
        counts = [0] * len(all_groves)
        for b in json.loads(line)['b']:
            if b[0] not in rr.gset or b[4] & g2.REMAINS_FLAG:
                continue
            k = g2.assign_grove(b[1], b[3], all_groves, side)
            if k >= 0:
                counts[k] += 1
        crowd[t] = counts

    step = rr.step
    idx = [int(round(s['t'] / step)) for s in rr.stats]
    n_idx = max(idx) + 1
    lo_i = int(math.ceil(a.t_from / step - 1e-9))
    hi_i = int(round(rr.end / step))
    per = []
    for gr in groves:
        k, n = gr['k'], index[gr['k']]
        x = [None] * n_idx
        y = [None] * n_idx
        for s, i in zip(rr.stats, idx):
            if 'bloomFactor' in s and k < len(s['bloomFactor']):
                x[i] = s['bloomFactor'][k]
            c = crowd.get(s['t'])
            if c is not None:
                y[i] = c[n]
        lag, r = g2.best_lag(x, y, step, lo_i, hi_i)
        per.append(dict(island=k, columns=gr['columns'], lag=lag, r=r,
                        counted=lag is not None and r is not None and r >= a.min_r))
    used = [p for p in per if p['counted']]
    med = statistics.median(p['lag'] for p in used) if used else float('nan')
    row = dict(run=w.name, from_s=a.t_from, to_s=a.t_to, median_lag_s=med, islands_counted=len(used),
               islands_considered=len(per), per_island=per)
    if a.json:
        print(json.dumps(row))
    else:
        print('%s  %g-%g s  gather lag median %s s over %d of %d islands (>= %d columns, r >= %g)  per island: %s'
              % (w.name, a.t_from, a.t_to, '%.0f' % med if used else 'nan', len(used), len(per), a.min_columns, a.min_r,
                 '  '.join('%d:%s/%s%s' % (p['island'], '-' if p['lag'] is None else '%g' % p['lag'],
                                            '-' if p['r'] is None else '%.2f' % p['r'], '' if p['counted'] else '(out)')
                           for p in per)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
