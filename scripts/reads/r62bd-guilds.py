"""The r62bd screen's per-world tallies beside r62-read (logbook/specs/r62bd-prereg/predictions.md).

For each run directory named, from its stats.jsonl alone:
  - the share of a mouth's blows that finish the part they touch, per 3,000 s bin, from the cumulative
    columns mouthBlows and mouthBlowsFinishing (the bin's own blows, not the running total);
  - the peak population against the ceiling of 25,000, and the second it came;
  - the guild mix at 12,000 and 24,000 s: alive, leaves (photosynthetic), absorbers (absorptive), jointed, and
    the non-photosynthetic, which is the grazers' upper bound.
A run without the mouth-blow columns (a control) prints its population and guilds only.

  python -I scripts/reads/r62bd-guilds.py <run dir> [<run dir> ...] [--bin 3000] [--end 24000]
"""
import argparse
import json
import os


def rows(run):
    with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def at(rs, second):
    best = None
    for r in rs:
        if r['t'] <= second + 1e-6:
            best = r
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--bin', type=int, default=3000)
    ap.add_argument('--end', type=int, default=24000)
    a = ap.parse_args()

    for run in a.runs:
        rs = [r for r in rows(run) if r['t'] <= a.end + 1e-6]
        name = os.path.basename(os.path.dirname(os.path.normpath(run)))
        print(f'== {name} ({run})')
        peak = max(rs, key=lambda r: r['alive'])
        print(f'peak alive {peak["alive"]} at {peak["t"]:.0f} s (ceiling 25,000)')

        for second in (12000, 24000):
            r = at(rs, second)
            if r is None:
                continue
            print(f'at {r["t"]:.0f} s: alive {r["alive"]}, leaves {r["photosynthetic"]}, '
                  f'absorbers {r.get("absorptive")}, jointed {r.get("jointed")}, '
                  f'non-photosynthetic {r["alive"] - r["photosynthetic"]}')

        if 'mouthBlows' not in rs[-1]:
            print('no mouth-blow columns (the dial at 1): finishing share not read')
            continue

        print(f'bin (s)\tblows\tfinishing\tshare')
        last_b = last_f = 0
        edge = a.bin
        for r in rs:
            if r['t'] + 1e-6 >= edge:
                b, f = r['mouthBlows'], r['mouthBlowsFinishing']
                db, df = b - last_b, f - last_f
                share = f'{df / db:.4f}' if db > 0 else 'n/a'
                print(f'{edge - a.bin}-{edge}\t{db}\t{df}\t{share}')
                last_b, last_f = b, f
                edge += a.bin
                if edge > a.end:
                    break


if __name__ == '__main__':
    main()
