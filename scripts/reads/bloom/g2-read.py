"""G2's read: the bloom screen's gates and story material, per run, and the ranking across runs.

Usage: python -I scripts/reads/bloom/g2-read.py RUN [RUN ...] [--traits TSV | --traits ARM=TSV ...]
                                               [--islands-from RUN] [--report FILE]
                                               [--end 24000] [--from 6000] [--window 3000] [--upkeep-w 0.908]
                                               [--floor-measure mean|fewest|d244] [--crossing-gap 10]
                                               [--floors 30,40]
       python -I scripts/reads/bloom/g2-read.py --self-test

The specification is logbook/specs/bloom-build/g2-read-spec.md (G2 of logbook/specs/grazer-film-plan.md), and the
numbers here are its numbers. Reads the files only, writes nothing, steps nothing. Standard library only.

A RUN is an arm folder (the latest dated run is read) or a run directory. A run that resumed another is read as one world
with it, through r61-grazer-economy.py's Economy, which this imports with r62-health.py and r62g-windows.py (D222's tests,
D244's floor) rather than restating them. The judged windows are --window seconds from --from to --end (six of 3,000 s
from 6,000 to 24,000): a sample at t is in the window [t0, t1) and the last window takes its end.

The trait table is what `Evosim.Farm --grazer-traits <run dir> <out tsv>` writes (src/Evosim.Farm/GrazerTraits.cs).
--traits PATH gives it for a single run; --traits ARM=PATH (ARM the run's arm name) gives one per run; with neither, the
read looks for grazer-traits.tsv in the run directory, then beside the arm folder.

The islands are the report's header line `island bloom, N islands (...)` that the bloom build writes (Report.cs,
IslandBloomLine), taken from the run's report: the file `<arm folder>.md` beside the arm folder (or --report), else
report.md in the run directory. A run without the bloom (arm A) carries no such line; --islands-from RUN takes the plan
from a sibling run of the same seed, since the plan depends only on the landscape. Gate 2 is judged only where a run has
the bloom's own columns (`bloomFactor` in stats.jsonl); it is not judged for a run without them.

An island's effective radius is r_eff = sqrt(columns x cell^2 / pi), cell being the config's fieldMatterCellMetres
(5 m); a grove is the disc of radius r_eff + 10 m round the island's centroid. In a Box world each axis takes the
shorter way round (D204), as grazer-moves-read.py does. A point inside two groves belongs to the nearer centroid, for the
walk of fixes and for the crowd counts alike.

What is read, in order, per run:
  header      run, arm, seed, hashes.
  gate 1      grazers survive, in two parts that both must hold.
              (a) The grazer floor (30, D244) in every judged window, by --floor-measure: mean (the default: the window's mean
              grazer count is at least the floor), fewest (the fewest at any stats sample is), or d244 (a window whose
              mean is under the floor is not judged, and the run must reach the floor at some sample). Under every measure
              a window whose fewest is 0 fails. Both the fewest and the mean are printed per window.
              (b) D222 over the whole run, as the r62cf read recorded it (logbook/specs/r62cf-read/24000/README.md): the
              born share at the run's end (r62-health.born_share, at least 0.8); the best 3,000 s birth cohort's mouthed
              children per body (as r62g-cohorts.py computes it, kept here because it is a script; at least 1); the deepest
              chain (r62g-read, at least 10); test 2 over the summer-start 500 s rows (r62g-windows, a switch into summer),
              each judged when its mean grazer count is at least the floor, with kept W/g over --upkeep-w at least 0.9; and
              test 3, the living share of the last 3,000 s bin (r62-health.living_bins, at least 0.5; a run on the corpse
              rule has no such measure). Printed at the floor of 30 (the gate), at 40, and strict (no first check, every
              summer-start row judged). A per-window table of the same measures is printed as information and is not part
              of the gate.
  gate 2      the bloom reaches the food (a run with the bloom's columns). Islands of at least 5 columns whose mean
              bloomLeafArea over [--from, --end] is at least 1 m^2 are judged. For each, the Pearson correlation of
              bloomFactor_k(t) with bloomLeafArea_k(t + lag) for t in the judged span and t + lag within the record, for
              lags 0 to 3,000 s in steps of the stats cadence; the best lag and its correlation. It passes when every
              judged island reaches 0.5 (and fails if none is judged). Every island's row is printed with why one is left out.
  gate 3      grazers evolve. The traits are mass_kg, parts, limb_push and the brain's mean drive (rest and fed; the larger
              of the two shifts counts). The sample is the grazers born in each window (with a row in the trait table). The
              first and last windows with at least 30 are used, the nearest to each end. The founder SD is over every
              founder grazer in the table: parent -1, or source floor, trickle or pool. The scale is max(founder SD, the
              first window's SD) and the print says which. The shift is |mean(last) - mean(first)| / scale; the gate passes
              at 1 or more. SDs are sample standard deviations (n - 1).
  4           reported: grazer clades holding at least 10% of the living grazers (per window, the most at any stats sample and
              the count at the window's last sample; the test is three clades in some window); kills of grazers by grazers
              and by others; and what the record says of grazers' remains (remains founded from grazers' bodies and closed;
              bites on remains are not recorded by victim, and the total of biteKeptRemains is printed).
  5           story material: crossings and strolls per window (a move is a fix over grove B whose last fix over a grove was
              over grove A, B not A; fixes outside every grove do not reset A; a crossing when the banded edge gap, the
              centroid distance less (r_A + 10) less (r_B + 10), is at least --crossing-gap, else a stroll), per grazer life, and each crossing's median
              distance (fix to fix, the shorter way round) and duration; crowds at blooms (grazers inside each grove at each
              stats sample, correlated with bloomFactor_k at the best lag from 0 to 3,000 s, the crowd lagging the bloom);
              busts and recoveries in the world's grazer count over the judged span (a fall to half or less of the highest
              count in the 1,500 s before it; a recovery is a return to that count); and the four parts of story material.
  ranking     with several runs: the arms that pass every gate, ranked on the four parts by the mean of their seeds (1 is the
              most; ties share the mean rank), the ranks summed, the lowest sum wins, a tie to the simplest arm (A, B, C, D).
              An arm is the run's name less its seed suffix.
  summary     one line a run: the gate verdicts and the story parts.
"""
import argparse
import importlib.util
import json
import math
import os
import re
import statistics
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
READS = os.path.dirname(HERE)

CELL_DEFAULT = 5.0
GROVE_BAND = 10.0
TOUCH_SLACK = 2 * GROVE_BAND
MIN_COLUMNS = 5
MIN_LEAF_AREA = 1.0
GATE2_R = 0.5
LAG_MAX = 3000.0
CROWD_R = 0.3
CLADE_SHARE = 0.10
MIN_BIRTHS = 30
MIN_GRAZERS = 30
BUST_BEFORE = 1500.0
BUST_FRACTION = 0.5
SHIFT_GATE = 1.0
FOUNDER_SOURCES = ("floor", "trickle", "pool")
REMAINS_FLAG = 8
BORN_SHARE_MIN = 0.8
CHAIN_MIN = 10
REPLACEMENT_MIN = 1.0
UPKEEP_MIN = 0.9
LIVING_SHARE_MIN = 0.5
TRAITS = ("mass_kg", "parts", "limb_push", "drive_rest", "drive_fed")
PARTS = ("crossings", "crowd islands", "recoveries", "largest shift")


# ---------------------------------------------------------------------------------------------- pure functions

def pearson(xs, ys):
    """Pearson r over the pairs where neither is None, or None when fewer than 3 pairs or either side is constant."""
    n = 0
    sx = sy = sxx = syy = sxy = 0.0
    for x, y in zip(xs, ys):
        if x is None or y is None:
            continue
        n += 1
        sx += x
        sy += y
        sxx += x * x
        syy += y * y
        sxy += x * y
    if n < 3:
        return None
    vx = sxx - sx * sx / n
    vy = syy - sy * sy / n
    if vx <= 1e-12 * sxx or vy <= 1e-12 * syy or vx <= 0 or vy <= 0:
        return None
    return (sxy - sx * sy / n) / math.sqrt(vx * vy)


