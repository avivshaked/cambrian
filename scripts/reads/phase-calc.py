"""Where a farm run's wall goes, phase by phase, from one run of a WORLD_PHASE_PROBE build of the farm
(Evosim.Core's WorldPhaseProbe: build with -p:WorldPhaseProbe=1), on either engine, e.g.
scripts/card/card-run.ps1 -Exe <that build's Evosim.Farm.exe>. The probe's lines go to the farm's stderr.

Two sources, both over the same stretch of stats rows:
  - the farm's own phases from stats.jsonl: wallPhysicsMs, wallWorldMs, wallHarnessMs, wallWritersMs and the
    physics step's grid, water, bodies, commit and after parts, each the growth from the first row to the
    last, divided by the simulated seconds between them (ms of wall per simulated second);
  - the probe's world phases from the farm's stderr (scratch/logs/<arm>.err): each line is the mean ms a
    metabolic step (0.5 s) over the 40 steps of 20 simulated seconds before it. The lines whose 20 s fall
    inside the stats stretch are averaged, a phase missing from a line counting 0, and doubled to ms per
    simulated second.

The share of each phase is its ms per simulated second over the stepping wall per simulated second, which
is the stretch's wallTotalMs growth over its seconds (the pace's own wall; reads/pace-calc.py). The probe's total
is checked against the farm's wallWorldMs over the same stretch: the probe's timers add a little.

    python -I scripts/reads/phase-calc.py <runs root> <arm> <err log>
"""
import glob
import json
import os
import re
import sys

FARM = ['wallPhysicsMs', 'wallWorldMs', 'wallHarnessMs', 'wallWritersMs']
PHYSICS = ['wallStepGridMs', 'wallStepWaterMs', 'wallStepBodiesMs', 'wallStepCommitMs', 'wallStepAfterMs']
LINE = re.compile(r'^world-probe, ms a step over (\d+) steps at (\d+) bodies: ([0-9.]+), (.*)$')


def main():
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    root, arm, err = sys.argv[1:]
    runs = sorted(glob.glob(os.path.join(root, arm, '*', 'run.json')), key=os.path.getmtime)
    if not runs:
        raise SystemExit(f'{arm}: no run.json under {root}')
    run_dir = os.path.dirname(runs[-1])
    j = json.load(open(runs[-1], encoding='utf-8'))
    rows = [json.loads(line) for line in open(os.path.join(run_dir, 'stats.jsonl'), encoding='utf-8')]
    first, last = rows[0], rows[-1]
    secs = last['t'] - first['t']
    wall = (last['wallTotalMs'] - first['wallTotalMs']) / secs
    print(f"{arm}: engine {j['engine']}, {j['status']} ({j['reason']}); rows t {first['t']}-{last['t']}, "
          f"alive {first['alive']}-{last['alive']}; {secs:.0f} s stepped over {wall * secs / 1000:.1f} s of wall; "
          f"pace {1000 / wall:.3f}; stepping wall {wall:.1f} ms per simulated second")

    print('\nthe farm\'s phases, ms per simulated second (share of the stepping wall)')
    for k in FARM + PHYSICS:
        v = (last.get(k, 0) - first.get(k, 0)) / secs
        print(f"  {'  ' if k in PHYSICS else ''}{k[4:-2]:<16} {v:8.1f}  {100 * v / wall:5.1f}%")

    # The probe: one line every 20 simulated seconds from the resume point; keep those inside the stretch.
    at0 = float((j.get('resumedFrom') or {}).get('seconds') or 0)
    lines = [l.strip() for l in open(err, encoding='utf-8', errors='replace') if l.startswith('world-probe')]
    parsed = []
    for i, l in enumerate(lines):
        m = LINE.match(l)
        if not m:
            continue
        steps = int(m.group(1))
        end_t = at0 + (i + 1) * steps * 0.5
        phases = {'(world total)': float(m.group(3))}
        for part in m.group(4).split(', '):
            name, _, v = part.rpartition(' ')
            phases[name] = float(v)
        parsed.append((end_t - steps * 0.5, end_t, int(m.group(2)), phases))
    inside = [p for p in parsed if p[0] >= first['t'] - 1e-6 and p[1] <= last['t'] + 1e-6]
    print(f"\nthe probe: {len(parsed)} lines, {len(inside)} inside the stretch "
          f"({inside[0][0]:.0f}-{inside[-1][1]:.0f} s, bodies {inside[0][2]}-{inside[-1][2]})" if inside else
          f"\nthe probe: {len(parsed)} lines, none inside the stretch (resume point read as {at0})")
    if not inside:
        return
    names = sorted({n for p in inside for n in p[3]})
    mean = {n: 2 * sum(p[3].get(n, 0.0) for p in inside) / len(inside) for n in names}
    world = (last['wallWorldMs'] - first['wallWorldMs']) / secs
    print(f"probe's world total {mean['(world total)']:.1f} ms per simulated second against the farm's "
          f"wallWorldMs {world:.1f} ({100 * mean['(world total)'] / world:.0f}%)")
    print('\nthe host\'s world phases, ms per simulated second, sorted (share of the stepping wall)')
    for n in sorted(mean, key=lambda n: -mean[n]):
        if n == '(world total)':
            continue
        print(f"  {n:<32} {mean[n]:8.1f}  {100 * mean[n] / wall:5.1f}%")


if __name__ == '__main__':
    main()
