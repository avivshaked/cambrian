#!/usr/bin/env python3
"""The clause reader for round 48 (logbook/specs/r48-prereg-draft.md, M1 to P1): round 47's
world with the owner's four rulings of 2026-09-24 on (D119 the bud and the rigid-group floors,
D120 reproduction paid as it goes and the proportional overhead, D121 senescence on upkeep
alone, D122 founders at the richest cell of their food with a 600 s endowment).

Modelled on round 47's reader (scripts/reads/r47-read.py): the same run-directory layout, the
same tolerant reading (a missing field prints 'absent' rather than raising, a half-written last
line of a live file is dropped), scripts/reads/contact_aliases.py's `field()` for every
stats.jsonl lookup, the censored-manifest rule (B1 fails a run whose manifest reads `error` or
`stopped`, and every clause prints `short` when its sample falls before 30,000 s), one row per
seed per clause and a `held in N of M` line per clause. Where a clause has two halves read
against different thresholds, each half is its own row (O2a/O2b, R1a/R1b).

NAMES below is the one dictionary of what this script reads. The new names were confirmed
against main at dff084e on 2026-09-24: src/Evosim.Core/Ecosystem/LineageEvent.cs (the birth
row's `bud`, the bud cell types joined by "+", and `budx`, how many the body built, only on a
birth that budded; `gm` 0 lump or 1 gestation and `gs` the share, on every birth row; `endow`,
joules, on a founder row when above 0), src/Evosim.Farm/Row.cs (cumulative `gestationBirths`
and `gestatedJoules`, and `gestationJoulesHeld` the living's accounts now, beside `births`;
`meanBirthInvestment`, `meanAge`, `stillbirths`, `selfOverlapStillbirths`) and Row.cs's field
dump (layout.json's `snowColumns`, `snowFloor` and `bed`; NNNNNNNNN.snow-columns.f32 each 1 m
column's snow in joules, NNNNNNNNN.snow-floor.f32 the stock of each column's lowest live cell,
bed.f32 the floor's y). The cell-type names in `bud` are StandardCellTypes' ("absorptive",
"photosynthetic", "consumer", "link", ...).

Which build a run is, for the clauses that ask of a new rule, is read off config.json's keys:
`floorsWeighRigidGroups` marks D119's build (the bud has no tunable of its own: it is drawn at
the cell-type rate in place of the retired type change), `gestationModeChance` D120's,
`founderEndowmentSeconds` and `foundersFollowFoodDepth` D122's, `senescenceWearsIntake` D121's.
A run without the key prints `absent` on the clause with that note, so a round 47 run reads
absent on M1, M2, M3, G1, G2 and E1 rather than a false zero.

How each clause is read, and what cannot be read as the draft words it:

  - M1 counts the birth rows carrying `bud` by the second read. M2 is the share of those rows
    with `budx` at 1 or more; the two-sided reading (`stillbirths` and `selfOverlapStillbirths`
    at the sample) is printed beside it.
  - M3 reads "a connected clade whose first member's row carries an absorptive bud" as the
    bud row's own descendants: a bud root is a birth row whose `bud` names `absorptive` and
    whose `abs` and `pho` are both 1, and its clade is every body whose parent chain passes
    through it, the root included. The living are the ids of the positions row at the second
    read (the lineage's born-and-not-dead when there is no positions file). A body under two
    nested bud roots counts for both. Held when any bud root has ten or more living members;
    the best clade's stomachs (abs 1, pho 0) and mixotrophs among its living are printed.
  - G1 is `gestationBirths` at the sample. G2 compares the share of children (birth rows whose
    kind is not a founder's; a founder's mode is the founding draw and not selection) with `gm`
    1 in the last 5,000 s against the share in the 5,000 s after the floor closes
    (config.json's `floorClosesAfterSeconds`, 3,000 s); the share over every birth row, founders
    included, is printed beside it, and the strong reading is each mode's births (by the
    parent's mode) per death (by the dead body's own mode) in the last window. A read before
    the end of the early window is absent.
  - O1, O2a, S1 compare `meanBirthInvestment`, `alive` and `meanAge` at the sample with round
    47's same seed at its sample at 30,000 s, read at read time from runs/r47-s<N> (the newest
    run directory; N from the arm's `-s<N>` suffix, else from run.json's `seed`; or named with
    --baseline-arms). O2b is the maximum `alive` over every sample under config.json's
    `maximumPopulation` (25,000) and a manifest reason that is not `ceiling-*`.
  - S2 is the median age at death of the stomach children (birth rows with `abs` 1, `pho` 0 and
    a kind other than a founder's) that died by the second read; the living are counted beside
    it and left out of the median.
  - F1, F2, F3 are round 47's L1, L2, L3 as they were: a pool founder is a founder row with
    `src` "pool".
  - F4 is the draft's wording at cf72687: every pool founder's first absorptive.jsonl row
    (the smallest t at or after its birth; `densityHere`, the snow density the body was fed at)
    reads at least its column's mean snow (the column sum in the snow-columns dump at or before
    its birth over the column's water, rock removed, as round 47's L4 read it). The layer the
    rule places a founder in cannot be read itself, since no record holds the snow by layer
    (the farm dumps the column sums and the lowest live cell only); the body's own sample
    stands in for it. Three things to read it with: `densityHere` is share-multiplied, so a
    founder in a crowded cell reads under the cell's density; the absorptive row carries y and
    no x or z, so the column is the one under the founder's first positions row; and a founder
    that died before its first sample has only its death row, which is counted. Held over the
    founders that have both readings, the rest counted beside it. Printed beside it: the
    median first reading against round 47's 0.12 J/m3, the depth reading (the first positions
    row: how many in the top 12 m, the median depth and height above the floor), and round 47's
    L4 (the breeders' column mean at their first positions row, at or over 1 J/m3).
  - E1 is a reading: the founder rows' `endow` (count, median, range; all founders and the pool
    founders apart) against the endowed founders' median age at death and the share that bred.
  - B1 is round 47's L9: the largest |auditResidual| over every sample under 0.1 J,
    |matterResidual| at the sample under 1e-5 units, `diverged` 0 at the last sample, and a
    manifest that is not `error` or `stopped`. The diverged dumps are counted beside it.
  - R1 is round 47's L5 (R1a) and L6 (R1b), printed as readings with their round 47 verdicts in
    `as_r47`, and the same readings of round 47's same seed beside them (`base_*`). R1a
    inherits L5's limit: a table's column sum is the snow on the table plus the shaded room
    under the cap, so no record isolates the table's top.
  - P1 is the run's whole pace: run.json's `timesRealTime`, or the report footer's `(Nx real
    time)` when the manifest lacks it. A run whose manifest still reads `running`, or a read
    at a second, is absent on the clause and prints the pace over the last --window seconds
    from `wallTotalMs` as a reading.

A context row per seed prints the crowd, the births and the gestation births, the founders by
source, the config's build markers and the header's round 48 tokens.

Run from anywhere:

    python scripts/reads/r48-read.py                               # r48-s1 r48-s2 r48-s3
    python scripts/reads/r48-read.py 10000 r48-s2                  # the watch's call form
    python scripts/reads/r48-read.py --runs-root scratch/r48-build/runs --arms r48smoke
    python scripts/reads/r48-read.py --out logbook/specs/r48-read/clauses.tsv

The `<second> <arm>` form reads every record up to that second and no further, so a clause
asked at 30,000 s is read at the second given and marked short; it is the state at that mark
and not a verdict. A read takes the newest run directory of an arm. Round 47's runs are found
under --baseline-runs-root (runs/ by default). Touches nothing but reads; --out creates only
the directory of the path given, and only then.
"""
import argparse
import bisect
import glob
import json
import math
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

sys.path.insert(0, HERE)
from contact_aliases import field as _aliased_field  # noqa: E402  (path set above)

DEFAULT_ARMS = ["r48-s1", "r48-s2", "r48-s3"]

END_SECONDS = 30000.0
M1_BUDS = 50                       # M1: at least 50 bud rows
M2_SHARE = 0.5                     # M2: budx >= 1 on at least half
M3_LIVING = 10                     # M3: ten or more living in a bud-rooted clade
G2_WINDOW = 5000.0                 # G2: the two windows, s
O2_CEILING = 25000                 # O2b: the runaway ceiling (config's maximumPopulation)
S2_AGE = 600.0                     # S2: median age at death of stomach children above 600 s
L1_AGE = 300.0                     # F1: median age at death above 300 s
L3_LIVING = 10                     # F3: ten or more living bodies rooted in pool founders
F4_TOP = 12.0                      # F4's reading: the depth draw's band, m
L5_FROM = 10000.0                  # R1: at 10,000 s and after
L6_BAND = 10.0                     # R1b: the depth band under the underside, m (L6's)
L6_STRIDE = 500.0                  # R1b: the positions rows read, every this many seconds
L9_AUDIT = 0.1                     # B1: audit under 0.1 J
L9_MATTER = 1e-5                   # B1: matter residual under 1e-5 units
P1_PACE = 1.0                      # P1: at or above 1x real time
WINDOW = 2000.0                    # P1's window reading, s
SHELF = 12.0                       # round 46's shelf: floor within 12 m of the surface

