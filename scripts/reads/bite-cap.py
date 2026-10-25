"""What bites offered, kept and lost, per bin, from a contact-eating run's stats (D219-D226).

The stats carry the cumulative joules kept by biters' reserves (biteKeptLeaves, biteKeptOthers and biteKeptRemains,
summed as K) and the joules a bite offered that no reserve kept (biteWaste, W), so the offer is K + W. Of what a bite
keeps past its cap, the reserve gets 1 - intakeWasteFraction and the rest is waste, so W - K x f / (1 - f) is what
the cap cut plus what parts without intake bit: an upper bound on the cap's cut. The script reads f from the run's
config.json. biteStore is the store share's take (D224), cumulative.

    python -I bite-cap.py RUN [RUN ...] [--every 1500] [--end 4500]
"""
import argparse
import json
import os


def config_f(run):
    with open(os.path.join(run, 'config.json'), encoding='utf-8') as f:
        c = json.load(f)
    found = []

    def walk(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k == 'intakeWasteFraction':
                    found.append(v)
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)

    walk(c)
    if len(found) != 1:
        raise SystemExit(f'{run}: {len(found)} intakeWasteFraction fields in config.json, not one')
    return float(found[0])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--every', type=int, default=1500)
    ap.add_argument('--end', type=int, default=4500)
    a = ap.parse_args()
    for run in a.runs:
        f = config_f(run)
        marks = list(range(0, a.end + 1, a.every))
        at = {}
        with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as fh:
            for line in fh:
                r = json.loads(line)
                t = r['t']
                if t > a.end:
                    break
                if t in marks:
                    k = sum(float(r.get(n) or 0) for n in ('biteKeptLeaves', 'biteKeptOthers', 'biteKeptRemains'))
                    at[t] = (k, float(r.get('biteWaste') or 0), float(r.get('biteStore') or 0))
        at.setdefault(0, (0.0, 0.0, 0.0))
        print(f'{os.path.basename(os.path.dirname(os.path.normpath(run)))}  ({run}; intakeWasteFraction {f})')
        print('        bin       offered J      kept J   cut+no-intake J   cut share   store J')
        for lo, hi in zip(marks, marks[1:]):
            if lo not in at or hi not in at:
                continue
            k = at[hi][0] - at[lo][0]
            w = at[hi][1] - at[lo][1]
            s = at[hi][2] - at[lo][2]
            o = k + w
            cut = w - k * f / (1 - f)
            share = cut / o if o > 0 else float('nan')
            print(f'  {lo:>5}-{hi:<5} {o:>14.1f} {k:>11.1f} {cut:>17.1f} {share:>11.3f} {s:>9.1f}')
        print()


if __name__ == '__main__':
    main()
