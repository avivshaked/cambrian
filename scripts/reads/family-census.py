"""The shape family in a world: the share of living jointed bodies whose genome carries a superellipsoid node, at T.

A body is alive at T if its lineage birth row is at or before T and it has no death row by T, and jointed if its birth
row says jnt 1 (round 67's readout 4, after G5's line of 10 %). Its genome is the record with its id in the run's
genomes.jsonl.gz. Animals (atk 1 and ink 1) are counted beside, since the family's question is the swimmers'. A body
alive at T whose genome the run does not hold (born in the run it resumed) is counted as unread. Prints no genome.

Usage: python -I scripts/reads/family-census.py <run dir> --at 24000
"""
import argparse
import gzip
import json
import os

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--at", type=float, required=True)
a = ap.parse_args()

born, died, jointed, animal = {}, {}, {}, {}
with open(os.path.join(a.run, "lineage.jsonl"), encoding="utf-8") as f:
    for line in f:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r.get("e") == "b":
            born[r["id"]] = r["t"]
            jointed[r["id"]] = r.get("jnt") == 1
            animal[r["id"]] = r.get("atk") == 1 and r.get("ink") == 1
        elif r.get("e") == "d":
            died[r["id"]] = r["t"]
alive = {i for i in born if born[i] <= a.at and died.get(i, float("inf")) > a.at}
want = {i for i in alive if jointed[i]}

family = {}
with gzip.open(os.path.join(a.run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
    for line in f:
        try:
            g = json.loads(line)
        except (ValueError, EOFError):
            continue
        if g.get("id") in want:
            family[g["id"]] = any(n.get("shape") == "superellipsoid" for n in g.get("nodes") or [])

for name, group in (("jointed", want), ("jointed animals", {i for i in want if animal[i]})):
    read = [i for i in group if i in family]
    fam = sum(1 for i in read if family[i])
    share = fam / len(read) if read else float("nan")
    print(f"{name}: alive {len(group)}, genomes read {len(read)}, with a superellipsoid node {fam} ({share:.1%})")