# ---------------------------------------------------------------------- the one name dictionary
NAMES = {
    # stats.jsonl (main's Row.cs). States unless marked cumulative.
    "stats_t": "t",
    "stats_alive": "alive",
    "stats_births": "births",                        # cumulative
    "stats_gest_births": "gestationBirths",          # cumulative, D120
    "stats_gest_joules": "gestatedJoules",           # cumulative, D120
    "stats_gest_held": "gestationJoulesHeld",        # the living's accounts now, D120
    "stats_invest": "meanBirthInvestment",
    "stats_age": "meanAge",
    "stats_stillb": "stillbirths",                   # cumulative
    "stats_self_stillb": "selfOverlapStillbirths",   # cumulative
    "stats_upt_lim": "uptakeLimitedShare",
    "stats_trickle": "trickleSpawns",                # cumulative
    "stats_pool": "poolSpawns",                      # cumulative
    "stats_audit": "auditResidual",                  # joules
    "stats_matter_resid": "matterResidual",          # units
    "stats_diverged": "diverged",                    # cumulative count
    "stats_wall_total": "wallTotalMs",               # cumulative

    # config.json (found by name anywhere in the tree).
    "config_shape": "worldShape",
    "config_area": "worldAreaSquareMetres",
    "config_reef_group": "reef",
    "config_reef_cover": "reefCover",
    "config_reef_cap_t": "reefCapThicknessMetres",
    "config_reef_fade": "reefFadeMetres",
    "config_reef_first_build": "reefCount",
    "config_pool_count": "foundingTricklePoolCount",
    "config_pool_share": "foundingTricklePoolShare",
    "config_floor_closes": "floorClosesAfterSeconds",
    "config_max_pop": "maximumPopulation",
    "config_marker_bud": "floorsWeighRigidGroups",           # D119's build
    "config_marker_gestation": "gestationModeChance",        # D120's build
    "config_marker_endow": "founderEndowmentSeconds",        # D122's build
    "config_marker_depth": "foundersFollowFoodDepth",        # D122's build
    "config_marker_senescence": "senescenceWearsIntake",     # D121's build

    # run.json (Manifest.cs).
    "manifest_reefs": "reefs",
    "manifest_reef_keys": ("x", "z", "r", "depth", "stem", "a2", "a3", "a4", "p2", "p3", "p4"),
    "manifest_reef_cover": "reefCover", "manifest_reef_cover_got": "reefCoverGot",
    "manifest_status": "status",
    "manifest_reason": "reason",
    "manifest_pace": "timesRealTime",
    "manifest_seed": "seed",

    # lineage.jsonl (LineageEvent.ToJson). A founder's `k` is "f" and its `p` -1; `src` is
    # "floor", "trickle" or "pool", and a pool founder's row alone carries `pool`.
    "lineage_event": "e", "lineage_birth": "b", "lineage_death": "d",
    "lineage_time": "t", "lineage_id": "id", "lineage_parent": "p", "lineage_kind": "k",
    "lineage_founder_kind": "f", "lineage_src": "src", "lineage_pool_src": "pool",
    "lineage_pool_index": "pool",
    "lineage_ink": "ink", "lineage_abs": "abs", "lineage_pho": "pho",
    "lineage_bud": "bud", "lineage_budx": "budx",            # D119, on a budded birth only
    "lineage_gm": "gm", "lineage_gs": "gs",                  # D120, on every birth row
    "lineage_endow": "endow",                                # D122, on a founder row above 0
    "bud_absorptive": "absorptive", "bud_join": "+",

    # positions.jsonl (PositionsRow.Write): {"t":..,"n":..,"b":[[id, x, y, z, flags], ...]};
    # flags: AbsorptiveBit 1, JointedBit 2, PhotosyntheticBit 4.
    # absorptive.jsonl (AbsorptiveSample.cs's row): one row per absorptive body per sample and
    # a final row at its death ("dead": true); densityHere is the snow density the body was fed
    # at, J/m3, already share-multiplied. A truncation marker row carries no id.
    "absorptive_t": "t", "absorptive_id": "id", "absorptive_age": "age",
    "absorptive_density": "densityHere", "absorptive_dead": "dead", "absorptive_y": "y",

    "positions_bodies": "b", "flag_abs": 1, "flag_jnt": 2, "flag_pho": 4,

    # fields/ (Row.cs DumpFields).
    "fields_layout": "layout.json", "fields_bed": "bed.f32", "fields_snow": "snow-columns",
    "fields_snow_floor": "snow-floor",

    "diverged_dir": "diverged",

    # The report: the header line starts "engine="; the footer's pace "(2.2x real time)".
    "header_prefix": "engine=",
    "footer_pace": r"\(([\d.]+)x real time\)",
}

_MISSING = object()


def sfield(row, name):
    return _aliased_field(row, name, default=_MISSING)


def num(row, name):
    """A stats value as a number, or None when missing or null."""
    v = sfield(row, name)
    if v is _MISSING or v is None:
        return None
    return v


def find_field(node, name):
    if isinstance(node, dict):
        if name in node:
            return node[name]
        for value in node.values():
            found = find_field(value, name)
            if found is not None:
                return found
    return None


# ---------------------------------------------------------------------- reading a run directory

def run_dir(runs_root, arm):
    base = os.path.join(runs_root, arm)
    if not os.path.isdir(base):
        raise SystemExit("no run directory for arm %r under %r" % (arm, runs_root))
    dirs = [d for d in sorted(os.listdir(base)) if os.path.isdir(os.path.join(base, d))]
    if not dirs:
        raise SystemExit("no run directory for arm %r under %r" % (arm, runs_root))
    if len(dirs) > 1:
        print("%s: %d run directories, reading the newest %r" % (arm, len(dirs), dirs[-1]),
              file=sys.stderr)
    return os.path.join(base, dirs[-1])


def stream_jsonl(path):
    """Rows one at a time. A half-written last line of a live file is dropped; a bad line
    anywhere else raises."""
    if not os.path.exists(path):
        return
    with open(path, encoding="utf-8") as f:
        pending = None
        for line in f:
            line = line.strip()
            if not line:
                continue
            if pending is not None:
                yield json.loads(pending)
            pending = line
        if pending is not None:
            try:
                yield json.loads(pending)
            except json.JSONDecodeError:
                return


def load_json(path):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def read_floats(path):
    with open(path, "rb") as f:
        data = f.read()
    count = len(data) // 4
    return struct.unpack("<%df" % count, data[:count * 4])


def report_lines(runs_root, arm):
    path = os.path.join(runs_root, arm + ".md")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return f.read().splitlines()


def sample_at(ts, target):
    """The last sample at or before target: (t, short)."""
    earlier = [t for t in ts if t <= target]
    if not earlier:
        return None, True
    t = earlier[-1]
    return t, t < target


