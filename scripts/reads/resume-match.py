"""Whether a resumed run repeats its source over the seconds both recorded, by stats row and by lineage row.

    python -I scripts/reads/resume-match.py <resume run dir> <source run dir>

gpu-accept.py compares two runs from their first lines, which suits two runs from the same start; a resume starts
at its checkpoint, so this aligns the rows by their second instead. Stats: every row at a second both runs hold,
every field but the wall clocks (wall*) and the counts that run from the process's start (harnessBodySteps,
fluidLinkSteps; src/Evosim.Farm.Gpu/CLAUDE.md), and prints the first second at which any field differs, with up to
eight of the fields. Lineage: the rows whose second falls inside both runs' spans, compared as text in order.
Exit 0 when both match on everything shared, 1 on a difference, 2 on a usage or data problem.
"""
import json
import os
import sys

SKIP = {'harnessBodySteps', 'fluidLinkSteps'}


def parsed(path):
    # A live run's last line may be half written; it is left out, and only the last line may be.
    with open(path, encoding='utf-8') as f:
        lines = f.read().split('\n')
    for i, line in enumerate(lines):
        if not line.strip():
            continue
        try:
            yield line, json.loads(line)
        except ValueError:
            if i < len(lines) - 1 and any(x.strip() for x in lines[i + 1:]):
                raise


def rows(path):
    return {r['t']: r for _, r in parsed(path)}


def lineage(path, lo, hi):
    return [line for line, r in parsed(path) if lo < r.get('t', -1) <= hi]


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    a, b = argv[1].rstrip('/\\'), argv[2].rstrip('/\\')
    for d in (a, b):
        if not os.path.exists(os.path.join(d, 'stats.jsonl')):
            print('no stats.jsonl in %s' % d)
            return 2
    ra, rb = rows(os.path.join(a, 'stats.jsonl')), rows(os.path.join(b, 'stats.jsonl'))
    shared = sorted(set(ra) & set(rb))
    if not shared:
        print('no second is in both runs: resume %s to %s, source %s to %s' % (min(ra), max(ra), min(rb), max(rb)))
        return 2
    first = None
    for t in shared:
        x, y = ra[t], rb[t]
        diff = [k for k in sorted(set(x) | set(y))
                if not k.startswith('wall') and k not in SKIP and x.get(k) != y.get(k)]
        if diff:
            first = (t, diff)
            break
    lo, hi = min(shared) - 10, max(shared)
    la = lineage(os.path.join(a, 'lineage.jsonl'), lo, hi)
    lb = lineage(os.path.join(b, 'lineage.jsonl'), lo, hi)
    lin_first = next((i for i, (p, q) in enumerate(zip(la, lb)) if p != q), None)
    print('stats: %d shared seconds, %s to %s' % (len(shared), shared[0], shared[-1]))
    if first:
        t, diff = first
        print('  first differs at %s s on %d fields, e.g. %s' % (t, len(diff), ', '.join(
            '%s %r against %r' % (k, ra[t].get(k), rb[t].get(k)) for k in diff[:8])))
    else:
        print('  identical on every field but the wall clocks and %s' % ', '.join(sorted(SKIP)))
    print('lineage: %d rows in the resume and %d in the source between %s and %s s' % (len(la), len(lb), lo, hi))
    if lin_first is not None:
        print('  first differs at row %d: %s | %s' % (lin_first, la[lin_first][:140], lb[lin_first][:140]))
    elif len(la) != len(lb):
        print('  the shorter is a prefix of the longer')
    else:
        print('  identical')
    return 1 if first or lin_first is not None or len(la) != len(lb) else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
