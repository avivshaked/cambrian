#!/usr/bin/env python3
"""The clause reader for round 44 (logbook/0113-the-part-made-by-a-rule.md, H1 to H6), on the
module-gene build (logbook/specs/module-gene-spec.md).

Modelled on round 43's reader (logbook/specs/r43-read/clauses.py): same run-directory layout,
same tolerant reading of stats.jsonl/positions.jsonl/lineage.jsonl/config.json/run.json, same
TSV shape (one row per seed per clause, written with --out). scripts/reads/contact_aliases.py's
`field()` is reused for every stats.jsonl lookup, so a run recorded under the farm/Unity naming
split still reads (harmlessly, since none of H1-H6 touch the renamed contact/overlap columns
today) and a genuinely missing field comes back as the `_MISSING` sentinel rather than a
KeyError.

ASSUMED_NAMES below are this script's only guesses about the module-gene build's column and
field names, taken from rule 8 ("What is recorded") of the build spec, written before the build
landed. Correct them here, in one place, once real names are known:

  - stats.jsonl's `moduleAdds`, `moduleDrops`, `moduleAddsRefused` are read as true cumulative
    counters (like `floorSpawns`, `wraps`, `crowded`): a window is two samples differenced, and
    a window with no prior sample uses 0 as the baseline. These back the report's `mod add`,
    `mod drop`, `mod refused` columns, which rule 8 says are "per window".
  - stats.jsonl's `modulesStanding` is read as an *instantaneous* standing count (like
    `matterLocked`, `corpses`) rather than a running total, because it backs the report's
    `modules` column, described in rule 8 as "living indeterminate modules beyond the genome
    minimum, summed" — a state, not an accumulation. The build spec calls all four "cumulative
    counters"; if `modulesStanding` turns out to be windowed instead, only H3's use of it needs
    to change (a window diff in place of a direct read).
  - The report's `indet %` (the share of the living with any indeterminate node) has no stats
    field named for it in rule 8. This script does not guess one: it computes the share itself
    at every positions.jsonl sample, by joining `ind` (rule 8's new field on lineage birth rows)
    to the ids positions.jsonl carries at that sample. `ind > 0` is "has an indeterminate node".
    If the build adds a dedicated stats field later, prefer it — the join is a fallback, not a
    conviction.
  - G7's "uniform count" (round 43's clause, reused by H5) is not defined anywhere on file
    either. Read here as the classic occupancy expectation — the number of the run's
    `totalColumns` a uniform random placement of `alive` bodies would be expected to occupy,
    `totalColumns * (1 - (1 - 1/totalColumns)**alive)` — and `cols` (occupiedColumns/totalColumns)
    is compared against that expected *fraction*. Round 43's entry computed this reading by hand
    from the report; no script on file reproduces it, so this is this script's own reconstruction
    and is marked as such in its output, not as a settled definition.

Run from anywhere:

    python scripts/reads/r44-read.py
    python scripts/reads/r44-read.py --arms r43-s1 r43-s2 r43-s3      # the fixture: an older
                                                                       # round, missing every
                                                                       # module-gene field
    python scripts/reads/r44-read.py --out logbook/specs/r44-read/clauses.tsv

Touches nothing but reads; --out creates only the directory of the path given, and only then.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

sys.path.insert(0, HERE)
from contact_aliases import field as _aliased_field  # noqa: E402  (path set above)

DEFAULT_ARMS = ["r44-s1", "r44-s2", "r44-s3"]

# ---------------------------------------------------------------------- the assumed names
#
# Every name this script guesses at, in one place, per the task brief: "so they can be
# corrected after the build lands." See the module docstring above for what each one backs and
# why it was read that way.
ASSUMED_NAMES = {
    # stats.jsonl — cumulative, windowed like floorSpawns/wraps/crowded; back the report's
    # "mod add", "mod drop", "mod refused" (module-gene-spec.md rule 8).
    "stats_module_adds": "moduleAdds",
    "stats_module_drops": "moduleDrops",
    "stats_module_adds_refused": "moduleAddsRefused",
    # stats.jsonl — instantaneous standing count; backs the report's "modules".
    "stats_modules_standing": "modulesStanding",
    # the report/markdown table's own column labels — not parsed by this script (which reads
    # stats.jsonl directly, as r43's clauses.py does), kept here only so the printed rows and
    # this dictionary use the same words the entry's table will.
    "col_modules": "modules",
    "col_mod_add": "mod add",
    "col_mod_drop": "mod drop",
    "col_mod_refused": "mod refused",
    "col_indet_pct": "indet %",
    # lineage.jsonl birth-row field — the count of indeterminate nodes (rule 8).
    "lineage_ind": "ind",
    # And the one name this script guessed would not exist: the build wrote it after all, as the
    # instantaneous share of the living with any indeterminate node. Nothing here reads it yet —
    # the positions/lineage join below is what the clauses run on, and swapping them is a change
    # to a reading and not to a name. It is recorded so the swap has something to point at.
    "stats_indeterminate_share": "indeterminateShare",
}

# A sentinel distinct from every legitimate stats.jsonl value (including 0 and None-shaped
# JSON), so "the field is not on this row" is never confused with "the field read zero".
_MISSING = object()


def sfield(row, name):
    """row[name] (or its farm/Unity alias, via contact_aliases.field) — _MISSING if neither key
    is on the row. Never raises: a clause that needs a missing field prints 'absent' and the
    script keeps going, per the task's rule against crashing on an old run."""
    return _aliased_field(row, name, default=_MISSING)


