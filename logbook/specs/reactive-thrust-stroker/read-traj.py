"""Reads a trajectory CSV and says whether every joint actually strokes.

Amplitude is peak to peak over the last 40 s, so a stroke that dies out in the
first few seconds cannot pass on its transient. Period is the mean spacing of
upward zero crossings of the centred angle over the same window.
"""
import csv
import glob
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def read(path):
    with open(path, newline="") as handle:
        rows = list(csv.DictReader(handle))
    cols = rows[0].keys()
    joints = [c for c in cols if c.startswith("q") and c[1:].isdigit()]
    t = [float(r["t"]) for r in rows]
    series = {j: [float(r[j]) for r in rows] for j in joints}
    pos = [(float(r["x"]), float(r["y"]), float(r["z"])) for r in rows]
    return t, series, pos


def crossings(t, v, level):
    times = []
    for i in range(1, len(v)):
        if v[i - 1] <= level < v[i]:
            span = v[i] - v[i - 1]
            frac = (level - v[i - 1]) / span if span else 0.0
            times.append(t[i - 1] + frac * (t[i] - t[i - 1]))
    return times


def report(path):
    t, series, pos = read(path)
    print(os.path.basename(path), " last t =", t[-1], "s")

    start = next(i for i, x in enumerate(t) if x >= 20.0)
    for name, v in series.items():
        w = v[start:]
        tw = t[start:]
        lo, hi = min(w), max(w)
        mid = 0.5 * (lo + hi)
        ups = crossings(tw, w, mid)
        if len(ups) >= 2:
            period = (ups[-1] - ups[0]) / (len(ups) - 1)
            cycles = len(ups) - 1
        else:
            period, cycles = float("nan"), 0
        # sustained: compare the first and last thirds of the window
        third = len(w) // 3
        early = max(w[:third]) - min(w[:third])
        late = max(w[-third:]) - min(w[-third:])
        print("   {}: p2p {:.4f} rad over t=20-60 s  (min {:+.4f}, max {:+.4f})  "
              "period {:.3f} s from {} cycles  p2p early {:.4f} / late {:.4f}"
              .format(name, hi - lo, lo, hi, period, cycles, early, late))

    d = tuple(pos[-1][k] - pos[0][k] for k in range(3))
    net = sum(x * x for x in d) ** 0.5
    print("   root link: net displacement {:.4f} m  (dx {:+.4f}, dy {:+.4f}, dz {:+.4f})"
          .format(net, *d))
    print()


if __name__ == "__main__":
    where = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "traj-ours")
    for path in sorted(glob.glob(os.path.join(where, "*.csv"))):
        report(path)
