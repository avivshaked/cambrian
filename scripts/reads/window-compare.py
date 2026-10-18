"""A world's resume read against its whole run over a window of seconds: every row file on the world's clock must hold
the same rows from FROM (exclusive) to TO (inclusive), as group-resume-compare does after a second, but cut at TO, so a
short resume can be read against a longer run. Left out by construction: steering.jsonl and leafscent.jsonl (their
samplers count from the process's first step), the stats' wall timings and the stats' harnessBodySteps and
fluidLinkSteps.

    python -I window-compare.py <whole run dir> <resumed run dir> FROM TO

Prints a line a file, the first differing row and fields, and a verdict; exits 0 when every file is the same.
"""
import gzip
import json
import os
import sys

FILES = ['lineage.jsonl', 'stats.jsonl', 'digest.jsonl', 'absorptive.jsonl', 'positions.jsonl.gz']
COUNTERS = {'harnessBodySteps', 'fluidLinkSteps'}


def rows(path, lo, hi):
    if not os.path.isfile(path):
        return None
    opener = gzip.open if path.endswith('.gz') else open
    out = []
    with opener(path, 'rt', encoding='utf-8') as f:
        for line in f:
            if line.strip():
                r = json.loads(line)
                if lo + 1e-9 < r['t'] <= hi + 1e-9:
                    out.append(r)
    return out


def main():
    whole, resumed, lo, hi = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
    same = True
    for name in FILES:
        a, b = rows(os.path.join(whole, name), lo, hi), rows(os.path.join(resumed, name), lo, hi)
        if a is None and b is None:
            continue
        if a is None or b is None:
            print(f'{name}: in one run only')
            same = False
            continue
        first = None
        for i, (x, y) in enumerate(zip(a, b)):
            keys = sorted(k for k in set(x) | set(y)
                          if not (name == 'stats.jsonl' and (k.startswith('wall') or k in COUNTERS)) and x.get(k) != y.get(k))
            if keys:
                first = (i, x.get('t'), keys[:8])
                break
        if first is None and len(a) != len(b):
            first = (min(len(a), len(b)), None, ['row count'])
        if first:
            same = False
            print(f'{name}: DIFFERS at row {first[0]} (t {first[1]}), fields {", ".join(first[2])}; {len(a)} rows against {len(b)}')
        else:
            print(f'{name}: {len(a)} same')
    print(f'the same from {lo:g} to {hi:g} s' if same else 'NOT the same')
    sys.exit(0 if same else 1)


if __name__ == '__main__':
    main()