def present(row, name):
    return sfield(row, name) is not _MISSING


# ---------------------------------------------------------------------- reading a run directory

def run_dir(runs_root, arm):
    base = os.path.join(runs_root, arm)
    if not os.path.isdir(base):
        raise SystemExit("no run directory for arm %r under %r" % (arm, runs_root))
    dirs = [d for d in sorted(os.listdir(base)) if os.path.isdir(os.path.join(base, d))]
    if len(dirs) != 1:
        raise SystemExit(
            "expected exactly one run directory for %r, saw %r" % (arm, dirs))
    return os.path.join(base, dirs[0])


def load_jsonl(path):
    """A list of parsed rows, or None when the file does not exist — a run directory this old
    or this partial simply lacks it, which is not a crash.

    A live arm's JSONL can be mid-write on the one line the writer is appending when this
    script opens it (the file is append-only and every completed row is valid — CLAUDE.md's
    "A killed run leaves every completed row valid"), so a parse failure on the LAST line is
    read as an in-progress write and dropped rather than crashing the read; a parse failure on
    any earlier line is real corruption and still raises. The same rule r45-read.py's loader
    carries, and for the same reason: these clauses are read off arms that are still running."""
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        lines = [ln.strip() for ln in f if ln.strip()]
    rows = []
    for i, line in enumerate(lines):
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            if i == len(lines) - 1:
                break
            raise
    return rows


def load_json(path):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def sample_at(ts, by_t, target):
    """The stats.jsonl row at exactly `target` seconds, or the last row at or before it when
    the run ended early — "a manifest reading `error` or `stopped` is censored and read at its
    last sample" (round 44's Rules). Returns (row, actual_t, was_short)."""
    if target in by_t:
        return by_t[target], target, False
    earlier = [t for t in ts if t <= target]
    if earlier:
        t = max(earlier)
        return by_t[t], t, True
    t = ts[-1]
    return by_t[t], t, True


def lineage_ind_map(lineage_rows):
    """id -> `ind` (the count of indeterminate nodes) from every birth row that carries it.

    Returns (available, mapping). `available` is False when not one birth row in the file
    carries rule 8's `ind` field at all — true of every run recorded before the module-gene
    build, round 43 included, and the condition every H1-H4 clause below is gated on.
    """
    available = False
    mapping = {}
    if not lineage_rows:
        return available, mapping

    ind_key = ASSUMED_NAMES["lineage_ind"]
    for row in lineage_rows:
        if row.get("e") != "b":
            continue
        if ind_key in row:
            available = True
            mapping[row["id"]] = row[ind_key]
    return available, mapping


