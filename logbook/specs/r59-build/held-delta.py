"""Contact counts over each run's stats rows after a resume, as deltas from the source run's row at the
resume time: pairs, held pairs, the unheld rest, jointed, overlapping bodies and bed-or-glass bodies, per step.

Usage: python held-delta.py <source stats.jsonl> <at seconds> <run stats.jsonl> [<run stats.jsonl> ...]
"""
import json
import sys

KEYS = ["overlapPairs", "overlapPairsHeld", "overlapPairsJointed", "overlapBodies", "bedOrGlassBodies"]


def rows(path):
    with open(path) as f:
        return [json.loads(x) for x in f if x.strip()]


def main():
    src, at, runs = sys.argv[1], float(sys.argv[2]), sys.argv[3:]
    base = next(r for r in rows(src) if abs(r["t"] - at) < 1e-6)
    for path in runs:
        print(path.replace("\\", "/").split("/runs/")[-1])
        prev = base
        for r in rows(path):
            steps = r["steps"] - prev["steps"] if "steps" in r else None
            d = {k: r[k] - prev[k] for k in KEYS}
            n = steps or 1
            print(f"  t {r['t']:.0f}: steps {steps}; pairs {d['overlapPairs'] / n:.3f}, held {d['overlapPairsHeld'] / n:.3f}, "
                  f"unheld {(d['overlapPairs'] - d['overlapPairsHeld']) / n:.3f}, jointed {d['overlapPairsJointed'] / n:.3f}, "
                  f"bodies {d['overlapBodies'] / n:.3f}, bed or glass {d['bedOrGlassBodies'] / n:.3f}")
            prev = r


main()
