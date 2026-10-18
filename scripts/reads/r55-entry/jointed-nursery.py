"""Round 55's entry: which of the nursery-rooted living are jointed, and from which nursery run and body.

For each inoculated seed at the given second: the nursery-rooted living split by jointed and rigid, each
with how many are absorptive; the jointed ones by run and body; and how many generations each jointed
body sits below its nursery founder. Built on scripts/reads/r55-read.py's own functions.

Usage: python scripts/reads/r55-entry/jointed-nursery.py [--runs-root R] [--arms r55-s2 r55-s3] [--at 30000]
"""
import argparse
import importlib.util
import os
from collections import Counter
from statistics import median

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
spec = importlib.util.spec_from_file_location("r55", os.path.join(REPO, "scripts", "reads", "r55-read.py"))
r55 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r55)


def depth(births, i):
    n = 0
    while births.get(i, {}).get("p", -1) >= 0:
        i = births[i]["p"]
        n += 1
    return n


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"))
    ap.add_argument("--arms", nargs="+", default=["r55-s2", "r55-s3"])
    ap.add_argument("--at", type=float, default=30000.0)
    a = ap.parse_args()
    for arm in a.arms:
        d = r55.run_dir(a.runs_root, arm)
        births, death_t, index = r55.read(d, a.at)
        root = r55.root_of(births)
        alive = [i for i, b in births.items() if b["t"] <= a.at and death_t.get(i, float("inf")) > a.at]
        rooted = []
        for i in alive:
            o = r55.origin(births[root(i)], index)
            if o is not None:
                rooted.append((i, o))
        jointed = [(i, o) for i, o in rooted if births[i].get("jnt") == 1]
        rigid = [(i, o) for i, o in rooted if births[i].get("jnt") != 1]
        absorb = lambda xs: sum(1 for i, _ in xs if births[i].get("abs") == 1)
        print(f"== {arm} at {a.at:.0f} s: living {len(alive)}, nursery-rooted {len(rooted)}")
        print(f"   jointed {len(jointed)} (absorptive {absorb(jointed)}), rigid {len(rigid)} (absorptive {absorb(rigid)})")
        by = Counter((o["run"], o["body"]) for _, o in jointed)
        for (run, body), n in by.most_common():
            print(f"   jointed from {run} body {body}: {n}")
        roots_j = Counter(root(i) for i, _ in jointed)
        print(f"   jointed lines (roots) {len(roots_j)}; the founder itself jointed in "
              f"{sum(1 for r in roots_j if births[r].get('jnt') == 1)} of them")
        if jointed:
            ds = [depth(births, i) for i, _ in jointed]
            print(f"   generations below the founder, jointed: median {median(ds)}, range {min(ds)} to {max(ds)}")
        if rigid:
            ds = [depth(births, i) for i, _ in rigid]
            print(f"   generations below the founder, rigid: median {median(ds)}, range {min(ds)} to {max(ds)}")


if __name__ == "__main__":
    main()