# positions.jsonl's flag bits (src/Evosim.Core/Serialization/PositionsRow.cs) — unchanged by
# the module-gene build per rule 8.
ABSORPTIVE_BIT = 1
JOINTED_BIT = 2
PHOTOSYNTHETIC_BIT = 4


def indet_share_pct(ids, ind_map):
    """100 * the share of `ids` with ind > 0, or None for an empty group."""
    if not ids:
        return None
    n_indet = sum(1 for i in ids if ind_map.get(i, 0) > 0)
    return 100.0 * n_indet / len(ids)


# ---------------------------------------------------------------------- H1: the gene appears

def h1(seed, positions_rows, ind_available, ind_map):
    """H1: `indet %` reaches 10% by 15,000 s in 2 of 3."""
    if not ind_available:
        return dict(clause="H1", seed=seed, indet_pct_max_by_15000="absent",
                     at_t="absent", held="absent")
    if not positions_rows:
        return dict(clause="H1", seed=seed, indet_pct_max_by_15000="absent",
                     at_t="absent", held="absent", note="no positions.jsonl")

    best, best_t = 0.0, None
    for row in positions_rows:
        if row["t"] > 15000:
            continue
        ids = [b[0] for b in row["b"]]
        share = indet_share_pct(ids, ind_map)
        if share is not None and share >= best:
            best, best_t = share, row["t"]

    held = best >= 10.0
    return dict(clause="H1", seed=seed, indet_pct_max_by_15000=round(best, 3),
                 at_t=best_t, held=held)


# ---------------------------------------------------------------------- H2: plants keep it more

def h2(seed, positions_rows, ind_available, ind_map):
    """H2: at 30,000 s (or the run's last sample, if censored), the indeterminate share among
    photosynthetic bodies is above the share among jointed bodies, in 3 of 3."""
    if not ind_available:
        return dict(clause="H2", seed=seed, held="absent")
    if not positions_rows:
        return dict(clause="H2", seed=seed, held="absent", note="no positions.jsonl")

    last = max(positions_rows, key=lambda r: r["t"])
    photo_ids = [b[0] for b in last["b"] if b[4] & PHOTOSYNTHETIC_BIT]
    jnt_ids = [b[0] for b in last["b"] if b[4] & JOINTED_BIT]
    photo_pct = indet_share_pct(photo_ids, ind_map)
    jnt_pct = indet_share_pct(jnt_ids, ind_map)

    if photo_pct is None or jnt_pct is None:
        held = "absent"
    else:
        held = photo_pct > jnt_pct

    return dict(clause="H2", seed=seed, t=last["t"], photo_n=len(photo_ids),
                jnt_n=len(jnt_ids), photo_indet_pct=photo_pct, jnt_indet_pct=jnt_pct,
                held=held)


# ---------------------------------------------------------------------- H3: size tracks reserve

# The bin width for H3's founding-boom/drought scan. Not named anywhere in the entry or the
# spec — a design choice of this script, documented rather than buried, and easy to change from
# the one place it is defined.
H3_WINDOW_SECONDS = 1000.0


