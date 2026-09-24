"""Supplement: the absorptive log by age for stomachs, the snow profile by depth seen by every
absorptive body, the per-body cell density against its column mean, and R0 per class.
python scratch/r47-stomachs/ages.py"""
import json, os, math, bisect
from collections import defaultdict
import numpy as np

REPO = r"D:\Projects\experiments\evolution-simulator"
OUT = os.path.join(REPO, "scratch", "r47-stomachs")
RUNS = {1: "runs/r47-s1/2026-09-23-232448-7a7f5d41", 2: "runs/r47-s2/2026-09-23-232452-7a7f5d41",
        3: "runs/r47-s3/2026-09-24-022531-7a7f5d41"}


def med(xs):
    xs = sorted(xs)
    return (xs[len(xs) // 2], xs[len(xs) // 4], xs[(3 * len(xs)) // 4], len(xs)) if xs else None


def fmt(m, f="%.2f"):
    return "-" if m is None else (f + " [" + f + "-" + f + "] n=%d") % m


AGE_BINS = [0, 20, 100, 300, 1000, 2000, 3000, 1e9]
DEPTH_BINS = [0, 1, 2, 3, 5, 8, 12, 20, 60]
for s, rd in RUNS.items():
    o = json.load(open(os.path.join(OUT, "r47-s%d.json" % s)))
    by = {r["id"]: r for r in o["recs"]}
    d = os.path.join(REPO, rd)
    rows = defaultdict(list)
    for line in open(os.path.join(d, "absorptive.jsonl")):
        r = json.loads(line)
        if "id" in r:
            rows[r["id"]].append(r)
    print("\n### seed %d" % s)
    # age profile of pure stomachs (not mixotroph) that lived > 1000 s, and of mixotrophs
    for label, sel in (("pure stomachs living >1000 s", lambda r: not r["mixotroph"]),
                       ("mixotrophs (any life)", lambda r: r["mixotroph"])):
        print(label + ": age bin | densityHere | foodW | upkeepW | netW | energy J")
        acc = defaultdict(lambda: defaultdict(list))
        ids = set()
        for i, rs in rows.items():
            live = [r for r in rs if not r["dead"]]
            if not live or not sel(live[0]):
                continue
            if label.startswith("pure") and (live[-1]["age"] < 1000):
                continue
            ids.add(i)
            for r in live:
                k = bisect.bisect_right(AGE_BINS, r["age"]) - 1
                for f in ("densityHere", "foodW", "upkeepW", "netW", "energy"):
                    acc[k][f].append(r[f])
        print("  bodies: %d" % len(ids))
        for k in sorted(acc):
            a = acc[k]
            print("  %g-%g | %s | %s | %s | %s | %s" % (AGE_BINS[k], AGE_BINS[k + 1], fmt(med(a["densityHere"])),
                  fmt(med(a["foodW"]), "%.3f"), fmt(med(a["upkeepW"]), "%.3f"), fmt(med(a["netW"]), "%.3f"),
                  fmt(med(a["energy"]), "%.0f")))
    # depth profile of densityHere over every live absorptive row, by time third
    print("densityHere by depth (every live absorptive row; the snow as sampled where eaters are)")
    for lo, hi in ((0, 10000), (10000, 20000), (20000, 30001)):
        acc = defaultdict(list)
        for i, rs in rows.items():
            for r in rs:
                if r["dead"] or not (lo <= r["t"] < hi) or r["age"] > 30:
                    continue
                k = bisect.bisect_right(DEPTH_BINS, -r["y"]) - 1
                acc[k].append(r["densityHere"])
        print("  t %d-%d (rows at age <=30 s, before the body's own grazing has much effect): " % (lo, hi) + "; ".join(
            "%g-%g m %s" % (DEPTH_BINS[k], DEPTH_BINS[k + 1], fmt(med(acc[k]))) for k in sorted(acc)))
    # cell density vs column mean, joined on track (every 100 s)
    ratio_young, ratio_old = [], []
    for i, r in by.items():
        if not r["abs"]:
            continue
        tc = dict((t, v) for t, v in r.get("track_col", []))
        for a in rows.get(i, []):
            if a["dead"] or a["t"] not in tc or tc[a["t"]] is None or tc[a["t"]] <= 0:
                continue
            (ratio_young if a["age"] <= 100 else ratio_old).append(a["densityHere"] / tc[a["t"]])
    print("cell density / column mean at the body: age<=100 s %s; age>100 s %s" % (fmt(med(ratio_young)), fmt(med(ratio_old))))
    # R0 by class: children per body that died (all children, and stomach children)
    for cls in ("child:stomach", "child:mixo", "child:photo", "trickle", "pool"):
        rs = [r for r in o["recs"] if r["abs"] and r["src"] == cls and not r["alive_end"]]
        if rs:
            print("R0 %s: %.2f children per dead body (%.2f stomach children), n=%d" % (
                cls, np.mean([r["nkids"] for r in rs]), np.mean([r["kid_stomachs"] for r in rs]), len(rs)))
    # born-rich stomachs (dh0 > 1): the whole-body R0
    rs = [r for r in o["recs"] if r["abs"] and r.get("dh0", 0) > 1 and not r["alive_end"]]
    if rs:
        print("R0 born at dh>1: %.2f (n=%d), mixotroph share %.2f" % (np.mean([r["nkids"] for r in rs]), len(rs),
              np.mean([bool(r["pho"]) for r in rs])))
    # longest pure-stomach chain (generations of abs-pho0 parent->child)
    B = {r["id"]: r for r in o["recs"]}
    depth = {}

    def chain(i):
        if i in depth:
            return depth[i]
        r = B.get(i)
        if r is None or not r["abs"] or r["pho"]:
            return 0
        p = r["p"]
        depth[i] = 1 + (chain(p) if p in B else 0)
        return depth[i]
    mx = max((chain(i) for i in B), default=0)
    print("longest chain of pure stomachs (parent and child both abs 1, pho 0): %d" % mx)
    # longest chain of any absorptive (incl. mixo)
    depth2 = {}

    def chain2(i):
        if i in depth2:
            return depth2[i]
        r = B.get(i)
        if r is None or not r["abs"]:
            return 0
        depth2[i] = 1 + (chain2(r["p"]) if r["p"] in B else 0)
        return depth2[i]
    print("longest chain of absorptive bodies (pure or mixo): %d" % max((chain2(i) for i in B), default=0))
