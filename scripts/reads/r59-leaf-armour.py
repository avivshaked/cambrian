"""Leaf armour by year: how common, how thick, and whether grazers are what kills leaves.

Usage: python -I scripts/reads/r59-leaf-armour.py <run dir> [<run dir> ...] [--year 6000]

From lineage.jsonl:
  birth rows (e == "b"): pho (photosynthetic), prt (a 0/1 flag: some node carries protection), atk (attack);
  death rows (e == "d"): c, the cause (aged, starved, eaten, ...).
From genomes.jsonl.gz (one genome per birth, keyed by id): each node's protection and cell type.

Per year (birth time // year):
  leaf          leaf births (pho > 0)
  armoured %    of leaf births carrying any protection
  thick med/p90/max  the largest node protection in each armoured leaf's genome (leaf cap 0.5)
  on leaf %     of armoured nodes that are photosynthetic (the rest are stems, links and others)
  eaten % a/u   of leaves born that year and since dead, the share killed by a bite, armoured / unarmoured
  attackers     births with attack above zero

A trailing partial row of a live run is skipped. A genome missing from a live run's gzip tail is skipped.
"""
import gzip
import json
import sys
from collections import defaultdict


def rows_of(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            try:
                yield json.loads(line)
            except json.JSONDecodeError:
                continue


def genomes_of(path, wanted):
    try:
        with gzip.open(path, "rt", encoding="utf-8") as f:
            for line in f:
                try:
                    g = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if g.get("id") in wanted:
                    yield g
    except (EOFError, OSError):
        return


def pct(values, q):
    if not values:
        return float("nan")
    values = sorted(values)
    return values[min(len(values) - 1, int(q * len(values)))]


def read(run, year):
    born = {}
    deaths = {}
    attackers = defaultdict(int)
    for r in rows_of(f"{run}/lineage.jsonl"):
        e = r.get("e")
        if e == "b":
            y = int(r["t"] // year)
            if r.get("atk", 0) > 0:
                attackers[y] += 1
            if r.get("pho", 0) > 0:
                born[r["id"]] = (y, (r.get("prt", 0) or 0) > 0)
        elif e == "d":
            deaths[r["id"]] = r.get("c")

    armoured = {i for i, (_, a) in born.items() if a}
    thick = defaultdict(list)
    nodes_leaf = defaultdict(int)
    nodes_all = defaultdict(int)
    for g in genomes_of(f"{run}/genomes.jsonl.gz", armoured):
        y = born[g["id"]][0]
        top = 0.0
        for n in g.get("nodes", []):
            p = n.get("protection", 0) or 0
            if p > 0:
                nodes_all[y] += 1
                if n.get("cell") == "photosynthetic":
                    nodes_leaf[y] += 1
                top = max(top, p)
        if top > 0:
            thick[y].append(top)

    table = defaultdict(lambda: {"leaf": 0, "arm": 0, "dead_a": 0, "eaten_a": 0, "dead_u": 0, "eaten_u": 0})
    for i, (y, a) in born.items():
        b = table[y]
        b["leaf"] += 1
        if a:
            b["arm"] += 1
        c = deaths.get(i)
        if c is not None:
            b["dead_a" if a else "dead_u"] += 1
            if c == "eaten":
                b["eaten_a" if a else "eaten_u"] += 1
    return table, thick, nodes_leaf, nodes_all, attackers


def main():
    args = sys.argv[1:]
    year = 6000.0
    if "--year" in args:
        k = args.index("--year")
        year = float(args[k + 1])
        del args[k:k + 2]
    for run in args:
        table, thick, nodes_leaf, nodes_all, attackers = read(run, year)
        print(run)
        print(f"{'year':>4} {'leaf':>7} {'armoured %':>10} {'thick med':>9} {'p90':>6} {'max':>6} "
              f"{'on leaf %':>9} {'eaten % a':>9} {'u':>6} {'attackers':>9}")
        for y in sorted(table):
            b = table[y]
            t = thick.get(y, [])
            on_leaf = 100 * nodes_leaf[y] / nodes_all[y] if nodes_all[y] else float("nan")
            ea = 100 * b["eaten_a"] / b["dead_a"] if b["dead_a"] else float("nan")
            eu = 100 * b["eaten_u"] / b["dead_u"] if b["dead_u"] else float("nan")
            print(f"{y:>4} {b['leaf']:>7} {100 * b['arm'] / max(1, b['leaf']):>10.2f} {pct(t, 0.5):>9.4f} "
                  f"{pct(t, 0.9):>6.3f} {max(t) if t else float('nan'):>6.3f} {on_leaf:>9.1f} "
                  f"{ea:>9.2f} {eu:>6.2f} {attackers[y]:>9}")


if __name__ == "__main__":
    main()