def h3(seed, ts, by_t, positions_rows, ind_available, ind_map):
    """H3: among indeterminate bodies, the mean part count (read as the modules-per-body proxy
    below) rises through the founding boom and falls in the first window where `margin s` falls
    by a third, in 2 of 3."""
    modules_field = ASSUMED_NAMES["stats_modules_standing"]
    if not ind_available or not ts or not present(by_t[ts[0]], modules_field):
        return dict(clause="H3", seed=seed, held="absent")
    if not positions_rows:
        return dict(clause="H3", seed=seed, held="absent", note="no positions.jsonl")

    pos_by_t = {r["t"]: r for r in positions_rows}
    bins = []  # (t, ratio-or-None, margin-or-None)
    for t in ts:
        if t % H3_WINDOW_SECONDS != 0:
            continue
        row = by_t[t]
        pos = pos_by_t.get(t)
        modules = sfield(row, modules_field)
        margin = sfield(row, "meanReserveMargin")
        if pos is None or modules is _MISSING:
            continue
        ids = [b[0] for b in pos["b"]]
        n_indet = sum(1 for i in ids if ind_map.get(i, 0) > 0)
        ratio = (modules / n_indet) if n_indet > 0 else None
        bins.append((t, ratio, None if margin is _MISSING else margin))

    if len(bins) < 2:
        return dict(clause="H3", seed=seed, held="absent", note="too few bins")

    drought_i = None
    for i in range(1, len(bins)):
        prev_margin, margin = bins[i - 1][2], bins[i][2]
        if prev_margin and prev_margin > 0 and margin is not None and margin <= prev_margin * (2.0 / 3.0):
            drought_i = i
            break

    if drought_i is None:
        return dict(clause="H3", seed=seed, held=False,
                     note="no window where margin s fell by a third")

    first_ratio = next((r for (_, r, _) in bins[:drought_i] if r is not None), None)
    boom_ratio = bins[drought_i - 1][1]
    drought_ratio = bins[drought_i][1]

    rises = first_ratio is not None and boom_ratio is not None and boom_ratio > first_ratio
    falls = boom_ratio is not None and drought_ratio is not None and drought_ratio < boom_ratio
    held = bool(rises and falls)

    return dict(clause="H3", seed=seed, drought_t=bins[drought_i][0],
                margin_before=bins[drought_i - 1][2], margin_at=bins[drought_i][2],
                ratio_first=first_ratio, ratio_before_drought=boom_ratio,
                ratio_at_drought=drought_ratio, held=held)


# ---------------------------------------------------------------------- H4: the cap binds

H4_WINDOWS = [(0, 5000), (5000, 15000), (15000, 30000)]


def h4(seed, ts, by_t):
    """H4: `mod refused` per window is larger as a share of `mod add` (among indeterminate
    bodies) than D101's `self stillb` is of births, in 2 of 3. Read here as: the comparison
    holds in every one of the three windows below, per seed — 2 of 3 seeds is the outer count
    the entry's table asks for."""
    add_f = ASSUMED_NAMES["stats_module_adds"]
    ref_f = ASSUMED_NAMES["stats_module_adds_refused"]
    if not ts or not present(by_t[ts[0]], add_f) or not present(by_t[ts[0]], ref_f):
        return dict(clause="H4", seed=seed, held="absent")

    all_hold = True
    windows = []
    for lo, hi in H4_WINDOWS:
        b, hi_t, short = sample_at(ts, by_t, hi)
        if lo == 0:
            a = None
        else:
            a, _, _ = sample_at(ts, by_t, lo)

        add_win = sfield(b, add_f) - (sfield(a, add_f) if a else 0)
        ref_win = sfield(b, ref_f) - (sfield(a, ref_f) if a else 0)
        births_win = sfield(b, "births") - (sfield(a, "births") if a else 0)
        so_win = sfield(b, "selfOverlapStillbirths") - (
            sfield(a, "selfOverlapStillbirths") if a else 0)

        mod_refused_pct = (100.0 * ref_win / add_win) if add_win else None
        self_stillb_pct = (100.0 * so_win / births_win) if births_win else None
        win_held = (mod_refused_pct is not None and self_stillb_pct is not None and
                    mod_refused_pct > self_stillb_pct)

        windows.append(dict(window="%d-%d" % (lo, hi), at=hi_t, short=short,
                             mod_refused_pct=mod_refused_pct, self_stillb_pct=self_stillb_pct,
                             held=win_held))
        if not win_held:
            all_hold = False

    return dict(clause="H4", seed=seed, windows=windows, held=all_hold)


# ---------------------------------------------------------------------- H5: the economy holds

