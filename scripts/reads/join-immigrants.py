"""Join immigrants.jsonl of a stopped migrating world and its resumed continuation into a stitched run directory,
by stitch-resume.py's rule, which leaves this file out: the stopped run's rows at or before T, then the
continuation's rows, less any byte-equal to a row already taken. Writes <joined run dir>/immigrants.jsonl and
refuses if it exists.

    python -I scripts/reads/join-immigrants.py <stopped run dir> <continuation run dir> <T seconds> <joined run dir>
"""
import json
import os
import sys


def lines(path):
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            if line.endswith('\n'):
                yield line


def main(argv):
    if len(argv) != 5:
        print(__doc__)
        return 2
    stopped, cont, t, out = argv[1], argv[2], float(argv[3]), argv[4]
    target = os.path.join(out, 'immigrants.jsonl')
    if os.path.exists(target):
        print(f'{target} exists')
        return 1
    taken, seen = [], set()
    for line in lines(os.path.join(stopped, 'immigrants.jsonl')):
        if json.loads(line)['t'] <= t:
            taken.append(line)
            seen.add(line)
    before = len(taken)
    dropped = 0
    for line in lines(os.path.join(cont, 'immigrants.jsonl')):
        if line in seen:
            dropped += 1
            continue
        taken.append(line)
    with open(target, 'w', encoding='utf-8', newline='\n') as f:
        f.writelines(taken)
    print(f'{target}: {before} rows at or before {t:g} s from the stopped run, {len(taken) - before} after from the '
          f'continuation, {dropped} duplicates dropped')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
