"""Is round 46's flat crowd selected, inherited by pose, or laid flat by the water over a life?

python scripts/reads/r46-tilt-age.py <run dir> <second> [--stride 10]

Three readings from poses.jsonl, lineage.jsonl and the snapshot at <second>, root part only,
tilt as tilt.py reads it (0 flat, 90 on edge; the flat factor is |cos tilt|):

  1. tilt by age at <second>: the living binned by age, with the mean flat factor of each bin.
     A crowd laid flat by the water over a life reads flatter with age; a crowd born flat does not.
  2. tilt by the genome's buoyancy offset at <second>, |offset| above and below 0.2 (K2's cut).
  3. a child's first recorded tilt against its parent's tilt in the same pose row, over every
     pose row read (every `stride`-th row, so a child's first row is within stride samples of its
     birth): the correlation and the mean flat factors, and the same for the child's last row.
     A pose inherited at birth reads as a correlation near one at the first row; a pose reached
     over a life reads as a first-row factor near the founding's and a last-row factor near the
     crowd's.
"""
import json, math, os, sys
from collections import defaultdict


def rotate(q, v):
    x, y, z, w = q
    cx = y * v[2] - z * v[1]; cy = z * v[0] - x * v[2]; cz = x * v[1] - y * v[0]
    dx = y * cz - z * cy; dy = z * cx - x * cz; dz = x * cy - y * cx
    return (v[0] + 2 * (w * cx + dx), v[1] + 2 * (w * cy + dy), v[2] + 2 * (w * cz + dz))


def flat_factor(q, dims):
    axis = min(range(3), key=lambda i: dims[i])
    local = [0.0, 0.0, 0.0]; local[axis] = 1.0
    return abs(rotate(q, local)[1])


def mean(xs):
    return sum(xs) / len(xs) if xs else float("nan")


def corr(xs, ys):
    n = len(xs)
    if n < 3:
        return float("nan")
    mx, my = mean(xs), mean(ys)
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sxx = sum((x - mx) ** 2 for x in xs); syy = sum((y - my) ** 2 for y in ys)
    return sxy / math.sqrt(sxx * syy) if sxx > 0 and syy > 0 else float("nan")


def main():
    run = sys.argv[1]; second = int(sys.argv[2])
    stride = int(sys.argv[sys.argv.index("--stride") + 1]) if "--stride" in sys.argv else 10
    # the genomes: dims and offset of the root, for every body ever snapshotted we need dims;
    # the snapshot at `second` gives the living; earlier snapshots give the rest of the crowd.
    dims = {}; offset = {}
    snaps = sorted(os.listdir(os.path.join(run, "snapshots")))
    for name in snaps:
        with open(os.path.join(run, "snapshots", name), encoding="utf-8") as f:
            for line in f:
                g = json.loads(line)
                if g["id"] in dims:
                    continue
                root = g["nodes"][g["root"]]
                d = root["dimensions"]
                dims[g["id"]] = (d["x"], d["y"], d["z"])
                offset[g["id"]] = root.get("buoyancyOffset", 0.0)
    birth = {}; parent = {}
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            if r["e"] == "b":
                birth[r["id"]] = r["t"]; parent[r["id"]] = r["p"]
    living = None
    first = {}; last = {}; parent_at_first = {}
    n_rows = 0
    with open(os.path.join(run, "poses.jsonl"), encoding="utf-8") as f:
        for k, line in enumerate(f):
            is_target = line.startswith('{"t":%d,' % second)
            if k % stride and not is_target:
                continue
            row = json.loads(line); n_rows += 1
            here = {}
            for b in row["bodies"]:
                d = dims.get(b["id"])
                if d is None:
                    continue
                here[b["id"]] = flat_factor(b["r"], d)
            for i, ff in here.items():
                if i not in first:
                    first[i] = (row["t"], ff)
                    p = parent.get(i, -1)
                    if p in here:
                        parent_at_first[i] = here[p]
                last[i] = (row["t"], ff)
            if is_target:
                living = here
    if living is None:
        raise SystemExit("no pose row at %d" % second)
    print("%s at %d s: %d living with a genome, %d pose rows read (every %dth), %d bodies seen"
          % (run, second, len(living), n_rows, stride, len(first)))
    print("1. flat factor by age at %d s (1 flat, 0.5 random):" % second)
    bins = defaultdict(list)
    for i, ff in living.items():
        age = second - birth.get(i, second)
        bins[min(int(age // 500), 6)].append(ff)
    for b in sorted(bins):
        label = "%4d-%4d s" % (b * 500, b * 500 + 500) if b < 6 else "   3000+ s"
        print("   %s  n %5d  mean %.3f" % (label, len(bins[b]), mean(bins[b])))
    print("2. flat factor by the root's buoyancy offset at %d s:" % second)
    high = [ff for i, ff in living.items() if abs(offset.get(i, 0.0)) > 0.2]
    low = [ff for i, ff in living.items() if abs(offset.get(i, 0.0)) <= 0.2]
    print("   |offset| > 0.2  n %5d  mean %.3f" % (len(high), mean(high)))
    print("   |offset| <= 0.2 n %5d  mean %.3f" % (len(low), mean(low)))
    print("3. a child's first recorded pose against its parent's in the same row:")
    pairs = [(parent_at_first[i], first[i][1]) for i in parent_at_first]
    print("   pairs %d, correlation %.3f, parent mean %.3f, child mean at its first row %.3f"
          % (len(pairs), corr([p for p, c in pairs], [c for p, c in pairs]),
             mean([p for p, c in pairs]), mean([c for p, c in pairs])))
    lived = [i for i in first if last[i][0] - first[i][0] >= 1000]
    print("   bodies seen over 1,000 s or more: %d; flat factor at their first row %.3f, at their last %.3f, "
          "correlation first-to-last %.3f"
          % (len(lived), mean([first[i][1] for i in lived]), mean([last[i][1] for i in lived]),
             corr([first[i][1] for i in lived], [last[i][1] for i in lived])))
    for lo, hi in ((0, 10000), (10000, 20000), (20000, 30000)):
        born = [i for i in first if lo <= first[i][0] < hi]
        print("   bodies first seen in %5d-%5d s: n %5d, flat factor at the first row %.3f, at the last %.3f"
              % (lo, hi, len(born), mean([first[i][1] for i in born]), mean([last[i][1] for i in born])))


main()