H5_ALIVE_5000 = (250, 650)
H5_ALIVE_30000 = (500, 1400)
H5_UPT_LIM_PCT = (40.0, 95.0)
H5_SNOW_SHARE = (0.10, 0.40)
H5_RIM_QUARTER = (0.12, 0.40)
H5_COLS_RATIO = (0.85, 1.05)


def _uniform_occupied_fraction(total_columns, alive):
    """The classic occupancy expectation: the fraction of `total_columns` a uniform random
    placement of `alive` bodies would be expected to occupy. See the module docstring's note on
    G7's "uniform count", which this reconstructs rather than quotes."""
    if total_columns <= 0 or alive <= 0:
        return None
    return 1.0 - (1.0 - 1.0 / total_columns) ** alive


def h5(seed, ts, by_t, budget):
    """H5: the economy is round 43's — G1, G4 and G7's bounds, moved to this entry's numbers,
    in 3 of 3."""
    if not ts:
        return dict(clause="H5", seed=seed, held="absent")

    r5000, t5000, _ = sample_at(ts, by_t, 5000)
    r30000, t30000, _ = sample_at(ts, by_t, 30000)
    alive5000, alive30000 = r5000["alive"], r30000["alive"]

    g1_hold = (H5_ALIVE_5000[0] <= alive5000 <= H5_ALIVE_5000[1] and
               H5_ALIVE_30000[0] <= alive30000 <= H5_ALIVE_30000[1])

    g4_hold = True
    g4_detail = {}
    for target in (15000, 30000):
        row, t_actual, _ = sample_at(ts, by_t, target)
        if budget is None:
            g4_detail[target] = "absent"
            g4_hold = False
            continue
        upt = 100.0 * sfield(row, "uptakeLimitedShare")
        detritus = sfield(row, "detritusJoules")
        snow = detritus / budget if detritus is not _MISSING else None
        ok = (snow is not None and
              H5_UPT_LIM_PCT[0] <= upt <= H5_UPT_LIM_PCT[1] and
              H5_SNOW_SHARE[0] <= snow <= H5_SNOW_SHARE[1])
        g4_detail[target] = dict(at=t_actual, upt_lim_pct=upt, snow_share=snow, held=ok)
        if not ok:
            g4_hold = False

    g7_hold = True
    g7_detail = {}
    for target in (5000, 15000, 30000):
        row, t_actual, _ = sample_at(ts, by_t, target)
        per_patch = sfield(row, "alivePerPatch")
        alive = row["alive"]
        occ = sfield(row, "occupiedColumns")
        tot = sfield(row, "totalColumns")

        if per_patch is _MISSING or len(per_patch) < 4 or alive == 0 or \
                occ is _MISSING or tot is _MISSING or tot == 0:
            g7_detail[target] = "absent"
            g7_hold = False
            continue

        rim = per_patch[3] / alive
        expected_frac = _uniform_occupied_fraction(tot, alive)
        cols_ratio = (occ / tot) / expected_frac if expected_frac else None
        ok = (cols_ratio is not None and
              H5_RIM_QUARTER[0] <= rim <= H5_RIM_QUARTER[1] and
              H5_COLS_RATIO[0] <= cols_ratio <= H5_COLS_RATIO[1])
        g7_detail[target] = dict(at=t_actual, rim_quarter=rim, cols_ratio=cols_ratio, held=ok)
        if not ok:
            g7_hold = False

    held = g1_hold and g4_hold and g7_hold
    return dict(clause="H5", seed=seed, alive_5000=alive5000, alive_30000=alive30000,
                g1_hold=g1_hold, g4=g4_detail, g7=g7_detail, held=held)


# ---------------------------------------------------------------------- H6: the books close

