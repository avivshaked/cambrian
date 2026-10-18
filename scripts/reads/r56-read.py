"""The reader for round 56's clauses (the pre-registration, logbook/0135), each seed against round 55's same seed.

G:  the first winter does not empty the tank: seed 2's living at 10,000 s are at least a quarter of round 55
    seed 2's at 10,000 s. Read before seeds 3 and 1 start.
S1: the seasons bite: in years 2 and 3 (10,000-20,000 s and 20,000-30,000 s) the fewest living in the year's
    second half are at least 20% below the most in its first half, in both years, in 3 of 3 seeds.
    Read on round 55 too, where it should fail.
S2: the hold loosens: the jointed share of the living at 30,000 s at least twice round 55's same seed, in at
    least 2 of 3 seeds.
S3: the leaves keep more in reserve: the median breeding margin (rm) of the living leaves (photosynthetic,
    rigid, not absorptive) at 30,000 s above round 55's same seed, in 3 of 3 seeds.
Readings: the living and the jointed every 2,500 s, the light factor at each, the leaves' median margin and
lifespan gene at 10,000, 20,000 and 30,000 s, and the largest root lineage's share of the living.
B1 is read by round 54's reader.

Usage: python scripts/reads/r56-read.py [--runs-root R] [--seeds 1 2 3] [--end T] [--gate]
"""
import argparse
import glob
import json
import os
from collections import Counter
from statistics import median

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
YEAR, END = 10000.0, 30000.0
G_SHARE, S1_DROP, S2_RATIO = 0.25, 0.20, 2.0


def run_dir(root, arm):
    dirs = sorted(d for d in glob.glob(os.path.join(root, arm, "*")) if os.path.isdir(d))
    return dirs[-1] if dirs else None


def rows(path):
    """A JSONL file's complete rows; a live run's last line may be half written and is skipped."""
    for l in open(path, encoding="utf-8"):
        if l.endswith("\n") and l.strip():
            yield json.loads(l)


def stats(d):
    return [r for r in rows(os.path.join(d, "stats.jsonl"))]


def at(series, t):
    best = None
    for r in series:
        if r["t"] <= t:
            best = r
    return best


def lineage(d, upto):
    births, death_t = {}, {}
    for r in rows(os.path.join(d, "lineage.jsonl")):
        if r.get("e") == "b" and r["t"] <= upto:
            births[r["id"]] = r
        elif r.get("e") == "d":
            death_t[r["id"]] = r["t"]
    return births, death_t


def root_of(births):
    memo = {}

    def root(i):
        path = []
        while i not in memo and births.get(i, {}).get("p", -1) >= 0:
            path.append(i)
            i = births[i]["p"]
        r = memo.get(i, i)
        for j in path:
            memo[j] = r
        memo[i] = r
        return r
    return root


def living(births, death_t, t):
    return [i for i, b in births.items() if b["t"] <= t and death_t.get(i, float("inf")) > t]


def leaf(b):
    return b.get("pho") == 1 and b.get("jnt") == 0 and b.get("abs") == 0


def s1(series, end):
    """Per year from the second: (most living in the first half, fewest in the second, held)."""
    out = []
    y = YEAR
    while y + YEAR <= end:
        first = [r["alive"] for r in series if y <= r["t"] < y + YEAR / 2]
        second = [r["alive"] for r in series if y + YEAR / 2 <= r["t"] <= y + YEAR]
        if first and second:
            hi, lo = max(first), min(second)
            out.append((y, hi, lo, lo <= (1 - S1_DROP) * hi))
        y += YEAR
    return out


