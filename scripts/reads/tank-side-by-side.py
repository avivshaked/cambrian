"""Tank arms side by side at equal generations (D172): each arm's tank-generations.jsonl, cut to the generations every arm
swam, so that an arm that stopped early is compared with the others where they stood at the same generation.

    python scripts/reads/tank-side-by-side.py <run folder> <run folder> [...] [--window 5]

Prints, a generation a line, each arm's mean energy eaten, the same averaged over the last --window generations (the
curve D172's stop reads), its top's search score, and its kills and meals per swimmer; then each arm's last generation
and whether it stopped on the rule or at its cap.
"""
import argparse
import json
import math
import os


def rows(folder):
    with open(os.path.join(folder, 'tank-generations.jsonl'), encoding='utf-8') as f:
        return [json.loads(line) for line in f if line.strip()]


def smoothed(values, g, window):
    if g < window - 1:
        return math.nan
    part = values[g - window + 1:g + 1]
    return math.nan if any(v is None for v in part) else sum(part) / window


def num(v, width=7, places=1):
    return ('%*.*f' % (width, places, v)) if v is not None and not (isinstance(v, float) and math.isnan(v)) else ' ' * (width - 1) + '-'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--window', type=int, default=5)
    a = ap.parse_args()

    arms = [(os.path.basename(os.path.normpath(r)), rows(r)) for r in a.runs]
    common = min(len(r) for _, r in arms)
    print('generations 0 to %d, which every arm swam; per arm: eaten, eaten over %d, top score, kills, meals' % (common - 1, a.window))
    print('gen  ' + '  '.join('%-40s' % name for name, _ in arms))
    curves = [[row.get('eaten') for row in r] for _, r in arms]
    for g in range(common):
        cells = []
        for (name, r), curve in zip(arms, curves):
            row = r[g]
            cells.append('%s %s %s %s %s' % (num(row.get('eaten')), num(smoothed(curve, g, a.window)), num(row['top']['score']),
                                              num(row.get('kills'), 5, 2), num(row.get('meals'), 5, 2)))
        print('%3d  %s' % (g, '  '.join('%-40s' % c for c in cells)))
    for name, r in arms:
        last = r[-1]
        how = 'stopped on the rule' if last.get('stop', {}).get('stop') else 'not stopped on the rule (running, at its cap, or no rule)'
        print('%s: last generation written %d, %s' % (name, last['gen'], how))


if __name__ == '__main__':
    main()
