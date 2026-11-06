"""The shape family split by where a lineage's founder came from: living jointed animals at T, counted by their founder's
source (the floor, the trickle, or the pool's exam) and by whether their genome carries a superellipsoid node.

Founders and sources are found as in pool-origins.py, and the family as in family-census.py: name the runs of one world
in order, the first first, each read up to the first lineage row of the run after it. Genomes are read from every run
named. An animal whose genome is not found is counted as unread. Prints no genome.

Usage: python -I scripts/reads/family-by-origin.py [<source run dir> ...] <run dir> --at 36000
"""
import argparse
import collections
import gzip
import json
import os

ap = argparse.ArgumentParser()
ap.add_argument("runs", nargs="+")
ap.add_argument("--at", type=float, required=True)
a = ap.parse_args()
last_run = a.runs[-1]

origin_of = {}
index_path = os.path.join(last_run, "pool", "index.tsv")
if os.path.exists(index_path):
    with open(index_path, encoding="utf-8") as f:
        header = f.readline().rstrip("\r\n").split("\t")
        col = header.index("origin") if "origin" in header else None
        for line in f:
            cells = line.rstrip("\r\n").split("\t")
            if cells and cells[0] != "":
                origin_of[int(cells[0])] = cells[col] if col is not None else "pool"


def first_t(run):
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            if line.strip():
                return json.loads(line)["t"]
    return float("inf")


parent, born, died, kind, source = {}, {}, {}, {}, {}
for k, run in enumerate(a.runs):
    cut = first_t(a.runs[k + 1]) if k + 1 < len(a.runs) else float("inf")
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            try:
                r = json.loads(line)
            except ValueError:
                continue  # a torn line
            if r["t"] >= cut:
                break
            if r.get("e") == "b":
                i = r["id"]
                parent[i] = r.get("p", -1)
                born[i] = r["t"]
                kind[i] = (r.get("jnt") == 1, r.get("atk") == 1 and r.get("ink") == 1)
                if parent[i] < 0:
                    src = r.get("src", "?")
                    source[i] = "pool:" + origin_of.get(r.get("pool", -1), "?") if src == "pool" else src
            elif r.get("e") == "d":
                died[r["id"]] = r["t"]

root = {}


def root_of(i):
    chain = []
    while i not in root:
        p = parent.get(i, -1)
        if p < 0 or p not in parent:
            root[i] = i
            break
        chain.append(i)
        i = p
    for c in chain:
        root[c] = root[i]
    return root[i]


want = {i for i in born if born[i] <= a.at and died.get(i, float("inf")) > a.at and kind[i][0] and kind[i][1]}
family = {}
for run in a.runs:
    with gzip.open(os.path.join(run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
        try:
            for line in f:
                try:
                    g = json.loads(line)
                except ValueError:
                    continue
                if g.get("id") in want:
                    family[g["id"]] = any(n.get("shape") == "superellipsoid" for n in g.get("nodes") or [])
        except EOFError:
            pass  # a gzip cut mid-stream by a run still writing

table = collections.defaultdict(lambda: [0, 0, 0])  # with, without, unread
for i in want:
    cell = table[source.get(root_of(i), "?")]
    cell[0 if family.get(i) else 1 if i in family else 2] += 1
arm = os.path.basename(os.path.dirname(os.path.normpath(last_run)))
print(f"{arm} at {a.at:g} s: {len(want)} living jointed animals")
for src in sorted(table):
    w, wo, un = table[src]
    n = w + wo
    line = (f"  {src}: {n + un} animals; with a superellipsoid node {w} ({w / n:.1%} of {n} read)" if n else
            f"  {src}: {un} animals, none read")
    print(line + (f"; unread {un}" if un and n else ""))
fam_lines = collections.Counter(root_of(i) for i in want if family.get(i))
print(f"  the family's animals lie in {len(fam_lines)} lineages; the largest five hold "
      + ", ".join(f"{n} ({source.get(r, '?')})" for r, n in fam_lines.most_common(5)))
