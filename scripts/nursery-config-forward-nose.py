"""Bring the nose bench's nose configs forward onto the merged build's format, as text.

nursery-config-forward.py refuses them: their cellTypes table holds the nose, which its template's table does not. Each
of them is BASE (ecbba999e231021c) with the nose's lines added and, in three, a noise changed. So this takes each file's
own line difference from BASE and lays it onto BASE_FWD (BASE forwarded by nursery-config-forward.py), changing nothing
else. It then reads every file back as JSON and checks:
  - the new file equals the old one in every value, apart from configHash, plus the keys BASE_FWD added to BASE;
  - those added keys carry BASE_FWD's values.

Usage: python -I scripts/nursery-config-forward-nose.py BASE BASE_FWD OLD OUT [--hash H]
Without --hash the configHash line holds 0000000000000000. OUT is refused if it exists.
"""
import argparse
import difflib
import json
import os
import re
import sys

ap = argparse.ArgumentParser()
ap.add_argument("base"); ap.add_argument("base_fwd"); ap.add_argument("old"); ap.add_argument("out")
ap.add_argument("--hash", default="0000000000000000")
a = ap.parse_args()

if os.path.exists(a.out):
    sys.exit("REFUSED: " + a.out + " exists")


def lines(p):
    with open(p, "r", encoding="utf-8", newline="") as f:
        return f.read().splitlines(keepends=True)


HASH = re.compile(r'("configHash"\s*:\s*")[0-9a-f]{16}(")')
base, fwd, old = lines(a.base), lines(a.base_fwd), lines(a.old)
# The hash lines differ in every file; compare without them, and set the new one at the end.
strip = lambda ls: [HASH.sub(r"\g<1>0000000000000000\g<2>", x) for x in ls]
base_s, fwd_s, old_s = strip(base), strip(fwd), strip(old)

# Map each BASE line to its BASE_FWD line (BASE_FWD only inserts lines, so every BASE line has one).
to_fwd = {}
for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, base_s, fwd_s, autojunk=False).get_opcodes():
    if tag == "equal":
        for k in range(i2 - i1):
            to_fwd[i1 + k] = j1 + k
    elif tag != "insert":
        sys.exit("REFUSED: BASE_FWD changes BASE's lines %d-%d (%s), not only inserts" % (i1 + 1, i2, tag))
if len(to_fwd) != len(base_s):
    sys.exit("REFUSED: not every BASE line is in BASE_FWD")

# Lay OLD's edits of BASE onto BASE_FWD, from the bottom up so the indices hold.
out = list(fwd_s)
ops = difflib.SequenceMatcher(None, base_s, old_s, autojunk=False).get_opcodes()
for tag, i1, i2, j1, j2 in reversed(ops):
    if tag == "equal":
        continue
    if i1 < i2:
        start, end = to_fwd[i1], to_fwd[i2 - 1] + 1
        if end - start != i2 - i1:
            sys.exit("REFUSED: BASE_FWD inserted inside an edited block at BASE line %d" % (i1 + 1))
    else:
        # A pure insertion before BASE line i1: after BASE_FWD's own line for BASE line i1 - 1.
        start = end = (to_fwd[i1 - 1] + 1) if i1 > 0 else 0
    out[start:end] = old_s[j1:j2]

text = "".join(out)
text, n = HASH.subn(r"\g<1>" + a.hash + r"\g<2>", text)
if n != 1:
    sys.exit("REFUSED: %d configHash lines" % n)


def flat(o, p=""):
    if isinstance(o, dict):
        r = {}
        for k, v in o.items():
            r.update(flat(v, p + "." + k if p else k))
        return r
    if isinstance(o, list):
        r = {}
        for i, v in enumerate(o):
            r.update(flat(v, "%s[%d]" % (p, i)))
        return r
    return {p: o}


jb, jf, jo, jn = (flat(json.loads("".join(x))) for x in (base, fwd, old, [text]))
added = {k: v for k, v in jf.items() if k not in jb}
for d in (jb, jf, jo, jn):
    d.pop("configHash", None)
want = dict(jo); want.update(added)
if jn != want:
    bad = sorted(set(jn) ^ set(want)) + sorted(k for k in jn if k in want and jn[k] != want[k])
    sys.exit("REFUSED: values differ at " + ", ".join(bad[:10]))
if any(k in jo for k in added):
    sys.exit("REFUSED: OLD already has a key BASE_FWD added")

with open(a.out, "x", encoding="utf-8", newline="") as f:
    f.write(text)
print("%d keys added from BASE_FWD, %d edit blocks from OLD; configHash %s; wrote %s" % (
    len(added), sum(1 for o in ops if o[0] != "equal"), a.hash, a.out))
