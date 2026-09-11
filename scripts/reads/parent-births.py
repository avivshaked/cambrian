"""Births per parent: does a joint (or an absorptive gut) change how many children an
organism leaves, from `lineage.jsonl` alone.

Round 34 (logbook/0080) makes a joint's standing charge, its stroke and the brains behind it
all cost about nothing (`idle 0.0001 W/N`, the smallest non-zero LinkCell will accept). If the
price was ever what removed joints from the population, a jointed parent should now breed
about as well as a rigid one. This script does not decide that on its own -- it has no
sense of the founder-draw confound CLAUDE.md's population-floor gotcha warns about, and it
reads whichever arm it is pointed at without knowing whether that arm's floor ever closed --
but it gives the raw children-per-parent numbers the question needs.

For every organism born in a chosen window, this reads:

  - its lifetime: birth time to death time, or to the last event this file has seen if it
    is never recorded as dying (a right-censored lifetime, printed the same as any observed
    one -- an organism still alive when the file ends is not marked apart from one that
    starved, so a window close to the end of a run mixes the two under one number)
  - its number of children (regular reproduction only -- see BirthKind below)
  - children per 1,000 s of that lifetime

and groups the result two ways: by the organism's own expressed `jnt` flag (jointed vs.
rigid) and, separately, by its own `abs` flag (absorptive vs. not), for three windows read
off the organism's own birth time -- 0-5,000 s, 5,000-15,000 s and 0-30,000 s -- unless
--from/--to name a single custom window instead.

`--deaths` adds a second read of the same window, grouped only by own `jnt` (jointed vs.
rigid): the age-at-death distribution (birth to death, binned 0-50/50-200/200-500/
500-1500/1500-3000/3000+ s, plus a still-alive-at-end bucket for anyone `read_lineage`
never saw die -- the same censoring `report_window`'s lifetime already carries, read here
as a bucket instead of a number), the birth row's own `bf` (birth fraction of adult) and
`as` (adult scale) fields' mean and median, and what share of the group was itself born to
a jointed parent (the parent's own `jnt` flag, looked up from the same table regardless of
which window the parent itself falls in -- a parent born before the window is still a
parent). An organism with `p == -1` (a founder or an inoculant) has no parent to look up and
is excluded from that one share, counted separately as "no parent" instead of silently
folded into either side.

Row format, from LineageEvent.cs / RunDirectory.cs (`e`: "b" birth or "d" death; birth carries
`t`, `id`, `p` parent id (-1 for a founder), `k` birth kind ("f" floor, "r" reproduction,
"i" inoculation), `jnt`/`abs`/`pho` expressed-phenotype flags, among other fields death does
not carry; death carries `t`, `id`, `c` cause). Read with a tolerant parser -- a run still
being written can end mid-line -- but neither `r34-s1` nor `r35-s1`, the two arms this was
written against, was live when it ran.

"Number of children" counts only BirthKind "r" (regular reproduction): a floor spawn or an
inoculation names no real parent (`p` is -1), so nothing here can inflate a parent's count
with those. Every organism born in the window is counted as a parent candidate, including
one that goes on to have zero children -- that is what "children per parent" is answering.

Usage:

    python scripts/reads/parent-births.py <arm>
    python scripts/reads/parent-births.py <arm> --from 3000 --to 9000
    python scripts/reads/parent-births.py <arm> --deaths

Never sleeps, polls or writes outside the repo; reads only. Exits 2 on a usage error or an
arm it cannot find.
"""
import argparse
import glob
import json
import os
import sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

DEFAULT_WINDOWS = [
    (0, 5000, "0-5,000 s"),
    (5000, 15000, "5,000-15,000 s"),
    (0, 30000, "0-30,000 s"),
]

