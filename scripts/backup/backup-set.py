"""The backup set of D260 (logbook/specs/data-retention-and-backup.md): what cannot be recreated.

Writes scratch/backup/files.txt (one absolute path per line, for `restic backup --files-from-verbatim`) and prints
the set's size by part. Reads directory listings only and writes nothing else.

    python -I scripts/backup/backup-set.py

The parts:
- every run's record and checkpoints (runs/<arm>/<dir>/: the kept kinds below, checkpoints/, and recordings.tar.gz);
- the runs in current use whole (WHOLE below);
- the nursery's run data inside worktrees (scratch/wt-*/runs/), and the gitignored pools (rounds/nursery/pools/);
- the owner's deliveries (scratch/owner/), assets/, and the papers (research/papers/, private, never redistributed).
Left out: .env and every secret, build output, renders, and the replay data a resume regenerates.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'scratch', 'backup', 'files.txt')

# The runs in current use, kept whole (D259, the lanes' keep lists of 2026-10-29).
WHOLE = ('r62cf-', 'r63b-', 'r63c-', 'r63d-', 'r62-h6202', 'r62p-t3-6201', 'r62d-u-6102', 'cefix-s4')
# A run's record: what a regeneration needs and what the record reads.
KEEP_FILES = {'config.json', 'run.json', 'stats.jsonl', 'lineage.jsonl', 'genomes.jsonl.gz', 'digest.jsonl',
              'digest-settings.json', 'steering.jsonl', 'recordings.tar.gz'}
KEEP_PREFIXES = ('manifest', 'report')
KEEP_DIRS = {'checkpoints', 'snapshots', 'pool'}
SKIP_NAMES = {'.env'}


def walk(top):
    for d, dirs, files in os.walk(top):
        dirs[:] = [x for x in dirs if not os.path.islink(os.path.join(d, x))]
        for f in files:
            if f in SKIP_NAMES or f.startswith('.env'):
                continue
            p = os.path.join(d, f)
            if not os.path.islink(p):
                yield p


def size(p):
    try:
        return os.path.getsize(p)
    except OSError:
        return 0


def run_files(runs_dir):
    for arm in sorted(os.listdir(runs_dir)):
        a = os.path.join(runs_dir, arm)
        if not os.path.isdir(a) or os.path.islink(a) or arm == 'derived':
            continue
        whole = arm.startswith(WHOLE)
        for rd in sorted(os.listdir(a)):
            r = os.path.join(a, rd)
            if not os.path.isdir(r) or os.path.islink(r):
                continue
            if whole:
                for p in walk(r):
                    yield 'runs in use, whole', p
                continue
            for name in os.listdir(r):
                p = os.path.join(r, name)
                if os.path.isdir(p) and name in KEEP_DIRS:
                    for q in walk(p):
                        yield 'runs, record and checkpoints', q
                elif os.path.isfile(p) and (name in KEEP_FILES or name.startswith(KEEP_PREFIXES)):
                    yield 'runs, record and checkpoints', p


def main():
    parts = {}
    lines = []

    def add(part, p):
        lines.append(p)
        n, b = parts.get(part, (0, 0))
        parts[part] = (n + 1, b + size(p))

    for part, p in run_files(os.path.join(ROOT, 'runs')):
        add(part, p)
    scratch = os.path.join(ROOT, 'scratch')
    for wt in sorted(os.listdir(scratch)):
        r = os.path.join(scratch, wt, 'runs')
        if wt.startswith('wt-') and os.path.isdir(r):
            for p in walk(r):
                add('worktree runs (nursery)', p)
    for part, rel in (('nursery pools', 'rounds/nursery/pools'), ('owner deliveries', 'scratch/owner'),
                      ('assets', 'assets'), ('papers', 'research/papers')):
        top = os.path.join(ROOT, *rel.split('/'))
        if os.path.isdir(top):
            for p in walk(top):
                add(part, p)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    total = 0
    for part, (n, b) in parts.items():
        total += b
        print(f'{part:32s} {n:8d} files {b / 1e9:8.2f} GB')
    print(f'{"total":32s} {len(lines):8d} files {total / 1e9:8.2f} GB')
    print(f'list: {OUT}')


if __name__ == '__main__':
    sys.exit(main())