def read_arm(root, arm, end):
    d = run_dir(root, arm)
    if d is None:
        print(f"{arm}: absent")
        return None
    series = stats(d)
    last = series[-1]["t"] if series else 0
    print(f"\n== {arm} ({d}); last stats row t={last}")
    for t in [x * 2500.0 for x in range(1, int(end // 2500) + 1)]:
        r = at(series, t)
        if r:
            print(f"t={t:.0f}: alive {r['alive']}, jointed {r['jointed']}, light factor {r.get('dayFactor', 'n/a')}")
    births, death_t = lineage(d, end)
    root = root_of(births)
    leaves = {}
    for t in (10000.0, 20000.0, 30000.0):
        if t > end:
            continue
        alive = living(births, death_t, t)
        ls = [births[i] for i in alive if leaf(births[i])]
        clades = Counter(root(i) for i in alive)
        top = clades.most_common(1)[0] if clades else (None, 0)
        rm = median(b["rm"] for b in ls) if ls else None
        lsg = median(b["ls"] for b in ls if "ls" in b) if ls else None
        leaves[t] = rm
        print(f"t={t:.0f}: living {len(alive)}, leaves {len(ls)}, the leaves' median margin {rm}, median lifespan gene {lsg}; "
              f"largest lineage (root {top[0]}) {top[1] / len(alive) if alive else 0:.3f} of the living")
    r_end = at(series, end)
    return {
        "series": series,
        "jointed_share": r_end["jointed"] / r_end["alive"] if r_end and r_end["alive"] else None,
        "margin": leaves.get(end),
        "alive_10k": (at(series, YEAR) or {}).get("alive"),
        "s1": s1(series, end),
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"))
    ap.add_argument("--seeds", nargs="+", type=int, default=[1, 2, 3])
    ap.add_argument("--end", type=float, default=END)
    ap.add_argument("--round", default="r56", help="the arms' prefix (r55 reads the null)")
    ap.add_argument("--baseline", default="r55")
    ap.add_argument("--gate", action="store_true", help="read G alone, seed 2 at 10,000 s")
    a = ap.parse_args()

    if a.gate:
        new = read_arm(a.runs_root, f"{a.round}-s2", YEAR)
        base = read_arm(a.runs_root, f"{a.baseline}-s2", YEAR)
        if new and base and new["alive_10k"] is not None and base["alive_10k"]:
            ratio = new["alive_10k"] / base["alive_10k"]
            print(f"\nG: seed 2's living at 10,000 s {new['alive_10k']} against {a.baseline}'s {base['alive_10k']}, "
                  f"{ratio:.3f} -> {'held' if ratio >= G_SHARE else 'failed'}")
        return

    results = {}
    for s in a.seeds:
        results[s] = (read_arm(a.runs_root, f"{a.round}-s{s}", a.end), read_arm(a.runs_root, f"{a.baseline}-s{s}", a.end))

    print("\n== clauses")
    s1_held = s2_held = s3_held = 0
    for s, (new, base) in results.items():
        if not new:
            continue
        years = new["s1"]
        ok = len(years) >= 2 and all(h for *_, h in years)
        s1_held += ok
        print(f"S1 seed {s}: " + "; ".join(f"year from {y:.0f}: most {hi}, fewest {lo}" for y, hi, lo, _ in years)
              + f" -> {'held' if ok else 'failed'}")
        if base:
            j, jb = new["jointed_share"], base["jointed_share"]
            if j is not None and jb is not None:
                ok2 = (jb > 0 and j >= S2_RATIO * jb) or (jb == 0 and j > 0)
                s2_held += ok2
                print(f"S2 seed {s}: jointed share {j:.4f} against {jb:.4f} -> {'held' if ok2 else 'failed'}")
            m, mb = new["margin"], base["margin"]
            if m is not None and mb is not None:
                ok3 = m > mb
                s3_held += ok3
                print(f"S3 seed {s}: leaves' median margin {m:.1f} against {mb:.1f} -> {'held' if ok3 else 'failed'}")
    n = len(a.seeds)
    print(f"S1 {s1_held} of {n} (bar {n} of {n}); S2 {s2_held} of {n} (bar 2 of 3); S3 {s3_held} of {n} (bar {n} of {n})")


if __name__ == "__main__":
    main()
