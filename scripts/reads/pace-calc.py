"""The card's pace against the CPU's, from the runs of one sitting of scripts/card/pace-read.ps1 (or any farm
runs given by arm name): each run's pace and start-up, and the card over the CPU on the means and per pair.

The pace, fixed before the first sitting on 2026-10-08 (card-single-plan.md, clause 3): the simulated seconds stepped from
the run's first stats row to its last, over the stepping wall between those rows, which is the growth of the
row's cumulative wallTotalMs (the sum of the physics, world, harness and writer phases). The first row's
own wall is left out, because it carries the restore and the card's first block. Start-up is reported
apart: the run's whole wall (run.json wallClockMinutes) less the stepping wall, with the card's kernel
compile from run.json beside it.

    python -I scripts/reads/pace-calc.py <runs root> <arm> [<arm> ...]
"""
import glob
import json
import os
import statistics
import sys


def read(root, arm):
    runs = sorted(glob.glob(os.path.join(root, arm, '*', 'run.json')), key=os.path.getmtime)
    if not runs:
        raise SystemExit(f'{arm}: no run.json under {root}')
    run_dir = os.path.dirname(runs[-1])
    j = json.load(open(runs[-1], encoding='utf-8'))
    rows = [json.loads(line) for line in open(os.path.join(run_dir, 'stats.jsonl'), encoding='utf-8')]
    first, last = rows[0], rows[-1]
    stepped = last['t'] - first['t']
    step_wall = (last['wallTotalMs'] - first['wallTotalMs']) / 1000.0
    whole = j['wallClockMinutes'] * 60.0
    gpu = j.get('gpu') or {}
    return {
        'arm': arm, 'engine': j['engine'], 'status': f"{j['status']} ({j['reason']})",
        'from': first['t'], 'to': last['t'], 'alive': (first['alive'], last['alive']),
        'stepped': stepped, 'step_wall': step_wall, 'pace': stepped / step_wall,
        'whole': whole, 'startup': whole - step_wall, 'compile_s': (gpu.get('compileMs') or 0) / 1000.0,
    }


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    root, arms = sys.argv[1], sys.argv[2:]
    rs = [read(root, a) for a in arms]
    print(f"{'arm':<18} {'engine':<9} {'status':<16} {'rows t':<14} {'alive':<12} {'stepped':>7} {'step wall':>9} {'pace':>6} {'whole':>7} {'start-up':>8} {'compile':>7}")
    for r in rs:
        print(f"{r['arm']:<18} {r['engine']:<9} {r['status']:<16} {r['from']:>6}-{r['to']:<7} {r['alive'][0]:>5}-{r['alive'][1]:<6} "
              f"{r['stepped']:>7.0f} {r['step_wall']:>8.1f}s {r['pace']:>6.3f} {r['whole']:>6.1f}s {r['startup']:>7.1f}s {r['compile_s']:>6.1f}s")
    card = [r['pace'] for r in rs if r['engine'] == 'gpu']
    cpu = [r['pace'] for r in rs if r['engine'] == 'dynamics']
    if card and cpu:
        print(f"card pace {', '.join(f'{p:.3f}' for p in card)} (mean {statistics.mean(card):.3f}); "
              f"cpu pace {', '.join(f'{p:.3f}' for p in cpu)} (mean {statistics.mean(cpu):.3f})")
        print(f"card over cpu: {statistics.mean(card) / statistics.mean(cpu):.2f} on the means; "
              f"range {min(card) / max(cpu):.2f} to {max(card) / min(cpu):.2f} over every card-cpu pair")


if __name__ == '__main__':
    main()
