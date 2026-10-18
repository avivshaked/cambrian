"""Round 58's Y re-read to an earlier end: r58-read.py's winter depths and Pearson pairs, stats only.

Same windows, same first year, same pairs and same correlation as r58-read.py; the one change is --end, so
a world that only the card made after some second can be read without its last years. Run it at --end 48000
first: it must print r58-read.txt's seven r values exactly before its earlier ends mean anything.

Usage: python scripts/reads/r58-y-cut.py --end 42000 --joined DIR DIR DIR --isolated DIR DIR DIR
"""
import argparse
import glob
import json
import math
import os

YEAR = 6000.0
SUMMER_FRAC = (-250.0 / 6000.0, 2750.0 / 6000.0)
WINTER_FRAC = (2750.0 / 6000.0, 5750.0 / 6000.0)
FIRST_YEAR_READ = 2


def resolve(d):
    if os.path.exists(os.path.join(d, "stats.jsonl")):
        return d
    return sorted(x for x in glob.glob(os.path.join(d, "*")) if os.path.exists(os.path.join(x, "stats.jsonl")))[-1]


def depths(d, end):
    stats = []
    with open(os.path.join(resolve(d), "stats.jsonl"), encoding="utf-8") as f:
        for line in f:
            if line.endswith("\n") and line.strip():
                r = json.loads(line)
                stats.append((r["t"], r["alive"]))
    end = min(end, stats[-1][0])
    out, y = {}, 1
    while True:
        base = (y - 1) * YEAR
        s0, s1 = base + SUMMER_FRAC[0] * YEAR, base + SUMMER_FRAC[1] * YEAR
        w0, w1 = base + WINTER_FRAC[0] * YEAR, base + WINTER_FRAC[1] * YEAR
        if w1 > end:
            break
        summer = [a for t, a in stats if max(s0, 0) <= t < s1]
        winter = [a for t, a in stats if w0 <= t < w1]
        most = max(summer) if summer else None
        fewest = min(winter) if winter else None
        out[y] = 1 - fewest / most if most and fewest is not None else None
        y += 1
    return out


def pearson(xs, ys):
    n = len(xs)
    if n < 2:
        return None
    mx, my = sum(xs) / n, sum(ys) / n
    sx = math.sqrt(sum((x - mx) ** 2 for x in xs))
    sy = math.sqrt(sum((y - my) ** 2 for y in ys))
    if sx == 0 or sy == 0:
        return None
    return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / (sx * sy)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--end", type=float, required=True)
    ap.add_argument("--joined", nargs=3, required=True)
    ap.add_argument("--isolated", nargs=3, required=True)
    a = ap.parse_args()
    means = {}
    for group, dirs in (("joined", a.joined), ("isolated", a.isolated)):
        ds = [(os.path.basename(os.path.normpath(d)), depths(d, a.end)) for d in dirs]
        rs = []
        for i in range(3):
            for j in range(i + 1, 3):
                (nu, u), (nv, v) = ds[i], ds[j]
                ys = [y for y in sorted(u) if y >= FIRST_YEAR_READ and y in v and u[y] is not None and v[y] is not None]
                r = pearson([u[y] for y in ys], [v[y] for y in ys])
                print(f"   {group} {nu} x {nv}: years {ys[0]} to {ys[-1]} ({len(ys)}), r = {r:.3f}")
                if r is not None:
                    rs.append(r)
        means[group] = sum(rs) / len(rs)
        print(f"   {group} mean r = {means[group]:.3f} over {len(rs)} pairs")
    print(f"Y at --end {a.end:g}: {'held' if means['joined'] > means['isolated'] else 'failed'}")


if __name__ == "__main__":
    main()
