"""A migrating group's resume read against its whole run, world by world, after the resume's second.

Round 58's replay clause (B2 of its pre-registration) resumes the joined group from a checkpoint and asks that it be
the whole group from there on. This is that read. Every row file the farm writes on the world's clock (lineage,
immigrants, stats, digest, absorptive, positions) must hold the same rows after the second, in the same order. Three
things are left out by construction, not by tolerance:
- steering.jsonl: its sampler counts its 100 s from the process's first step, so a resumed run samples at other
  seconds (an instrument, not the world);
- the stats' wall timings (every field starting "wall"), which are the machine's clock;
- the stats' harnessBodySteps and fluidLinkSteps, which count from the process's start.
Everything else in a stats row must be the same. A file absent from both runs is skipped; absent from one is a
difference. The resumed run must hold immigrant rows after the second, so the read reaches an exchange; with --lone,
for a world that does not migrate, it need not.

Two options serve a run on a build that adds stats fields, read against one recorded before them:
--ignore=a,b,c leaves those stats fields out of both runs, and --until=T reads only the rows at or before T, for runs
that stop at different seconds.

    python -I scripts/reads/group-resume-compare.py [--lone] [--ignore=a,b] [--until=T] <second> <whole run dir> <resumed run dir> [<whole> <resumed> ...]

The run directories are given in pairs, one pair a world. Prints one line a world and a verdict; exits 0 when every
world is the same, 1 when one differs, 2 on bad arguments.
"""
import gzip
import json
import os
import sys

FILES = ['lineage.jsonl', 'immigrants.jsonl', 'stats.jsonl', 'digest.jsonl', 'absorptive.jsonl',
         'positions.jsonl.gz']
COUNTERS = {'harnessBodySteps', 'fluidLinkSteps'}


def rows(path, after, until=float('inf')):
    if not os.path.isfile(path):
        return None
    opener = gzip.open if path.endswith('.gz') else open
    out = []
    with opener(path, 'rt', encoding='utf-8') as f:
        for line in f:
            if not line.strip():
                continue
            r = json.loads(line)
            if after + 1e-9 < r['t'] <= until + 1e-9:
                out.append(r)
    return out


def clean(name, r, ignore=frozenset()):
    if name != 'stats.jsonl':
        return r
    return {k: v for k, v in r.items() if not k.startswith('wall') and k not in COUNTERS and k not in ignore}


def main():
    args = sys.argv[1:]
    lone = '--lone' in args
    ignore = frozenset(f for a in args if a.startswith('--ignore=') for f in a[len('--ignore='):].split(',') if f)
    until = min([float(a[len('--until='):]) for a in args if a.startswith('--until=')] or [float('inf')])
    args = [a for a in args if a != '--lone' and not a.startswith('--ignore=') and not a.startswith('--until=')]
    pairs = args[1:]
    if not pairs or len(pairs) % 2:
        print('give the second, then whole and resumed run directories in pairs')
        return 2
    after = float(args[0])
    ok = True
    for whole, resumed in zip(pairs[0::2], pairs[1::2]):
        notes = []
        for name in FILES:
            a = rows(os.path.join(whole, name), after, until)
            b = rows(os.path.join(resumed, name), after, until)
            if a is None and b is None:
                continue
            if a is None or b is None:
                notes.append(f'{name}: in one run only')
                ok = False
                continue
            a = [clean(name, r, ignore) for r in a]
            b = [clean(name, r, ignore) for r in b]
            if a == b:
                notes.append(f'{name} {len(a)} same')
                continue
            ok = False
            first = next((i for i, (x, y) in enumerate(zip(a, b)) if x != y), min(len(a), len(b)))
            where = a[first]['t'] if first < len(a) else b[first]['t']
            keys = ''
            if first < len(a) and first < len(b):
                keys = ', fields ' + ', '.join(sorted(k for k in set(a[first]) | set(b[first]) if a[first].get(k) != b[first].get(k))[:8])
            notes.append(f'{name}: DIFFERS at row {first} after the second (t {where}){keys}; {len(a)} rows against {len(b)}')
        if not lone and not rows(os.path.join(resumed, 'immigrants.jsonl'), after):
            notes.append('no exchange after the second in the resumed run')
            ok = False
        run = os.path.normpath(resumed)
        print(f'{os.path.basename(os.path.dirname(run))}/{os.path.basename(run)}: ' + '; '.join(notes))
    print('the same after ' + args[0] + ' s' if ok else 'NOT the same')
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
