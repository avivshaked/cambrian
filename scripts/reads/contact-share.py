"""Mouth blows per grazer-second, per window, for one or more runs: the true-shape screen's contact share.

The true-shape screen (logbook/specs/r64t-prereg/predictions.md) asks how much less grazers bite when a mouth must touch
the true surface. This is that read. stats.jsonl's cumulative mouthBlows (every mouth's blows, so an upper bound on the
grazers' own) is differenced over each window, and divided by the grazer-seconds lived in it. A grazer is counted as
scripts/reads/bloom/r63g-motion/income.py counts one: a body born with atk > 0 and ink > 0, alive from its birth row
(lineage.jsonl "e":"b") to its death row ("e":"d") or to the end of the read. The rule is copied from that script (its
module cannot be imported: it runs on import), so change both together.

    python -I scripts/reads/contact-share.py [--from 6000] [--window 3000] [--end T] [--against RUN] RUN [RUN ...]

--end defaults to the last stats row of each run, so a live run is read to where it has got; a window not yet complete is
left out. The files are opened for reading only, which a running farm's writer allows. With --against, each window is also
printed as a ratio to the same window of that run (the run's rows are read the same way; a window absent from it prints
"-"). Prints one row per window and a summary per run (the whole span from --from). Exits 0, or 2 on bad arguments.
"""
import argparse
import json
import os
import sys


def jsonl(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    yield json.loads(line)
                except ValueError:
                    break  # a row the writer has not finished


def read_run(run, t0, window, end):
    """Returns (windows, last_t): windows is a list of (lo, hi, blows, grazer_seconds)."""
    born, died, graz = {}, {}, set()
    for r in jsonl(os.path.join(run, 'lineage.jsonl')):
        if r.get('e') == 'b':
            born[r['id']] = r['t']
            if r.get('atk', 0) > 0 and r.get('ink', 0) > 0:
                graz.add(r['id'])
        elif r.get('e') == 'd':
            died[r['id']] = r['t']
    blows = {}
    for r in jsonl(os.path.join(run, 'stats.jsonl')):
        if 'mouthBlows' in r:
            blows[round(r['t'])] = r['mouthBlows']
    if not blows:
        raise SystemExit(f'{run}: stats.jsonl holds no mouthBlows column')
    last_t = max(blows)
    stop = last_t if end is None else min(end, last_t)
    out = []
    lo = t0
    while lo + window <= stop + 1:
        hi = lo + window
        a, b = blows.get(round(lo)), blows.get(round(hi))
        if a is None or b is None:
            lo = hi
            continue
        s = 0.0
        for i in graz:
            x0, x1 = max(lo, born[i]), min(hi, died.get(i, stop))
            if x1 > x0:
                s += x1 - x0
        out.append((lo, hi, b - a, s))
        lo = hi
    return out, last_t


def rate(w):
    return w[2] / w[3] if w[3] > 0 else float('nan')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--from', dest='t0', type=float, default=6000.0)
    ap.add_argument('--window', type=float, default=3000.0)
    ap.add_argument('--end', type=float, default=None)
    ap.add_argument('--against', default=None)
    a = ap.parse_args()
    ref = None
    if a.against:
        ref = {w[0]: rate(w) for w in read_run(a.against, a.t0, a.window, a.end)[0]}
    for run in a.runs:
        ws, last_t = read_run(run, a.t0, a.window, a.end)
        print(f'== {run}  (stats to {last_t:.0f} s)')
        head = f'{"window":>13} | {"blows":>10} {"grazer-s":>11} | {"blows/g-s":>10}'
        if ref is not None:
            head += ' | ' + ('vs ' + os.path.basename(os.path.normpath(a.against))).rjust(16)
        print(head)
        tb = tg = 0.0
        for w in ws:
            line = f'{w[0]:>6.0f}-{w[1]:<6.0f} | {w[2]:>10.0f} {w[3]:>11.0f} | {rate(w):>10.5f}'
            if ref is not None:
                r0 = ref.get(w[0])
                line += f' | {(rate(w) / r0 if r0 else float("nan")):>16.3f}' if r0 is not None else f' | {"-":>16}'
            print(line)
            tb += w[2]
            tg += w[3]
        print(f'{"summary":>13} | {tb:>10.0f} {tg:>11.0f} | {(tb / tg if tg else float("nan")):>10.5f}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