def best_lag(x, y, step, i_lo, i_hi, max_lag=LAG_MAX):
    """The lag (seconds) and r of the best Pearson correlation of x[i] with y[i + L], i in [i_lo, i_hi] and i + L in range,
    for L from 0 to max_lag in whole steps. The series sit on one grid (index i is second i * step); a hole is None. The
    best is the largest r, the smallest lag on a tie. (None, None) when no lag has a correlation."""
    best = (None, None)
    n = min(len(x), len(y))
    for L in range(0, int(max_lag / step + 1e-9) + 1):
        hi = min(i_hi, n - 1 - L)
        if hi - i_lo + 1 < 3:
            break
        r = pearson(x[i_lo:hi + 1], y[i_lo + L:hi + L + 1])
        if r is not None and (best[1] is None or r > best[1] + 1e-12):
            best = (L * step, r)
    return best


def gap(a, b, side):
    """The distance along one axis, the shorter way round when the world is joined (side), else plain."""
    d = abs(b - a)
    if side:
        d %= side
        d = min(d, side - d)
    return d


def island_radius(columns, cell):
    """r_eff: the radius of a disc of the island's area."""
    return math.sqrt(columns * cell * cell / math.pi)


def make_groves(islands, cell):
    """Groves from parsed islands: the disc of radius r_eff + 10 m round the centroid."""
    out = []
    for isl in islands:
        r = island_radius(isl["columns"], cell)
        out.append(dict(k=isl["k"], cx=isl["cx"], cz=isl["cz"], r_eff=r, r=r + GROVE_BAND, columns=isl["columns"]))
    return out


def assign_grove(x, z, groves, side):
    """The index into groves of the grove holding the point, the nearer centroid when two do, else -1."""
    best, bd = -1, None
    for n, g in enumerate(groves):
        d = math.hypot(gap(x, g["cx"], side), gap(z, g["cz"], side))
        if d <= g["r"] and (bd is None or d < bd):
            best, bd = n, d
    return best


def edge_gap(a, b, groves, side):
    """The banded edge gap between groves a and b: the centroid distance less (r_A + 10) less (r_B + 10), metres. Negative
    when the groves overlap."""
    ga, gb = groves[a], groves[b]
    d = math.hypot(gap(ga["cx"], gb["cx"], side), gap(ga["cz"], gb["cz"], side))
    return d - ga["r"] - gb["r"]


def classify_move(a, b, groves, side, min_gap=0.0):
    """'crossing' when the banded edge gap between groves a and b is at least min_gap metres (at 0, when the groves do not
    touch), else 'stroll'."""
    return "crossing" if edge_gap(a, b, groves, side) >= min_gap else "stroll"


def founder_scale(founder_sd, first_sd):
    """(scale, which): the larger of the founder SD and the first window's SD."""
    if first_sd > founder_sd:
        return first_sd, "first window"
    return founder_sd, "founders"


def sample_sd(xs):
    return statistics.stdev(xs) if len(xs) >= 2 else 0.0


def shift_in_scale(mean_first, mean_last, scale):
    diff = abs(mean_last - mean_first)
    if scale > 0:
        return diff / scale
    return math.inf if diff > 0 else 0.0


def find_busts(times, counts, before=BUST_BEFORE, fraction=BUST_FRACTION, lo=None):
    """Busts in a count series. A bust starts at the first sample whose count is at most `fraction` of the highest count in
    the `before` seconds ahead of it (samples with t - before <= t' < t), and lasts until the count returns to that highest
    count (the recovery) or the series ends. Returns a list of dicts: t, ref (that highest count), low (the lowest count
    in the episode), low_t, recovery (the time of return, or None). Only samples at or after `lo` start a bust."""
    out = []
    n = len(times)
    j = 0
    open_ = None
    for i in range(n):
        t, c = times[i], counts[i]
        if open_ is not None:
            if c < open_["low"]:
                open_["low"], open_["low_t"] = c, t
            if c >= open_["ref"]:
                open_["recovery"] = t
                out.append(open_)
                open_ = None
            continue
        if lo is not None and t < lo - 1e-9:
            continue
        while times[j] < t - before - 1e-9:
            j += 1
        if j >= i:
            continue
        ref = max(counts[j:i])
        if ref > 0 and c <= fraction * ref:
            open_ = dict(t=t, ref=ref, low=c, low_t=t, recovery=None)
    if open_ is not None:
        out.append(open_)
    return out


def arm_of(name):
    """The run's name less its seed suffix: a trailing run of 3 to 5 digits, with its separator."""
    return re.sub(r"[-_]?\d{3,5}$", "", name)


def arm_letter(arm):
    """A, B, C or D from the arm's name (the last single-letter token), or None."""
    letter = None
    for tok in re.split(r"[-_.]", arm):
        if re.fullmatch(r"[A-Da-d]", tok):
            letter = tok.upper()
    return letter


def average_ranks(values):
    """Rank 1 for the largest; equal values share the mean of the ranks they span."""
    order = sorted(range(len(values)), key=lambda i: -values[i])
    ranks = [0.0] * len(values)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and values[order[j + 1]] == values[order[i]]:
            j += 1
        mean_rank = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            ranks[order[k]] = mean_rank
        i = j + 1
    return ranks


def rank_arms(parts_by_arm):
    """parts_by_arm: {arm: [four part values, each the mean of the arm's seeds]}. Returns (rows, winner) with rows
    [(arm, parts, ranks, sum)], ordered by sum then by the simplest arm (A, B, C, D, then name)."""
    arms = list(parts_by_arm)
    if not arms:
        return [], None
    ranks_by_part = [average_ranks([parts_by_arm[a][p] for a in arms]) for p in range(len(PARTS))]
    rows = []
    for n, a in enumerate(arms):
        ranks = [ranks_by_part[p][n] for p in range(len(PARTS))]
        rows.append((a, parts_by_arm[a], ranks, sum(ranks)))

    def key(row):
        letter = arm_letter(row[0])
        return (row[3], "ABCD".index(letter) if letter else 9, row[0])

    rows.sort(key=key)
    return rows, rows[0][0]


# ---------------------------------------------------------------------------------------------- the report's island line

ISLAND_HEAD = re.compile(r"island bloom, (\d+) islands \(label: P s, A, phase rad, columns, centre x z m\):\s*(.*)")
ISLAND_ITEM = re.compile(
    r"(\d+): P ([-+0-9.eE]+), A ([-+0-9.eE]+), phase ([-+0-9.eE]+), (\d+) columns, at ([-+0-9.eE]+) ([-+0-9.eE]+)")


def parse_islands(text):
    """The islands of a report's `island bloom, N islands (...)` line, or None when it has none. Each is a dict: k, P, A,
    phase, columns, cx, cz. Refuses a line whose island count disagrees with its items."""
    for line in text.splitlines():
        m = ISLAND_HEAD.search(line)
        if not m:
            continue
        n = int(m.group(1))
        items = [ISLAND_ITEM.fullmatch(part.strip()) for part in m.group(2).split(";")]
        if any(i is None for i in items) or len(items) != n:
            raise ValueError(f"the island line names {n} islands but its items do not parse to {n}: {line[:200]}")
        return [dict(k=int(i.group(1)), P=float(i.group(2)), A=float(i.group(3)), phase=float(i.group(4)),
                     columns=int(i.group(5)), cx=float(i.group(6)), cz=float(i.group(7))) for i in items]
    return None


# ---------------------------------------------------------------------------------------------- the trait table

def read_traits(path):
    """{id: row} from the grazer-traits TSV: '#' lines skipped, the columns by name, numbers as floats."""
    rows = {}
    header = None
    comments = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                continue
            if line.startswith("#"):
                comments.append(line)
                continue
            cells = line.split("\t")
            if header is None:
                header = cells
                continue
            r = dict(zip(header, cells))
            for k in ("born_t", "parent", "parts", "jointed", "mass_kg", "limb_push", "drive_rest", "drive_fed"):
                r[k] = float(r[k])
            r["id"] = int(r["id"])
            rows[r["id"]] = r
    return rows, comments


def is_founder_row(r):
    return r["parent"] < 0 or r["src"] in FOUNDER_SOURCES


# ---------------------------------------------------------------------------------------------- loading the reads

