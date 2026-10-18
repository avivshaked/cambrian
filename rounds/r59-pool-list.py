"""Write round 59's weighted trickle pool list (D174, D176) for the planted seeds 2 and 3.

Round 54's four stomachs take STOMACHS of the trickle and the leaf tank's ten direction swimmers
(inocula/r59-direction-tops) SWIMMERS, so the launcher's pool share is their sum and the weights carry the
split, as rounds/r55-pool-list.py made round 55's: the swimmers at one founder in two, round 55's nursery
share, and the stomachs kept at one in ten (the owner's ruling of 2026-10-14, D176). The floor draws nothing
from the pool; that is the launcher's EVOSIM_FLOOR_POOL_SHARE 0, not this list's.

Each swimmer's file is checked against its SHA-256 in the folder's origins.tsv before it is listed. Seed 1
keeps round 58's control pool, rounds/r55-pool-control.tsv, which this script does not touch.

Usage: python rounds/r59-pool-list.py   (paths in the list relative to rounds/)
"""
import csv
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SWIMMERS, STOMACHS = 0.5, 0.1
STOMACH_FILES = ["stomach-r45s1-100", "stomach-link-r46s1-1182", "stomach-link-r46s3-1513", "stomach-link-r46s3-2156"]
TOPS = os.path.join(ROOT, "inocula", "r59-direction-tops")

tops = list(csv.DictReader(open(os.path.join(TOPS, "pool.tsv"), encoding="utf-8"), delimiter="\t"))
origins = list(csv.DictReader(open(os.path.join(TOPS, "origins.tsv"), encoding="utf-8"), delimiter="\t"))
want = {r["file"]: r["sha256"].lower() for r in origins}

if len(tops) != 10:
    sys.exit(f"{len(tops)} swimmers in the folder's pool.tsv, not 10")
for t in tops:
    # The committed bytes: a checkout may write CRLF, and the farm hashes a pool genome by its lines anyway.
    got = hashlib.sha256(open(os.path.join(TOPS, t["path"]), "rb").read().replace(b"\r\n", b"\n")).hexdigest()
    if want.get(t["path"]) != got:
        sys.exit(f"{t['path']}: SHA-256 {got} is not origins.tsv's {want.get(t['path'])}")
for s in STOMACH_FILES:
    if not os.path.exists(os.path.join(ROOT, "inocula", "pool-r47", s + ".json")):
        sys.exit(f"no stomach at inocula/pool-r47/{s}.json")

out = os.path.join(ROOT, "rounds", "r59-pool.tsv")
lines = ["path\tweight\torigin\trun\tid\tkind\townEaten\n"]
for s in STOMACH_FILES:
    lines.append(f"../inocula/pool-r47/{s}.json\t{STOMACHS / len(STOMACH_FILES)!r}\tstomach\tpool-r47\t-\tstomach\t-\n")
for t in tops:
    lines.append(f"../inocula/r59-direction-tops/{t['path']}\t{SWIMMERS / len(tops)!r}\tswimmer\t{t['run']}\t{t['id']}\tswimmer\t{t['ownEaten']}\n")
text = "".join(lines)
# A list that would come out the same is left alone, line endings and all: rewriting the same bytes in a checkout
# that converts line endings makes git's status call the file changed (2026-10-17), which stops the launchers.
old = open(out, "rb").read().replace(b"\r\n", b"\n").decode("utf-8") if os.path.exists(out) else None
if old != text:
    with open(out, "w", encoding="utf-8", newline="\n") as w:
        w.write(text)

print(f"{out}: {len(STOMACH_FILES)} stomachs at {STOMACHS / len(STOMACH_FILES)!r} and {len(tops)} swimmers at "
      f"{SWIMMERS / len(tops)!r}, hashes checked against origins.tsv; pool share {SWIMMERS + STOMACHS!r}")
