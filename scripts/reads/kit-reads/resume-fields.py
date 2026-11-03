"""A resume against the run it resumes, from --from on, with every field compared but the wall clocks and the two counters
that count from the process's start (harnessBodySteps, fluidLinkSteps; src/Evosim.Farm.Gpu/CLAUDE.md). Prints the first
row and field that differ, or none, and the counts compared.

Usage: python -I scripts/reads/kit-reads/resume-fields.py <run dir> <resumed run dir> <from seconds>
"""
import json
import os
import sys

SKIP = {'harnessBodySteps', 'fluidLinkSteps'}


def rows(d):
    with open(os.path.join(d, 'stats.jsonl'), encoding='utf-8') as f:
        return {r['t']: r for r in (json.loads(l) for l in f if l.strip())}


a, b, start = rows(sys.argv[1]), rows(sys.argv[2]), float(sys.argv[3])
ts = sorted(t for t in b if t > start and t in a)
values = 0
for t in ts:
    ra, rb = a[t], b[t]
    keys = sorted(k for k in set(ra) | set(rb) if not k.startswith('wall') and k not in SKIP)
    for k in keys:
        values += 1
        if ra.get(k) != rb.get(k):
            print('first difference: t', t, 'field', k, ra.get(k), rb.get(k))
            sys.exit(1)
print('rows', len(ts), 'from', ts[0] if ts else None, 'to', ts[-1] if ts else None, 'values compared', values,
      'first difference: none')
