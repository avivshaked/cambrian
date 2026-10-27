"""The variety of body plans in a run: the quartiles of the bred grazers' part counts (`pt` on the birth rows) by
birth bin, for the structural mutation screen's side readout (logbook/specs/r62sm-prereg/predictions.md). A bred grazer
is a body born with atk 1 and ink 1 and a parent (p >= 0), as grazer-tally.py defines a grazer. Also prints the count
of distinct part counts in each bin.

Usage: python -I scripts/reads/part-count-read.py RUN [RUN ...] [--bin 3000] [--end 24000]
"""
import argparse
import json
import os

ap = argparse.ArgumentParser()
ap.add_argument("runs", nargs="+")
ap.add_argument("--bin", type=float, default=3000.0)
ap.add_argument("--end", type=float, default=24000.0)
a = ap.parse_args()


def quartiles(v):
    v = sorted(v)
    n = len(v)
    if n == 0:
        return None

    def q(f):
        x = f * (n - 1)
        lo = int(x)
        hi = min(lo + 1, n - 1)
        return v[lo] + (v[hi] - v[lo]) * (x - lo)
    return q(0.25), q(0.5), q(0.75)


for run in a.runs:
    bins = {}
    for line in open(os.path.join(run, "lineage.jsonl"), encoding="utf-8"):
        if not line.strip():
            continue
        r = json.loads(line)
        if r.get("e") != "b" or r.get("atk") != 1 or r.get("ink") != 1 or r.get("p", -1) < 0:
            continue
        t = float(r["t"])
        if t > a.end:
            continue
        bins.setdefault(int(t // a.bin), []).append(int(r["pt"]))
    print(run)
    print("  %-13s %6s %6s %6s %6s %6s %8s" % ("born", "n", "q1", "median", "q3", "max", "distinct"))
    for k in sorted(bins):
        v = bins[k]
        q1, md, q3 = quartiles(v)
        print("  %-13s %6d %6.1f %6.1f %6.1f %6d %8d" % (
            "%d-%d" % (k * a.bin, (k + 1) * a.bin), len(v), q1, md, q3, max(v), len(set(v))))
