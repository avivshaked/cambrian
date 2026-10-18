"""One look at the pace of every running farm world: simulated seconds per wall second over the last --recent
simulated seconds of rows, against the --before simulated seconds that preceded them, from stats.jsonl's cumulative
wallTotalMs. A world whose recent pace is under --ratio of its earlier pace is printed as SLOW, and the script exits 2.

Usage: python -I scripts/pace-check.py [RUN ...] [--runs-root runs] [--recent 300] [--before 3000] [--ratio 0.5]

A RUN is a run directory or an arm folder (its latest dated run). With none given, every run under --runs-root whose
run.json reads status "running" is checked. Reads the tail of each stats.jsonl only; writes nothing. Standard library
only. A watch runs it once per look (CLAUDE.md: a watch is one look that exits).
"""
import argparse, glob, json, os, sys


def latest(path):
    if os.path.exists(os.path.join(path, "stats.jsonl")):
        return path
    dirs = sorted(d for d in glob.glob(os.path.join(path, "*")) if os.path.isdir(d))
    return dirs[-1] if dirs else None


def tail_rows(path, nbytes):
    size = os.path.getsize(path)
    with open(path, "rb") as f:
        f.seek(max(0, size - nbytes))
        lines = f.read().splitlines()
    if size > nbytes:
        lines = lines[1:]
    rows = []
    for line in lines:
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if "t" in r and "wallTotalMs" in r:
            rows.append((float(r["t"]), float(r["wallTotalMs"])))
    return rows


def pace(rows, t0, t1):
    span = [r for r in rows if t0 <= r[0] <= t1]
    if len(span) < 2 or span[-1][1] <= span[0][1]:
        return None
    return (span[-1][0] - span[0][0]) / ((span[-1][1] - span[0][1]) / 1000.0)


def main():
    a = argparse.ArgumentParser()
    a.add_argument("runs", nargs="*")
    a.add_argument("--runs-root", default="runs")
    a.add_argument("--recent", type=float, default=300)
    a.add_argument("--before", type=float, default=3000)
    a.add_argument("--ratio", type=float, default=0.5)
    o = a.parse_args()
    if o.runs:
        dirs = [latest(p) for p in o.runs]
    else:
        dirs = []
        for m in glob.glob(os.path.join(o.runs_root, "*", "*", "run.json")):
            try:
                if json.load(open(m, encoding="utf-8")).get("status") == "running":
                    dirs.append(os.path.dirname(m))
            except (ValueError, OSError):
                pass
    slow = False
    if not dirs:
        print("pace: no running world")
    for d in dirs:
        if not d:
            continue
        name = os.path.basename(os.path.dirname(d.rstrip("\\/")))
        rows = tail_rows(os.path.join(d, "stats.jsonl"), 8_000_000)
        if not rows:
            print(f"pace {name}: no rows")
            continue
        t = rows[-1][0]
        now = pace(rows, t - o.recent, t)
        was = pace(rows, t - o.recent - o.before, t - o.recent)
        if now is None or was is None:
            print(f"pace {name}: t {t:.0f}, too few rows")
            continue
        flag = now < o.ratio * was
        slow |= flag
        print(f"pace {name}: t {t:.0f}, recent {now:.2f}x real time, before {was:.2f}x"
              f"{' SLOW' if flag else ''}")
    sys.exit(2 if slow else 0)


if __name__ == "__main__":
    main()