_reads = {}


def load_reads():
    """r61-grazer-economy, r62-health and r62g-windows, imported by path (they import each other the same way)."""
    if _reads:
        return _reads

    def load(name, file):
        spec = importlib.util.spec_from_file_location(name, os.path.join(READS, file))
        mod = importlib.util.module_from_spec(spec)
        sys.modules[name] = mod
        spec.loader.exec_module(mod)
        return mod

    sys.path.insert(0, READS)
    _reads["r61"] = load("r61econ", "r61-grazer-economy.py")
    _reads["r62"] = load("r62health", "r62-health.py")
    _reads["r62gw"] = load("r62gwindows", "r62g-windows.py")
    _reads["r62g"] = load("r62gread", "r62g-read.py")
    sys.modules["r61econ"] = _reads["r61"]
    _reads["r61"].r59.runrec.VERBOSE = False
    return _reads


# ---------------------------------------------------------------------------------------------- one run

def fmt(x, spec="{:.3f}"):
    if x is None or (isinstance(x, float) and math.isnan(x)):
        return "-"
    if isinstance(x, float) and math.isinf(x):
        return "inf"
    return spec.format(x)


def judged_windows(a):
    out = []
    t0 = a.t_from
    while t0 + a.window <= a.end + 1e-9:
        out.append((t0, t0 + a.window))
        t0 += a.window
    return out


def in_window(t, win, last):
    t0, t1 = win
    return t0 - 1e-9 <= t < t1 - 1e-9 or (last and abs(t - t1) < 1e-9)


def find_report(w, explicit):
    """The text of the first report with an island line: --report, else <arm folder>.md, else report.md, along the chain."""
    cands = [explicit] if explicit else []
    for d, _, _ in reversed(w.segs):
        cands.append(os.path.normpath(os.path.dirname(d)) + ".md")
        cands.append(os.path.join(d, "report.md"))
    for c in cands:
        if c and os.path.isfile(c):
            with open(c, encoding="utf-8", errors="replace") as f:
                text = f.read()
            if ISLAND_HEAD.search(text):
                return c, text
    return None, None


