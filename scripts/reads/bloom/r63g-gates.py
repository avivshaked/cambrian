"""r63g's leaf gates (scratch/wt-r63b's logbook/specs/r63g-prereg/predictions.md, gates 1, 2 and 5): the living leaf
bodies (stats.jsonl's `photosynthetic`) at --end, the mean over 21,000-24,000 s against the mean over 12,000-15,000 s,
and the fewest after 3,000 s; with the bodies alive at most and at --end, and the matter standing, for the report.

Usage: python -I scripts/reads/bloom/r63g-gates.py RUN [RUN ...] [--end 24000]

A RUN is an arm folder (its latest dated run is read) or a run directory; a resumed run is read from its first dated run
on. Reads stats.jsonl only; writes nothing. Standard library only.
"""
import argparse, glob, json, os


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


def mean(xs):
    return sum(xs) / len(xs) if xs else float("nan")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=24000.0)
    a = ap.parse_args()
    print("run                 leaves@end  mean21-24k  mean12-15k  ratio  fewest>3000s (at)   alive max  alive@end  matter@end")
    for p in a.runs:
        rows = rows_of(p, a.end)
        last = rows[-1]
        late = [r["photosynthetic"] for r in rows if 21000 <= r["t"] < 24000]
        mid = [r["photosynthetic"] for r in rows if 12000 <= r["t"] < 15000]
        after = [r for r in rows if r["t"] > 3000]
        few = min(after, key=lambda r: r["photosynthetic"])
        name = os.path.basename(os.path.normpath(p))
        print(f"{name:18s}  {last['photosynthetic']:10d}  {mean(late):10.0f}  {mean(mid):10.0f}  "
              f"{mean(late) / mean(mid):5.2f}  {few['photosynthetic']:7d} ({int(few['t']):6d})  {max(r['alive'] for r in rows):9d}  "
              f"{last['alive']:9d}  {last['matterStanding']:10.0f}   t={int(last['t'])}")


if __name__ == "__main__":
    main()
