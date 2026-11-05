"""The living animals of a world split by where their lineage's founder came from, at the times asked.

A founder is a body born without a parent (lineage birth row with p -1). Its src says where it came from: the floor, the
trickle or the pool; a pool founder's row names its index into the run's pinned pool/index.tsv, whose origin column says
which nursery exam it was. Every body belongs to the lineage of the founder at the root of its parent chain. An animal
is a body born with a mouth (atk 1 and ink 1 in its birth row, as what-evolved.py defines it), and it is alive at T if
it was born at or before T and had not died by T. Reads lineage.jsonl and pool/index.tsv; writes nothing.

Round 67's pre-registration (logbook/specs/r67-prereg/predictions.md, readouts 2 and 3) reads this at 12,000 s and
24,000 s: the share of living animals from a pool founder, and that share split by exam.

A resumed world's own lineage starts at its resume second, so its founders are in the run it resumed. Name the runs in
order, the first first: each run's rows are read up to the first row of the run after it, and the last run's whole.
The pool's index is read from the last run.

Usage: python -I scripts/reads/pool-origins.py [<source run dir> ...] <run dir> --at 12000[,24000,...]
"""
import argparse
import collections
import json
import os
import sys

ap = argparse.ArgumentParser()
ap.add_argument("runs", nargs="+")
ap.add_argument("--at", required=True, help="simulated seconds, comma-separated")
a = ap.parse_args()
times = sorted(float(x) for x in a.at.split(","))
a.run = a.runs[-1]

index_path = os.path.join(a.run, "pool", "index.tsv")
origin_of = {}
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


torn = []


def rows():
    # A live run's last line may be half written, and a run whose program was ended from outside may end in one: a line
    # that does not parse is skipped and counted, and the count is printed.
    for k, run in enumerate(a.runs):
        cut = first_t(a.runs[k + 1]) if k + 1 < len(a.runs) else float("inf")
        with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
            for line in f:
                if not line.strip():
                    continue
                try:
                    r = json.loads(line)
                except ValueError:
                    torn.append(os.path.basename(os.path.normpath(run)))
                    continue
                if r["t"] >= cut:
                    break
                yield r


parent, born, died, animal, source = {}, {}, {}, {}, {}
for r in rows():
    e = r.get("e")
    if e == "b":
        i = r["id"]
        parent[i] = r.get("p", -1)
        born[i] = r["t"]
        animal[i] = r.get("atk") == 1 and r.get("ink") == 1
        if parent[i] < 0:
            src = r.get("src", "?")
            if src == "pool":
                src = "pool:" + origin_of.get(r.get("pool", -1), "?")
            source[i] = src
    elif e == "d":
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
    r = root[i]
    for c in chain:
        root[c] = r
    return r


last = max(born.values()) if born else 0.0
print(f"{os.path.basename(os.path.normpath(a.run))}: {len(born)} births to {last:.0f} s; pool origins {sorted(set(origin_of.values()))}"
      + (f"; {len(torn)} line(s) that did not parse skipped ({', '.join(sorted(set(torn)))})" if torn else ""))
for t in times:
    if t > last:
        print(f"  t {t:.0f}: past the record's last birth ({last:.0f} s)")
        continue
    alive = [i for i in born if born[i] <= t and died.get(i, float("inf")) > t and animal[i]]
    by = collections.Counter(source.get(root_of(i), "?") for i in alive)
    pool = sum(n for k, n in by.items() if k.startswith("pool"))
    share = pool / len(alive) if alive else float("nan")
    parts = ", ".join(f"{k} {n}" for k, n in sorted(by.items()))
    print(f"  t {t:.0f}: living animals {len(alive)}; from a pool founder {pool} ({share:.0%}); by source: {parts}")
