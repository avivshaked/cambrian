"""The gripping grazer control's grip read (logbook/specs/grazer-grip-control-plan.md), beside r58g-read.py and
r58g-reach.py, which read admissions, deaths, kills, counters and reach as they did for round 58's control.
Usage: python -I r59g-grip-read.py <grazer run dir> [--still-m 0.015] [--near-m 1.5] [--every N]
The record keeps no per-body grip row, so a grip is read from where a body is. A gripping body is held to its
point by a spring, and positions are written to the centimetre; a body in round 57's current drifts at about
0.1 m/s. So a grazer is "still" at a sample when its centre moved no more than --still-m since the sample before.
Prints:
  per grazer of the line: planted at, first still at and its y, the share of its sampled life spent still, its
    death and age, and its kills;
  per sample (every Nth): the line alive, still, and still within the leaves' band (y from -5.5 to -1.5 m, the cap
    tops' 2 to 4 m with a body's height above), the leaves' mean y, and the leaves within --near-m of a still
    grazer against those near a moving one, per grazer;
  the gate's reading at the last sample: still grazers of the planted count;
  kills by the line, split by whether the killer was still at the sample before the kill;
  the world's grips at each checkpoint (ckpt-grip.py's body_grips): bodies that can grip, held, formed, broken.
    These count every body with the cell, the world's own as well as the grazers.
Reads the files only; writes nothing.
"""
import argparse
import glob
import importlib.util
import json
import math
import os
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'scripts', 'reads'))
import runrec  # noqa: E402


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_pr = _load('positions_read', os.path.join(ROOT, 'scripts', 'positions-read.py'))
_ck = _load('ckpt_grip', os.path.join(ROOT, 'logbook', 'specs', 'r59-build', 'ckpt-grip.py'))


def rows(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('run')
    ap.add_argument('--still-m', type=float, default=0.015)
    ap.add_argument('--near-m', type=float, default=1.5)
    ap.add_argument('--every', type=int, default=1)
    a = ap.parse_args()

    lineage = list(rows(os.path.join(a.run, 'lineage.jsonl')))
    births = [r for r in lineage if r.get('e') == 'b']
    inoc = [r for r in births if r.get('k') == 'i']
    line = {r['id'] for r in inoc}
    born = {r['id']: r for r in births}
    for r in births:
        if r.get('p') in line:
            line.add(r['id'])
    deaths = {r['id']: r for r in lineage if r.get('e') == 'd' and r['id'] in line}
    kills = [r for r in lineage if r.get('e') == 'k' and r.get('by') in line]
    print(f'run {a.run}')
    print(f'{len(inoc)} inoculants planted, {len(line)} in the line; still means moved <= {a.still_m} m between samples')

    last = {}          # id -> (x, y, z) at the sample before
    still_at = {}      # id -> set of sample times it was still
    sampled = {}       # id -> samples it was seen in after its first
    first_still = {}   # id -> (t, y)
    planted = {}       # id -> (t, y) at its first sample
    still_by_t = {}    # t -> ids still at t
    print('== per sample: line alive | still | still in the band | leaves mean y | leaves near a still grazer, '
          'and near a moving one, per grazer')
    near2 = a.near_m * a.near_m
    times = []
    for i, text in enumerate(l for l in runrec.positions_lines(a.run) if l.strip()):
        row = json.loads(text)
        t = row['t']
        times.append(t)
        bodies = row['b']
        leaves = [(b[1], b[2], b[3]) for b in bodies if _pr.guild_of(b[4]) == 'leaf']
        mine = [b for b in bodies if b[0] in line]
        still_now, moving_now = [], []
        for b in mine:
            p = (b[1], b[2], b[3])
            if b[0] not in planted:
                planted[b[0]] = (t, b[2])
            elif b[0] in last:
                sampled[b[0]] = sampled.get(b[0], 0) + 1
                if math.dist(p, last[b[0]]) <= a.still_m:
                    still_now.append(p)
                    still_at.setdefault(b[0], set()).add(t)
                    first_still.setdefault(b[0], (t, b[2]))
                else:
                    moving_now.append(p)
            last[b[0]] = p
        still_by_t[t] = {b[0] for b in mine if t in still_at.get(b[0], ())}
        band = sum(1 for p in still_now if -5.5 <= p[1] <= -1.5)

        def near(points):
            if not points:
                return '-'
            n = sum(1 for g in points for q in leaves
                    if (g[0] - q[0]) ** 2 + (g[1] - q[1]) ** 2 + (g[2] - q[2]) ** 2 <= near2)
            return f'{n / len(points):.2f}'

        if i % a.every == 0:
            ly = f'{statistics.mean(q[1] for q in leaves):.2f}' if leaves else '-'
            print(f'  t {t}: {len(mine)} | {len(still_now)} | {band} | {ly} | {near(still_now)} and {near(moving_now)}')

    print('== per grazer: planted t, y | first still t, y | still share | death | age | kills')
    for r in sorted(inoc, key=lambda r: r['id']):
        g = r['id']
        pt = planted.get(g)
        fs = first_still.get(g)
        share = len(still_at.get(g, ())) / sampled[g] if sampled.get(g) else 0.0
        d = deaths.get(g)
        age = (d['t'] - born[g]['t']) if d else None
        k = sum(1 for x in kills if x.get('by') == g)
        print(f"  id {g}: {pt[0] if pt else '-'}, {pt[1]:.2f} | " if pt else f"  id {g}: - | ", end='')
        print(f"{fs[0]}, {fs[1]:.2f} | " if fs else '- | ', end='')
        print(f"{share:.2f} | {(d['t'], d.get('c')) if d else 'alive'} | {age} | {k}")

    if times:
        t_end = times[-1]
        print(f'== the gate at t {t_end}: {len(still_by_t.get(t_end, ()))} still of {len(inoc)} planted')

    print('== kills by the line: the killer still at the sample before the kill, or not')
    for x in kills:
        before = [t for t in times if t <= x['t']]
        tb = before[-1] if before else None
        s = tb is not None and x['by'] in still_by_t.get(tb, ())
        print(f"  t {x['t']} by {x['by']} ({'still' if s else 'moving'} at {tb}): victim {x['id']}, root {x.get('root')}, "
              f"parts {x.get('parts')}, tj {x.get('tj')}")
    held = sum(1 for x in kills if any(t <= x['t'] for t in times) and
               x['by'] in still_by_t.get(max(t for t in times if t <= x['t']), ()))
    print(f'  {len(kills)} kills, {held} by a still grazer')

    print('== the world\'s grips at each checkpoint (every body with the cell)')
    for ck in sorted(glob.glob(os.path.join(a.run, 'checkpoints', '*.ckpt'))):
        bodies, held_n, formed, broken, seconds = _ck.body_grips(_ck.payload(ck))
        print(f'  {os.path.basename(ck)}: {bodies} can grip, {held_n} held, {formed} formed, {broken} broken, '
              f'{seconds:.1f} link-seconds held')


if __name__ == '__main__':
    main()
