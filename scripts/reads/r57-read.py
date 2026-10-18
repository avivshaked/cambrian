"""The reader for round 57's clauses (the pre-registration, logbook/0136), each seed against itself across its years.

Round 57's year is 6,000 s (D151): summer from -250 to 2,750 s of each year and winter from 2,750 to
5,750 s, the switches centred on midnights. Year 1 is 0 to 6,000 s; a run of 48,000 s holds years 1 to 8.
Seed 2's year 1 is the screen's, seen before the pre-registration, so every clause reads years 2 to 8.

W:  the winters bite: in each of years 2 to 8, the fewest living in the winter at least W_DROP below the
    most living in the summer before it, in at least 6 of the 7 years, in 3 of 3 seeds.
R:  the leaves come to keep more: the median breeding margin (rm) of the leaves born in year 8 above that
    of the leaves born in year 2, in 3 of 3 seeds.
T:  the leaves come to breed at winter's end: the share of the summer's leaf births that fall in its
    first day (-250 to 750 s of the year) higher in years 6 to 8 together than in years 2 to 4
    together, in 3 of 3 seeds.
V:  the margin narrows: the interquartile range of rm among the leaves born in year 8 below that of
    those born in year 2, in 3 of 3 seeds.
Every leaf birth read is a birth from a parent; the founders the trickle, the floor and the pool place
are left out, since their margin is the pool's and not one the world selected.
Readings: per year, the most and fewest living in summer and winter, the light factor's range, the leaf
births and their median and interquartile rm, the share of summer leaf births in the first summer day, and
at each year's end the largest root lineage's share of the living and the jointed share.
B1 is read by round 54's reader.

Usage: python scripts/reads/r57-read.py [--runs-root R] [--round r57] [--seeds 1 2 3] [--end T] [--screen ARM]
"""
import argparse
import glob
import json
import os
from collections import Counter
from statistics import median, quantiles

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
YEAR, DAY, END = 6000.0, 1000.0, 48000.0
SUMMER = (-250.0, 2750.0)
WINTER = (2750.0, 5750.0)
W_DROP = 0.20
FIRST_YEAR_READ = 2


def run_dir(root, arm):
    dirs = sorted(d for d in glob.glob(os.path.join(root, arm, "*")) if os.path.isdir(d))
    return dirs[-1] if dirs else None


def rows(path):
    """A JSONL file's complete rows; a live run's last line may be half written and is skipped."""
    for l in open(path, encoding="utf-8"):
        if l.endswith("\n") and l.strip():
            yield json.loads(l)


def leaf(b):
    return b.get("pho") == 1 and b.get("jnt") == 0 and b.get("abs") == 0


def window(year, span):
    """Year n's (from 1) window in seconds of the run."""
    base = (year - 1) * YEAR
    return base + span[0], base + span[1]


def root_of(births):
    """Each id's founder, following parents back (round 56's reader)."""
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


def iqr(xs):
    if len(xs) < 4:
        return None
    q = quantiles(xs, n=4)
    return q[2] - q[0]


