"""The share of grazer fixes more than 20 m past every grove's edge, per window (the smell screen's mark 2, beside it).

Usage: python -I scripts/reads/smell/far-share.py RUN --from 12000 --to 24000 [--window 3000] [--far 20]
                                                  [--min-columns 5] [--all-groves] [--report FILE] [--json]

RUN is a run directory or an arm folder (whose latest dated run is read; a run that resumed another is read as one world
with it, through g2-read's loader). Reads positions.jsonl.gz, lineage and the report's island line. Steps nothing.

DEFINITIONS.
  grazer  a body born with attack and intake both 1 (g2-read's `mouthed`), not remains (flag 8). A FIX is one such body
          in one row of positions.jsonl.gz (every sampled second).
  grove   g2-read.make_groves, as plume-bench.py takes them: the disc of r_eff + 10 m round the centroid of each island of
          at least --min-columns columns (5) on the report's `island bloom` line. By default a grove counts at a second
          only if it holds at least one leaf then (a body with the photosynthetic flag 4 and not the absorptive flag 1,
          inside its disc), as plume-bench.py takes them: a leafless grove is no food place. With no grove holding a
          leaf, no fix is far. --all-groves counts the planned set at every second instead.
  edge distance  the fix's distance to a grove's centroid (each axis the shorter way round the joined box), less the
          grove's radius; negative inside it. The fix's distance to the nearest edge is the least over groves.
  far     the nearest edge is more than --far metres (20) away. The share is far fixes over all grazer fixes in the
          window [t0, t1) (the last window takes its end). Windows are --window seconds from --from to --to; with no
          --window, one span. A window judged on under 30 grazers (D244) is marked.
Exits 2 on a missing input. One line a window; --json prints one row each.
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


def main():
    ap = argparse.ArgumentParser(description='Share of grazer fixes far past every grove\'s edge.')
    ap.add_argument('run')
    ap.add_argument('--from', dest='t_from', type=float, required=True)
    ap.add_argument('--to', dest='t_to', type=float, required=True)
    ap.add_argument('--window', type=float, default=None)
    ap.add_argument('--far', type=float, default=20.0)
    ap.add_argument('--min-columns', type=int, default=5)
    ap.add_argument('--all-groves', action='store_true')
    ap.add_argument('--report')
    ap.add_argument('--json', action='store_true')
    a = ap.parse_args()

    g2 = load_g2()
    R = g2.load_reads()
    path = os.path.normpath(a.run)
    if not os.path.isdir(path):
        fail(path + ': not a directory')
    path = R['r61'].r59.resolve(path)
    ns = argparse.Namespace(end=a.t_to, year=6000.0, every=6000.0, window=500.0, upkeep_w=0.908, t_from=a.t_from,
                            bin=a.t_to - a.t_from)
    g = argparse.Namespace(end=a.t_to, t_from=a.t_from, window=a.t_to - a.t_from)
    rr = g2.RunRead(path, g, ns)
    w = rr.w
    if not w.segs or not R['r61'].r59.runrec.has_positions(w.segs[-1][0]):
        fail(w.dir + ': no positions file')
    islands, why = g2.read_islands_for(rr, a.report, None)
    if not islands:
        fail(why)
    cell = float(R['r61'].r59.find(w.config, 'fieldMatterCellMetres') or g2.CELL_DEFAULT)
    shape = R['r61'].r59.find(w.config, 'worldShape')
    area = R['r61'].r59.find(w.config, 'worldAreaSquareMetres')
    side = math.sqrt(float(area)) if (shape == 'Box' and area) else None
    groves = [x for x in g2.make_groves(islands, cell) if x['columns'] >= a.min_columns]
    if not groves:
        fail('no island of at least %d columns' % a.min_columns)

    if a.window:
        wins, t0 = [], a.t_from
        while t0 < a.t_to - 1e-9:
            wins.append((t0, min(t0 + a.window, a.t_to)))
            t0 += a.window
    else:
        wins = [(a.t_from, a.t_to)]
    far = [0] * len(wins)
    tot = [0] * len(wins)
    who = [set() for _ in wins]
    edge_d = [[] for _ in wins]
    last_i = len(wins) - 1
    for t, line in w.seg_positions(lambda t: t <= a.t_to + 1e-9):
        wi = next((n for n, win in enumerate(wins) if g2.in_window(t, win, n == last_i)), None)
        if wi is None:
            continue
        bodies = json.loads(line)['b']
        live = groves
        if not a.all_groves:
            live = [gr for gr in groves if any(
                (b[4] & 4) and not (b[4] & 1)
                and math.hypot(g2.gap(b[1], gr['cx'], side), g2.gap(b[3], gr['cz'], side)) <= gr['r'] for b in bodies)]
        for b in bodies:
            if b[0] not in rr.gset or b[4] & g2.REMAINS_FLAG:
                continue
            d = min((math.hypot(g2.gap(b[1], gr['cx'], side), g2.gap(b[3], gr['cz'], side)) - gr['r'] for gr in live),
                    default=float('inf'))
            tot[wi] += 1
            who[wi].add(b[0])
            if d != float('inf'):
                edge_d[wi].append(d)
            if d > a.far:
                far[wi] += 1
    name = w.name
    for n, (t0, t1) in enumerate(wins):
        row = dict(run=name, from_s=t0, to_s=t1, far_m=a.far, fixes=tot[n], grazers=len(who[n]), far_fixes=far[n],
                   far_share=far[n] / tot[n] if tot[n] else float('nan'),
                   median_edge_distance_m=statistics.median(edge_d[n]) if edge_d[n] else float('nan'),
                   groves=len(groves), under_30=len(who[n]) < 30)
        if a.json:
            print(json.dumps(row))
        else:
            print('%s  %g-%g s  far share %.4f (%d of %d grazer fixes, %d grazers; more than %g m past every one of %d %sgroves'
                  '\'s edge)  median edge distance %.1f m%s'
                  % (name, t0, t1, row['far_share'], far[n], tot[n], len(who[n]), a.far, len(groves), '' if a.all_groves else 'leafed ',
                     row['median_edge_distance_m'], '  (under 30 grazers: not judged, D244)' if row['under_30'] else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
