"""Leaves and bodies by window, from a run's stats rows: for a resumed step, whose windows start where the step began.
"Leaves" is the stats column `photosynthetic`, as r63g-gates.py reads it. Reads the given run directory only, so a
resumed run is read from its own start.

Usage: python -I scripts/reads/bloom/window-leaves.py RUN_DIR [RUN_DIR ...] [--from 24000] [--end 48000] [--window 3000]

Prints, per run, each window's mean, fewest and most leaves and its mean of living bodies, then the fewest leaves after
--from with their second, and each window's mean as a ratio of the window before. Writes nothing.
"""
import argparse, json, os


def rows(run):
    out = []
    with open(os.path.join(run, "stats.jsonl"), encoding="utf-8") as f:
        for line in f:
            try:
                r = json.loads(line)
            except ValueError:
                continue  # a live run's last line, half written
            if "photosynthetic" in r:
                out.append(r)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--from", dest="start", type=float, default=24000.0)
    ap.add_argument("--end", type=float, default=48000.0)
    ap.add_argument("--window", type=float, default=3000.0)
    a = ap.parse_args()
    for run in a.runs:
        rs = [r for r in rows(run) if a.start <= r["t"] <= a.end]
        print(f"== {run}  rows {len(rs)}  last t {rs[-1]['t'] if rs else '-'}")
        print("   window        leaves mean  fewest   most   alive mean   ratio to the window before")
        prev = None
        t0 = a.start
        while t0 < a.end:
            t1 = t0 + a.window
            w = [r for r in rs if t0 <= r["t"] < t1 or (t1 >= a.end and r["t"] == a.end)]
            if w:
                lv = [r["photosynthetic"] for r in w]
                mean = sum(lv) / len(lv)
                alive = sum(r["alive"] for r in w) / len(w)
                ratio = f"{mean / prev:.2f}" if prev else "-"
                print(f"   {int(t0):>5}-{int(t1):<5}  {mean:10.0f}  {min(lv):6d}  {max(lv):5d}  {alive:10.0f}   {ratio}")
                prev = mean
            t0 = t1
        if rs:
            low = min(rs, key=lambda r: r["photosynthetic"])
            print(f"   fewest leaves from {int(a.start)} s: {low['photosynthetic']} at {low['t']} s")


if __name__ == "__main__":
    main()