class RunRead:
    """One world: the Economy, the stats, the grazers and their windows."""

    def __init__(self, path, a, ns):
        R = load_reads()
        self.R = R
        self.a = a
        self.ns = ns
        self.path = path
        self.e = R["r61"].Economy(path, ns)
        self.w = self.e.w
        self.name = self.w.name
        self.seed = self.w.seed
        self.arm = arm_of(self.name)
        self.end = min(a.end, self.w.end)
        self.wins = judged_windows(a)
        self.stats = list(self.w.seg_rows("stats.jsonl"))
        self.stats = [s for s in self.stats if s["t"] <= self.end + 1e-9]
        ts = [s["t"] for s in self.stats]
        gaps = sorted(b - a_ for a_, b in zip(ts, ts[1:]) if b - a_ > 1e-9)
        self.step = gaps[len(gaps) // 2] if gaps else 10.0
        born = self.w.born
        self.grazers = sorted((i for i in born if self.e.mouthed(i) and born[i][0] <= self.end + 1e-9),
                              key=lambda i: (born[i][0], i))
        self.gset = set(self.grazers)
        self._series()
        self._clades()

    # -- the grazer count at every stats sample
    def _series(self):
        born, deaths = self.w.born, self.w.deaths
        ts = [s["t"] for s in self.stats]
        births = sorted(born[i][0] for i in self.grazers)
        ends = sorted(deaths[i][0] for i in self.grazers if i in deaths)
        self.times = ts
        self.counts = []
        bi = di = 0
        for t in ts:
            while bi < len(births) and births[bi] <= t + 1e-9:
                bi += 1
            while di < len(ends) and ends[di] <= t + 1e-9:
                di += 1
            self.counts.append(bi - di)

    def window_counts(self, win, last):
        c = [n for t, n in zip(self.times, self.counts) if in_window(t, win, last)]
        return (min(c), sum(c) / len(c)) if c else (None, None)

    # -- the clades
    def _clades(self):
        born = self.w.born
        self.clade = {}
        for i in self.grazers:
            p = born[i][1]
            self.clade[i] = self.clade[p] if (p is not None and p >= 0 and p in self.gset) else i

    def clade_shares(self):
        """Per stats sample, the number of clades holding at least CLADE_SHARE of the living grazers."""
        born, deaths = self.w.born, self.w.deaths
        events = []
        for i in self.grazers:
            events.append((born[i][0], 1, i))
            if i in deaths:
                events.append((deaths[i][0], -1, i))
        events.sort(key=lambda x: (x[0], -x[1]))
        alive = Counter()
        total = 0
        k = 0
        out = []
        for t in self.times:
            while k < len(events) and events[k][0] <= t + 1e-9:
                _, sign, i = events[k]
                alive[self.clade[i]] += sign
                total += sign
                k += 1
            out.append(sum(1 for v in alive.values() if total > 0 and v >= CLADE_SHARE * total))
        return out

    # -- depth of mouthed parent -> mouthed child chains, to a second
    def chain_depth(self, until):
        born = self.w.born
        depth = {}
        best = 0
        for i in self.grazers:
            if born[i][0] > until + 1e-9:
                break
            p = born[i][1]
            founder = p is None or p < 0 or p not in born
            depth[i] = depth[p] + 1 if (not founder and p in depth) else 0
            best = max(best, depth[i])
        return best

    # -- mouthed children per body, by 3,000 s birth cohort, children counted to the end
    def cohorts(self, width=3000.0):
        born = self.w.born
        kids = Counter()
        for i in self.grazers:
            p = born[i][1]
            if p is not None and p >= 0:
                kids[p] += 1
        out = []
        c0 = 0.0
        while c0 < self.end - 1e-9:
            c1 = c0 + width
            coh = [i for i in self.grazers if c0 < born[i][0] <= c1]
            out.append((c0, c1, len(coh), sum(kids[i] for i in coh) / len(coh) if coh else None))
            c0 = c1
        return out


# ---------------------------------------------------------------------------------------------- gate 1

def d222_measures(rr, ns):
    """The whole-run measures of D222, as the r62cf read recorded them (logbook/specs/r62cf-read/24000/README.md): the
    largest grazer count at a 500 s window's end (r62g-windows), born share at the run's end (r62-health.born_share), the
    best 3,000 s birth cohort's mouthed children per body (computed here as r62g-cohorts.py does; it is a script and cannot
    be imported), the deepest chain (r62g-read), the living share of the last 3,000 s bin (r62-health.living_bins), and the
    summer-start 500 s rows (r62g-windows: a switch into summer) with their kept W/g over the upkeep."""
    R = rr.R
    r62, r62gw, r62g = R["r62"], R["r62gw"], R["r62g"]
    e = rr.e
    _, _, rows500 = r62gw.arm(rr.path, ns)
    living, founded, born_n = r62.born_share(e, rr.end)
    cohorts = [c for c in rr.cohorts() if c[3] is not None]
    row = r62g.arm_row(rr.path, ns)
    if e.contact:
        lb = r62.living_bins(e, rr.end - 3000.0, rr.end, 3000.0)
        t3 = lb[0][4] if lb else None
    else:
        t3 = None
    summer = [dict(t0=r["t0"], t1=r["t1"], gmean=r["gmean"], gend=r["gend"],
                   share=(r["keptw"] / ns.upkeep_w if r["keptw"] is not None else None))
              for r in rows500 if r["switch"] and r["season"] == "summer"]
    return dict(gmax=max((r["gend"] for r in rows500), default=0), living=living,
                born_share=born_n / living if living else None,
                best_cohort=max((c[3] for c in cohorts), default=None), chain=row["chain"], chain_mine=rr.chain_depth(rr.end),
                t3=t3, contact=e.contact, summer=summer)


def d222_verdict(m, floor):
    """(verdict, why, judged summer rows). floor 0 is the strict reading: no first check, every summer-start row judged."""
    why = []
    if floor and m["gmax"] < floor:
        why.append(f"never reaches {floor:g} grazers (most {m['gmax']})")
    if m["born_share"] is None or m["born_share"] < BORN_SHARE_MIN:
        why.append(f"born share {fmt(m['born_share'])} under {BORN_SHARE_MIN}")
    if m["best_cohort"] is None or m["best_cohort"] < REPLACEMENT_MIN:
        why.append(f"best cohort {fmt(m['best_cohort'], '{:.2f}')} under {REPLACEMENT_MIN:g}")
    if m["chain"] < CHAIN_MIN:
        why.append(f"chain {m['chain']} under {CHAIN_MIN}")
    judged = [r for r in m["summer"] if floor == 0 or r["gmean"] >= floor]
    for r in judged:
        if r["share"] is None or r["share"] < UPKEEP_MIN:
            why.append(f"test 2 at {r['t0']:g} s is {fmt(r['share'], '{:.2f}')} under {UPKEEP_MIN}")
    if m["contact"]:
        if m["t3"] is None or m["t3"] < LIVING_SHARE_MIN:
            why.append(f"test 3 {fmt(m['t3'])} under {LIVING_SHARE_MIN}")
    return not why, why, judged


def gate1(rr, a, ns):
    R = rr.R
    r62 = R["r62"]
    e = rr.e
    measure = a.floor_measure
    floor = a.floors[0]
    print("\n== gate 1. grazers survive: the floor in every judged window, and D222 over the whole run (D244's reading)")

    # ---- the per-window information
    cohorts = rr.cohorts()
    last_i = len(rr.wins) - 1
    rows = []
    for n, win in enumerate(rr.wins):
        t0, t1 = win
        cmin, cmean = rr.window_counts(win, n == last_i)
        T = min(t1, rr.end)
        living, founded, born_n = r62.born_share(e, T)
        closed = [c for c in cohorts if c[1] <= T + 1e-9 and c[3] is not None]
        if e.contact:
            lb = r62.living_bins(e, t0, t1, t1 - t0)
            lshare = lb[0][4] if lb else None
        else:
            lshare = None
        rows.append(dict(win=win, cmin=cmin, cmean=cmean, share=born_n / living if living else None,
                         chain=rr.chain_depth(T), repl=max((c[3] for c in closed), default=None), lshare=lshare))
    what = {"mean": "a window's mean grazer count is at least the floor",
            "fewest": "a window's fewest grazers at any sample is at least the floor",
            "d244": "a window whose mean is under the floor is not judged, and the run must reach the floor at some sample"}[measure]
    print(f"   floor measure: {measure} ({what}), floor {floor:g}. Under every measure a window whose fewest is 0 fails: "
          "extinction is not survival however brief.")
    print(f"   {'window':<12} {'g fewest':>8} {'g mean':>6}  {'not 0':>5} {'floor':>5} || information, not part of the gate: "
          f"{'born share':>10} {'chain':>5} {'best repl':>9} {'living share':>12}")
    count_max = max(rr.counts) if rr.counts else 0
    window_fail = []
    for r in rows:
        t0, t1 = r["win"]
        nonzero = r["cmin"] is not None and r["cmin"] > 0
        if measure == "mean":
            ok = r["cmean"] is not None and r["cmean"] >= floor
        elif measure == "fewest":
            ok = r["cmin"] is not None and r["cmin"] >= floor
        else:
            ok = None if (r["cmean"] is None or r["cmean"] < floor) else True
        word = "n/j" if ok is None else ("ok" if ok else "FAIL")
        if not nonzero:
            window_fail.append(f"{t0:g}-{t1:g} has a sample with no grazer alive")
        if ok is False:
            window_fail.append(f"{t0:g}-{t1:g} {'mean ' + fmt(r['cmean'], '{:.1f}') if measure == 'mean' else 'fewest ' + str(r['cmin'])} under {floor:g}")
        print(f"   {t0:>5g}-{t1:<6g} {fmt(r['cmin'], '{}'):>8} {fmt(r['cmean'], '{:.1f}'):>6}  {'ok' if nonzero else 'FAIL':>5} {word:>5} || "
              f"{'':>36}{fmt(r['share']):>10} {r['chain']:>5} {fmt(r['repl'], '{:.2f}'):>9} {fmt(r['lshare']):>12}")
    if measure == "d244":
        if count_max < floor:
            window_fail.append(f"the run never reaches {floor:g} grazers at any sample (most {count_max})")
        print(f"   d244: the run reaches {count_max} grazers at its largest sample (floor {floor:g}); n/j = not judged")
    floor_ok = not window_fail
    print(f"   the floor in every judged window ({measure}): {'PASS' if floor_ok else 'FAIL - ' + '; '.join(window_fail)}")

    # ---- the whole-run D222
    m = d222_measures(rr, ns)
    print(f"\n   D222 over the whole run, as the r62cf read recorded it (its README, 'D222, by D244's reading first'); bars: born share "
          f"{BORN_SHARE_MIN}, best cohort {REPLACEMENT_MIN:g}, chain {CHAIN_MIN}, test 2 share {UPKEEP_MIN}, test 3 share {LIVING_SHARE_MIN}")
    print(f"   {'grazers at most':>15} {'born share':>10} {'best cohort':>11} {'chain':>5} {'test 3':>7}  test-2 rows judged at {floor:g} (window start: share)")
    judged_f = [r for r in m["summer"] if r["gmean"] >= floor]
    print(f"   {m['gmax']:>15} {fmt(m['born_share']):>10} {fmt(m['best_cohort'], '{:.2f}'):>11} {m['chain']:>5} "
          f"{fmt(m['t3']) if m['contact'] else 'n/a':>7}  "
          + (", ".join(f"{r['t0']:g}: {fmt(r['share'], '{:.2f}')}" for r in judged_f) or "none"))
    print("   summer-start rows (500 s, r62g-windows): " + ", ".join(
        f"{r['t0']:g} g mean {r['gmean']:.1f} share {fmt(r['share'], '{:.2f}')}" for r in m["summer"]))
    print(f"   chain: {m['chain']} in r62g-read, {m['chain_mine']} computed here: {'AGREE' if m['chain'] == m['chain_mine'] else 'DIFFER'}")
    if not m["contact"]:
        print("   test 3 is not judged: the run is on the corpse rule, which has no living-share measure")
    if not judged_f:
        print(f"   test 2 has no verdict at {floor:g}: no summer-start row holds a mean of {floor:g} grazers")
    out = {}
    for label, f in (("floor", floor), ("also", a.floors[1] if len(a.floors) > 1 else None), ("strict", 0)):
        if f is None:
            continue
        ok, why, judged = d222_verdict(m, f)
        out[label] = ok
        name = {"floor": f"verdict at the floor of {f:g} (the gate)", "also": f"verdict at {f:g}, a sensitivity",
                "strict": "strict verdict, every summer-start row judged and no first check"}[label]
        print(f"   {name}: {'PASS' if ok else 'FAIL'}" + ("" if ok else " - " + "; ".join(why)))
    g1 = bool(floor_ok and out.get("floor"))
    print(f"   gate 1: {'PASS' if g1 else 'FAIL'} (the floor in every judged window, and D222 at {floor:g})")
    return g1, out, m


# ---------------------------------------------------------------------------------------------- gate 2

def gate2(rr, a, islands):
    stats = rr.stats
    has_factor = any("bloomFactor" in s for s in stats)
    print("\n== gate 2. the bloom reaches the food")
    if not has_factor:
        print("   NOT JUDGED: the run's stats carry no bloomFactor column, so it has no bloom (arm A's kind)")
        return None, {}
    area = [s.get("bloomLeafArea") for s in stats]
    if not any(v is not None for v in area):
        print("   NOT JUDGED: stats carry bloomFactor but no bloomLeafArea (no leaf smell, so no leaf area is gathered)")
        return None, {}
    step = rr.step
    idx = [int(round(s["t"] / step)) for s in stats]
    n_idx = max(idx) + 1
    lo_i = int(math.ceil(a.t_from / step - 1e-9))
    hi_i = int(round(rr.end / step))
    k_count = len(stats[-1]["bloomFactor"])
    cols = {isl["k"]: isl["columns"] for isl in (islands or [])}
    judged = []
    series = {}
    print(f"   {'island':>6} {'columns':>7} {'mean leaf m2':>12} {'judged':>6} {'best lag s':>10} {'r':>7}")
    for k in range(k_count):
        x = [None] * n_idx
        y = [None] * n_idx
        for s, i in zip(stats, idx):
            if "bloomFactor" in s and k < len(s["bloomFactor"]):
                x[i] = s["bloomFactor"][k]
            if s.get("bloomLeafArea") is not None and k < len(s["bloomLeafArea"]):
                y[i] = s["bloomLeafArea"][k]
        series[k] = (x, y)
        span = [v for v in y[lo_i:hi_i + 1] if v is not None]
        mean_leaf = sum(span) / len(span) if span else None
        columns = cols.get(k)
        reasons = []
        if columns is None:
            reasons.append("no column count (no island line)")
        elif columns < MIN_COLUMNS:
            reasons.append(f"{columns} columns, under {MIN_COLUMNS}")
        if mean_leaf is None or mean_leaf < MIN_LEAF_AREA:
            reasons.append(f"mean lit leaf area {fmt(mean_leaf, '{:.2f}')} m2, under {MIN_LEAF_AREA:g}")
        if reasons:
            print(f"   {k:>6} {fmt(columns, '{}'):>7} {fmt(mean_leaf, '{:.2f}'):>12} {'no':>6} {'-':>10} {'-':>7}   left out: "
                  + "; ".join(reasons))
            continue
        lag, r = best_lag(x, y, step, lo_i, hi_i)
        judged.append((k, lag, r))
        print(f"   {k:>6} {columns:>7} {mean_leaf:>12.2f} {'yes':>6} {fmt(lag, '{:g}'):>10} {fmt(r):>7}"
              f"{'' if (r is not None and r >= GATE2_R) else '   below ' + str(GATE2_R)}")
    if not judged:
        print("   FAIL: no island of at least 5 columns holds a mean lit leaf area of 1 m2, so there is nothing to follow the bloom")
        return False, {}
    ok = all(r is not None and r >= GATE2_R for _, _, r in judged)
    print(f"   {'PASS' if ok else 'FAIL'}: {sum(1 for _, _, r in judged if r is not None and r >= GATE2_R)} of {len(judged)} "
          f"judged islands reach {GATE2_R} at their best lag")
    return ok, {k: (lag, r) for k, lag, r in judged}


# ---------------------------------------------------------------------------------------------- gate 3

def gate3(rr, a, traits_path):
    print("\n== gate 3. grazers evolve")
    if not traits_path or not os.path.isfile(traits_path):
        print(f"   NOT JUDGED: no trait table ({traits_path or 'none given'}); run Evosim.Farm --grazer-traits <run dir> <out tsv>")
        return None, None
    rows, comments = read_traits(traits_path)
    print(f"   trait table {traits_path}: {len(rows)} grazers")
    for c in comments:
        if c.startswith("# drive:") or c.startswith("# note:"):
            print("   " + c[:240])
    missing = [i for i in rr.grazers if i not in rows]
    if missing:
        print(f"   {len(missing)} grazers of the lineage have no row in the table (no genome row, or not developable)")
    last_i = len(rr.wins) - 1
    births = []
    for n, win in enumerate(rr.wins):
        ids = [i for i in rr.grazers if in_window(rr.w.born[i][0], win, n == last_i)]
        sample = [rows[i] for i in ids if i in rows]
        births.append((win, len(ids), sample))
    print("   grazer births per window (lineage / with traits): " +
          ", ".join(f"{w[0]:g}-{w[1]:g}: {nb}/{len(s)}" for w, nb, s in births))
    usable = [n for n, (_, _, s) in enumerate(births) if len(s) >= MIN_BIRTHS]
    if len(usable) < 2:
        print(f"   FAIL: fewer than two windows with {MIN_BIRTHS} grazer births, so no first and last window to compare")
        return False, None
    first, last = usable[0], usable[-1]
    fw, lw = births[first][0], births[last][0]
    note = []
    if first != 0:
        note.append(f"the first judged window has too few births; the nearest with {MIN_BIRTHS} is {fw[0]:g}-{fw[1]:g}")
    if last != len(births) - 1:
        note.append(f"the last judged window has too few births; the nearest with {MIN_BIRTHS} is {lw[0]:g}-{lw[1]:g}")
    print(f"   first window used {fw[0]:g}-{fw[1]:g} (n {len(births[first][2])}), last {lw[0]:g}-{lw[1]:g} (n {len(births[last][2])})"
          + ("; " + "; ".join(note) if note else ""))
    founders = [r for r in rows.values() if is_founder_row(r)]
    print(f"   founder grazers (parent -1, or source floor, trickle or pool): {len(founders)}")
    print(f"   {'trait':<11} {'shift':>7} {'mean first':>11} {'mean last':>11} {'n 1/2':>9} {'sd first':>9} {'sd last':>9} "
          f"{'founder sd':>10} {'scale':>9} {'scale from':<12}")
    shifts = {}
    for t in TRAITS:
        f_vals = [r[t] for r in births[first][2]]
        l_vals = [r[t] for r in births[last][2]]
        fsd = sample_sd([r[t] for r in founders])
        sd1, sd2 = sample_sd(f_vals), sample_sd(l_vals)
        scale, which = founder_scale(fsd, sd1)
        m1, m2 = statistics.mean(f_vals), statistics.mean(l_vals)
        sh = shift_in_scale(m1, m2, scale)
        shifts[t] = sh
        print(f"   {t:<11} {fmt(sh, '{:.2f}'):>7} {m1:>11.4g} {m2:>11.4g} {len(f_vals):>4}/{len(l_vals):<4} {sd1:>9.3g} {sd2:>9.3g} "
              f"{fsd:>10.3g} {scale:>9.3g} {which:<12}" + ("   (scale 0)" if scale == 0 else ""))
    drive = max(shifts["drive_rest"], shifts["drive_fed"])
    gate_shifts = {"mass_kg": shifts["mass_kg"], "parts": shifts["parts"], "limb_push": shifts["limb_push"], "drive": drive}
    top = max(gate_shifts, key=lambda k: gate_shifts[k])
    ok = gate_shifts[top] >= SHIFT_GATE
    print(f"   the drive's shift is the larger of its two ({fmt(drive, '{:.2f}')}); the largest shift is {top}, "
          f"{fmt(gate_shifts[top], '{:.2f}')} scale units")
    print(f"   {'PASS' if ok else 'FAIL'}: at least one trait shifts by {SHIFT_GATE:g} scale unit")
    return ok, gate_shifts[top]


# ---------------------------------------------------------------------------------------------- 4 and 5

def section4(rr, a):
    print("\n== 4. the world is rich (reported)")
    shares = rr.clade_shares()
    last_i = len(rr.wins) - 1
    best_overall = 0
    print(f"   {'window':<12} {'most clades at 10%':>18} {'at last sample':>14}")
    for n, win in enumerate(rr.wins):
        c = [(t, k) for t, k in zip(rr.times, shares) if in_window(t, win, n == last_i)]
        if not c:
            continue
        mx = max(k for _, k in c)
        best_overall = max(best_overall, mx)
        print(f"   {win[0]:>5g}-{win[1]:<6g} {mx:>18} {c[-1][1]:>14}")
    print(f"   three clades each hold 10% of the living grazers in some window: "
          f"{'YES' if best_overall >= 3 else 'NO'} (the most at once: {best_overall})")
    e = rr.e
    by_g = by_o = unknown = 0
    in_by_g = in_by_o = 0
    for (t, victim, by, j) in e.kill_j:
        if victim not in rr.gset or t > rr.end + 1e-9:
            continue
        if by is None:
            unknown += 1
        elif by in rr.gset:
            by_g += 1
        else:
            by_o += 1
        if a.t_from < t <= rr.end + 1e-9 and by is not None:
            if by in rr.gset:
                in_by_g += 1
            else:
                in_by_o += 1
    print(f"   kill rows with a grazer as victim, whole run: by a grazer {by_g}, by another body {by_o}, attacker not named "
          f"{unknown}; in the judged span: by a grazer {in_by_g}, by another {in_by_o}")
    rem_from, rem_closed = set(), 0
    founded = {}
    for r in rr.w.seg_rows("lineage.jsonl"):
        if r["t"] > rr.end + 1e-9:
            break
        if r.get("e") == "r" and r.get("from") in rr.gset:
            founded[r["id"]] = r["t"]
            rem_from.add(r["id"])
    for r in rr.w.seg_rows("lineage.jsonl"):
        if r["t"] > rr.end + 1e-9:
            break
        if r.get("e") == "rc" and r["id"] in rem_from:
            rem_closed += 1
    bk = [s.get("biteKeptRemains") for s in rr.stats if s.get("biteKeptRemains") is not None]
    print(f"   remains founded from grazers' bodies: {len(rem_from)}; of them closed (burnt out): {rem_closed}. The record does "
          f"not say who bit a remains (no row by victim), so bites on grazers' remains are not counted; the total kept from "
          f"all remains is stats biteKeptRemains, {fmt(bk[-1] if bk else None, '{:.0f}')} J to {rr.end:g} s")
    return best_overall


def read_islands_for(rr, explicit_report, islands_from):
    """(islands, note). The run's own report line, or --islands-from's sibling's."""
    if islands_from:
        other = load_reads()["r61"].r59.resolve(islands_from)
        for c in (os.path.normpath(os.path.dirname(other)) + ".md", os.path.join(other, "report.md")):
            if os.path.isfile(c):
                with open(c, encoding="utf-8", errors="replace") as f:
                    text = f.read()
                if ISLAND_HEAD.search(text):
                    return parse_islands(text), f"islands from {c} (--islands-from)"
        return None, f"--islands-from {islands_from}: no island line in its report"
    path, text = find_report(rr.w, explicit_report)
    if text is None:
        return None, "the run's report carries no island line (a run without the bloom); pass --islands-from RUN for the plan"
    return parse_islands(text), f"islands from {path}"


def section5(rr, a, groves, side, bloom_islands):
    print("\n== 5. story material (reported)")
    R = rr.R
    w = rr.w
    wins, last_i = rr.wins, len(rr.wins) - 1
    parts = dict.fromkeys(PARTS, 0)
    out = dict(crossings=0, strolls=0, crowd_ok=0, recoveries=0)

    # busts and recoveries come from the stats alone
    busts = find_busts(rr.times, rr.counts, lo=a.t_from)
    print(f"\n   busts and recoveries in the world's grazer count, over [{a.t_from:g}, {rr.end:g}] s "
          f"(a fall to half or less of the highest count in the {BUST_BEFORE:g} s before it; a recovery is a return to it)")
    if not busts:
        print("   none")
    for b in busts:
        depth = b["low"] / b["ref"]
        rec = f"recovered at {b['recovery']:g} s, {b['recovery'] - b['t']:g} s later" if b["recovery"] is not None else "not recovered by the end"
        print(f"   bust at {b['t']:g} s from {b['ref']} to a low of {b['low']} at {b['low_t']:g} s (depth {depth:.2f} of the earlier highest); {rec}")
    recoveries = sum(1 for b in busts if b["recovery"] is not None)
    out["recoveries"] = recoveries

    if not groves:
        print("\n   crossings, strolls and crowds: NOT READ, no islands (the run's report has no island line and --islands-from was not given)")
        out.update(crossings=0, crowd_ok=0)
        return out
    if not w.segs or not R["r61"].r59.runrec.has_positions(w.segs[-1][0]):
        print("\n   crossings, strolls and crowds: NOT READ, the run has no positions file")
        return out

    gset = rr.gset
    K = len(groves)
    last = {}
    moves = []
    crowd = {}
    fixed_in = defaultdict(set)
    seen_all = set()
    for t, line in w.seg_positions(lambda t: t <= rr.end + 1e-9):
        row = json.loads(line)["b"]
        counts = [0] * K
        wi = next((n for n, win in enumerate(wins) if in_window(t, win, n == last_i)), None)
        for b in row:
            i = b[0]
            if i not in gset or b[4] & REMAINS_FLAG:
                continue
            x, z = b[1], b[3]
            k = assign_grove(x, z, groves, side)
            if k >= 0:
                counts[k] += 1
            if wi is not None:
                fixed_in[wi].add(i)
                seen_all.add(i)
            if k < 0:
                continue
            was = last.get(i)
            last[i] = (k, t, x, z)
            if was is not None and was[0] != k:
                d = math.hypot(gap(was[2], x, side), gap(was[3], z, side))
                ga, gb = groves[was[0]], groves[k]
                moves.append(dict(i=i, t1=was[1], t2=t, a=was[0], b=k, dist=d, dur=t - was[1],
                                  cent=math.hypot(gap(ga["cx"], gb["cx"], side), gap(ga["cz"], gb["cz"], side)),
                                  egap=edge_gap(was[0], k, groves, side)))
        crowd[t] = counts

    G = a.crossing_gap
    print(f"\n   moves between groves: a move is a fix over grove B whose last fix over a grove was over grove A, B not A. "
          f"A crossing is a move whose banded edge gap (centroid distance less (r_A + 10) less (r_B + 10)) is at least "
          f"--crossing-gap, {G:g} m; every other move is a stroll. Counts are also given at a gap of 0 m (groves that do "
          f"not touch).")
    print(f"   {'window':<12} {'strolls':>7} {'crossings':>9} {'strolls@0':>9} {'cross@0':>7} {'grazers fixed':>13} "
          f"{'strolls/g':>9} {'cross/g':>8} {'cross fix-to-fix m':>18} {'cross centroids m':>17} {'cross s (median)':>16}")
    tot_s = tot_c = tot_s0 = tot_c0 = 0
    cross_all = []
    for n, win in enumerate(wins):
        ms = [m for m in moves if in_window(m["t2"], win, n == last_i)]
        c = [m for m in ms if m["egap"] >= G]
        c0 = [m for m in ms if m["egap"] >= 0.0]
        s_ = len(ms) - len(c)
        s0 = len(ms) - len(c0)
        g = len(fixed_in[n])
        tot_s += s_
        tot_c += len(c)
        tot_s0 += s0
        tot_c0 += len(c0)
        cross_all.extend(c)
        print(f"   {win[0]:>5g}-{win[1]:<6g} {s_:>7} {len(c):>9} {s0:>9} {len(c0):>7} {g:>13} "
              f"{fmt(s_ / g if g else None, '{:.3f}'):>9} {fmt(len(c) / g if g else None, '{:.3f}'):>8} "
              f"{fmt(statistics.median(m['dist'] for m in c) if c else None, '{:.1f}'):>18} "
              f"{fmt(statistics.median(m['cent'] for m in c) if c else None, '{:.1f}'):>17} "
              f"{fmt(statistics.median(m['dur'] for m in c) if c else None, '{:.0f}'):>16}")
    lives = len(seen_all)
    print(f"   judged span at a gap of {G:g} m: {tot_s} strolls and {tot_c} crossings by {lives} grazers fixed in it: per grazer life "
          f"{fmt(tot_s / lives if lives else None, '{:.3f}')} strolls and {fmt(tot_c / lives if lives else None, '{:.3f}')} crossings; "
          f"crossings' median distance {fmt(statistics.median(m['dist'] for m in cross_all) if cross_all else None, '{:.1f}')} m fix to fix "
          f"({fmt(statistics.median(m['cent'] for m in cross_all) if cross_all else None, '{:.1f}')} m between the groves' centroids), "
          f"median duration {fmt(statistics.median(m['dur'] for m in cross_all) if cross_all else None, '{:.0f}')} s")
    print(f"   judged span at a gap of 0 m: {tot_s0} strolls and {tot_c0} crossings")
    out.update(crossings=tot_c, strolls=tot_s, crossings0=tot_c0, strolls0=tot_s0)

    # crowds at blooms
    print(f"\n   crowds at blooms: grazers inside each grove at each stats sample, correlated with bloomFactor at the best lag "
          f"from 0 to {LAG_MAX:g} s (the crowd lags the bloom)")
    if not bloom_islands or not any("bloomFactor" in s for s in rr.stats):
        print("   NOT READ: the run has no bloomFactor column to correlate with")
        return out
    step = rr.step
    idx = [int(round(s["t"] / step)) for s in rr.stats]
    n_idx = max(idx) + 1
    lo_i = int(math.ceil(a.t_from / step - 1e-9))
    hi_i = int(round(rr.end / step))
    print(f"   {'island':>6} {'columns':>7} {'mean crowd':>10} {'best lag s':>10} {'r':>7}")
    ok = 0
    for n, g in enumerate(groves):
        k = g["k"]
        x = [None] * n_idx
        y = [None] * n_idx
        for s, i in zip(rr.stats, idx):
            if "bloomFactor" in s and k < len(s["bloomFactor"]):
                x[i] = s["bloomFactor"][k]
            c = crowd.get(s["t"])
            if c is not None:
                y[i] = c[n]
        span = [v for v in y[lo_i:hi_i + 1] if v is not None]
        lag, r = best_lag(x, y, step, lo_i, hi_i)
        if r is not None and r >= CROWD_R:
            ok += 1
        print(f"   {k:>6} {g['columns']:>7} {fmt(sum(span) / len(span) if span else None, '{:.2f}'):>10} {fmt(lag, '{:g}'):>10} {fmt(r):>7}"
              f"{'   >= ' + str(CROWD_R) if (r is not None and r >= CROWD_R) else ''}")
    out["crowd_ok"] = ok
    print(f"   islands whose crowd correlation reaches {CROWD_R}: {ok} of {K}")
    return out


# ---------------------------------------------------------------------------------------------- the driver

def read_run(path, a, traits_map, islands_from):
    R = load_reads()
    ns = argparse.Namespace(end=a.end, year=6000.0, every=6000.0, window=500.0, upkeep_w=a.upkeep_w, t_from=a.t_from, bin=a.window)
    rr = RunRead(path, a, ns)
    w = rr.w
    m = w.manifest
    src = m.get("source") or {}
    print(f"\n######## {rr.name}  arm {rr.arm}  seed {rr.seed}  read to {rr.end:g} s  ({os.path.relpath(w.dir)})")
    print(f"   configHash {m.get('configHash')}  gitCommit {str(src.get('gitCommit'))[:12]}{' (DIRTY)' if src.get('gitDirty') else ''}  "
          f"coreHash {str(src.get('coreHash'))[:8]}  dynamicsHash {str(src.get('dynamicsHash'))[:8]}  farmHash {str(src.get('farmHash'))[:8]}")
    chain = " <- ".join(os.path.basename(d) for d, _, _ in reversed(w.segs))
    print(f"   chain {chain}; {len(rr.grazers)} grazers born (atk 1 and ink 1); judged windows "
          + ", ".join(f"{t0:g}-{t1:g}" for t0, t1 in rr.wins) + f"; stats cadence {rr.step:g} s")
    for n_ in w.notes:
        print(f"   note: {n_}")

    islands, why = read_islands_for(rr, a.report, islands_from)
    print(f"   {why}" + (f": {len(islands)} islands" if islands else ""))
    cell = float(R["r61"].r59.find(w.config, "fieldMatterCellMetres") or CELL_DEFAULT)
    shape = R["r61"].r59.find(w.config, "worldShape")
    area = R["r61"].r59.find(w.config, "worldAreaSquareMetres")
    side = math.sqrt(float(area)) if (shape == "Box" and area) else None
    groves = make_groves(islands, cell) if islands else []
    if groves:
        print(f"   cell {cell:g} m; {'joined edges, side %g m' % side if side else 'edges not joined (%s)' % shape}; groves r_eff + {GROVE_BAND:g} m: "
              + "; ".join(f"{g['k']}: {g['columns']}c r_eff {g['r_eff']:.1f}" for g in groves))

    g1, g1_all, _ = gate1(rr, a, ns)
    g2, _ = gate2(rr, a, islands)
    traits_path = traits_map.get(rr.name) or traits_map.get(None)
    if not traits_path:
        for c in (os.path.join(w.dir, "grazer-traits.tsv"), os.path.join(os.path.dirname(w.dir), "grazer-traits.tsv")):
            if os.path.isfile(c):
                traits_path = c
                break
    g3, shift = gate3(rr, a, traits_path)
    section4(rr, a)
    story = section5(rr, a, groves, side, islands)
    parts = dict(zip(PARTS, (story.get("crossings", 0), story.get("crowd_ok", 0), story.get("recoveries", 0), shift or 0.0)))
    return dict(name=rr.name, arm=rr.arm, seed=rr.seed, g1=g1, g1_all=g1_all, g2=g2, g3=g3, parts=parts)


def verdict_word(v):
    return "NOT JUDGED" if v is None else ("PASS" if v else "FAIL")


def rank_and_print(results):
    by_arm = defaultdict(list)
    for r in results:
        by_arm[r["arm"]].append(r)
    print("\n######## ranking")
    ranked = {}
    for arm, rs in by_arm.items():
        bad = [(r["name"], g) for r in rs for g, v in (("gate 1", r["g1"]), ("gate 2", r["g2"]), ("gate 3", r["g3"]))
               if v is False or (g != "gate 2" and v is None)]
        if bad:
            print(f"   arm {arm}: not ranked, {', '.join(f'{n} fails or lacks {g}' for n, g in bad)}")
            continue
        ranked[arm] = [statistics.mean(r["parts"][p] for r in rs) for p in PARTS]
    if not ranked:
        print("   no arm passes every gate, so nothing is ranked")
        return None
    rows, winner = rank_arms(ranked)
    print(f"   {'arm':<14} " + " ".join(f"{p:>14}" for p in PARTS) + f" | " + " ".join(f"{'rank':>5}" for _ in PARTS) + f" {'sum':>5}")
    for arm, vals, ranks, total in rows:
        print(f"   {arm:<14} " + " ".join(f"{v:>14.3f}" for v in vals) + " | " + " ".join(f"{r:>5g}" for r in ranks) + f" {total:>5g}")
    print(f"   the lowest rank sum wins; a tie goes to the simplest arm (A, B, C, D). Winner: arm {winner}"
          + ("" if len(rows) > 1 else " (the only arm that passes every gate)"))
    return winner


# ---------------------------------------------------------------------------------------------- self-test

def self_test():
    ok_all = True

    def check(name, cond, detail=""):
        nonlocal ok_all
        ok_all &= bool(cond)
        print(f"{'PASS' if cond else 'FAIL'}  {name}" + (f"  ({detail})" if detail else ""))

    # 1. the correlation at the best lag finds a lag planted in a synthetic series
    import random
    rng = random.Random(7)
    step, n, lag_s = 10.0, 900, 700.0
    base = [math.sin(i / 13.0) + 0.5 * math.sin(i / 5.1 + 1.0) + 0.1 * rng.random() for i in range(n + 400)]
    L = int(lag_s / step)
    x = base[:n]
    y = [None] * L + [base[i] + 0.02 * rng.random() for i in range(n - L)]   # y[i + L] = x[i]
    lag, r = best_lag(x, y, step, 100, 800)
    check("the best lag finds a planted lag of 700 s", lag == lag_s and r is not None and r > 0.95, f"lag {lag}, r {fmt(r)}")
    lag0, r0 = best_lag(x, [None] * n, step, 100, 800)
    check("a series with no values gives no lag", lag0 is None and r0 is None)
    flat = [1.0] * n
    check("a constant series has no correlation", pearson(flat, x) is None)

    # 2. the founder SD floor picks the larger
    scale, which = founder_scale(0.05, 0.4)
    check("the floor picks the first window's SD when it is larger", scale == 0.4 and which == "first window")
    scale, which = founder_scale(0.9, 0.4)
    check("the floor picks the founders' SD when it is larger", scale == 0.9 and which == "founders")
    check("a shift is the means' gap in scale units", abs(shift_in_scale(1.0, 3.0, 1.0) - 2.0) < 1e-12
          and shift_in_scale(1.0, 1.0, 0.0) == 0.0 and shift_in_scale(1.0, 2.0, 0.0) == math.inf)

    # 3. a move between touching groves is a stroll, and one between apart groves is a crossing
    cell = 5.0
    isl = [dict(k=0, cx=100.0, cz=100.0, columns=25), dict(k=1, cx=125.0, cz=100.0, columns=25),
           dict(k=2, cx=190.0, cz=100.0, columns=25), dict(k=3, cx=5.0, cz=100.0, columns=25)]
    groves = make_groves(isl, cell)
    r_eff = island_radius(25, cell)
    # 0 and 1 are 25 m apart: touching, since 25 < 2 r_eff + 20 = 2 x 7.98 + 20
    check("groves 25 m apart touch, so a move is a stroll", classify_move(0, 1, groves, None) == "stroll", f"r_eff {r_eff:.2f}")
    check("groves 90 m apart do not touch, so a move is a crossing", classify_move(0, 2, groves, None) == "crossing")
    # 60 m apart: the banded edge gap is 60 - 2 x (14.10 + 10) = 11.8 m
    check("a banded edge gap of 11.8 m is a crossing at a gap of 10 m and a stroll at 12 m",
          abs(edge_gap(0, 1, make_groves([dict(k=0, cx=100.0, cz=100.0, columns=25), dict(k=1, cx=160.0, cz=100.0, columns=25)], cell), None) - 11.8) < 0.1
          and classify_move(0, 1, make_groves([dict(k=0, cx=100.0, cz=100.0, columns=25), dict(k=1, cx=160.0, cz=100.0, columns=25)], cell), None, 10.0) == "crossing"
          and classify_move(0, 1, make_groves([dict(k=0, cx=100.0, cz=100.0, columns=25), dict(k=1, cx=160.0, cz=100.0, columns=25)], cell), None, 12.0) == "stroll")
    check("the shorter way round a joined world: 5 and 195 on a 200 m side are 10 m apart", abs(gap(5.0, 195.0, 200.0) - 10.0) < 1e-12)
    check("across the seam of a joined 200 m world, groves 15 m apart are a stroll; unjoined they are 185 m apart, a crossing",
          classify_move(3, 2, groves, 200.0) == "stroll" and classify_move(3, 2, groves, None) == "crossing")
    check("a point inside two groves goes to the nearer centroid",
          assign_grove(112.0, 100.0, groves, None) == 0 and assign_grove(113.0, 100.0, groves, None) == 1)
    check("a point in no grove is -1", assign_grove(50.0, 50.0, groves, None) == -1)

    # 4. a planted bust and recovery are found
    times = [10.0 * i for i in range(1, 301)]
    counts = [60] * 300
    for i in range(100, 110):
        counts[i] = 20            # a fall to a third at sample 100
    for i in range(110, 130):
        counts[i] = 40            # a partial climb
    for i in range(130, 300):
        counts[i] = 60            # back to the earlier highest count
    for i in range(250, 253):
        counts[i] = 31            # a fall to 31 of 60: more than half, no bust
    busts = find_busts(times, counts)
    check("one planted bust is found, and no other", len(busts) == 1, f"{len(busts)} found")
    b = busts[0]
    check("the bust is at the fall, depth a third, recovered when the count returns to 60",
          b["t"] == times[100] and b["ref"] == 60 and b["low"] == 20 and b["recovery"] == times[130], str(b))
    unrecovered = find_busts(times[:120], counts[:120])
    check("a bust still down at the end has no recovery", len(unrecovered) == 1 and unrecovered[0]["recovery"] is None)

    # 5. the rank sum and the tie rule
    parts = {"x-A": [10, 5, 0, 1.0], "x-B": [20, 5, 2, 0.5], "x-C": [20, 1, 2, 0.5]}
    rows, winner = rank_arms(parts)
    sums = {r[0]: r[3] for r in rows}
    # crossings: B and C tie for first (1.5 each), A third; crowd: A and B tie (1.5), C third; recoveries: B, C tie (1.5),
    # A third; shift: A first, B and C tie (2.5)
    check("ranks are averaged over ties and summed", sums == {"x-A": 3 + 1.5 + 3 + 1, "x-B": 1.5 + 1.5 + 1.5 + 2.5, "x-C": 1.5 + 3 + 1.5 + 2.5},
          str(sums))
    check("the lowest sum wins", winner == "x-B", f"winner {winner}")
    tie = {"s-C": [1, 1, 1, 1.0], "s-A": [1, 1, 1, 1.0], "s-B": [1, 1, 1, 1.0]}
    check("a tie goes to the simplest arm, A before B before C", rank_arms(tie)[1] == "s-A", rank_arms(tie)[1])
    check("an arm is the name less its seed suffix", arm_of("g2-B-6301") == "g2-B" and arm_of("r62cf-6202") == "r62cf"
          and arm_letter("g2-B") == "B" and arm_letter("r62cf") is None)

    # the island line parses
    line = ("island bloom, 2 islands (label: P s, A, phase rad, columns, centre x z m): 0: P 6000, A 0.5, phase 1.234, 12 columns, "
            "at 62.1 159.2; 1: P 8000.5, A 0.912, phase 5.6, 1 columns, at 127.5 72.5")
    parsed = parse_islands("junk\n" + line + "\n")
    check("the report's island line parses", parsed and len(parsed) == 2 and parsed[1]["columns"] == 1 and parsed[0]["cx"] == 62.1
          and parsed[0]["P"] == 6000.0, str(parsed))
    check("a run with no island line gives none", parse_islands("# report\nnothing here\n") is None)
    check("island radius: 12 columns of 5 m", abs(island_radius(12, 5.0) - 9.772050238058398) < 1e-9)

    print("\nself-test: " + ("ALL PASS" if ok_all else "FAILED"))
    return 0 if ok_all else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="*")
    ap.add_argument("--traits", action="append", default=[], help="PATH for one run, or ARM=PATH (repeatable); ARM is the run's name")
    ap.add_argument("--islands-from", default=None, help="a sibling run of the same seed whose report carries the island plan (arm A)")
    ap.add_argument("--report", default=None, help="the run's report file, where it is not <arm folder>.md")
    ap.add_argument("--end", type=float, default=24000.0)
    ap.add_argument("--from", dest="t_from", type=float, default=6000.0)
    ap.add_argument("--window", type=float, default=3000.0)
    ap.add_argument("--upkeep-w", type=float, default=0.908)
    ap.add_argument("--floor-measure", choices=("mean", "fewest", "d244"), default="mean",
                    help="how the grazer floor judges a window: the window's mean count (default), the fewest at any "
                         "sample, or d244 (a window under the floor, by its mean, is not judged; the run must reach the floor once)")
    ap.add_argument("--crossing-gap", type=float, default=10.0,
                    help="metres: a move is a crossing when the banded edge gap (centroid distance less r_A + 10 less r_B + 10) is at least this")
    ap.add_argument("--floors", default="30,40", help="the grazer floor of the gate (D244) and the sensitivity beside it")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    if not a.runs:
        ap.error("give at least one RUN, or --self-test")
    a.floors = [float(x) for x in a.floors.split(",") if x.strip()]
    traits_map = {}
    for t in a.traits:
        if "=" in t and not os.path.exists(t):
            k, v = t.split("=", 1)
            traits_map[k] = v
        else:
            traits_map[None] = t
    if None in traits_map and len(a.runs) > 1:
        ap.error("--traits PATH names one table, which fits one run; give ARM=PATH for each of several")
    print(f"== G2's read: floor measure {a.floor_measure}, crossing gap {a.crossing_gap:g} m; windows of {a.window:g} s from {a.t_from:g} to {a.end:g} s; floor {a.floors[0]:g} (D244), sensitivity at "
          f"{('%g' % a.floors[1]) if len(a.floors) > 1 else '-'}; spec logbook/specs/bloom-build/g2-read-spec.md")
    results = [read_run(r, a, traits_map, a.islands_from) for r in a.runs]
    print("\n######## summary")
    for r in results:
        p = r["parts"]
        print(f"   {r['name']}: gate 1 {verdict_word(r['g1'])} (at 40 {verdict_word(r['g1_all'].get('also'))}, strict "
              f"{verdict_word(r['g1_all'].get('strict'))}), gate 2 {verdict_word(r['g2'])}, gate 3 {verdict_word(r['g3'])}; "
              f"story: crossings {p['crossings']}, crowd islands {p['crowd islands']}, recoveries {p['recoveries']}, "
              f"largest shift {fmt(p['largest shift'], '{:.2f}')}")
    if len(results) > 1:
        rank_and_print(results)


if __name__ == "__main__":
    main()
