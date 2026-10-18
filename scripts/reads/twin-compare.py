"""Round 58's twins read (B2's first half): each joined world against its isolated twin, row for row, before the
first exchange. A twin has the joined world's seed, launcher and settings but no exchange, so every row the farm
writes on the world's clock must be the same strictly before the first exchange's second. At that second the joined
world plants its first arrivals and the two part. Compared: lineage, stats, digest, absorptive and positions;
left out by construction, as group-resume-compare.py leaves them out: steering.jsonl and the stats' wall timings
(every field starting "wall"). The harness's two step counters stay in: neither run was resumed. A file absent
from both runs is skipped; absent from one is a difference.

    python -I scripts/reads/twin-compare.py <first exchange second> <joined run dir> <twin run dir> [<joined> <twin> ...]

The run directories go in pairs, one pair a world. Prints one line a world and a verdict; exits 0 when every world
matches its twin before the second, 1 when one differs, 2 on bad arguments.
"""
import gzip
import json
import os
import sys

FILES = ['lineage.jsonl', 'stats.jsonl', 'digest.jsonl', 'absorptive.jsonl', 'positions.jsonl.gz']


def rows(path, before):
    if not os.path.isfile(path):
        return None
    opener = gzip.open if path.endswith('.gz') else open
    out = []
    with opener(path, 'rt', encoding='utf-8') as f:
        for line in f:
            if not line.endswith('\n'):
                break
            r = json.loads(line)
            if r['t'] >= before:
                continue
            if os.path.basename(path) == 'stats.jsonl':
                r = {k: v for k, v in r.items() if not k.startswith('wall')}
            out.append(r)
    return out


def main(argv):
    if len(argv) < 4 or len(argv) % 2:
        print(__doc__)
        return 2
    before = float(argv[1])
    pairs = argv[2:]
    same = True
    for joined, twin in zip(pairs[0::2], pairs[1::2]):
        notes = []
        for name in FILES:
            a, b = rows(os.path.join(joined, name), before), rows(os.path.join(twin, name), before)
            if a is None and b is None:
                continue
            if a is None or b is None:
                notes.append(f'{name} missing from {"the joined world" if a is None else "the twin"}')
                same = False
                continue
            if a == b:
                notes.append(f'{name} {len(a)} same')
                continue
            same = False
            first = next((i for i, (x, y) in enumerate(zip(a, b)) if x != y), min(len(a), len(b)))
            where = a[first]['t'] if first < len(a) else b[first]['t']
            notes.append(f'{name}: DIFFERS at row {first} (t {where}); {len(a)} rows against {len(b)}')
        print(f'{os.path.basename(os.path.dirname(os.path.normpath(joined)))} against '
              f'{os.path.basename(os.path.dirname(os.path.normpath(twin)))} before {before:g} s: ' + '; '.join(notes))
    print('the same before the first exchange' if same else 'NOT the same')
    return 0 if same else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
