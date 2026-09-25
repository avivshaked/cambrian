"""The stomach's share over time in a line founded by a stomach bud on a leaf (round 48, 0120).

A line is every descendant of its first body through lineage.jsonl's parent links, the first
body included. For each window, over the line's rows in absorptive.jsonl (which logs a body
with an absorptive part, so a member that shed its stomach drops out of the file), it prints
how many members were logged, and the medians of the stomach's share of the body's volume
(absVolume / volume) and of its income (foodW / (foodW + lightW)). The count of living members
in the window comes from lineage.jsonl, so the shed ones are counted there.

    python scripts/reads/r48-entry/budshare.py r48-s1 48048 --every 1000
"""
import argparse
import glob
import json
import os
import statistics

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def run_dir(arm):
    runs = sorted(glob.glob(os.path.join(REPO, "runs", arm, "*", "run.json")))
    if not runs:
        raise SystemExit("no run under runs/%s" % arm)
    return os.path.dirname(runs[-1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("arm")
    ap.add_argument("first", type=int)
    ap.add_argument("--every", type=float, default=1000.0)
    a = ap.parse_args()
    run = run_dir(a.arm)

    parent, born, died = {}, {}, {}
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            if r["e"] == "b":
                parent[r["id"]] = r["p"]
                born[r["id"]] = r["t"]
            elif r["e"] == "d":
                died[r["id"]] = r["t"]

    line_ids = {a.first}
    for i in sorted(born, key=lambda k: born[k]):
        if parent.get(i) in line_ids:
            line_ids.add(i)
    end = max(born.values())

    windows = {}
    with open(os.path.join(run, "absorptive.jsonl"), encoding="utf-8") as f:
        for line in f:
            k = line.find('"id":')
            i = int(line[k + 5:line.find(",", k)])
            if i not in line_ids:
                continue
            r = json.loads(line)
            if r.get("dead"):
                continue
            w = int(r["t"] // a.every)
            vol = r["volume"]
            inc = r["foodW"] + r["lightW"]
            windows.setdefault(w, []).append((
                i,
                r["absVolume"] / vol if vol > 0 else float("nan"),
                r["foodW"] / inc if inc > 0 else float("nan"),
            ))

    print("%s line of %d: %d bodies ever" % (a.arm, a.first, len(line_ids)))
    print("window_s\tliving_at_end\tlogged_bodies\tmedian_abs_volume_share\tmedian_food_income_share")
    t0 = int(born[a.first] // a.every)
    for w in range(t0, int(end // a.every) + 1):
        t_end = (w + 1) * a.every
        living = sum(1 for i in line_ids if born[i] <= t_end and died.get(i, float("inf")) > t_end)
        rows = windows.get(w, [])
        bodies = {i for i, _, _ in rows}
        vs = [v for _, v, _ in rows if v == v]
        fs = [x for _, _, x in rows if x == x]
        print("%d\t%d\t%d\t%s\t%s" % (
            w * a.every, living, len(bodies),
            "%.4f" % statistics.median(vs) if vs else "-",
            "%.4f" % statistics.median(fs) if fs else "-"))


if __name__ == "__main__":
    main()
