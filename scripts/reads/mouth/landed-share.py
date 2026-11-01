"""The landed share of a world's mouth blows under D275, per window (logbook/specs/r66-prereg/predictions.md, check 4 and
mark 4).

A mouth blow that would do damage either lands (the stats column `mouthBlows`) or is refused because the mouth was out of
reach (`mouthBlowsRefused`). Both are cumulative from the founding, or from a resume's own start. The landed share over a
window is the landed blows' rise over the rise of both.

    python -I scripts/reads/mouth/landed-share.py <run dir> [<run dir> ...] [--windows 0-1200,3000-6000,21000-24000]
"""
import argparse
import json
import os
import sys


def rows(run):
    with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def at_or_before(series, t):
    """The last row at or before t, or None."""
    best = None
    for r in series:
        if r['t'] <= t:
            best = r
        else:
            break
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--windows', default='')
    a = ap.parse_args()
    for run in a.runs:
        series = list(rows(run))
        if not series:
            print(f'{run}: no stats rows')
            continue
        if 'mouthBlowsRefused' not in series[-1]:
            print(f'{run}: no mouthBlowsRefused column; D275 is off or the build predates it')
            continue
        last = series[-1]['t']
        spans = a.windows.split(',') if a.windows else [f"0-{last:g}"]
        print(f'{run}  (to {last:g} s)')
        for span in spans:
            lo, hi = (float(x) for x in span.split('-'))
            r1 = at_or_before(series, hi)
            r0 = at_or_before(series, lo)
            if r1 is None:
                print(f'  {span:>13}  no row')
                continue
            land0 = r0['mouthBlows'] if r0 else 0
            ref0 = r0['mouthBlowsRefused'] if r0 else 0
            landed = r1['mouthBlows'] - land0
            refused = r1['mouthBlowsRefused'] - ref0
            total = landed + refused
            share = landed / total if total else float('nan')
            print(f'  {span:>13}  landed {landed:>9}  refused {refused:>9}  share {share:.3f}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
