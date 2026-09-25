"""The gpu engine's acceptance reads (logbook/specs/gpu-port-spec.md section 4): two farm runs of
one world, compared the way the spec's three acceptances ask.

    python scripts/reads/gpu-accept.py <run A> <run B> [--runs-root DIR] [--every N]

A run is a run directory, or an arm name under --runs-root (default runs/), in which case the
newest run directory under it is read. What it prints:

  - each run's engine: `dynamics`, or `gpu` with its precision, device, group size and mean
    mode, the overflow counts, the bodies refused for their class and the neurons at 1e36;
  - each run's pace: simulated seconds, wall minutes, times real time and the wall split;
  - identity: the first stats.jsonl sample at which any field other than the wall clocks
    differs (every `wall*` field is left out: they time the machine, not the world), the first
    digest.jsonl row whose hash differs (when both runs wrote one), and whether lineage.jsonl
    and positions.jsonl are byte-equal, on the shorter run's lines when one run goes on longer
    (a 300 s check against a recorded seed: the prefix is the claim, as for the stats);
  - with --every N, alive, births, light, food, mean height and the audit at every Nth shared
    sample side by side, which is the read when the two are not meant to be identical (single
    against double).

Exit codes: 0 identical on everything both runs recorded; 1 a difference; 2 a usage or data
problem. A single-precision run against a double one exits 1 by design: the spec's reading of
that pair is the distributions, not identity.
"""
import argparse
import glob
import json
import os
import sys

WALL_PREFIX = 'wall'


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def run_dir(name, runs_root):
    if os.path.isfile(os.path.join(name, 'run.json')):
        return name
    arm = os.path.join(runs_root, name)
    candidates = sorted(d for d in glob.glob(os.path.join(arm, '*')) if os.path.isfile(os.path.join(d, 'run.json')))
    if not candidates:
        fail(f'{name}: neither a run directory nor an arm with one under {runs_root}')
    return candidates[-1]


def rows(path):
    out = []
    if not os.path.exists(path):
        return None
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                out.append(json.loads(line))
    return out


def describe(label, directory):
    m = json.load(open(os.path.join(directory, 'run.json'), encoding='utf-8'))
    print(f'{label}: {directory}')
    engine = m.get('engine')
    gpu = m.get('gpu')
    if gpu:
        print(f'  engine   gpu {gpu.get("precision")} on {gpu.get("device")} (driver {gpu.get("driver")}), '
              f'group {gpu.get("groupSize")}, mean {gpu.get("mean")}, classes {gpu.get("classLinks")} links / '
              f'{gpu.get("classNeurons")} neurons, compile {gpu.get("compileMs")} ms')
        print(f'  gpu      overflow candidates {gpu.get("overflowCandidates")} overlaps {gpu.get("overflowOverlaps")} '
              f'held {gpu.get("overflowHeld")} cell entries {gpu.get("overflowEntries")}; refused for class '
              f'{gpu.get("refusedForClass")}; neurons at 1e36 {gpu.get("neuronsAtCeiling")} '
              f'(most {gpu.get("neuronsAtCeilingMost")})')
    else:
        print(f'  engine   {engine} {m.get("engineVersion")}, threads {m.get("threads")}')
    src = m.get('source', {})
    print(f'  source   commit {str(src.get("gitCommit"))[:10]} dirty {src.get("gitDirty")} '
          f'dynamics {str(src.get("dynamicsHash"))[:12]} farm {str(src.get("farmHash"))[:12]} '
          f'config {m.get("configHash")}')
    print(f'  pace     {m.get("simulatedSeconds")} s of {m.get("requestedSeconds")} in '
          f'{m.get("wallClockMinutes")} min, {m.get("timesRealTime")} x real time, status {m.get("status")} '
          f'({m.get("reason")}), diverged {m.get("divergedTotal")}')
    return m


def wall_split(stats):
    if not stats:
        return None
    last = stats[-1]
    total = last.get('wallTotalMs') or 0
    if not total:
        return None
    parts = ['wallPhysicsMs', 'wallWorldMs', 'wallHarnessMs', 'wallWritersMs']
    return ', '.join(f'{p[4:-2].lower()} {100.0 * (last.get(p) or 0) / total:.0f}%' for p in parts)


