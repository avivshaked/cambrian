"""Read a round-55 world run for the smoke's questions (proposals/propose-r55-mixed-inoculation.md).

Births and their brains, brain growth by mutation, pool founders by origin and how their lines did,
and the genome's size on disk per simulated hour.
Usage: python scripts/reads/r55-smoke-read.py <run directory>
"""
import csv
import gzip
import json
import os
import sys
from collections import Counter, defaultdict

run = sys.argv[1]
lineage = [json.loads(l) for l in open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") if l.strip()]
births = [r for r in lineage if r.get("e") == "b"]
deaths = {r["id"] for r in lineage if r.get("e") == "d"}
end_t = max(r["t"] for r in lineage)
parent = {r["id"]: r["p"] for r in births}
born = {r["id"]: r for r in births}

index = list(csv.DictReader(open(os.path.join(run, "pool", "index.tsv"), encoding="utf-8"), delimiter="\t")) \
    if os.path.exists(os.path.join(run, "pool", "index.tsv")) else []

kinds = Counter(("offspring" if r["p"] >= 0 else r.get("src", r.get("k"))) for r in births)
print(f"run {run}: {len(births)} births to {end_t:.0f} s; {dict(kinds)}")

# Brains: every stored genome's model and size; children against their parents.
links, hidden, per_node, central = {}, {}, 0, 0
raw_bytes, rows = 0, 0
with gzip.open(os.path.join(run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
    for line in f:
        rows += 1
        raw_bytes += len(line.encode("utf-8"))
        g = json.loads(line)
        b = g.get("brain")
        if b is None:
            per_node += 1
            continue
        central += 1
        # A rigid body has no output to drive, so its brain is empty; only bodies with outputs count.
        if not b.get("outputs"):
            continue
        links[g["id"]] = len(b.get("links", []))
        hidden[g["id"]] = len(b.get("hidden", []))
gz = os.path.getsize(os.path.join(run, "genomes.jsonl.gz"))
lin = os.path.getsize(os.path.join(run, "lineage.jsonl"))
hours = end_t / 3600
print(f"genomes stored {rows}: central {central}, without a central brain {per_node}")
print(f"genome size: {raw_bytes / max(rows, 1) / 1024:.2f} KB raw, {gz / max(rows, 1):.0f} B compressed each; "
      f"genomes.jsonl.gz {gz / 1e6:.1f} MB and lineage.jsonl {lin / 1e6:.1f} MB, "
      f"{(gz + lin) / 1e6 / hours:.1f} MB per simulated hour")

changed = [(links[c] - links[p], hidden[c] - hidden[p]) for c, p in parent.items() if p >= 0 and c in links and p in links]
if changed:
    dl = Counter(1 if d > 0 else -1 if d < 0 else 0 for d, _ in changed)
    dh = Counter(1 if d > 0 else -1 if d < 0 else 0 for _, d in changed)
    print(f"children against parents ({len(changed)}): links up {dl[1]}, down {dl[-1]}, same {dl[0]}; "
          f"hidden up {dh[1]}, down {dh[-1]}, same {dh[0]}")
early = [links[r["id"]] for r in births if r["t"] < 1000 and r["id"] in links]
late = [links[r["id"]] for r in births if r["t"] > end_t - 1000 and r["id"] in links]
if early and late:
    print(f"mean links of bodies with outputs: births in the first 1,000 s {sum(early) / len(early):.1f}, in the last {sum(late) / len(late):.1f}; "
          f"hidden max {max(hidden.values())}")

# Pool founders by origin, and their lines at the end.
def root(i):
    while parent.get(i, -1) >= 0:
        i = parent[i]
    return i

alive = [i for i in born if i not in deaths]
roots = Counter(root(i) for i in alive)
pool_founders = [r for r in births if r.get("src") == "pool"]
by_origin = defaultdict(lambda: [0, 0, 0])  # founders, descendants born, alive at the end
desc = Counter(root(r["id"]) for r in births if r["p"] >= 0)
for r in pool_founders:
    o = index[r["pool"]] if index else {"origin": "pool", "run": "?", "body": "?"}
    key = (o["origin"], o["run"], o["body"])
    by_origin[key][0] += 1
    by_origin[key][1] += desc[r["id"]]
    by_origin[key][2] += roots[r["id"]]
print(f"pool founders {len(pool_founders)}; alive at the end {len(alive)}, of which descended from a pool founder "
      f"{sum(roots[r['id']] for r in pool_founders)}")
print("origin | run | body | founders | descendants born | alive at the end")
for key in sorted(by_origin):
    f_, d_, a_ = by_origin[key]
    print(" | ".join(map(str, key)) + f" | {f_} | {d_} | {a_}")