# Age-at-death bins for --deaths, in seconds. "alive at end" is not a numeric bin -- it is
# every organism read_lineage never saw a "d" row for, right-censored the same way
# report_window's lifetime is.
DEATH_BINS = [
    (0, 50, "0-50 s"),
    (50, 200, "50-200 s"),
    (200, 500, "200-500 s"),
    (500, 1500, "500-1,500 s"),
    (1500, 3000, "1,500-3,000 s"),
    (3000, float("inf"), "3,000+ s"),
]


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def find_lineage(arm):
    """Latest run directory under runs/<arm>/ (names sort chronologically) -- the same
    convention scripts/reads/parent-age.py and positions-read.py use."""
    candidates = sorted(glob.glob(os.path.join("runs", arm, "*")))
    if not candidates:
        fail(f"No run directory under runs/{arm}/.")
    path = os.path.join(candidates[-1], "lineage.jsonl")
    if not os.path.exists(path):
        fail(f"No lineage.jsonl in {candidates[-1]}.")
    return path


def read_lineage(path):
    """One forward pass. Parent and child rows are chronological in this file (a parent must
    exist to reproduce), so a running count of children-per-parent needs no second pass.

    Returns (organisms, last_t) where organisms maps id -> dict with birth time `t`, `p`,
    `jnt`, `abs`, `bf` (birth fraction of adult), `as_` (adult scale -- `as` is a Python
    keyword, so the dict key is spelled `as_`), `k`, `children` (count of BirthKind "r"
    births naming this id as parent), and `death` (None if never seen dying). last_t is the
    latest timestamp of any row in the file, used to censor the lifetime of an organism this
    file never recorded dying.
    """
    organisms = {}
    last_t = 0.0

    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except ValueError:
                continue  # a live file's partial last line; neither arm read here was live

            t = row.get("t", 0.0)
            if t > last_t:
                last_t = t

            if row.get("e") == "b":
                organisms[row["id"]] = {
                    "t": t,
                    "p": row.get("p", -1),
                    "jnt": row.get("jnt", 0),
                    "abs": row.get("abs", 0),
                    "bf": row.get("bf", 1.0),
                    "as_": row.get("as", 1.0),
                    "k": row.get("k"),
                    "children": 0,
                    "death": None,
                }
                if row.get("k") == "r":
                    parent = organisms.get(row.get("p", -1))
                    if parent is not None:
                        parent["children"] += 1
            elif row.get("e") == "d":
                target = organisms.get(row["id"])
                if target is not None:
                    target["death"] = t

    return organisms, last_t


def median(values):
    if not values:
        return 0.0
    s = sorted(values)
    n = len(s)
    mid = n // 2
    return s[mid] if n % 2 else (s[mid - 1] + s[mid]) / 2.0


def mean(values):
    return sum(values) / len(values) if values else 0.0


def group_stats(records):
    """records: list of (lifetime, children). Returns the five printed numbers."""
    n = len(records)
    children = [c for _, c in records]
    lifetimes = [l for l, _ in records]
    rates = [c / (l / 1000.0) if l > 0 else 0.0 for l, c in records]
    return {
        "n": n,
        "mean_children": mean(children),
        "median_children": median(children),
        "mean_lifetime": mean(lifetimes),
        "mean_rate": mean(rates),
    }


def print_table(title, groups):
    print(f"  {title}")
    print(f"    {'group':<14}{'n':>7}{'mean children':>16}{'median children':>18}"
          f"{'mean lifetime s':>17}{'children/1000s':>16}")
    for label, stats in groups:
        print(
            f"    {label:<14}{stats['n']:>7}{stats['mean_children']:>16.3f}"
            f"{stats['median_children']:>18.3f}{stats['mean_lifetime']:>17.1f}"
            f"{stats['mean_rate']:>16.4f}")


def report_window(arm, organisms, last_t, lo, hi, label):
    in_window = [
        o for o in organisms.values()
        if lo <= o["t"] < hi
    ]
    print(f"== {arm}: parents born {label} (n={len(in_window)}) ==")
    if not in_window:
        print("  (none)")
        return

    records = []
    for o in in_window:
        lifetime = (o["death"] if o["death"] is not None else last_t) - o["t"]
        records.append((max(lifetime, 0.0), o["children"], o["jnt"], o["abs"]))

    jointed = group_stats([(l, c) for l, c, jnt, _ in records if jnt == 1])
    rigid = group_stats([(l, c) for l, c, jnt, _ in records if jnt == 0])
    print_table("by own joint (jnt) flag", [("jointed (jnt=1)", jointed), ("rigid (jnt=0)", rigid)])

    absorptive = group_stats([(l, c) for l, c, _, abs_ in records if abs_ == 1])
    non_absorptive = group_stats([(l, c) for l, c, _, abs_ in records if abs_ == 0])
    print_table(
        "by own absorptive (abs) flag",
        [("absorptive (abs=1)", absorptive), ("non-abs (abs=0)", non_absorptive)])
    print()