def median(xs):
    xs = sorted(xs)
    n = len(xs)
    if n == 0:
        return None
    if n % 2:
        return xs[n // 2]
    return 0.5 * (xs[n // 2 - 1] + xs[n // 2])


# ---------------------------------------------------------------------- the reefs

def interval_union(intervals):
    """The union of closed intervals (lo, hi) as a sorted list of disjoint ones."""
    out = []
    for lo, hi in sorted(i for i in intervals if i[1] > i[0]):
        if out and lo <= out[-1][1]:
            if hi > out[-1][1]:
                out[-1] = (out[-1][0], hi)
        else:
            out.append((lo, hi))
    return out


def interval_clip(intervals, lo, hi):
    return [(max(a, lo), min(b, hi)) for a, b in intervals if min(b, hi) > max(a, lo)]


def interval_minus(intervals, cuts):
    """intervals less the union of cuts; both lists of (lo, hi)."""
    out = []
    cuts = interval_union(cuts)
    for a, b in intervals:
        pieces = [(a, b)]
        for c, d in cuts:
            nxt = []
            for p, q in pieces:
                if d <= p or c >= q:
                    nxt.append((p, q))
                    continue
                if c > p:
                    nxt.append((p, c))
                if d < q:
                    nxt.append((d, q))
            pieces = nxt
        out.extend(pieces)
    return out


def interval_length(intervals):
    return sum(b - a for a, b in intervals)


def interval_contains(intervals, y):
    """Half-open [lo, hi), as the first build's band test was."""
    return any(lo <= y < hi for lo, hi in intervals)


class Reefs:
    """ReefGeometry's distance, ported from main's ReefGeometry (D118, 9a467ce): OfReef, Cap,
    RoundCap, Stem and SmoothUnion for the value only, not their derivatives; InsideOutline,
    OutlineRadius and LightTransmission as they are. One record per reef from run.json."""

    def __init__(self, recs, cap_t, fade, cover=None, cover_got=None):
        self.recs = [dict(r) for r in recs]
        self.count = len(self.recs)
        self.cap_t = cap_t
        self.fade = fade
        self.cover = cover
        self.cover_got = cover_got
        self.half_t = 0.5 * cap_t
        self.fillet = self.half_t
        self.xs = [r["x"] for r in self.recs]
        self.zs = [r["z"] for r in self.recs]
        self.mid_y = []
        self.round = []
        self.outer_max = []
        for r in self.recs:
            self.mid_y.append(-(r["depth"] + self.half_t))
            is_round = r["a2"] == 0.0 and r["a3"] == 0.0 and r["a4"] == 0.0
            self.round.append(is_round)
            if is_round:
                self.outer_max.append(r["r"])
            else:
                # The constructor's sampling: 2,048 angles, a margin of 1e-3 of r0.
                om = 0.0
                for s in range(2048):
                    om = max(om, self._outline(r, 2.0 * math.pi * s / 2048.0)[0])
                self.outer_max.append(om + 1e-3 * r["r"])

    # ---- the outline
    @staticmethod
    def _outline(r, theta):
        """ReefGeometry.Outline's o and o1 (its first derivative in the angle)."""
        c2, s2 = math.cos(2.0 * theta + r["p2"]), math.sin(2.0 * theta + r["p2"])
        c3, s3 = math.cos(3.0 * theta + r["p3"]), math.sin(3.0 * theta + r["p3"])
        c4, s4 = math.cos(4.0 * theta + r["p4"]), math.sin(4.0 * theta + r["p4"])
        o = r["r"] * (1.0 + r["a2"] * c2 + r["a3"] * c3 + r["a4"] * c4)
        o1 = -r["r"] * (2.0 * r["a2"] * s2 + 3.0 * r["a3"] * s3 + 4.0 * r["a4"] * s4)
        return o, o1

    def outline_radius(self, i, theta):
        """ReefGeometry.OutlineRadius: r(theta), theta from +x toward +z."""
        return self._outline(self.recs[i], theta)[0]

    def inside_outline(self, i, x, z, scale=1.0, plus=0.0):
        """ReefGeometry.InsideOutline at scale 1 and plus 0; otherwise inside the outline
        scaled about the axis by `scale` and pushed out by `plus` metres: rho <= scale*r(theta)
        + plus."""
        r = self.recs[i]
        dx = x - r["x"]
        dz = z - r["z"]
        rho2 = dx * dx + dz * dz
        if self.round[i]:
            lim = scale * r["r"] + plus
            return rho2 <= lim * lim
        outer = scale * self.outer_max[i] + plus
        if rho2 > outer * outer:
            return False
        if rho2 <= 0.0:
            return True
        lim = scale * self._outline(r, math.atan2(dz, dx))[0] + plus
        return rho2 <= lim * lim

    def inside_any_outline(self, x, z, scale=1.0, plus=0.0):
        return [i for i in range(self.count) if self.inside_outline(i, x, z, scale, plus)]

    def cap_radius(self, i):
        return self.recs[i]["r"]

    def cap_top_y(self, i):
        return -self.recs[i]["depth"]

    def cap_underside_y(self, i):
        return -(self.recs[i]["depth"] + self.cap_t)

    def stem_radius(self, i):
        return self.recs[i]["stem"]

    def is_island(self, i):
        return not (self.recs[i]["stem"] > 0.0)

    # ---- the distance
    def _cap_q(self, i, x, z):
        """The cap's q at a horizontal place (None on the axis of a lobed reef, where Cap
        takes the flat blob), and rho."""
        r = self.recs[i]
        dx = x - r["x"]
        dz = z - r["z"]
        rho = math.sqrt(dx * dx + dz * dz)
        if self.round[i]:
            return rho - (r["r"] - self.half_t), rho        # RoundCap's q
        if not rho > 0.0:
            return None, rho
        o, o1 = self._outline(r, math.atan2(dz, dx))
        c = o / math.sqrt(o * o + o1 * o1)
        return (rho - o) * c + self.half_t, rho

    def _cap(self, i, x, y, z):
        q, rho = self._cap_q(i, x, z)
        v = y - self.mid_y[i]
        if q is not None and q > 0.0:
            return math.sqrt(q * q + v * v) - self.half_t
        return abs(v) - self.half_t

    def _stem(self, i, rho, y):
        er = rho - self.recs[i]["stem"]
        ey = y - self.mid_y[i]
        if er > 0.0 and ey > 0.0:
            return math.sqrt(er * er + ey * ey)
        if er > 0.0 or (ey <= 0.0 and er > ey):
            return er
        return ey

    def of_reef(self, i, x, y, z):
        cap = self._cap(i, x, y, z)
        if self.is_island(i):
            return cap
        rho = math.hypot(x - self.xs[i], z - self.zs[i])
        stem = self._stem(i, rho, y)
        k = self.fillet
        gap = cap - stem
        spread = abs(gap)
        if spread >= k:
            return cap if gap <= 0.0 else stem
        h = (k - spread) / k
        return min(cap, stem) - k * h * h * h / 6.0

    def signed_distance(self, x, y, z, reefs=None):
        """The minimum over the reefs (over `reefs` when given)."""
        idx = range(self.count) if reefs is None else reefs
        return min(self.of_reef(i, x, y, z) for i in idx)

    def near(self, x, z, margin):
        """The reefs whose widest outline, plus margin, reaches the place horizontally; a reef
        outside it has a positive distance there (the cap's is at least c_min (rho - r_max) and
        the stem stands inside the outline)."""
        out = []
        for i in range(self.count):
            dx = x - self.xs[i]
            dz = z - self.zs[i]
            m = self.outer_max[i] + margin
            if dx * dx + dz * dz < m * m:
                out.append(i)
        return out

    # ---- the light
    def under_cap_light(self, x, y, z):
        """ReefGeometry.LightTransmission's test: inside some cap's outline and below that
        cap's own top."""
        for i in range(self.count):
            if not (y < -self.recs[i]["depth"]):
                continue
            if self.inside_outline(i, x, z):
                return True
        return False

    # ---- a column's rock
    def rock_intervals(self, x, z):
        """The rock on the vertical line at (x, z): per reef, the cap's extent where the cap's
        distance is negative (|v| < t/2 over the flat blob, |v| < sqrt((t/2)^2 - q^2) on the
        rim) and the stem's, from below any floor to the cap's mid-plane, where the line is
        inside the stem. The smooth join's fillet is left out."""
        out = []
        ht = self.half_t
        for i in range(self.count):
            q, rho = self._cap_q(i, x, z)
            if q is None or q <= 0.0:
                h = ht
            elif q < ht:
                h = math.sqrt(ht * ht - q * q)
            else:
                h = 0.0
            if h > 0.0:
                out.append((self.mid_y[i] - h, self.mid_y[i] + h))
            if not self.is_island(i) and rho < self.recs[i]["stem"]:
                out.append((-float("inf"), self.mid_y[i]))
        return interval_union(out)

    def rock_in_column(self, x, z, floor_y):
        return interval_length(interval_clip(self.rock_intervals(x, z), floor_y, 0.0))


def read_reefs(config, manifest):
    """(Reefs, None) or (None, the reason there is none)."""
    N = NAMES
    group = config.get(N["config_reef_group"]) if isinstance(config, dict) else None
    if not isinstance(group, dict):
        return None, "config.json has no `reef` group (a run before the reef build)"
    if N["config_reef_first_build"] in group:
        return None, ("a first-build reef config (`reefCount` in the reef group: the round "
                      "reefs before D118's rebuild, which this reader does not read)")
    cover = group.get(N["config_reef_cover"])
    if cover is None:
        return None, "the reef group has no `reefCover`"
    if not cover > 0:
        return None, "the config's reefCover is 0 (no reef in this world)"
    missing = [N[k] for k in ("config_reef_cap_t", "config_reef_fade") if group.get(N[k]) is None]
    if missing:
        return None, "the reef group lacks %s" % ", ".join(missing)
    listed = (manifest or {}).get(N["manifest_reefs"])
    if not isinstance(listed, list) or not listed:
        return None, ("the config asks for a reef cover of %g and run.json carries no `reefs` "
                      "list" % cover)
    recs = []
    for k, r in enumerate(listed):
        lack = [key for key in N["manifest_reef_keys"] if not isinstance(r, dict) or r.get(key) is None]
        if lack:
            return None, "run.json's reef %d lacks %s" % (k, ", ".join(lack))
        recs.append({key: float(r[key]) for key in N["manifest_reef_keys"]})
    m = manifest or {}
    return Reefs(recs, float(group[N["config_reef_cap_t"]]), float(group[N["config_reef_fade"]]),
                 m.get(N["manifest_reef_cover"], cover), m.get(N["manifest_reef_cover_got"])), None


def pool_named(config):
    """(count, share) or (None, reason)."""
    count = find_field(config, NAMES["config_pool_count"])
    if count is None:
        return None, ("config.json names no pool (`foundingTricklePoolCount` absent: a run "
                      "before D117's build)")
    if not count:
        return None, "the config's foundingTricklePoolCount is 0 (no pool in this world)"
    return (count, find_field(config, NAMES["config_pool_share"])), None


# ---------------------------------------------------------------------- the lineage, whole

def read_lineage(d, cutoff):
    """Every birth and death up to cutoff, held whole (a round's lineage is a few to tens of
    MB). The round 48 fields are kept where a row carries them; `has_*` says whether any row
    did."""
    path = os.path.join(d, "lineage.jsonl")
    N = NAMES
    L = dict(present=os.path.exists(path), birth={}, parent={}, kind={}, src={}, pool={},
             ink={}, absf={}, pho={}, bud={}, budx={}, gm={}, gs={}, endow={}, died={},
             children={}, has_bud=False, has_gm=False, has_endow=False)
    if not L["present"]:
        L["root"] = lambda i: i
        L["pool_founders"] = []
        return L
    for row in stream_jsonl(path):
        t = row.get(N["lineage_time"], 0.0)
        if t > cutoff:
            continue
        e = row.get(N["lineage_event"])
        i = row.get(N["lineage_id"])
        if e == N["lineage_birth"]:
            p = row.get(N["lineage_parent"], -1)
            L["birth"][i] = t
            L["parent"][i] = p
            k = row.get(N["lineage_kind"])
            L["kind"][i] = k
            L["ink"][i] = row.get(N["lineage_ink"], 0)
            L["absf"][i] = row.get(N["lineage_abs"], 0)
            L["pho"][i] = row.get(N["lineage_pho"], 0)
            if N["lineage_bud"] in row:
                L["has_bud"] = True
                L["bud"][i] = row[N["lineage_bud"]]
                L["budx"][i] = row.get(N["lineage_budx"])
            if N["lineage_gm"] in row:
                L["has_gm"] = True
                L["gm"][i] = row[N["lineage_gm"]]
                L["gs"][i] = row.get(N["lineage_gs"])
            if N["lineage_endow"] in row:
                L["has_endow"] = True
                L["endow"][i] = row[N["lineage_endow"]]
            if k == N["lineage_founder_kind"] or p is None or p < 0:
                L["src"][i] = row.get(N["lineage_src"])
                if N["lineage_pool_index"] in row:
                    L["pool"][i] = row[N["lineage_pool_index"]]
            else:
                L["children"][p] = L["children"].get(p, 0) + 1
        elif e == N["lineage_death"]:
            L["died"][i] = t
    cache = {}

    def root(i):
        path_ = []
        parent = L["parent"]
        while i not in cache and parent.get(i, -1) not in (-1, None):
            path_.append(i)
            i = parent[i]
        r = cache.get(i, i)
        for p in path_:
            cache[p] = r
        return r

    L["root"] = root
    L["pool_founders"] = sorted(i for i, s in L["src"].items() if s == N["lineage_pool_src"])
    return L


def is_stomach(L, i):
    return bool(L["ink"].get(i)) or (bool(L["absf"].get(i)) and not L["pho"].get(i))


def is_founder(L, i):
    return L["kind"].get(i) == NAMES["lineage_founder_kind"] or L["parent"].get(i, -1) in (-1, None)


def living_ids(L, pos):
    """(ids, source): the positions row at the second read, or the lineage's born-and-not-dead."""
    if pos.get("present") and pos.get("last_line") is not None:
        row = json.loads(pos["last_line"])
        return [b[0] for b in row.get(NAMES["positions_bodies"], [])], "positions row at %g s" % pos["last_t"]
    if L["present"]:
        return [i for i in L["birth"] if i not in L["died"]], "lineage (born, not dead)"
    return None, None


def field_dumps(d, cutoff, kind):
    dumps = {}
    for p in glob.glob(os.path.join(d, "fields", "*.%s.f32" % kind)):
        m = re.match(r"^(\d{9})\.", os.path.basename(p))
        if m and int(m.group(1)) <= cutoff:
            dumps[int(m.group(1))] = p
    return dumps


# ---------------------------------------------------------------------- the fields

def read_bed(d, config):
    """The bed's grid: floor, live mask and shelf mask per 1 m column, or None."""
    fdir = os.path.join(d, "fields")
    layout = load_json(os.path.join(fdir, NAMES["fields_layout"]))
    bed_path = os.path.join(fdir, NAMES["fields_bed"])
    if not layout or "bed" not in layout or not os.path.exists(bed_path):
        return None
    bed = layout["bed"]
    nx, nz, cell = bed["cellsX"], bed["cellsZ"], float(bed["cellMetres"])
    floor = read_floats(bed_path)
    if len(floor) != nx * nz:
        return None
    radius = None
    if str(find_field(config, NAMES["config_shape"]) or "").lower() == "tank":
        radius = math.sqrt(float(find_field(config, NAMES["config_area"])) / math.pi)
    live, shelf = [], []
    for ix in range(nx):
        for iz in range(nz):
            ok = floor[ix * nz + iz] < 0.0
            if radius is not None:
                ok = ok and (((ix + 0.5) * cell - radius) ** 2 +
                             ((iz + 0.5) * cell - radius) ** 2 <= radius * radius)
            live.append(ok)
            shelf.append(ok and floor[ix * nz + iz] >= -SHELF)
    return dict(nx=nx, nz=nz, cell=cell, floor=floor, live=live, shelf=shelf,
                snow_layout=layout.get("snowColumns"), radius=radius)


def snow_dumps(d, cutoff):
    dumps = {}
    for p in glob.glob(os.path.join(d, "fields", "*.%s.f32" % NAMES["fields_snow"])):
        m = re.match(r"^(\d{9})\.", os.path.basename(p))
        if m and int(m.group(1)) <= cutoff:
            dumps[int(m.group(1))] = p
    return dumps


def column_water(bed, reefs, ix, iz):
    """A column's water in metres: its depth from the bed less any reef's rock in it."""
    floor_y = bed["floor"][ix * bed["nz"] + iz]
    if reefs is None:
        return -floor_y
    c = bed["cell"]
    return -floor_y - reefs.rock_in_column((ix + 0.5) * c, (iz + 0.5) * c, floor_y)


# ---------------------------------------------------------------------- positions, one pass

def positions_pass(d, cutoff, read_t, reefs, bed, want_first, stride, band):
    """One pass over positions.jsonl. Collects the row at the read second (M3, F3), the R1b
    samples and each wanted id's first place at or after its birth (F4). A row is parsed only
    when one of those needs it."""
    path = os.path.join(d, "positions.jsonl")
    out = dict(present=os.path.exists(path), last_line=None, last_t=None, l6=[], first={})
    if not out["present"]:
        return out
    pending = dict(want_first)         # id -> birth t
    earliest = min(pending.values()) if pending else None
    N = NAMES
    l6_geom = l6_geometry(reefs, bed, band) if (reefs is not None and bed is not None) else None
    if l6_geom is not None:
        out["l6_volumes"] = (l6_geom["v_under"], l6_geom["v_beside"])
    with open(path, encoding="utf-8") as f:
        for line in f:
            if not line.startswith('{"t":'):
                continue
            comma = line.find(",", 5)
            try:
                t = float(line[5:comma])
            except ValueError:
                continue
            if t > cutoff:
                break
            if t <= read_t:
                out["last_line"] = line
                out["last_t"] = t
            need_l6 = (l6_geom is not None and t >= L5_FROM and t <= read_t
                       and abs(t / stride - round(t / stride)) < 1e-9)
            need_first = bool(pending) and t >= earliest
            if not (need_l6 or need_first):
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                break                  # a half-written last line
            bodies = row.get(N["positions_bodies"], [])
            if need_first:
                for b in bodies:
                    bt = pending.get(b[0])
                    if bt is not None and t >= bt:
                        out["first"][b[0]] = (t, b[1], b[2], b[3])
                        del pending[b[0]]
                earliest = min(pending.values()) if pending else None
            if need_l6:
                out["l6"].append(l6_count(t, bodies, reefs, bed, l6_geom))
    if out["last_line"] is not None:
        try:
            json.loads(out["last_line"])
        except json.JSONDecodeError:
            out["last_line"] = None    # the read second's row was the half-written last line
    return out


def l6_region(reefs, x, z, band):
    """('under' or 'beside' or None, the band's intervals at the place). Under is inside any
    cap's outline; beside is inside no outline and inside some reef's outline scaled by two.
    The band is the union, over those reefs, of each cap's underside down `band` metres."""
    idx = reefs.inside_any_outline(x, z)
    region = "under"
    if not idx:
        idx = reefs.inside_any_outline(x, z, scale=2.0)
        region = "beside"
    if not idx:
        return None, []
    bands = interval_union([(reefs.cap_underside_y(i) - band, reefs.cap_underside_y(i))
                            for i in idx])
    return region, bands


def l6_geometry(reefs, bed, band):
    """The under and beside regions' water volumes in the band, summed over the bed's columns
    at their centres: each column's band less the rock in it, never under the floor."""
    c = bed["cell"]
    v_under = v_beside = 0.0
    for ix in range(bed["nx"]):
        for iz in range(bed["nz"]):
            k = ix * bed["nz"] + iz
            if not bed["live"][k]:
                continue
            x, z = (ix + 0.5) * c, (iz + 0.5) * c
            region, bands = l6_region(reefs, x, z, band)
            if region is None:
                continue
            water = interval_minus(interval_clip(bands, bed["floor"][k], 0.0),
                                   reefs.rock_intervals(x, z))
            h = interval_length(water) * c * c
            if region == "under":
                v_under += h
            else:
                v_beside += h
    undersides = [reefs.cap_underside_y(i) for i in range(reefs.count)]
    return dict(band=band, v_under=v_under, v_beside=v_beside,
                shallowest_underside=max(undersides), deepest_underside=min(undersides))


def l6_count(t, bodies, reefs, bed, g):
    n_under = n_beside = 0
    top = g["shallowest_underside"]
    bottom = g["deepest_underside"] - g["band"]
    for b in bodies:
        x, y, z = b[1], b[2], b[3]
        if not (bottom <= y < top):
            continue                   # outside every band; saves the outline tests
        region, bands = l6_region(reefs, x, z, g["band"])
        if region is None or not interval_contains(bands, y):
            continue
        if region == "under":
            n_under += 1
        else:
            n_beside += 1
    du = n_under / g["v_under"] if g["v_under"] > 0 else None
    db = n_beside / g["v_beside"] if g["v_beside"] > 0 else None
    return (t, n_under, n_beside, du, db)



# ---------------------------------------------------------------------- round 47's L5 and L6, as they were

def l5(seed, reefs, reef_reason, bed, dumps):
    if reef_reason:
        return absent("L5", seed, reef_reason)
    if bed is None:
        return absent("L5", seed, "no fields/bed.f32 or layout.json")
    sl = bed["snow_layout"]
    if not sl or sl["cellsX"] != bed["nx"] or sl["cellsZ"] != bed["nz"]:
        return absent("L5", seed, "the snow's columns and the bed's do not share a layout")
    c = bed["cell"]
    tables, opens = [], []
    for ix in range(bed["nx"]):
        for iz in range(bed["nz"]):
            k = ix * bed["nz"] + iz
            if not bed["live"][k]:
                continue
            x, z = (ix + 0.5) * c, (iz + 0.5) * c
            w = column_water(bed, reefs, ix, iz)
            if not w > 0:
                continue
            if reefs.inside_any_outline(x, z):
                tables.append((k, w))
            elif not bed["shelf"][k] and not reefs.inside_any_outline(x, z, 2.0, reefs.fade):
                # Open: outside every outline scaled by two plus the fade, i.e. a cap radius
                # beyond the rim (the outline scaled as the radius was) and the fade past it.
                opens.append((k, w))
    if not tables or not opens:
        return absent("L5", seed, "no table columns or no open-floor columns on the grid",
                      tables=len(tables), open=len(opens))
    seconds = sorted(s for s in dumps if s >= L5_FROM)
    if not seconds:
        return absent("L5", seed, "no snow dump at or after 10,000 s yet",
                      tables=len(tables), open=len(opens))
    area = c * c
    held_m3 = held_m2 = 0
    ratios = []
    last = None
    for s in seconds:
        snow = read_floats(dumps[s])
        tm3 = sum(snow[k] / (w * area) for k, w in tables) / len(tables)
        om3 = sum(snow[k] / (w * area) for k, w in opens) / len(opens)
        tm2 = sum(snow[k] / area for k, _ in tables) / len(tables)
        om2 = sum(snow[k] / area for k, _ in opens) / len(opens)
        held_m3 += tm3 > om3
        held_m2 += tm2 > om2
        ratios.append(tm3 / om3 if om3 > 0 else float("inf"))
        last = (s, tm3, om3, tm2, om2)
    return dict(clause="L5", seed=seed, table_columns=len(tables), open_columns=len(opens),
                dumps=len(seconds), dumps_held_J_m3=held_m3, dumps_held_J_m2=held_m2,
                min_ratio_J_m3=min(ratios), last_at=last[0], table_J_m3=last[1],
                open_J_m3=last[2], table_J_m2=last[3], open_J_m2=last[4],
                note="a table column's sum is the snow on the table plus the room under the "
                     "cap down to the floor; no record isolates the table's top. J/m3 is over "
                     "the column's water (rock out), J/m2 over its area; held on J/m3",
                held=held_m3 == len(seconds))


def l6(seed, reefs, reef_reason, bed, pos, band):
    if reef_reason:
        return absent("L6", seed, reef_reason)
    if bed is None:
        return absent("L6", seed, "no fields/bed.f32, so no volumes")
    if not pos["present"]:
        return absent("L6", seed, "no positions.jsonl")
    rows = pos["l6"]
    if not rows:
        return absent("L6", seed, "no positions sample at or after 10,000 s yet")
    vu, vb = pos.get("l6_volumes", (None, None))
    held = sum(1 for r in rows if r[3] is not None and r[4] is not None and r[3] < r[4])
    ties = sum(1 for r in rows if r[3] is not None and r[3] == r[4])
    last = rows[-1]
    undersides = [reefs.cap_underside_y(i) for i in range(reefs.count)]
    return dict(clause="L6", seed=seed,
                band="each cap's underside down %g m (undersides %.3g to %.3g m)"
                     % (band, max(undersides), min(undersides)),
                volume_under_m3=vu, volume_beside_m3=vb, samples=len(rows),
                samples_fewer_under=held, ties=ties,
                last_at=last[0], under=last[1], beside=last[2], under_per_m3=last[3],
                beside_per_m3=last[4],
                mean_under_per_m3=sum(r[3] or 0 for r in rows) / len(rows),
                mean_beside_per_m3=sum(r[4] or 0 for r in rows) / len(rows),
                held=held == len(rows))


def window_stats(ts, by_t, t_end, width, name):
    """(t_from, t_to, mean alive, mean of `name` over the window, pace x real time). The window
    runs from the last sample at or before t_end - width (the first sample, early in a run) to
    t_end, and its means are over the samples after its start."""
    j = bisect.bisect_right(ts, t_end)            # samples at or before t_end are ts[:j]
    if j < 2:
        return None
    k = bisect.bisect_right(ts, t_end - width) - 1
    k = max(0, min(k, j - 2))
    t_from = ts[k]
    inside = ts[k + 1:j]
    if not inside:
        return None
    alive = [num(by_t[t], NAMES["stats_alive"]) for t in inside]
    alive = [a for a in alive if a is not None]
    vals = None
    if name:
        vs = [num(by_t[t], name) for t in inside]
        vs = [v for v in vs if v is not None]
        vals = sum(vs) / len(vs) if vs else None
    w0 = num(by_t[t_from], NAMES["stats_wall_total"])
    w1 = num(by_t[t_end], NAMES["stats_wall_total"])
    pace = None
    if w0 is not None and w1 is not None and w1 > w0:
        pace = (t_end - t_from) / ((w1 - w0) / 1000.0)
    return (t_from, t_end, sum(alive) / len(alive) if alive else None, vals, pace)

# ---------------------------------------------------------------------- the clauses

def absent(clause, seed, note, **extra):
    r = dict(clause=clause, seed=seed, held="absent", note=note)
    r.update(extra)
    return r


def marker(config, key):
    """Whether config.json carries the build marker NAMES[key] (its value is not asked)."""
    return find_field_present(config, NAMES[key])


def find_field_present(node, name):
    if isinstance(node, dict):
        if name in node:
            return True
        return any(find_field_present(v, name) for v in node.values())
    return False


def pre_build(what, key):
    return "config.json lacks `%s` (a run before %s's build)" % (NAMES[key], what)


def last_stats(ts, by_t, cutoff):
    """(t, row, short) at the last sample at or before min(cutoff, 30,000 s)."""
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    return t, (by_t[t] if t is not None else None), short


# ---- M: the bud

def bud_cells(L, i):
    b = L["bud"].get(i)
    return [] if not b else str(b).split(NAMES["bud_join"])


def m1(seed, L, config):
    if not L["present"]:
        return absent("M1", seed, "no lineage.jsonl")
    if not L["has_bud"] and not marker(config, "config_marker_bud"):
        return absent("M1", seed, pre_build("D119", "config_marker_bud") +
                      ", and no birth row carries `bud`")
    rows = list(L["bud"])
    by_cell = {}
    for i in rows:
        for c in bud_cells(L, i):
            by_cell[c] = by_cell.get(c, 0) + 1
    children = sum(1 for i in L["birth"] if not is_founder(L, i))
    return dict(clause="M1", seed=seed, bud_rows=len(rows), children=children,
                per_child=(len(rows) / children if children else None),
                by_cell=" ".join("%s:%d" % kv for kv in sorted(by_cell.items())) or None,
                first_at=(min(L["birth"][i] for i in rows) if rows else None),
                held=len(rows) >= M1_BUDS)


def m2(seed, L, config, last):
    if not L["present"]:
        return absent("M2", seed, "no lineage.jsonl")
    if not L["has_bud"] and not marker(config, "config_marker_bud"):
        return absent("M2", seed, pre_build("D119", "config_marker_bud"))
    rows = list(L["bud"])
    stillb = num(last, NAMES["stats_stillb"]) if last else None
    self_stillb = num(last, NAMES["stats_self_stillb"]) if last else None
    if not rows:
        return absent("M2", seed, "no bud row, so the clause has no subject",
                      stillbirths=stillb, self_overlap_stillbirths=self_stillb)
    dist = {}
    for i in rows:
        x = L["budx"].get(i)
        dist[x] = dist.get(x, 0) + 1
    expressed = sum(1 for i in rows if (L["budx"].get(i) or 0) >= 1)
    share = expressed / len(rows)
    return dict(clause="M2", seed=seed, bud_rows=len(rows), expressed=expressed, share=share,
                budx=" ".join("%s:%d" % (k, v) for k, v in sorted(dist.items(), key=lambda kv: str(kv[0]))),
                stillbirths=stillb, self_overlap_stillbirths=self_stillb,
                held=share >= M2_SHARE)


def m3(seed, L, config, pos, read_t):
    if not L["present"]:
        return absent("M3", seed, "no lineage.jsonl")
    if not L["has_bud"] and not marker(config, "config_marker_bud"):
        return absent("M3", seed, pre_build("D119", "config_marker_bud"))
    absorptive_buds = [i for i in L["bud"] if NAMES["bud_absorptive"] in bud_cells(L, i)]
    roots = set(i for i in absorptive_buds if L["absf"].get(i) == 1 and L["pho"].get(i) == 1)
    living, source = living_ids(L, pos)
    if living is None:
        return absent("M3", seed, "no positions row and no lineage to name the living")
    if not roots:
        return dict(clause="M3", seed=seed, absorptive_bud_rows=len(absorptive_buds),
                    bud_roots=0, living=len(living), living_from=source,
                    note="no absorptive bud on a plant (abs 1 and pho 1)", held=False)
    parent = L["parent"]
    nearest = {}

    def nearest_root(i):
        """The nearest bud root at or above i, or None."""
        chain = []
        while i is not None and i not in nearest:
            if i in roots:
                nearest[i] = i
                break
            chain.append(i)
            p = parent.get(i, -1)
            i = p if p not in (-1, None) else None
        r = nearest.get(i) if i is not None else None
        for c in chain:
            nearest[c] = r
        return r

    counts = {}
    members = {}
    for i in living:
        r = nearest_root(i)
        while r is not None:
            counts[r] = counts.get(r, 0) + 1
            members.setdefault(r, []).append(i)
            p = parent.get(r, -1)
            r = nearest_root(p) if p not in (-1, None) else None
    best = max(counts, key=counts.get) if counts else None
    out = dict(clause="M3", seed=seed, absorptive_bud_rows=len(absorptive_buds),
               bud_roots=len(roots), roots_with_living=len(counts), living=len(living),
               living_from=source, short=(pos.get("last_t") or 0) < read_t if pos.get("present") else None)
    if best is not None:
        mem = members[best]
        out.update(best_root=best, best_root_born=L["birth"].get(best), best_living=counts[best],
                   best_stomachs=sum(1 for i in mem if L["absf"].get(i) == 1 and not L["pho"].get(i)),
                   best_mixotrophs=sum(1 for i in mem if L["absf"].get(i) == 1 and L["pho"].get(i) == 1))
    out["held"] = bool(counts) and counts[best] >= M3_LIVING
    return out


# ---- G: gestation

def g1(seed, L, config, ts, by_t, cutoff):
    t, row, short = last_stats(ts, by_t, cutoff)
    v = num(row, NAMES["stats_gest_births"]) if row else None
    if v is None:
        note = "gestationBirths missing at the sample"
        if not marker(config, "config_marker_gestation"):
            note = pre_build("D120", "config_marker_gestation") + "; " + note
        return absent("G1", seed, note, at=t)
    gm_children = sum(1 for i, g in L["gm"].items() if g == 1 and not is_founder(L, i))
    return dict(clause="G1", seed=seed, at=t, short=short, gestation_births=v,
                births=num(row, NAMES["stats_births"]),
                gestated_J=num(row, NAMES["stats_gest_joules"]),
                held_in_accounts_J=num(row, NAMES["stats_gest_held"]),
                gm1_child_rows=gm_children, held=v > 0)


def g2(seed, L, config, ts, cutoff):
    if not L["present"]:
        return absent("G2", seed, "no lineage.jsonl")
    if not L["has_gm"]:
        note = "no birth row carries `gm`"
        if not marker(config, "config_marker_gestation"):
            note = pre_build("D120", "config_marker_gestation") + "; " + note
        return absent("G2", seed, note)
    fc = find_field(config, NAMES["config_floor_closes"])
    fc = float(fc) if fc else 3000.0
    t_end, _ = sample_at(ts, min(cutoff, END_SECONDS))
    early = (fc, fc + G2_WINDOW)

    def share(lo, hi, children_only):
        ids = [i for i, t in L["birth"].items() if lo <= t < hi and i in L["gm"]
               and not (children_only and is_founder(L, i))]
        g = sum(1 for i in ids if L["gm"][i] == 1)
        return (g / len(ids) if ids else None), len(ids)

    e_share, e_n = share(early[0], early[1], True)
    if t_end is None or t_end < early[1]:
        return absent("G2", seed, "the read ends at %s s, before the early window's end at %g s"
                      % (t_end, early[1]), early_window="%g to %g s" % early,
                      early_share_so_far=e_share, early_children=e_n)
    late = (t_end - G2_WINDOW, t_end + 1e-9)
    l_share, l_n = share(late[0], late[1], True)
    e_all, _ = share(early[0], early[1], False)
    l_all, _ = share(late[0], late[1], False)
    # The strong reading: each mode's births (by the parent's mode) per death (by the dead
    # body's own mode) in the last window.
    births = {0: 0, 1: 0}
    deaths = {0: 0, 1: 0}
    for i, t in L["birth"].items():
        if late[0] <= t < late[1] and not is_founder(L, i):
            m = L["gm"].get(L["parent"].get(i))
            if m in births:
                births[m] += 1
    for i, t in L["died"].items():
        if late[0] <= t < late[1]:
            m = L["gm"].get(i)
            if m in deaths:
                deaths[m] += 1
    out = dict(clause="G2", seed=seed, early_window="%g to %g s" % early,
               late_window="%g to %g s" % (late[0], t_end), early_share=e_share,
               early_children=e_n, late_share=l_share, late_children=l_n,
               early_share_all_rows=e_all, late_share_all_rows=l_all,
               gestation_births_per_death=(births[1] / deaths[1] if deaths[1] else None),
               lump_births_per_death=(births[0] / deaths[0] if deaths[0] else None),
               gestation_births=births[1], gestation_deaths=deaths[1],
               lump_births=births[0], lump_deaths=deaths[0])
    if e_share is None or l_share is None:
        out["held"] = "absent"
        out["note"] = "no child born in one of the two windows"
    else:
        out["held"] = l_share >= e_share
    return out


# ---- O and S: against round 47's same seed

def base_value(base, name):
    """(t, value) at round 47's sample at or before 30,000 s."""
    if base is None:
        return None, None
    ts, by_t = base
    t, _ = sample_at(ts, END_SECONDS)
    if t is None:
        return None, None
    return t, num(by_t[t], name)


def compare(clause, seed, ts, by_t, cutoff, base, base_name, same_run, name, key, above):
    t, row, short = last_stats(ts, by_t, cutoff)
    v = num(row, name) if row else None
    if v is None:
        return absent(clause, seed, "%s missing at the sample" % name, at=t)
    if base is None:
        return absent(clause, seed, "no round 47 run for this seed (%s)" % base_name,
                      at=t, **{key: v})
    bt, bv = base_value(base, name)
    if bv is None:
        return absent(clause, seed, "round 47's run has no %s at 30,000 s" % name,
                      at=t, **{key: v}, baseline=base_name)
    out = dict(clause=clause, seed=seed, at=t, short=short)
    out[key] = v
    out.update(baseline=base_name, baseline_at=bt, baseline_value=bv,
               held=(v > bv) if above else (v < bv))
    if same_run:
        out["note"] = "the baseline is this run itself"
    return out


def o2b(seed, ts, by_t, config, manifest, cutoff):
    ceiling = find_field(config, NAMES["config_max_pop"]) or O2_CEILING
    vals = [(t, num(by_t[t], NAMES["stats_alive"])) for t in ts if t <= min(cutoff, END_SECONDS)]
    vals = [(t, a) for t, a in vals if a is not None]
    if not vals:
        return absent("O2b", seed, "no alive at any sample")
    tmax, amax = max(vals, key=lambda ta: ta[1])
    reason = (manifest or {}).get(NAMES["manifest_reason"])
    runaway = isinstance(reason, str) and reason.startswith("ceiling")
    return dict(clause="O2b", seed=seed, max_alive=amax, max_at=tmax, ceiling=ceiling,
                manifest_reason=reason, samples=len(vals),
                held=amax < ceiling and not runaway)


def s2(seed, L):
    if not L["present"]:
        return absent("S2", seed, "no lineage.jsonl")
    kids = [i for i in L["birth"] if not is_founder(L, i) and L["absf"].get(i) == 1
            and not L["pho"].get(i)]
    if not kids:
        return absent("S2", seed, "no stomach child (abs 1, pho 0, not a founder) born")
    ages = [L["died"][i] - L["birth"][i] for i in kids if i in L["died"]]
    m = median(ages)
    out = dict(clause="S2", seed=seed, stomach_children=len(kids), dead=len(ages),
               still_alive=len(kids) - len(ages), median_age_at_death_s=m,
               quartiles=(None if not ages else "%.0f to %.0f" % (
                   sorted(ages)[len(ages) // 4], sorted(ages)[(3 * len(ages)) // 4])))
    if m is None:
        out["held"] = "absent"
        out["note"] = "no stomach child has died yet"
    else:
        out["held"] = m > S2_AGE
    return out


# ---- F: the pool founders (round 47's L1 to L3, and F4)

def f1(seed, L, pool_reason):
    if pool_reason:
        return absent("F1", seed, pool_reason)
    if not L["present"]:
        return absent("F1", seed, "no lineage.jsonl")
    founders = L["pool_founders"]
    if not founders:
        return absent("F1", seed, "the config names a pool and no founder row reads src pool yet")
    ages = [L["died"][i] - L["birth"][i] for i in founders if i in L["died"]]
    alive = sum(1 for i in founders if i not in L["died"])
    m = median(ages)
    out = dict(clause="F1", seed=seed, pool_founders=len(founders), dead=len(ages),
               still_alive=alive, median_age_at_death_s=m,
               quartiles=(None if not ages else "%.0f to %.0f" % (
                   sorted(ages)[len(ages) // 4], sorted(ages)[(3 * len(ages)) // 4])))
    if m is None:
        out["held"] = "absent"
        out["note"] = "no pool founder has died yet"
    else:
        out["held"] = m > L1_AGE
    return out


def f2(seed, L, pool_reason):
    if pool_reason:
        return absent("F2", seed, pool_reason)
    founders = L["pool_founders"]
    if not founders:
        return absent("F2", seed, "no pool founder yet")
    bred = [i for i in founders if L["children"].get(i, 0) > 0]
    by_index = {}
    for i in bred:
        k = L["pool"].get(i)
        by_index[k] = by_index.get(k, 0) + 1
    return dict(clause="F2", seed=seed, pool_founders=len(founders), bred=len(bred),
                children=sum(L["children"].get(i, 0) for i in founders),
                bred_by_pool_index=(" ".join("%s:%d" % (k, v) for k, v in sorted(
                    by_index.items(), key=lambda kv: str(kv[0]))) or None),
                held=bool(bred))


def f3(seed, L, pool_reason, pos, read_t):
    if pool_reason:
        return absent("F3", seed, pool_reason)
    if not pos["present"] or pos["last_line"] is None:
        return absent("F3", seed, "no positions row at or before the second read")
    row = json.loads(pos["last_line"])
    ids = [b[0] for b in row.get(NAMES["positions_bodies"], [])]
    pf = set(L["pool_founders"])
    rooted = [i for i in ids if L["root"](i) in pf]
    by_index = {}
    for i in rooted:
        k = L["pool"].get(L["root"](i))
        by_index[k] = by_index.get(k, 0) + 1
    return dict(clause="F3", seed=seed, at=pos["last_t"], short=pos["last_t"] < read_t,
                living=len(ids), rooted_in_pool=len(rooted),
                of_them_stomachs=sum(1 for i in rooted if is_stomach(L, i)),
                by_pool_index=(" ".join("%s:%d" % kv for kv in sorted(by_index.items(),
                                                                        key=lambda kv: str(kv[0])))
                               or None),
                held=len(rooted) >= L3_LIVING)


F4_R47_MEDIAN = 0.12               # F4: round 47's founders' own first snow reading, J/m3


def first_absorptive(d, cutoff, births):
    """Each wanted id's first absorptive.jsonl row at or after its birth: id -> (t, age,
    densityHere, dead, y). A row without an id (the truncation marker) is skipped."""
    path = os.path.join(d, "absorptive.jsonl")
    out = {}
    if not os.path.exists(path):
        return None
    for row in stream_jsonl(path):
        i = row.get(NAMES["absorptive_id"])
        bt = births.get(i)
        if bt is None:
            continue
        t = row.get(NAMES["absorptive_t"])
        if t is None or t > cutoff or t < bt:
            continue
        v = row.get(NAMES["absorptive_density"])
        if v is None:
            continue
        if i not in out or t < out[i][0]:
            out[i] = (t, row.get(NAMES["absorptive_age"]), v, bool(row.get(NAMES["absorptive_dead"])),
                      row.get(NAMES["absorptive_y"]))
    return out


def f4(seed, L, pool_reason, pos, bed, reefs, config, snow, snow_floor, d, cutoff):
    """F4 as reworded at cf72687: each pool founder's first absorptive.jsonl reading of the
    snow at its body against its column's mean snow in the dump at or before its birth. The
    depth reading (the founder's first positions row) and round 47's L4 (the breeders' column
    snow at or over 1 J/m3) are printed beside it."""
    if pool_reason:
        return absent("F4", seed, pool_reason)
    founders = L["pool_founders"]
    if not founders:
        return absent("F4", seed, "no pool founder yet")
    births = {i: L["birth"][i] for i in founders}
    first = first_absorptive(d, cutoff, births)
    out = dict(clause="F4", seed=seed, pool_founders=len(founders),
               depth_rule=("on" if find_field(config, NAMES["config_marker_depth"]) else
                           "off" if marker(config, "config_marker_depth") else
                           "absent (a run before D122's build)"))
    if first is None:
        return absent("F4", seed, "no absorptive.jsonl", **out)
    if bed is None:
        return absent("F4", seed, "no fields/bed.f32 or layout.json", **out)
    sl = bed["snow_layout"]
    if not sl or sl["cellsX"] != bed["nx"] or sl["cellsZ"] != bed["nz"]:
        return absent("F4", seed, "the snow's columns and the bed's do not share a layout", **out)
    c = bed["cell"]
    cache = {}

    def dump_at(dumps, kind, t):
        earlier = [s for s in dumps if s <= t]
        if not earlier:
            return None
        s = max(earlier)
        if (kind, s) not in cache:
            cache[(kind, s)] = read_floats(dumps[s])
        return cache[(kind, s)]

    def column_of(i):
        """(ix, iz, k) of the founder's column, from its first positions row (the absorptive
        row carries y and no x or z)."""
        p = pos.get("first", {}).get(i) if pos.get("present") else None
        if p is None:
            return None
        ix = min(bed["nx"] - 1, max(0, int(p[1] // c)))
        iz = min(bed["nz"] - 1, max(0, int(p[3] // c)))
        return ix, iz, ix * bed["nz"] + iz

    reads, no_row, no_column, dead_first, samples = [], 0, 0, 0, []
    for i in founders:
        fr = first.get(i)
        if fr is None:
            no_row += 1
            continue
        col = column_of(i)
        cols = dump_at(snow, "col", L["birth"][i])
        if col is None or cols is None:
            no_column += 1
            continue
        ix, iz, k = col
        w = column_water(bed, reefs, ix, iz)
        if not w > 0:
            no_column += 1
            continue
        mean = cols[k] / (w * c * c)
        reads.append((i, fr[2], mean))
        dead_first += fr[3]
        if len(samples) < 8:
            samples.append("%d@%gs:%.3g/%.3g" % (i, fr[0], fr[2], mean))
    over = sum(1 for _, v, m in reads if v >= m)
    out.update(read=len(reads), no_absorptive_row=no_row, no_column=no_column,
               first_row_is_death_row=dead_first, at_or_over_mean=over,
               share=(over / len(reads) if reads else None),
               median_first_J_m3=median([v for _, v, _ in reads]),
               median_column_mean_J_m3=median([m for _, _, m in reads]),
               r47_median_first_J_m3=F4_R47_MEDIAN,
               samples_first_vs_mean=" ".join(samples) or None)

    # The depth reading: the founder's first positions row.
    places = [(i, pos["first"][i]) for i in founders if pos.get("present") and i in pos["first"]]
    if places:
        above = []
        for i, p in places:
            ix = min(bed["nx"] - 1, max(0, int(p[1] // c)))
            iz = min(bed["nz"] - 1, max(0, int(p[3] // c)))
            above.append(p[2] - bed["floor"][ix * bed["nz"] + iz])
        out.update(placed=len(places), in_top_12m=sum(1 for _, p in places if p[2] >= -F4_TOP),
                   median_depth_m=median([-p[2] for _, p in places]),
                   median_above_floor_m=median(above),
                   median_row_lag_s=median([p[0] - L["birth"][i] for i, p in places]))

    # Round 47's L4, a second reading: the breeders' column mean at their first positions row.
    bred = [i for i in founders if L["children"].get(i, 0) > 0]
    l4 = []
    for i in bred:
        p = pos.get("first", {}).get(i) if pos.get("present") else None
        col = column_of(i)
        cols = dump_at(snow, "col", p[0]) if p else None
        if col is None or cols is None:
            continue
        w = column_water(bed, reefs, col[0], col[1])
        if w > 0:
            l4.append(cols[col[2]] / (w * c * c))
    out.update(l4_breeders=len(bred), l4_placed=len(l4),
               l4_at_or_over_1=sum(1 for v in l4 if v >= 1.0))

    out["note"] = ("densityHere is what the body was fed at, share-multiplied (a crowded cell "
                   "reads under its density); the column is the founder's first positions row's; "
                   "held over the founders read")
    if not reads:
        out["held"] = "absent"
        out["note"] = "no pool founder has both a first absorptive row and a column; " + out["note"]
    else:
        out["held"] = over == len(reads)
    return out


# ---- E1: the endowment

def e1(seed, L, config):
    if not L["present"]:
        return absent("E1", seed, "no lineage.jsonl")
    if not L["has_endow"]:
        note = "no founder row carries `endow`"
        if not marker(config, "config_marker_endow"):
            note = pre_build("D122", "config_marker_endow") + "; " + note
        return absent("E1", seed, note)

    def summary(ids):
        vals = [L["endow"][i] for i in ids if i in L["endow"]]
        dead = [L["died"][i] - L["birth"][i] for i in ids if i in L["died"]]
        bred = sum(1 for i in ids if L["children"].get(i, 0) > 0)
        return vals, dead, bred

    founders = [i for i in L["endow"]]
    vals, dead, bred = summary(founders)
    pool = [i for i in founders if L["src"].get(i) == NAMES["lineage_pool_src"]]
    pv, pd, pb = summary(pool)
    return dict(clause="E1", seed=seed, endowed_founders=len(founders),
                endow_median_J=median(vals), endow_min_J=min(vals), endow_max_J=max(vals),
                dead=len(dead), median_age_at_death_s=median(dead),
                share_bred=bred / len(founders),
                pool_endowed=len(pool), pool_endow_median_J=median(pv),
                pool_median_age_at_death_s=median(pd),
                pool_share_bred=(pb / len(pool) if pool else None),
                held="reading")


# ---- B1: the books

def b1(seed, ts, by_t, manifest, cutoff, d):
    audits = [num(by_t[t], NAMES["stats_audit"]) for t in ts]
    audits = [a for a in audits if a is not None]
    audit_absmax = max(abs(a) for a in audits) if audits else None
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    resid = num(by_t[t], NAMES["stats_matter_resid"]) if t is not None else None
    div = num(by_t[ts[-1]], NAMES["stats_diverged"])
    status = manifest.get(NAMES["manifest_status"]) if manifest else None
    censored = status in ("error", "stopped")
    ddir = os.path.join(d, NAMES["diverged_dir"])
    dumps = (sum(1 for p in glob.glob(os.path.join(ddir, "**", "*"), recursive=True)
                 if os.path.isfile(p)) if os.path.isdir(ddir) else 0)
    held = (not censored and audit_absmax is not None and audit_absmax < L9_AUDIT
            and resid is not None and abs(resid) < L9_MATTER and div == 0)
    return dict(clause="B1", seed=seed, audit_absmax_J=audit_absmax, matter_resid_at=t,
                matter_resid=resid, short=short, diverged=div, diverged_dump_files=dumps,
                manifest_status=status, censored=censored, provisional=status == "running",
                held=held)


# ---- R1: round 47's L5 and L6 as readings

def as_reading(r, clause, base):
    """Relabel a round 47 L5/L6 row as a reading, its verdict kept in `as_r47`, and put the
    same reading of round 47's same seed beside it."""
    out = dict(r)
    out["clause"] = clause
    out["as_r47"] = r.get("held")
    out["held"] = "reading" if r.get("held") != "absent" else "absent"
    if base is not None:
        for k in ("table_J_m3", "open_J_m3", "min_ratio_J_m3", "dumps_held_J_m3", "last_at",
                  "under_per_m3", "beside_per_m3", "mean_under_per_m3", "mean_beside_per_m3",
                  "samples_fewer_under", "samples", "dumps"):
            if k in base:
                out["base_" + k] = base[k]
        if base.get("held") == "absent":
            out["base_note"] = base.get("note")
        out["base_as_r47"] = base.get("held")
    return out


def reef_readings(seed, d, config, manifest, cutoff, read_t, args, pos=None):
    """(L5 row, L6 row) of one run; the positions pass is made here when not handed one."""
    reefs, reef_reason = read_reefs(config, manifest)
    bed = read_bed(d, config)
    dumps = snow_dumps(d, cutoff)
    if pos is None:
        if reefs is not None and bed is not None:
            pos = positions_pass(d, cutoff, read_t, reefs, bed, {}, args.stride, args.l6_band)
        else:
            pos = dict(present=os.path.exists(os.path.join(d, "positions.jsonl")), l6=[])
    return (l5(seed, reefs, reef_reason, bed, dumps),
            l6(seed, reefs, reef_reason, bed, pos, args.l6_band))


# ---- P1: the pace

def p1(seed, ts, by_t, manifest, report, cutoff, width):
    status = (manifest or {}).get(NAMES["manifest_status"])
    t_end, _ = sample_at(ts, min(cutoff, END_SECONDS))
    w = window_stats(ts, by_t, t_end if t_end is not None else ts[-1], width, None)
    window_pace = w[4] if w else None
    if cutoff != float("inf") or status == "running":
        return absent("P1", seed, "the run has not ended (or the read is at a second): the "
                                  "whole-run pace is the footer's", manifest_status=status,
                      window="%g to %g s" % (w[0], w[1]) if w else None,
                      window_pace_x_real=window_pace)
    pace = (manifest or {}).get(NAMES["manifest_pace"])
    source = "run.json timesRealTime"
    if pace is None and report:
        for line in report:
            m = re.search(NAMES["footer_pace"], line)
            if m and "wall clock" in line:
                pace = float(m.group(1))
                source = "report footer"
    if pace is None:
        return absent("P1", seed, "no timesRealTime in run.json and no footer pace",
                      manifest_status=status, window_pace_x_real=window_pace)
    return dict(clause="P1", seed=seed, pace_x_real=pace, source=source,
                manifest_status=status, censored=status in ("error", "stopped"),
                last_window_pace_x_real=window_pace, held=pace >= P1_PACE)


def context_row(seed, ts, by_t, L, header, pool_reason, config, manifest, base_name):
    tokens = []
    if header:
        for pattern in (r"floors rigid groups", r"founders (?:in their food at its depth|in their food|in matter|anywhere)",
                        r"endowment \S+ s", r"senescence \S+ s(?: on upkeep)?",
                        r"overhead \S+ J", r"overhead scale x\S+ floor \S+ J",
                        r"gestation (?:mut=\S+ share=\S+ at \S+|off)", r"minkg=\S+", r"invest=\S+",
                        r"trickle \S+(?: s)?", r"pool \S+ of \d+", r"cellType mut \S+",
                        r"ceiling \d+"):
            m = re.search(r"(?:^| |, )(%s)" % pattern, header)
            if m:
                tokens.append(m.group(1).strip())
    last = by_t[ts[-1]]
    by_src = {}
    for s in L["src"].values():
        by_src[s] = by_src.get(s, 0) + 1
    markers = [k for k in ("config_marker_bud", "config_marker_gestation", "config_marker_senescence",
                           "config_marker_endow", "config_marker_depth") if marker(config, k)]
    return dict(clause="ctx", seed=seed, last_t=ts[-1], alive=num(last, NAMES["stats_alive"]),
                births=num(last, NAMES["stats_births"]),
                gestation_births=num(last, NAMES["stats_gest_births"]),
                upt_lim=num(last, NAMES["stats_upt_lim"]),
                trickle_spawns=num(last, NAMES["stats_trickle"]),
                pool_spawns=num(last, NAMES["stats_pool"]),
                founders_by_src=" ".join("%s:%d" % (k, v) for k, v in sorted(
                    by_src.items(), key=lambda kv: str(kv[0]))) or None,
                pool=pool_reason or "on",
                build_markers=" ".join(NAMES[k] for k in markers) or "none (a run before round 48's build)",
                manifest_status=(manifest or {}).get(NAMES["manifest_status"]),
                baseline=base_name,
                header_tokens="; ".join(tokens) if tokens else None)


# ---------------------------------------------------------------------- driving it over the arms

def read_stats(d, cutoff):
    rows = [r for r in stream_jsonl(os.path.join(d, "stats.jsonl"))
            if r.get(NAMES["stats_t"], 0) <= cutoff]
    by_t = {r[NAMES["stats_t"]]: r for r in rows}
    return sorted(by_t), by_t


def baseline_for(arm, i, args, manifest):
    if args.baseline_arms:
        if i < len(args.baseline_arms):
            return args.baseline_arms[i]
        return None
    m = re.search(r"-s(\d+)$", arm)
    if m:
        return "r47-s%s" % m.group(1)
    seed = (manifest or {}).get(NAMES["manifest_seed"])
    return ("r47-s%s" % seed) if seed is not None else None


def read_arm(runs_root, arm, i, cutoff, args, base_root):
    d = run_dir(runs_root, arm)
    ts, by_t = read_stats(d, cutoff)
    if not ts:
        raise SystemExit("no stats.jsonl rows (or none by the second asked) for %r" % arm)
    manifest = load_json(os.path.join(d, "run.json")) or {}
    config = load_json(os.path.join(d, "config.json")) or {}
    report = report_lines(runs_root, arm)
    header = None
    if report:
        header = next((ln for ln in report if ln.startswith(NAMES["header_prefix"])), None)
    read_t = min(cutoff, END_SECONDS)

    reefs, reef_reason = read_reefs(config, manifest)
    _, pool_reason = pool_named(config)
    L = read_lineage(d, cutoff)
    bed = read_bed(d, config)
    snow = field_dumps(d, cutoff, NAMES["fields_snow"])
    snow_floor = field_dumps(d, cutoff, NAMES["fields_snow_floor"])

    want_first = {}
    if not pool_reason and L["present"]:
        want_first = {i_: L["birth"][i_] for i_ in L["pool_founders"]}
    pos = positions_pass(d, cutoff, read_t, reefs, bed, want_first, args.stride, args.l6_band)

    base_name = baseline_for(arm, i, args, manifest)
    base = None
    same_run = False
    base_l5 = base_l6 = None
    if base_name:
        try:
            bd = run_dir(base_root, base_name)
            base = read_stats(bd, float("inf"))
            same_run = os.path.normcase(os.path.abspath(bd)) == os.path.normcase(os.path.abspath(d))
            if not base[0]:
                base = None
            elif not args.no_baseline_reefs:
                if same_run and cutoff == float("inf"):
                    base_l5 = base_l6 = "same"
                else:
                    bconfig = load_json(os.path.join(bd, "config.json")) or {}
                    bmanifest = load_json(os.path.join(bd, "run.json")) or {}
                    base_l5, base_l6 = reef_readings(base_name, bd, bconfig, bmanifest,
                                                     float("inf"), END_SECONDS, args)
        except SystemExit:
            base = None

    t_last, last, _ = last_stats(ts, by_t, cutoff)
    r5, r6 = reef_readings(arm, d, config, manifest, cutoff, read_t, args, pos)
    if base_l5 == "same":
        base_l5, base_l6 = r5, r6

    rows = [
        context_row(arm, ts, by_t, L, header, pool_reason, config, manifest, base_name),
        m1(arm, L, config),
        m2(arm, L, config, last),
        m3(arm, L, config, pos, read_t),
        g1(arm, L, config, ts, by_t, cutoff),
        g2(arm, L, config, ts, cutoff),
        compare("O1", arm, ts, by_t, cutoff, base, base_name, same_run,
                NAMES["stats_invest"], "mean_birth_investment", above=False),
        compare("O2a", arm, ts, by_t, cutoff, base, base_name, same_run,
                NAMES["stats_alive"], "alive", above=True),
        o2b(arm, ts, by_t, config, manifest, cutoff),
        compare("S1", arm, ts, by_t, cutoff, base, base_name, same_run,
                NAMES["stats_age"], "mean_age_s", above=True),
        s2(arm, L),
        f1(arm, L, pool_reason),
        f2(arm, L, pool_reason),
        f3(arm, L, pool_reason, pos, read_t),
        f4(arm, L, pool_reason, pos, bed, reefs, config, snow, snow_floor, d, cutoff),
        e1(arm, L, config),
        b1(arm, ts, by_t, manifest, cutoff, d),
        as_reading(r5, "R1a", base_l5),
        as_reading(r6, "R1b", base_l6),
        p1(arm, ts, by_t, manifest, report, cutoff, args.window),
    ]
    if base_name and base is not None:
        for r in rows:
            if r["clause"] in ("R1a", "R1b"):
                r["baseline"] = base_name
    return rows


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


CLAUSE_THRESHOLD_TEXT = {
    "M1": "3 of 3", "M2": "3 of 3", "M3": "1 of 3 or more", "G1": "3 of 3", "G2": "2 of 3",
    "O1": "3 of 3", "O2a": "2 of 3", "O2b": "3 of 3", "S1": "3 of 3", "S2": "2 of 3",
    "F1": "3 of 3", "F2": "3 of 3", "F3": "1 of 3 or more",
    "F4": "3 of 3", "E1": "a reading", "B1": "3 of 3",
    "R1a": "a reading", "R1b": "a reading", "P1": "3 of 3",
}


def show(r):
    parts = []
    for k, v in r.items():
        if k in ("clause", "seed"):
            continue
        parts.append("%s=%s" % (k, fmt_cell(v)))
    return "%-4s %-10s %s" % (r["clause"], r["seed"], "  ".join(parts))


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"),
                    help="defaults to <repo>/runs")
    ap.add_argument("--arms", nargs="+", default=DEFAULT_ARMS)
    ap.add_argument("--baseline-runs-root", default=os.path.join(REPO, "runs"),
                    help="where round 47's runs are, for O1, O2a, S1 and R1; defaults to <repo>/runs")
    ap.add_argument("--baseline-arms", nargs="+", default=None,
                    help="round 47's arms in the order of --arms; default r47-s<N> for seed N")
    ap.add_argument("--no-baseline-reefs", action="store_true",
                    help="skip R1's readings of round 47's run (a pass over its positions)")
    ap.add_argument("--stride", type=float, default=L6_STRIDE,
                    help="R1b reads the positions rows at multiples of this many seconds")
    ap.add_argument("--l6-band", type=float, default=L6_BAND,
                    help="R1b's depth band under the caps' underside, m")
    ap.add_argument("--window", type=float, default=WINDOW,
                    help="P1's window reading: the seconds read before the second read")
    ap.add_argument("--out", default=None,
                    help="write the TSV here (creates its directory; nothing is written "
                         "without this flag)")
    ap.add_argument("milestone", nargs="*",
                    help="the watch's `<second> <arm>`; reads that arm alone, up to that second")
    args = ap.parse_args()

    def absroot(p):
        return p if os.path.isabs(p) else os.path.join(os.getcwd(), p)

    runs_root = absroot(args.runs_root)
    base_root = absroot(args.baseline_runs_root)

    cutoff = float("inf")
    if args.milestone:
        if len(args.milestone) != 2:
            raise SystemExit("the positional form is `<second> <arm>`; saw %r" % args.milestone)
        cutoff = float(args.milestone[0])
        print("milestone %s s, %s (every record read up to that second)"
              % (args.milestone[0], args.milestone[1]))
        args.arms = [args.milestone[1]]

    clauses = list(CLAUSE_THRESHOLD_TEXT)
    all_rows = []
    by_clause = {c: [] for c in clauses}
    for i, arm in enumerate(args.arms):
        try:
            arm_rows = read_arm(runs_root, arm, i, cutoff, args, base_root)
        except SystemExit as e:
            print("skipping %r: %s" % (arm, e), file=sys.stderr)
            continue
        for r in arm_rows:
            all_rows.append(r)
            if r["clause"] in by_clause:
                by_clause[r["clause"]].append(r.get("held"))
            print(show(r))
        print()

    for c in clauses:
        results = by_clause[c]
        n_true = sum(1 for x in results if x is True)
        others = {}
        for x in results:
            if isinstance(x, str):
                others[x] = others.get(x, 0) + 1
        note = "".join(" (%d %s)" % (n, k) for k, n in sorted(others.items()))
        print("%-5s held in %d of %d%s -- needs %s"
              % (c + ":", n_true, len(results), note, CLAUSE_THRESHOLD_TEXT[c]))

    if args.out:
        write_tsv(args.out, all_rows)
        print()
        print("wrote", args.out)


if __name__ == "__main__":
    main()
