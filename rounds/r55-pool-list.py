"""Write round 55's weighted trickle pool list (D149) from the nursery set's origins table.

Per nursery run and body: the exam genome and the most recent distinct per-generation bests, up to ten
genomes, the cell's weight shared evenly among them, every one of the 24 cells weighing the same. The
nursery set takes NURSERY of the trickle and round 54's four stomachs STOMACHS, so the launcher's pool
share is their sum and the weights carry the split (the owner's rulings of 2026-10-04: one founder in two
from the nursery, the stomachs kept at one in ten).

Also writes rounds/r55-pool-control.tsv, the control seed's pool: the four stomachs alone, equally weighted.

Usage: python rounds/r55-pool-list.py   (paths in both lists relative to rounds/)
"""
import csv
import os
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NURSERY, STOMACHS = 0.5, 0.1
PER_CELL = 10
STOMACH_FILES = ["stomach-r45s1-100", "stomach-link-r46s1-1182", "stomach-link-r46s3-1513", "stomach-link-r46s3-2156"]

rows = list(csv.DictReader(open(os.path.join(ROOT, "inocula", "r55-nursery", "origins.tsv"), encoding="utf-8"), delimiter="\t"))
cells = defaultdict(dict)  # (run, body) -> file -> (is exam, latest generation it was the top)
for r in rows:
    cell = cells[(r["run"], int(r["body"]))]
    exam, gen = cell.get(r["file"], (False, -1))
    if r["kind"] == "exam":
        exam = True
    else:
        gen = max(gen, int(r["gen"]))
    cell[r["file"]] = (exam, gen)

weights = defaultdict(float)
origin = {}
cell_weight = NURSERY / len(cells)
for (run, body), files in sorted(cells.items()):
    exams = [f for f, (e, _) in files.items() if e]
    bests = sorted((f for f, (e, _) in files.items() if not e), key=lambda f: -files[f][1])
    chosen = (exams + bests)[:PER_CELL]
    for f in chosen:
        weights[f] += cell_weight / len(chosen)
        # A file met in two cells (one generation-0 founder is) keeps its first cell's origin.
        origin.setdefault(f, (run, str(body), "exam" if files[f][0] else "best", str(files[f][1])))

out = os.path.join(ROOT, "rounds", "r55-pool.tsv")
with open(out, "w", encoding="utf-8", newline="\n") as w:
    w.write("path\tweight\torigin\trun\tbody\tkind\tlastTopGen\n")
    for s in STOMACH_FILES:
        w.write(f"../inocula/pool-r47/{s}.json\t{STOMACHS / len(STOMACH_FILES)!r}\tstomach\tpool-r47\t-\tstomach\t-1\n")
    for f in sorted(weights):
        run, body, kind, gen = origin[f]
        w.write(f"../inocula/r55-nursery/{f}\t{weights[f]!r}\tnursery\t{run}\t{body}\t{kind}\t{gen}\n")

with open(os.path.join(ROOT, "rounds", "r55-pool-control.tsv"), "w", encoding="utf-8", newline="\n") as w:
    w.write("path\tweight\torigin\trun\tbody\tkind\tlastTopGen\n")
    for s in STOMACH_FILES:
        w.write(f"../inocula/pool-r47/{s}.json\t1.0\tstomach\tpool-r47\t-\tstomach\t-1\n")

n = len(weights)
print(f"{out}: {len(STOMACH_FILES)} stomachs and {n} nursery genomes from {len(cells)} cells; pool share {NURSERY + STOMACHS}")
for (run, body), files in sorted(cells.items()):
    print(f"  {run} body {body}: {min(len(files), PER_CELL)} of {len(files)}")