def death_bin(age):
    """Which DEATH_BINS label an age-at-death falls into."""
    for lo, hi, label in DEATH_BINS:
        if lo <= age < hi:
            return label
    return DEATH_BINS[-1][2]  # age is +inf-adjacent; falls in the open-ended top bin


def death_report_group(label, group, organisms):
    n = len(group)
    print(f"  {label} (n={n})")
    if n == 0:
        print("    (none)")
        return

    # -- age at death, one bucket per organism: a numeric bin, or "alive at end" ------------
    counts = {lbl: 0 for _, _, lbl in DEATH_BINS}
    counts["alive at end"] = 0
    for o in group:
        if o["death"] is None:
            counts["alive at end"] += 1
        else:
            counts[death_bin(max(o["death"] - o["t"], 0.0))] += 1

    order = [lbl for _, _, lbl in DEATH_BINS] + ["alive at end"]
    print(f"    {'age at death':<16}{'n':>7}{'share':>9}")
    for lbl in order:
        c = counts[lbl]
        print(f"    {lbl:<16}{c:>7}{c / n:>9.1%}")

    # -- birth-row traits: bf (birth fraction of adult) and as (adult scale) ----------------
    bf = [o["bf"] for o in group]
    scale = [o["as_"] for o in group]
    print(f"    bf   (birth fraction): mean {mean(bf):.4f}, median {median(bf):.4f}")
    print(f"    as   (adult scale):    mean {mean(scale):.4f}, median {median(scale):.4f}")

    # -- share whose own parent was itself jointed ------------------------------------------
    known_parent = [o for o in group if o["p"] != -1 and o["p"] in organisms]
    no_parent = n - len(known_parent)
    if known_parent:
        jointed_parent_share = mean(
            [1.0 if organisms[o["p"]]["jnt"] == 1 else 0.0 for o in known_parent])
    else:
        jointed_parent_share = 0.0
    print(
        f"    parent itself jointed: {jointed_parent_share:.1%} of {len(known_parent)} with "
        f"a known parent ({no_parent} founder/inoculant with no parent to check)")


def deaths_window(arm, organisms, lo, hi, label):
    in_window = [o for o in organisms.values() if lo <= o["t"] < hi]
    print(f"== {arm}: --deaths, born {label} (n={len(in_window)}) ==")
    if not in_window:
        print("  (none)")
        print()
        return

    jointed = [o for o in in_window if o["jnt"] == 1]
    rigid = [o for o in in_window if o["jnt"] == 0]
    death_report_group("jointed (jnt=1)", jointed, organisms)
    death_report_group("rigid (jnt=0)", rigid, organisms)
    print()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("arm")
    parser.add_argument("--from", dest="from_t", type=float, default=None,
                         help="Start of a single custom window (seconds). Requires --to; "
                              "replaces the three default windows.")
    parser.add_argument("--to", dest="to_t", type=float, default=None,
                         help="End of a single custom window (seconds). Requires --from.")
    parser.add_argument("--deaths", action="store_true",
                         help="Also print, per window and own jnt flag: age-at-death "
                              "distribution, birth bf/as stats, and parent-jointed share.")
    args = parser.parse_args()

    if (args.from_t is None) != (args.to_t is None):
        fail("--from and --to must be given together.")

    if args.from_t is not None:
        windows = [(args.from_t, args.to_t, f"{args.from_t:g}-{args.to_t:g} s (custom)")]
    else:
        windows = DEFAULT_WINDOWS

    path = find_lineage(args.arm)
    organisms, last_t = read_lineage(path)
    print(f"# {args.arm}: {path}")
    print(f"# organisms recorded: {len(organisms)}, last event at t={last_t:g} s")
    print()

    for lo, hi, label in windows:
        report_window(args.arm, organisms, last_t, lo, hi, label)

    if args.deaths:
        for lo, hi, label in windows:
            deaths_window(args.arm, organisms, lo, hi, label)


if __name__ == "__main__":
    main()
