"""Where newborns sit in the water column over their first minute: each child's root height y (y up, the water's surface
at y = 0, the bed near -WorldDepthMetres) by age, from the newborn ledger's frame table, by the brain-at-birth bench's
groups (standstill, slow, moving; logbook/specs/r62p-read/brain-birth/). Asks whether the standstill newborns are held at
the surface (a part at or above y = 0, where Fluid.Restore pushes it down) or sit still below it.

Per group: the root y quartiles at the first frame and at ages 10, 20, 30 and 60 s (the frame nearest each age, within
5 s); the share whose root is within --near m of the surface at the first frame; the median vertical change between the
first two frames; and the share that rise, sink or hold (within 0.01 m) between them.

The frame table holds the root only, so a part above the root can reach the surface while the root sits lower; the
--near band is the root's distance, and the bench's buoyancy columns say whether a body floats.

Usage: python -I scripts/reads/newborn-height-read.py IDS FRAMES [--near 1.0]
  IDS is brain-birth-ids.py's file (id parent group firstSpeed ...); FRAMES is newborn-extract.py's grazer-frames.tsv.
"""
import argparse
import csv
import statistics

ap = argparse.ArgumentParser()
ap.add_argument("ids")
ap.add_argument("frames")
ap.add_argument("--near", type=float, default=1.0)
a = ap.parse_args()

group = {}
with open(a.ids, encoding="utf-8") as f:
    for r in csv.DictReader(f, delimiter="\t"):
        group[int(r["id"])] = r["group"]

track = {i: [] for i in group}
with open(a.frames, encoding="utf-8") as f:
    head = f.readline().rstrip("\n").split("\t")
    ci, ct, cy = head.index("id"), head.index("t"), head.index("y")
    for line in f:
        p = line.rstrip("\n").split("\t")
        i = int(p[ci])
        if i in track:
            track[i].append((float(p[ct]), float(p[cy])))

AGES = (10.0, 20.0, 30.0, 60.0)


def q(v):
    if not v:
        return "-"
    v = sorted(v)
    n = len(v)
    return "%.2f / %.2f / %.2f" % (v[n // 4], v[n // 2], v[(3 * n) // 4])


print("newborn-height-read: ids %s, frames %s, near-surface band %.2f m (root y > -%.2f)" % (a.ids, a.frames, a.near, a.near))
for g in ("standstill", "slow", "moving"):
    ids = [i for i in group if group[i] == g]
    first, near, d01, rise, sink, hold = [], 0, [], 0, 0, 0
    at = {x: [] for x in AGES}
    n = 0
    for i in ids:
        tr = sorted(track[i])
        if not tr:
            continue
        n += 1
        t0, y0 = tr[0]
        first.append(y0)
        if y0 > -a.near:
            near += 1
        if len(tr) >= 2:
            dy = tr[1][1] - y0
            d01.append(dy)
            if dy > 0.01:
                rise += 1
            elif dy < -0.01:
                sink += 1
            else:
                hold += 1
        for x in AGES:
            best = min(tr, key=lambda r: abs(r[0] - t0 - x))
            if abs(best[0] - t0 - x) <= 5.0:
                at[x].append(best[1])
    pairs = rise + sink + hold
    print("\n== %s: %d children with frames (of %d)" % (g, n, len(ids)))
    print("  root y at the first frame, q1 / median / q3: %s m" % q(first))
    for x in AGES:
        print("  root y at age %3.0f s (n %4d):           %s m" % (x, len(at[x]), q(at[x])))
    print("  within %.2f m of the surface at the first frame: %d of %d (%.3f)" % (a.near, near, n, near / n if n else float("nan")))
    if pairs:
        print("  first two frames: median dy %.4f m; rise %.3f, sink %.3f, hold %.3f (of %d)" % (
            statistics.median(d01), rise / pairs, sink / pairs, hold / pairs, pairs))
