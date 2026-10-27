"""Growth group 1's gene drift: the growth-reserve gene G (lineage birth rows' grs) by guild and 3,000 s bin, and each
run's divergences and wall share of applying growth (run.json).

A grazer is born mouthed (atk 1 and ink 1), a leaf born photosynthetic (pho 1) and not a grazer. Bred is p >= 0.

Usage: python -I scripts/reads/grs-drift-read.py RUN [RUN ...]
"""
import json
import os
import statistics
import sys


def latest(path):
    if os.path.exists(os.path.join(path, "lineage.jsonl")):
        return path
    subs = sorted(d for d in os.listdir(path) if os.path.isdir(os.path.join(path, d)))
    return os.path.join(path, subs[-1])


def q(xs, p):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


for arg in sys.argv[1:]:
    run = latest(arg)
    m = json.load(open(os.path.join(run, "run.json"), encoding="utf-8"))
    total = m.get("wallTotalMs") or 0
    grow = m.get("wallHarnessGrowthMs") or 0
    print(f"\n== {run}: configHash {m.get('configHash')}, diverged {m.get('divergedTotal')}, "
          f"growth applied {grow / total:.2%} of the wall" if total else f"\n== {run}")
    bins = {}
    missing = 0
    for line in open(os.path.join(run, "lineage.jsonl")):
        if '"e":"b"' not in line:
            continue
        r = json.loads(line)
        if r.get("p", -1) < 0:
            continue
        g = r.get("grs")
        if g is None:
            missing += 1
            continue
        kind = "grazer" if (r.get("atk") == 1 and r.get("ink") == 1) else ("leaf" if r.get("pho") == 1 else "other")
        b = int(r["t"] // 3000)
        bins.setdefault((kind, b), []).append(float(g))
    print(f"   bred births without grs: {missing}")
    print(f"   {'guild':<7} {'bin':>11} {'n':>6} {'q1':>7} {'median':>7} {'q3':>7}")
    for (kind, b) in sorted(bins):
        xs = bins[(kind, b)]
        print(f"   {kind:<7} {b * 3000:5d}-{b * 3000 + 3000:<5d} {len(xs):6d} {q(xs, .25):7.2f} "
              f"{statistics.median(xs):7.2f} {q(xs, .75):7.2f}")
