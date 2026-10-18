"""Phase 3b's check: a rerun of step 5's recipe on the 3b build against step 5's run.
SHA-256 of every result file, by RegistryEntry.IsResult's rule (not run.json, run.json.tmp,
recipe.json, timing.jsonl, *.log, *.err, *.txt). The files 3b adds are expected in B only.
Usage: python digest3b.py <B run folder>"""
import hashlib, os, sys

A = r'D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-general\scratch\step5\run'
B = sys.argv[1]
NEW = {'tank-draws.jsonl', 'tank-exam-draws.jsonl', 'tank-lineage.jsonl'}


def is_result(name):
    n = name.lower()
    if n in ('run.json', 'run.json.tmp', 'recipe.json', 'timing.jsonl'):
        return False
    return os.path.splitext(n)[1] not in ('.log', '.err', '.txt')


def digests(folder):
    out = {}
    for f in os.listdir(folder):
        p = os.path.join(folder, f)
        if os.path.isfile(p) and is_result(f):
            with open(p, 'rb') as h:
                out[f] = hashlib.sha256(h.read()).hexdigest()
    return out


a, b = digests(A), digests(B)
same = True
print('| File | Step 5 (41206ff) | 3b | |')
print('|---|---|---|---|')
for f in sorted(set(a) | set(b)):
    x, y = a.get(f), b.get(f)
    if f in NEW:
        verdict = 'new in 3b, as expected' if x is None and y is not None else 'UNEXPECTED'
        same &= verdict.startswith('new')
    else:
        verdict = 'identical' if x == y else 'DIFFERS'
        same &= x == y
    print(f'| {f} | {(x or "absent")[:16]} | {(y or "absent")[:16]} | {verdict} |')
print('\nDIGEST-IDENTICAL' if same else '\nDIFFERS')
sys.exit(0 if same else 1)
