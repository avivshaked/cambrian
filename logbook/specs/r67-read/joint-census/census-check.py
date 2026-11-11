"""joint-dof-probe, 2026-11-11: does the film's joint census (q4-check.py, birth-row jnt) overcount bodies that still
have a joint at T? Living animals chosen exactly as q4-check.py chooses them (both runs chained, atk 1 and ink 1 in the
birth row, born <= T, not dead by T); then each one's jointed bit in positions.jsonl.gz's sample at T, which Row.cs
computes from the body's phenotype at that sample. Cross-tabulates birth jnt against the sample's jointed bit, and counts
non-root kill rows before T for the bodies that changed.

Usage: python -I census-check.py <first run> <resumed run> [T]
"""
import collections
import gzip
import json
import os
import sys

runs = sys.argv[1:3]
T = float(sys.argv[3]) if len(sys.argv) > 3 else 24000.0
WATCH = [32869, 32291, 33942, 34788]


def first_t(run):
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            if line.strip():
                return json.loads(line)["t"]


born, died, animal, jnt = {}, {}, {}, {}
limb_kills = collections.Counter()
for k, run in enumerate(runs):
    cut = first_t(runs[k + 1]) if k + 1 < len(runs) else float("inf")
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            try:
                r = json.loads(line)
            except ValueError:
                continue
            if r["t"] >= cut:
                break
            e = r.get("e")
            if e == "b":
                i = r["id"]
                born[i] = r["t"]
                animal[i] = r.get("atk") == 1 and r.get("ink") == 1
                jnt[i] = r.get("jnt", 0)
            elif e == "d":
                died[r["id"]] = r["t"]
            elif e == "k" and r["t"] <= T and r.get("root") == 0:
                limb_kills[r["id"]] += 1

alive = [i for i in born if animal[i] and born[i] <= T and died.get(i, float("inf")) > T]

now = None
with gzip.open(os.path.join(runs[-1], "positions.jsonl.gz"), "rt", encoding="utf-8") as f:
    try:
        for line in f:
            if not line.startswith('{"t":'):
                continue
            t = float(line[5:line.index(",")])
            if t < T:
                continue
            row = json.loads(line)
            now = (row["t"], {b[0]: b[4] for b in row["b"]})
            break
    except EOFError:
        pass

if now is None:
    print("no positions sample at or after", T)
    sys.exit(1)

t_s, flags = now
print(f"T {T:.0f}, positions sample at {t_s}; living animals by the census rule: {len(alive)}")
tab = collections.Counter()
missing = 0
changed = []
for i in alive:
    if i not in flags:
        missing += 1
        continue
    j_now = 1 if flags[i] & 2 else 0
    tab[(jnt[i], j_now)] += 1
    if jnt[i] == 1 and j_now == 0:
        changed.append(i)
print(f"  not in the sample: {missing}")
for (b, n), c in sorted(tab.items()):
    print(f"  birth jnt {b}, jointed at sample {n}: {c}")
print(f"  birth-jointed now unjointed: {len(changed)}; of those with a non-root kill row by T: "
      f"{sum(1 for i in changed if limb_kills[i] > 0)}")
for i in WATCH:
    print(f"  body {i}: birth jnt {jnt.get(i)}, flags at sample {flags.get(i)}, limb kills by T {limb_kills[i]}")