def compare_stats(a, b):
    A = {r['t']: r for r in a}
    B = {r['t']: r for r in b}
    shared = sorted(set(A) & set(B))
    if not shared:
        fail('the two runs share no sample')
    only = len(set(A) ^ set(B))
    for i, t in enumerate(shared):
        r, q = A[t], B[t]
        keys = [k for k in set(r) | set(q) if not k.startswith(WALL_PREFIX)]
        diff = sorted(k for k in keys if r.get(k, '<absent>') != q.get(k, '<absent>'))
        if diff:
            prev = shared[i - 1] if i else 0
            print(f'stats    identical through t={prev}; first difference at t={t} in {len(diff)} fields, e.g. '
                  + ', '.join(f'{k}={r.get(k, "<absent>")}/{q.get(k, "<absent>")}' for k in diff[:5]))
            return False, shared
    print(f'stats    identical on all {len(shared)} shared samples to t={shared[-1]} '
          f'(wall clocks left out){"; " + str(only) + " samples in one run only" if only else ""}')
    return True, shared


def compare_digest(da, db):
    if da is None or db is None:
        print('digest   not written by both runs (EVOSIM_DIGEST_EVERY)')
        return True
    A = {r['step']: r for r in da}
    B = {r['step']: r for r in db}
    shared = sorted(set(A) & set(B))
    for i, s in enumerate(shared):
        if A[s]['hash'] != B[s]['hash'] or A[s]['bodies'] != B[s]['bodies']:
            prev = shared[i - 1] if i else 0
            print(f'digest   identical through step {prev}; parts at step {s} (t={A[s]["t"]}), '
                  f'bodies {A[s]["bodies"]}/{B[s]["bodies"]}')
            return False
    print(f'digest   identical on all {len(shared)} shared rows to step {shared[-1] if shared else 0}')
    return True


def compare_bytes(name, pa, pb):
    if not (os.path.exists(pa) and os.path.exists(pb)):
        print(f'{name:<8} not in both runs')
        return True
    with open(pa, 'rb') as fa, open(pb, 'rb') as fb:
        n = 0
        while True:
            la, lb = fa.readline(), fb.readline()
            n += 1
            if la != lb:
                if not la or not lb:
                    # A shorter run against a longer one of the same world (a 300 s check
                    # against a recorded seed): the prefix is the claim, as for the stats.
                    print(f'{name:<8} byte-equal on the shorter run\'s {n - 1} lines; the other goes on')
                    return True
                print(f'{name:<8} first differs at line {n}')
                return False
            if not la:
                print(f'{name:<8} byte-equal ({n - 1} lines)')
                return True


def side_by_side(a, b, shared, every):
    A = {r['t']: r for r in a}
    B = {r['t']: r for r in b}
    cols = ['alive', 'births', 'lightJoules', 'foodJoules', 'meanHeight', 'auditResidual']
    print(f'{"t":>8} ' + ' '.join(f'{c[:11] + " A":>13} {c[:11] + " B":>13}' for c in cols))
    for t in shared[::every]:
        r, q = A[t], B[t]
        cells = []
        for c in cols:
            for v in (r.get(c, '?'), q.get(c, '?')):
                cells.append(f'{v:>13.6g}' if isinstance(v, float) else f'{v:>13}')
        print(f'{t:>8} ' + ' '.join(cells))


def main():
    p = argparse.ArgumentParser(description='The gpu engine acceptance reads: two farm runs of one world.')
    p.add_argument('a')
    p.add_argument('b')
    p.add_argument('--runs-root', default='runs')
    p.add_argument('--every', type=int, default=0, help='print the side-by-side columns every Nth shared sample')
    args = p.parse_args()

    da, db = run_dir(args.a, args.runs_root), run_dir(args.b, args.runs_root)
    describe('A', da)
    describe('B', db)

    sa, sb = rows(os.path.join(da, 'stats.jsonl')), rows(os.path.join(db, 'stats.jsonl'))
    if not sa or not sb:
        fail('a run has no stats.jsonl rows')
    print(f'split A  {wall_split(sa)}')
    print(f'split B  {wall_split(sb)}')

    same, shared = compare_stats(sa, sb)
    same &= compare_digest(rows(os.path.join(da, 'digest.jsonl')), rows(os.path.join(db, 'digest.jsonl')))
    for name in ('lineage.jsonl', 'positions.jsonl'):
        same &= compare_bytes(name.split('.')[0], os.path.join(da, name), os.path.join(db, name))

    if args.every > 0:
        side_by_side(sa, sb, shared, args.every)

    return 0 if same else 1


if __name__ == '__main__':
    sys.exit(main())