def h6(seed, ts, by_t, manifest):
    """H6: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at
    30,000 s, `diverged` 0, in 3 of 3."""
    if not ts:
        return dict(clause="H6", seed=seed, held="absent")

    audit_values = [sfield(by_t[t], "auditResidual") for t in ts]
    audit_values = [v for v in audit_values if v is not _MISSING]
    audit_absmax = max(abs(v) for v in audit_values) if audit_values else None

    row30000, t_actual, short = sample_at(ts, by_t, 30000)
    matter_resid = sfield(row30000, "matterResidual")
    matter_resid = None if matter_resid is _MISSING else matter_resid

    diverged_total = sfield(by_t[ts[-1]], "diverged")
    diverged_total = None if diverged_total is _MISSING else diverged_total

    status = manifest.get("status") if manifest else None
    provisional = status == "running"
    censored = status in ("error", "stopped")

    held = (audit_absmax is not None and audit_absmax < 0.1 and
            matter_resid is not None and abs(matter_resid) < 1e-5 and
            diverged_total is not None and diverged_total == 0)

    return dict(clause="H6", seed=seed, audit_absmax=audit_absmax, matter_resid_at=t_actual,
                matter_resid=matter_resid, matter_resid_short=short,
                diverged_total=diverged_total, manifest_status=status,
                censored=censored, provisional=provisional, held=held)


# ---------------------------------------------------------------------- driving it over the arms

def read_arm(runs_root, arm):
    d = run_dir(runs_root, arm)

    stats = load_jsonl(os.path.join(d, "stats.jsonl"))
    if not stats:
        raise SystemExit("no stats.jsonl (or an empty one) for arm %r at %r" % (arm, d))
    by_t = {r["t"]: r for r in stats}
    ts = sorted(by_t)

    positions = load_jsonl(os.path.join(d, "positions.jsonl"))
    lineage = load_jsonl(os.path.join(d, "lineage.jsonl"))
    manifest = load_json(os.path.join(d, "run.json"))
    config = load_json(os.path.join(d, "config.json"))

    budget = None
    if config is not None:
        try:
            budget = config["world"]["matterBudgetUnits"] * config["world"]["joulesPerUnit"]
        except KeyError:
            budget = None

    ind_available, ind_map = lineage_ind_map(lineage)

    return [
        h1(arm, positions, ind_available, ind_map),
        h2(arm, positions, ind_available, ind_map),
        h3(arm, ts, by_t, positions, ind_available, ind_map),
        h4(arm, ts, by_t),
        h5(arm, ts, by_t, budget),
        h6(arm, ts, by_t, manifest),
    ]


def fmt_cell(v):
    if v is None:
        return ""
    if isinstance(v, float):
        return "%.6g" % v
    return str(v)


def write_tsv(path, rows):
    out_dir = os.path.dirname(path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    cols = []
    for r in rows:
        for k in r:
            if k not in cols:
                cols.append(k)

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            f.write("\t".join(fmt_cell(r.get(k)) for k in cols) + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                  formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"),
                     help="defaults to <repo>/runs")
    ap.add_argument("--arms", nargs="+", default=DEFAULT_ARMS)
    ap.add_argument("--out", default=None,
                     help="write the TSV here (creates its directory; nothing is written "
                          "without this flag)")
    args = ap.parse_args()

    all_rows = []
    by_clause = {c: [] for c in ("H1", "H2", "H3", "H4", "H5", "H6")}

    for arm in args.arms:
        try:
            arm_rows = read_arm(args.runs_root, arm)
        except SystemExit as e:
            print("skipping %r: %s" % (arm, e), file=sys.stderr)
            continue

        for r in arm_rows:
            all_rows.append(r)
            by_clause[r["clause"]].append(r.get("held"))
            print(r)

    print()
    for c in ("H1", "H2", "H3", "H4", "H5", "H6"):
        results = by_clause[c]
        n_true = sum(1 for x in results if x is True)
        n_absent = sum(1 for x in results if x == "absent")
        note = " (%d absent)" % n_absent if n_absent else ""
        print("%s: held in %d of %d%s" % (c, n_true, len(results), note))

    if args.out:
        write_tsv(args.out, all_rows)
        print()
        print("wrote", args.out)


if __name__ == "__main__":
    main()
