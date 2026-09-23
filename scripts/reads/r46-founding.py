"""Round 46's second founding, read from lineage.jsonl: who the trickle brought, how long its
stomachs lived, whether any bred, and whose lines the living belong to at a second. From round
47 (D117) the trickle's pool is a third source.

python scripts/reads/r46-founding.py <run dir> [<second>]

For each founder source (floor, trickle, pool) it prints the founders, the stomach founders
(ink 1), their ages at death (median and quartiles, with the floor's first cohort as the
comparison the entry's two-sided reading asks for), the absorptive stomachs (abs 1, pho 0, ink 0)
the same way, how many of each bred at all, and the births rooted in each source. A pool founder's
row reads src "pool" and carries its pool index ("pool"), and the pool is also broken down by that
index: founders, founders that bred, births rooted and, with a second, the living rooted in each
pool genome. A run recorded before D117 has no pool rows and prints a pool of none. With a second
it also reads the snapshot at that second and says how many of the living are rooted in each
source. Every row is a birth (e b) or a death (e d); the root of a body is found by walking p until
it reads -1.
"""
import json, os, sys
from collections import Counter, defaultdict

SOURCES = ("floor", "trickle", "pool")


def quart(xs):
    xs = sorted(xs)
    if not xs:
        return "none"
    n = len(xs)
    return "%d: median %.0f s, quartiles %.0f to %.0f, min %.0f, max %.0f" % (
        n, xs[n // 2], xs[n // 4], xs[(3 * n) // 4], xs[0], xs[-1])


def main():
    run = sys.argv[1]
    second = int(sys.argv[2]) if len(sys.argv) > 2 else None
    birth = {}
    parent = {}
    src = {}
    pool_index = {}
    ink = {}
    absf = {}
    phof = {}
    died = {}
    children = Counter()
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            if r["e"] == "b":
                birth[r["id"]] = r["t"]
                parent[r["id"]] = r["p"]
                ink[r["id"]] = r.get("ink", 0)
                absf[r["id"]] = r.get("abs", 0)
                phof[r["id"]] = r.get("pho", 0)
                if r["p"] == -1:
                    src[r["id"]] = r.get("src", "floor")
                    if "pool" in r:
                        pool_index[r["id"]] = r["pool"]
                else:
                    children[r["p"]] += 1
            elif r["e"] == "d":
                died[r["id"]] = r["t"]

    root_cache = {}

    def root(i):
        path = []
        while i not in root_cache and parent.get(i, -1) != -1:
            path.append(i)
            i = parent[i]
        r = root_cache.get(i, i)
        for p in path:
            root_cache[p] = r
        return r

    print("%s: %d births, %d deaths" % (run, len(birth), len(died)))
    for source in SOURCES:
        founders = [i for i, s in src.items() if s == source]
        stomachs = [i for i in founders if ink[i] == 1]
        absorptive = [i for i in founders if absf[i] == 1 and phof[i] == 0 and ink[i] == 0]
        bred = [i for i in founders if children[i] > 0]
        stomachs_bred = [i for i in stomachs if children[i] > 0]
        absorptive_bred = [i for i in absorptive if children[i] > 0]
        ages = [died[i] - birth[i] for i in stomachs if i in died]
        abs_ages = [died[i] - birth[i] for i in absorptive if i in died]
        leaf_ages = [died[i] - birth[i] for i in founders if ink[i] == 0 and absf[i] == 0 and i in died]
        rooted = sum(1 for i in birth if parent[i] != -1 and src.get(root(i)) == source)
        alive_founders = [i for i in founders if i not in died]
        print("  %-8s founders %d (first at %.0f s, last at %.0f s), of them stomachs %d, alive at the end %d"
              % (source, len(founders), min(birth[i] for i in founders) if founders else 0,
                 max(birth[i] for i in founders) if founders else 0, len(stomachs), len(alive_founders)))
        print("           founders that bred %d (%.2f), stomach founders that bred %d, children of stomach founders %d"
              % (len(bred), len(bred) / max(1, len(founders)), len(stomachs_bred),
                 sum(children[i] for i in stomachs)))
        print("           consumer founders' ages at death   " + quart(ages))
        print("           absorptive founders %d, bred %d; ages at death " % (len(absorptive), len(absorptive_bred)) + quart(abs_ages))
        print("           leaf founders' ages at death       " + quart(leaf_ages))
        print("           births rooted in this source %d" % rooted)

    # D117: the pool by genome. A founder's index is on its own row; a descendant's is its root's.
    if pool_index:
        rooted_by_index = Counter()
        for i in birth:
            if parent[i] != -1:
                r = root(i)
                if r in pool_index:
                    rooted_by_index[pool_index[r]] += 1
        for index in sorted(set(pool_index.values())):
            founders = [i for i, k in pool_index.items() if k == index]
            bred = [i for i in founders if children[i] > 0]
            ages = [died[i] - birth[i] for i in founders if i in died]
            print("  pool %02d  founders %d, bred %d, births rooted %d; ages at death %s"
                  % (index, len(founders), len(bred), rooted_by_index[index], quart(ages)))

    if second is not None:
        alive = []
        with open(os.path.join(run, "snapshots", "%09d.jsonl" % second), encoding="utf-8") as f:
            for line in f:
                alive.append(json.loads(line)["id"])
        by = Counter(src.get(root(i), "?") for i in alive)
        founder_births = Counter()
        for i in alive:
            r = root(i)
            founder_births[(src.get(r, "?"), int(birth[r] // 5000) * 5000)] += 1
        print("  living at %d s: %d, rooted in %s" % (second, len(alive), dict(by)))
        print("  by the founder's source and birth window (5,000 s bins): %s"
              % ", ".join("%s %d-%d: %d" % (k[0], k[1], k[1] + 5000, v) for k, v in sorted(founder_births.items())))
        if pool_index:
            by_index = Counter(pool_index[root(i)] for i in alive if root(i) in pool_index)
            print("  living at %d s rooted in each pool genome: %s"
                  % (second, ", ".join("%02d: %d" % (k, v) for k, v in sorted(by_index.items())) or "none"))


main()
