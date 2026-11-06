"""The living plants of a world by body plan: how many plans, how evenly the plants spread over them, and what the
plans are made of. Reads the JSON lines of `genome-query.py plans --cell photosynthetic --json`; writes nothing.

A plant is a living body whose genome reaches a photosynthetic node. Its plan is genome-query.py's signature, over the
nodes development reaches. Nodes are counted from the signature, each times its recursive limit, so the count is the
parts a genome can build and an upper bound on the parts its body has (development may prune). The effective number of
plans is 1 / (sum of squared shares), the number of equally common plans that would give the same spread.

Usage:
    python -I scripts/genome-query.py plans <run> --at T --cell photosynthetic --top 1000000 --json > plans.jsonl
    python -I scripts/reads/plant-census.py plans.jsonl [more.jsonl ...]
"""
import collections
import json
import re
import sys

REPEATS = re.compile(r"x(\d+)\[")

for path in sys.argv[1:]:
    with open(path, encoding="utf-8") as f:
        rows = [json.loads(line) for line in f if line.startswith("{")]
    n = sum(r["bodies"] for r in rows)
    if not n:
        print(f"{path}: no plants")
        continue
    nodes, shapes, cells = collections.Counter(), collections.Counter(), collections.Counter()
    moving = 0
    for r in rows:
        tokens = r["signature"].split()
        count = sum(int(m.group(1)) if (m := REPEATS.search(t)) else 1 for t in tokens)
        nodes[min(count, 6)] += r["bodies"]
        parts = [t.split(":", 1)[1].split("/") for t in tokens]  # cell, shape, joint[...]
        shapes[parts[0][1] if parts else "?"] += r["bodies"]
        cells[" + ".join(sorted({p[0] for p in parts}))] += r["bodies"]
        if any(not p[2].startswith("Fixed") for p in parts[1:]):
            moving += r["bodies"]
    share = lambda v: f"{100 * v / n:.1f} %"
    effective = 1 / sum((r["bodies"] / n) ** 2 for r in rows)
    print(f"{path}: {n} plants, {len(rows)} plans, effective number of plans {effective:.1f}")
    print("  nodes a genome builds (6 = 6 or more):", ", ".join(f"{k} {share(v)}" for k, v in sorted(nodes.items())))
    print("  root shape:", ", ".join(f"{k} {share(v)}" for k, v in shapes.most_common()))
    print("  cells:", ", ".join(f"{k} {share(v)}" for k, v in cells.most_common(6)))
    print(f"  a moving joint below the root: {share(moving)}")
