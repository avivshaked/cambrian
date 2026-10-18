"""The general nursery's one check (logbook/specs/nursery-general-build.md, section 6).

Compares a framework run of the baseline recipe with pilot-deep-brain2, generation by generation,
in the three files the search writes:
  tank-generations.jsonl  one row a generation; the old rows' "seconds" (wall time) is dropped
  tank-best.jsonl         one genome a generation, by line
  tank-crossovers.jsonl   one row a child, compared for the children born in the generations read

Prints a table, one row per file and generation: "identical", or the first differing line with
the first differing field. Exit 0 when every cell is identical, 1 otherwise.

    python scripts/reads/nursery-baseline-check.py <old run folder> <new run folder> [--generations n]
"""
import argparse
import json
import os
import sys


def rows(path):
    with open(path, encoding='utf-8') as f:
        return [line.rstrip('\r\n') for line in f if line.strip()]


def first_field(a, b, prefix=''):
    """The first key path at which two parsed JSON values differ, or None."""
    if type(a) != type(b):
        return prefix or '(the row)'
    if isinstance(a, dict):
        for k in list(a.keys()) + [k for k in b.keys() if k not in a]:
            if k not in a or k not in b:
                return f'{prefix}{k} (only in {"new" if k not in a else "old"})'
            d = first_field(a[k], b[k], f'{prefix}{k}.')
            if d:
                return d
        if list(a.keys()) != list(b.keys()):
            return prefix + '(key order)'
        return None
    if isinstance(a, list):
        if len(a) != len(b):
            return f'{prefix}(length {len(a)} against {len(b)})'
        for i, (x, y) in enumerate(zip(a, b)):
            d = first_field(x, y, f'{prefix}[{i}].')
            if d:
                return d
        return None
    return None if a == b else prefix.rstrip('.') + f': {a!r} against {b!r}'


def compare_lines(old, new, label):
    """Text first, since the check is for bytes; the field only to say where."""
    if len(old) != len(new):
        return f'{len(old)} rows against {len(new)}'
    for i, (a, b) in enumerate(zip(old, new)):
        if a != b:
            try:
                where = first_field(json.loads(a), json.loads(b)) or 'the text (same values)'
            except ValueError:
                where = 'the text'
            return f'row {i + 1} differs at {where}'
    return 'identical'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('old')
    ap.add_argument('new')
    ap.add_argument('--generations', type=int, default=None,
                    help='how many generation rows to compare; by default every row the new run wrote '
                         '(a recipe of n generations writes n + 1, generation 0 being the founders)')
    a = ap.parse_args()

    def gen_rows(folder, drop_seconds):
        out = {}
        for line in rows(os.path.join(folder, 'tank-generations.jsonl')):
            r = json.loads(line)
            if drop_seconds:
                r.pop('seconds', None)
                # The row as the new writer writes it: the same keys in the same order, less one.
                line = json.dumps(r, separators=(',', ':'), ensure_ascii=False)
            out.setdefault(r['gen'], []).append(line)
        return out

    def best_rows(folder):
        return {i: [line] for i, line in enumerate(rows(os.path.join(folder, 'tank-best.jsonl')))}

    def cross_rows(folder):
        out = {}
        for line in rows(os.path.join(folder, 'tank-crossovers.jsonl')):
            out.setdefault(json.loads(line)['born'], []).append(line)
        return out

    table = []
    # The old generation rows are re-serialised without "seconds", so the new ones are too, for a
    # like-for-like text: the check is on values and order, and Python writes both the same way.
    old_g, new_g = gen_rows(a.old, True), gen_rows(a.new, True)
    g = a.generations if a.generations is not None else len(new_g)
    print(f'comparing generations 0 to {g - 1}: every row the new run wrote' if a.generations is None
          else f'comparing generations 0 to {g - 1}, as asked')
    for k in range(g):
        table.append(('tank-generations.jsonl', k, compare_lines(old_g.get(k, []), new_g.get(k, []), 'gen')))
    old_b, new_b = best_rows(a.old), best_rows(a.new)
    for k in range(g):
        table.append(('tank-best.jsonl', k, compare_lines(old_b.get(k, []), new_b.get(k, []), 'best')))
    old_c, new_c = cross_rows(a.old), cross_rows(a.new)
    for k in range(g):
        o, n = old_c.get(k, []), new_c.get(k, [])
        cell = compare_lines(o, n, 'cross') if (o or n) else 'identical (no children born)'
        table.append(('tank-crossovers.jsonl', k, cell + f' ({len(o)} rows)' if cell == 'identical' else cell))

    print('| File | Generation | Verdict |')
    print('|---|---|---|')
    for f, k, v in table:
        print(f'| `{f}` | {k} | {v} |')
    ok = all(v.startswith('identical') for _, _, v in table)
    print()
    print('ALL IDENTICAL' if ok else 'DIFFERS')
    sys.exit(0 if ok else 1)


if __name__ == '__main__':
    main()