def read_arm(root, arm, end):
    d = run_dir(root, arm)
    if d is None:
        print(f"{arm}: absent")
        return None
    series = list(rows(os.path.join(d, "stats.jsonl")))
    last = series[-1]["t"] if series else 0
    every, death_t = {}, {}
    for r in rows(os.path.join(d, "lineage.jsonl")):
        if r.get("e") == "b" and r["t"] <= end:
            every[r["id"]] = r
        elif r.get("e") == "d":
            death_t[r["id"]] = r["t"]
    root = root_of(every)
    # Born of a parent only: a founder (the trickle's, the floor's or the pool's) carries a src and
    # the pool's margin, not one the world selected.
    births = [r for r in every.values() if r.get("src") is None and leaf(r)]
    print(f"\n== {arm} ({d}); last stats row t={last:.0f}, {len(births)} leaf births to {end:.0f} s")
    years = {}
    for y in range(1, int(end // YEAR) + 1):
        s0, s1 = window(y, SUMMER)
        w0, w1 = window(y, WINTER)
        if last < w1:
            break
        summer = [r["alive"] for r in series if max(s0, 0) <= r["t"] < s1]
        winter = [r["alive"] for r in series if w0 <= r["t"] < w1]
        light = [r.get("dayFactor") for r in series if (y - 1) * YEAR <= r["t"] < y * YEAR and r.get("dayFactor") is not None]
        yb = [b for b in births if (y - 1) * YEAR - 250 <= b["t"] < y * YEAR - 250]
        sb = [b for b in yb if s0 <= b["t"] < s1]
        first_day = [b for b in sb if b["t"] < s0 + DAY]
        rm = [b["rm"] for b in yb if "rm" in b]
        rec = {
            "most_summer": max(summer) if summer else None,
            "fewest_winter": min(winter) if winter else None,
            "rm_median": median(rm) if rm else None,
            "rm_iqr": iqr(rm),
            "first_day_share": len(first_day) / len(sb) if sb else None,
            "summer_births": len(sb),
            "first_day_births": len(first_day),
        }
        years[y] = rec
        ye = y * YEAR
        alive = [i for i, b in every.items() if b["t"] <= ye and death_t.get(i, float("inf")) > ye]
        clades = Counter(root(i) for i in alive)
        top = clades.most_common(1)[0][1] / len(alive) if alive else None
        end_row = max((r for r in series if r["t"] <= ye), key=lambda r: r["t"], default=None)
        jshare = end_row["jointed"] / end_row["alive"] if end_row and end_row.get("alive") else None
        drop =(1 - rec["fewest_winter"] / rec["most_summer"]) if rec["most_summer"] and rec["fewest_winter"] is not None else None
        print(f"year {y}: summer most {rec['most_summer']}, winter fewest {rec['fewest_winter']}"
              + (f" (down {drop:.1%})" if drop is not None else "")
              + (f"; light factor {min(light):.3f} to {max(light):.3f}" if light else "")
              + f"; leaf births {len(yb)}, rm median {rec['rm_median']}, rm IQR {rec['rm_iqr']}"
              + (f"; first summer day {rec['first_day_share']:.3f} of {len(sb)} summer births" if sb else "")
              + (f"; at {ye:.0f} s the largest root lineage {top:.3f} of the living" if top is not None else "")
              + (f", jointed {jshare:.4f}" if jshare is not None else ""))
    return years


def clauses(seed, years):
    out = {}
    read = [y for y in range(FIRST_YEAR_READ, 9) if y in years]
    bites = [y for y in read if years[y]["most_summer"] and years[y]["fewest_winter"] is not None
             and years[y]["fewest_winter"] <= (1 - W_DROP) * years[y]["most_summer"]]
    out["W"] = (len(read) == 7 and len(bites) >= 6, f"{len(bites)} of {len(read)} years bite")
    if 2 in years and 8 in years and years[2]["rm_median"] is not None and years[8]["rm_median"] is not None:
        out["R"] = (years[8]["rm_median"] > years[2]["rm_median"], f"rm median {years[8]['rm_median']:.1f} against {years[2]['rm_median']:.1f}")
        a, b = years[8]["rm_iqr"], years[2]["rm_iqr"]
        if a is not None and b is not None:
            out["V"] = (a < b, f"rm IQR {a:.1f} against {b:.1f}")
    def first_day_share(ys):
        sb = sum(years[y]["summer_births"] for y in ys)
        fd = sum(years[y]["first_day_births"] for y in ys)
        return (fd / sb if sb else None), sb
    early, late = (2, 3, 4), (6, 7, 8)
    if all(y in years for y in early + late):
        se, ne = first_day_share(early)
        sl, nl = first_day_share(late)
        if se is not None and sl is not None:
            out["T"] = (sl > se, f"first summer day {sl:.3f} of {nl} summer leaf births in years 6 to 8"
                                 f" against {se:.3f} of {ne} in years 2 to 4")
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"))
    ap.add_argument("--round", default="r57", help="the arms' prefix")
    ap.add_argument("--seeds", nargs="+", type=int, default=[1, 2, 3])
    ap.add_argument("--end", type=float, default=END)
    ap.add_argument("--screen", help="read one arm's years only (the screen), no clauses")
    a = ap.parse_args()

    if a.screen:
        read_arm(a.runs_root, a.screen, a.end)
        return

    held = {}
    print_rows = []
    for s in a.seeds:
        years = read_arm(a.runs_root, f"{a.round}-s{s}", a.end)
        if not years:
            continue
        for k, (ok, note) in clauses(s, years).items():
            held.setdefault(k, 0)
            held[k] += ok
            print_rows.append(f"{k} seed {s}: {note} -> {'held' if ok else 'failed'}")
    print("\n== clauses")
    for r in print_rows:
        print(r)
    n = len(a.seeds)
    print("; ".join(f"{k} {v} of {n} (bar {n} of {n})" for k, v in sorted(held.items())))


if __name__ == "__main__":
    main()
