"""Where bite income comes from: the shares of kept bite joules from living leaves (`biteKeptLeaves`, bodies with
photosynthetic tissue), from other living bodies (`biteKeptOthers`) and from remains (`biteKeptRemains`), over the whole
run to --end and over the last window (--from to --end), from stats.jsonl's running totals. The swallowed guts
(`gutSwallowedJoules`) are reported beside them, since a kill's gut is income from an animal too.

Usage: python -I scripts/reads/bloom/income-split.py RUN [RUN ...] [--from 21000] [--end 24000]

A RUN is an arm folder (its dated runs are read in order) or a run directory. Reads stats.jsonl only; writes nothing.
"""
import argparse, glob, json, os

COLS = ("biteKeptLeaves", "biteKeptOthers", "biteKeptRemains", "gutSwallowedJoules")


def run_dirs(path):
    if os.path.exists(os.path.join(path, "stats.jsonl")):
        return [path]
    return sorted(d for d in glob.glob(os.path.join(path, "*")) if os.path.exists(os.path.join(d, "stats.jsonl")))


def rows_of(path, end):
    rows = {}
    for d in run_dirs(path):
        with open(os.path.join(d, "stats.jsonl"), encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    r = json.loads(line)
                    if r["t"] <= end:
                        rows[r["t"]] = r
    return [rows[t] for t in sorted(rows)]


def shares(a, b):
    d = [b.get(c, 0.0) - (a.get(c, 0.0) if a else 0.0) for c in COLS]
    bite = d[0] + d[1] + d[2]
    if bite <= 0:
        return "no bite income"
    return (f"leaves {d[0] / bite:.2f}  others {d[1] / bite:.2f}  remains {d[2] / bite:.2f}  "
            f"(bite kept {bite / 1e6:.2f} MJ, swallowed {d[3] / 1e6:.2f} MJ)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--from", dest="start", type=float, default=21000.0)
    ap.add_argument("--end", type=float, default=24000.0)
    a = ap.parse_args()
    for p in a.runs:
        rows = rows_of(p, a.end)
        name = os.path.basename(os.path.normpath(p))
        before = [r for r in rows if r["t"] <= a.start]
        print(f"{name}  t={int(rows[-1]['t'])}")
        print(f"  whole run     {shares(None, rows[-1])}")
        if before:
            print(f"  {int(a.start)}-{int(rows[-1]['t'])}  {shares(before[-1], rows[-1])}")


if __name__ == "__main__":
    main()
