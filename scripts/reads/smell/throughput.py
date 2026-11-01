"""Body-seconds per wall second, per world and summed over a card group, from the run's own rows (the smell screen's
mark 4, beside the pace).

Usage: python -I scripts/reads/smell/throughput.py RUN [RUN ...] [--from 12000 --to 24000 | --last 6000] [--json]
       python -I scripts/reads/smell/throughput.py --group g1=RUN,RUN,RUN --group g2=RUN,RUN,RUN [--last 6000]

A RUN is a run directory or an arm folder (whose latest dated run is read). RUNs given bare form one group, "all";
--group NAME=RUN,RUN,... names each group (repeatable). Reads stats.jsonl only, steps nothing.

DEFINITIONS (pace is scripts/reads/pace-calc.py's: the simulated seconds stepped from the span's first stats row to its
last, over the growth of the cumulative wallTotalMs between those rows; the first row's own wall is left out because it
carries the restore and the card's first block).
  span          all of the run's rows, or the rows with --from <= t <= --to, or with --last N the rows within N s of the
                last. The span's first row only starts the clock.
  pace          (t_last - t_first) / ((wallTotalMs_last - wallTotalMs_first) / 1000), world seconds per wall second.
  mean alive    the time-weighted mean of the `alive` column over the span: each row's count over the interval since the
                row before it (the span's first row only starts the clock).
  body-seconds per wall second   pace x mean alive, which is the sum of alive x dt over the span over its wall seconds.
  group         the sum of its worlds' body-seconds per wall second, and of their paces (the paces' sum is world-seconds
                per wall second, which is how much simulated time the group yields).
Worlds in a group are assumed to step concurrently, as a card group does; the sum is then the group's throughput. A
resumed run's stats are its own, so a resume is read from its own first row. Exits 2 on a missing input.
"""
import argparse
import glob
import json
import os
import sys


def fail(msg):
    print(msg, file=sys.stderr)
    sys.exit(2)


def resolve_run(path):
    path = os.path.normpath(path)
    if not os.path.isdir(path):
        fail(path + ': not a directory')
    if os.path.isfile(os.path.join(path, 'stats.jsonl')):
        return path
    subs = sorted(d for d in glob.glob(os.path.join(path, '*')) if os.path.isfile(os.path.join(d, 'stats.jsonl')))
    if not subs:
        fail(path + ': no run with a stats.jsonl')
    return subs[-1]


def read_rows(run):
    rows = []
    with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
        lines = f.read().splitlines()
    for n, line in enumerate(lines):
        if not line.strip():
            continue
        try:
            rows.append(json.loads(line))
        except ValueError:
            if n != len(lines) - 1:      # only a torn last row of a live run is forgiven
                fail('%s: row %d does not parse' % (run, n + 1))
    return rows


def world(run, a):
    rows = read_rows(run)
    for key in ('t', 'alive', 'wallTotalMs'):
        if rows and key not in rows[0]:
            fail('%s: stats rows have no %s column' % (run, key))
    if a.last is not None and rows:
        rows = [r for r in rows if r['t'] >= rows[-1]['t'] - a.last]
    else:
        rows = [r for r in rows if (a.t_from is None or r['t'] >= a.t_from) and (a.t_to is None or r['t'] <= a.t_to)]
    if len(rows) < 2:
        fail('%s: fewer than two stats rows in the span' % run)
    wall = (rows[-1]['wallTotalMs'] - rows[0]['wallTotalMs']) / 1000.0
    if wall <= 0:
        fail('%s: no wall time grows across the span' % run)
    stepped = rows[-1]['t'] - rows[0]['t']
    bs = sum(r['alive'] * (r['t'] - p['t']) for p, r in zip(rows, rows[1:]))
    pace = stepped / wall
    return dict(world=os.path.basename(os.path.dirname(run)), from_s=rows[0]['t'], to_s=rows[-1]['t'], pace=pace,
                mean_alive=bs / stepped, body_s_per_wall_s=bs / wall)


def main():
    ap = argparse.ArgumentParser(description='Body-seconds per wall second per world and per card group.')
    ap.add_argument('runs', nargs='*')
    ap.add_argument('--group', action='append', default=[], help='NAME=RUN,RUN,... (repeatable)')
    ap.add_argument('--from', dest='t_from', type=float)
    ap.add_argument('--to', dest='t_to', type=float)
    ap.add_argument('--last', type=float, help='only the last N simulated seconds of each run')
    ap.add_argument('--json', action='store_true')
    a = ap.parse_args()
    groups = {}
    for g in a.group:
        if '=' not in g:
            ap.error('--group wants NAME=RUN,RUN,...')
        name, runs = g.split('=', 1)
        groups[name] = [r for r in runs.split(',') if r]
    if a.runs:
        groups['all'] = list(a.runs)
    if not groups:
        ap.error('give RUNs or --group')
    if a.last is not None and (a.t_from is not None or a.t_to is not None):
        ap.error('--last and --from/--to do not mix')
    out = []
    for name, runs in groups.items():
        ws = [world(resolve_run(r), a) for r in runs]
        row = dict(group=name, worlds=ws, pace_sum=sum(w['pace'] for w in ws),
                   body_s_per_wall_s_sum=sum(w['body_s_per_wall_s'] for w in ws))
        out.append(row)
        if a.json:
            print(json.dumps(row))
        else:
            print('%s: %d worlds, world-s per wall-s %.2f, body-s per wall-s %.0f   %s'
                  % (name, len(ws), row['pace_sum'], row['body_s_per_wall_s_sum'],
                     '   '.join('%s [%g-%g s] pace %.3f mean alive %.0f body-s/wall-s %.0f'
                               % (w['world'], w['from_s'], w['to_s'], w['pace'], w['mean_alive'], w['body_s_per_wall_s'])
                               for w in ws)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
