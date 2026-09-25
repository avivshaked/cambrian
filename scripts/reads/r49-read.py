#!/usr/bin/env python3
"""The clause reader for round 49 (logbook/0121-a-founder-earns-its-last-tenth.md): round 48's
world with D124's founder cap at 0.9 of the gate, D123's senses read on the last step, the
bite rebuild, record format 2 and a checkpoint every 100 s.

Built from round 48's reader (scripts/reads/r48-read.py): the same run-directory layout, the
same tolerant reading (a missing field prints 'absent' rather than raising, a half-written last
line of a live file is dropped), scripts/reads/contact_aliases.py's `field()` for every
stats.jsonl lookup, scripts/reads/runrec.py for the positions and the snapshots in either
record format, one row per seed per clause and a `held in N of M` line per clause. The items of
0121's "What round 49's reader must add to round 48's" are numbered below as they are there.

What round 49 adds to the names (confirmed against the round 49 branch at 64d2f0d,
2026-09-25): a founder row's `fsnow` and `fcol` (the snow at its landing point and its
column's mean, J/m3), `fmat` and `fmcol` (the dissolved matter's, units/m3) and `capcut` (the
joules the cap took, only when it took any); a death row's cause `c`, `ga` (the gestation
account it died holding) and `res` (its reserve), each of the last two only above 0; the stats
row's `foundersCapped` and `founderJoulesCapped` when the cap is on; config.json's
`founderReserveCapFraction`, which marks the build. A run without it reads `absent` on the
clauses that need it, never a false zero.

How each clause is read:

  - W1 (item 2) over the founders that eat one food: a snow eater (birth row abs 1, pho 0)
    holds when `fsnow` >= `fcol`, a matter eater (pho 1, abs 0) when `fmat` >= `fmcol`, each
    within a relative 1e-6; a founder without its pair fails. Counts are printed by group (the
    source, or `poolN` for pool index N) and diet, and up to eight failures with their first
    positions height, the 1 m layer it lies in and its column. Mixotrophs are counted and not
    asked (the rule sets one at the richer share of its two foods).
  - W2 (item 3): each pool founder's first absorptive.jsonl `densityHere` at or after its birth
    over its `fsnow`; the median against 0.02 to 0.3, and the lag from landing to that row, by
    pool index.
  - W3 (item 4): the pool founders' median `fsnow` over `fcol`, at least 1.6, by pool index.
  - G1 is `gestationBirths` at the sample. G2 (item 5) is turned round from round 48's: it
    holds when the share of children with `gm` 1 in the last 5,000 s is below the share in
    the 5,000 s after the floor closes. The strong reading, births per death by mode in the
    last window, is printed beside it.
  - G3 and G4 (item 6): the `ga` of the gestating bodies' death rows (birth row `gm` 1) at or
    before the sample, summed. G3 holds when the sum is at least a fifth of `gestatedJoules`.
    G4 derives the price per gestating birth, banked less `gestationJoulesHeld` less the sum,
    over `gestationBirths`, and holds at 50 J or more.
  - C1 (item 7) is a reading: at 5,000, 15,000 and 30,000 s, the growth of `overlapBodies`
    over the growth of `harnessBodySteps` in the 500 s before (the share of the living touching
    another body at a physics step), and the share of the snapshot's bodies whose genome has a
    neuron input of kind Sensor on the Contact or the Damage channel.
  - C2 (item 8): no `the checkpoint due at ... waits` line in <logs-dir>/<arm>.err, and the
    manifest's `checkpoints` equal to its simulated seconds over its cadence at a cadence of
    100 s. Without the log in --logs-dir, the newest <repo>/scratch/wt-*/scratch/logs/<arm>.err
    is read, since run-farm.ps1 logs under the tree it runs from. A running arm is absent
    (the manifest's count is written at the end); the files on
    disk, `partsKilled` and `moduleRebuilds` are printed beside it.
  - C3 (item 9): no `starved` death row carries `res`; the deaths by cause with the solvent
    ones and their median reserve are printed.
  - B1 is round 48's, with the manifest's ending asked: `ended` on `budget` (not asked of a
    running arm, which reads provisional).
  - V1 (item 10): every film window under --windows-root whose last identity row names the
    arm; a window is faithful when its verdict is `faithful`, every identity row agrees and
    `sourcesDiffer` is empty. Held with one faithful window. The exit code is not in the
    window's record; the verdict row stands for it.
  - V2 is asked of seed 1 alone, from --v2-log: the last `verify-checkpoint exit N` and
    `compare-det exit N` lines, both 0. V3 is the run directory's size on disk, under 2 GB.
  - M1 counts bud rows (100 or more), M2 the share with `budx` >= 1 (three quarters), M3 as in
    round 48. M4 (item 12): in each M3 clade with ten or more living, the median of `foodW`
    over `foodW` plus `lightW` over the living members logged in absorptive.jsonl's last
    sample at or before the second read, under 2%, as scripts/reads/r48-entry/budshare.py reads
    it.
  - O1, O3, S1 and X1 (item 13): `alive`, `jointed`, `meanAge` and `detritusCv` at the sample
    against their own bars, round 48's same seed beside (seed 2's at 13,690 s, the sample it
    stopped at). O2 is the largest `alive` under 25,000 and no runaway ending.
  - F3: no line rooted in a pool founder (the parent walk from the living at the second read)
    with ten or more living; the largest line's root, pool index, `capcut` and `fsnow` beside.
  - E1 is a reading: `endow`, the age at death and the share that bred, by group.
  - R1 is round 47's L5 and L6 read as round 48 read them, round 48's same seed beside.
  - P1 (item 14): the whole run's pace, with the pace over the last 2,500 s on every run.
  - EK1 (item 11): no founder with a child within 1 s of its birth row, and those within 10 s
    beside, by group. EK2: `foundersCapped` equal to the founder rows with `capcut` at or
    before the sample, `founderJoulesCapped` their sum within 1e-6 relative, and every
    one-part pool stomach's (pool index 0) `capcut` in 17.0 to 18.5 J. EK3: the one-part
    stomachs' median age at death above 300 s. EK4 is pooled over the seeds read: at most a
    fifth of the one-part stomachs have a child, and those that do have their first a median
    of 10 s or more after landing. EK5: every pool founder's median age at death above 300 s.
    EK6, pooled: the breeders' median `fsnow` above the rest's, a reading under five breeders.
    EK7 is a reading: the share capped, `capcut` and `endow` by group.
  - S2 (item 15): in a seed with ten or more dead stomach children (abs 1, pho 0, not a
    founder), their median age at death under 1,500 s; a seed with fewer is not asked.

A context row per seed prints the crowd, the births, the founders by source, the build markers
and the header's tokens.

Run from anywhere:

    python scripts/reads/r49-read.py                               # r49-s1 r49-s2 r49-s3
    python scripts/reads/r49-read.py 10000 r49-s2                  # the watch's call form
    python scripts/reads/r49-read.py --runs-root <root> --arms r49smoke-s1
    python scripts/reads/r49-read.py --windows-root scratch/r49-windows --v2-log <log>
    python scripts/reads/r49-read.py --out logbook/specs/r49-read/clauses.tsv

The `<second> <arm>` form reads every record up to that second and no further, so a clause
asked at 30,000 s is read at the second given and marked short; it is the state at that mark
and not a verdict. A read takes the newest run directory of an arm. Round 48's runs are found
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


def _default_runs_root():
    """<repo>/runs, or the main tree's when this copy sits in a worktree under scratch/wt-*.

    The round's seeds are launched from the worktree `scratch/wt-r49` with the main tree's runs
    root, and the watch runs this script from the worktree (where the seeds' logs are), passing
    `<second> <arm>` and nothing else; the worktree's own runs/ holds no run."""
    parent = os.path.dirname(REPO)
    if os.path.basename(parent) == "scratch" and os.path.basename(REPO).startswith("wt-"):
        main_runs = os.path.join(os.path.dirname(parent), "runs")
        if os.path.isdir(main_runs):
            return main_runs
    return os.path.join(REPO, "runs")

sys.path.insert(0, HERE)
from contact_aliases import field as _aliased_field  # noqa: E402  (path set above)
import runrec  # noqa: E402  (either record: positions.jsonl or positions.jsonl.gz)

DEFAULT_ARMS = ["r49-s1", "r49-s2", "r49-s3"]

END_SECONDS = 30000.0
W1_TOLERANCE = 1e-6                # W1: a relative tolerance on at-or-over
W2_BAND = (0.02, 0.3)              # W2: the pool founders' first densityHere over fsnow
W3_RATIO = 1.6                     # W3: the pool founders' median fsnow over fcol
G2_WINDOW = 5000.0                 # G2: the two windows, s
G3_SHARE = 0.2                     # G3: held at death over banked, at least
G4_PRICE = 50.0                    # G4: joules a gestating birth, at least
C1_SECONDS = (5000, 15000, 30000)  # C1: the seconds read
C1_WINDOW = 500.0                  # C1: the touch share's window before each, s
C2_CADENCE = 100.0                 # C2: the checkpoint cadence the round runs at, s
M1_BUDS = 100                      # M1: at least 100 bud rows
M2_SHARE = 0.75                    # M2: budx >= 1 on at least three quarters
M3_LIVING = 10                     # M3: ten or more living in a bud-rooted clade
M4_SHARE = 0.02                    # M4: the stomach's median share of income, under
O1_BAND = (4000, 16000)            # O1: alive at 30,000 s
O2_CEILING = 25000                 # O2: the runaway ceiling (config's maximumPopulation)
O3_JOINTED = 100                   # O3: jointed at 30,000 s, under
S1_AGE = 1500.0                    # S1: meanAge at 30,000 s, above
S2_AGE = 1500.0                    # S2: the stomach children's median age at death, under
S2_DEAD = 10                       # S2: asked of a seed with at least this many dead
F3_LIVING = 10                     # F3: no pool-rooted clade with this many living
EK1_LAND = 1.0                     # EK1: a child within this many seconds of landing
EK1_WIDE = 10.0                    # EK1's reading beside it, s
EK2_CUT = (17.0, 18.5)             # EK2: the one-part stomach's capcut, J
EK3_AGE = 300.0                    # EK3: the one-part stomachs' median age at death, above
EK4_SHARE = 0.2                    # EK4: pooled share of one-part stomachs that breed, at most
EK4_LAG = 10.0                     # EK4: the breeders' median first-child lag, at least, s
EK5_AGE = 300.0                    # EK5: every pool founder's median age at death, above
ONE_PART = 0                       # the pool index of the one-part stomach
V3_BYTES = 2e9                     # V3: a run directory under 2 GB
X1_BAND = (0.4, 1.0)               # X1: detritusCv at 30,000 s
L5_FROM = 10000.0                  # R1: at 10,000 s and after
L6_BAND = 10.0                     # R1b: the depth band under the underside, m (L6's)
L6_STRIDE = 500.0                  # R1b: the positions rows read, every this many seconds
L9_AUDIT = 0.1                     # B1: audit under 0.1 J
L9_MATTER = 1e-5                   # B1: matter residual under 1e-5 units
P1_PACE = 1.0                      # P1: at or above 1x real time
WINDOW = 2500.0                    # P1's window reading, s
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

    # Round 49 (0121). stats.jsonl: the cap's two, the contact census, the plan's counts.
    "stats_capped": "foundersCapped",                # cumulative, D124, when the cap is on
    "stats_capped_J": "founderJoulesCapped",         # cumulative, D124
    "stats_overlap_bodies": "overlapBodies",         # cumulative body-steps touching a body
    "stats_body_steps": "harnessBodySteps",          # cumulative body-steps
    "stats_jointed": "jointed",
    "stats_det_cv": "detritusCv",
    "stats_parts_killed": "partsKilled",             # cumulative
    "stats_module_rebuilds": "moduleRebuilds",       # cumulative
    "config_marker_cap": "founderReserveCapFraction",        # D124's build
    "manifest_checkpoints": "checkpoints",
    "manifest_checkpoint_every": "checkpointEverySeconds",
    "manifest_simulated": "simulatedSeconds",
    # lineage.jsonl, round 49: a founder row's landing readings and its cut; a death row's
    # cause `c`, the account it died holding `ga` and its reserve `res` (each only above 0).
    "lineage_fsnow": "fsnow", "lineage_fcol": "fcol", "lineage_fmat": "fmat",
    "lineage_fmcol": "fmcol", "lineage_capcut": "capcut",
    "lineage_cause": "c", "lineage_ga": "ga", "lineage_res": "res",
    "cause_starved": "starved",
    "absorptive_food": "foodW", "absorptive_light": "lightW",
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
    MB). The round 48 and 49 fields are kept where a row carries them; `has_*` says whether any
    row did. `first_child` is each parent's first child's birth time."""
    path = os.path.join(d, "lineage.jsonl")
    N = NAMES
    L = dict(present=os.path.exists(path), birth={}, parent={}, kind={}, src={}, pool={},
             ink={}, absf={}, pho={}, bud={}, budx={}, gm={}, gs={}, endow={}, died={},
             children={}, first_child={}, fsnow={}, fcol={}, fmat={}, fmcol={}, capcut={},
             cause={}, ga={}, res={}, has_bud=False, has_gm=False, has_endow=False,
             has_landing=False, has_cause=False)
    if not L["present"]:
        L["root"] = lambda i: i
        L["pool_founders"] = []
        return L
    landing = (("lineage_fsnow", "fsnow"), ("lineage_fcol", "fcol"), ("lineage_fmat", "fmat"),
               ("lineage_fmcol", "fmcol"), ("lineage_capcut", "capcut"))
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
                for key, into in landing:
                    if N[key] in row:
                        L[into][i] = row[N[key]]
                        if into != "capcut":
                            L["has_landing"] = True
            else:
                L["children"][p] = L["children"].get(p, 0) + 1
                if p not in L["first_child"] or t < L["first_child"][p]:
                    L["first_child"][p] = t
        elif e == N["lineage_death"]:
            L["died"][i] = t
            if N["lineage_cause"] in row:
                L["has_cause"] = True
                L["cause"][i] = row[N["lineage_cause"]]
            if N["lineage_ga"] in row:
                L["ga"][i] = row[N["lineage_ga"]]
            if N["lineage_res"] in row:
                L["res"][i] = row[N["lineage_res"]]
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
    """One pass over the positions, in either record (runrec.py: positions.jsonl, or
    positions.jsonl.gz's members). Collects the row at the read second (M3, F3), the R1b
    samples and each wanted id's first place at or after its birth (F4). A row is parsed only
    when one of those needs it."""
    out = dict(present=runrec.has_positions(d), last_line=None, last_t=None, l6=[], first={})
    if not out["present"]:
        return out
    pending = dict(want_first)         # id -> birth t
    earliest = min(pending.values()) if pending else None
    N = NAMES
    l6_geom = l6_geometry(reefs, bed, band) if (reefs is not None and bed is not None) else None
    if l6_geom is not None:
        out["l6_volumes"] = (l6_geom["v_under"], l6_geom["v_beside"])
    for line in runrec.positions_lines(d):
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
        return absent("L6", seed, "no positions in either record")
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
    L["m3_clades"] = {}
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
    L["m3_clades"] = {r: members[r] for r in counts if counts[r] >= M3_LIVING}
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
        out["held"] = l_share < e_share
    return out


# ---- round 48's same seed, and O2

def base_value(base, name):
    """(t, value) at round 47's sample at or before 30,000 s."""
    if base is None:
        return None, None
    ts, by_t = base
    t, _ = sample_at(ts, END_SECONDS)
    if t is None:
        return None, None
    return t, num(by_t[t], name)


def o2b(seed, ts, by_t, config, manifest, cutoff):
    ceiling = find_field(config, NAMES["config_max_pop"]) or O2_CEILING
    vals = [(t, num(by_t[t], NAMES["stats_alive"])) for t in ts if t <= min(cutoff, END_SECONDS)]
    vals = [(t, a) for t, a in vals if a is not None]
    if not vals:
        return absent("O2", seed, "no alive at any sample")
    tmax, amax = max(vals, key=lambda ta: ta[1])
    reason = (manifest or {}).get(NAMES["manifest_reason"])
    runaway = isinstance(reason, str) and reason.startswith("ceiling")
    return dict(clause="O2", seed=seed, max_alive=amax, max_at=tmax, ceiling=ceiling,
                manifest_reason=reason, samples=len(vals),
                held=amax < ceiling and not runaway)


# ---- W: a founder's food at landing

def at_or_over(here, column):
    return here >= column or here >= column * (1.0 - W1_TOLERANCE)


def founder_group(L, i):
    """A founder's group: its source, and its pool index for a pool founder."""
    s = L["src"].get(i)
    if s == NAMES["lineage_pool_src"]:
        return "pool%s" % L["pool"].get(i)
    return str(s)


def diet(L, i):
    a, p = L["absf"].get(i) == 1, L["pho"].get(i) == 1
    return "mixotroph" if a and p else "snow" if a else "matter" if p else "none"


def w1_failures(L):
    """(checked, failures) over the single-diet founders. A failure is (id, diet, here,
    column), here or column None for a founder that lacks its pair."""
    checked, failures = [], []
    for i in sorted(L["src"]):
        d = diet(L, i)
        if d == "snow":
            here, col = L["fsnow"].get(i), L["fcol"].get(i)
        elif d == "matter":
            here, col = L["fmat"].get(i), L["fmcol"].get(i)
        else:
            continue
        checked.append(i)
        if here is None or col is None or not at_or_over(here, col):
            failures.append((i, d, here, col))
    return checked, failures


def w1(seed, L, pos, bed, config):
    if not L["present"]:
        return absent("W1", seed, "no lineage.jsonl")
    if not L["has_landing"]:
        return absent("W1", seed, "no founder row carries fsnow or fmat (a run before "
                                  "round 49's build)")
    rule = find_field(config, NAMES["config_marker_depth"])
    checked, failures = w1_failures(L)
    if not checked:
        return absent("W1", seed, "no founder eats one food only")
    groups = {}
    for i in checked:
        g = "%s/%s" % (founder_group(L, i), diet(L, i))
        groups[g] = [groups.get(g, [0, 0])[0] + 1, groups.get(g, [0, 0])[1]]
    for i, d, _, _ in failures:
        groups["%s/%s" % (founder_group(L, i), d)][1] += 1
    listed = []
    for i, d, here, col in failures[:8]:
        p = pos.get("first", {}).get(i) if pos.get("present") else None
        where = "no positions row"
        if p is not None:
            k = int(math.floor(-p[2]))
            column = "?"
            if bed is not None:
                column = "%d,%d" % (int(p[1] // bed["cell"]), int(p[3] // bed["cell"]))
            where = "y %.3f in the layer %d to %d m, column %s, at %g s" % (p[2], -k - 1, -k, column, p[0])
        listed.append("%d %s %s against %s, %s" % (i, d, fmt_cell(here), fmt_cell(col), where))
    return dict(clause="W1", seed=seed, founders=len(L["src"]), checked=len(checked),
                mixotrophs_not_asked=sum(1 for i in L["src"] if diet(L, i) == "mixotroph"),
                failures=len(failures),
                lacking_pair=sum(1 for f in failures if f[2] is None or f[3] is None),
                at_or_over_by_group=" ".join("%s:%d/%d" % (g, n - f, n)
                                             for g, (n, f) in sorted(groups.items())),
                failures_listed="; ".join(listed) or None,
                depth_rule=("on" if rule else "off"),
                note=(None if rule else "the depth rule is off in this world, so it claims nothing"),
                held=(not failures) if rule else "absent")


def by_index(pairs, fmt=None):
    """'k:median(n N)' over (pool index, value) pairs."""
    by = {}
    for k, v in pairs:
        by.setdefault(k, []).append(v)
    return " ".join("%s:%s(n %d)" % (k, fmt_cell(median(v)), len(v))
                    for k, v in sorted(by.items(), key=lambda kv: str(kv[0]))) or None


def w2(seed, L, first):
    if not L["present"]:
        return absent("W2", seed, "no lineage.jsonl")
    founders = L["pool_founders"]
    if not founders:
        return absent("W2", seed, "no pool founder yet")
    if first is None:
        return absent("W2", seed, "no absorptive.jsonl")
    ratios, lags = [], []
    for i in founders:
        fr, here = first.get(i), L["fsnow"].get(i)
        if fr is None or not here:
            continue
        ratios.append((L["pool"].get(i), fr[2] / here))
        lags.append((L["pool"].get(i), fr[0] - L["birth"][i]))
    m = median([r for _, r in ratios])
    return dict(clause="W2", seed=seed, pool_founders=len(founders), read=len(ratios),
                median_first_over_fsnow=m, band="%g to %g" % W2_BAND,
                median_lag_s=median([g for _, g in lags]),
                ratio_by_pool_index=by_index(ratios), lag_by_pool_index=by_index(lags),
                held=("absent" if m is None else W2_BAND[0] <= m <= W2_BAND[1]))


def w3(seed, L):
    if not L["pool_founders"]:
        return absent("W3", seed, "no pool founder yet")
    ids = [i for i in L["pool_founders"] if L["fsnow"].get(i) is not None and L["fcol"].get(i)]
    if not ids:
        return absent("W3", seed, "no pool founder row carries fsnow and fcol")
    ratios = [(L["pool"].get(i), L["fsnow"][i] / L["fcol"][i]) for i in ids]
    m = median([r for _, r in ratios])
    return dict(clause="W3", seed=seed, pool_founders=len(ids), median_fsnow_over_fcol=m,
                by_pool_index=by_index(ratios),
                median_fsnow=median([L["fsnow"][i] for i in ids]),
                median_fcol=median([L["fcol"][i] for i in ids]), held=m >= W3_RATIO)


def absorptive_pass(d, cutoff, read_t, births):
    """One pass over absorptive.jsonl: (each wanted id's first row at or after its birth, as
    id -> (t, age, densityHere, dead, y); the last sample at or before read_t, as (t, id ->
    (foodW, lightW)) over the rows that are not death rows). None when there is no file."""
    path = os.path.join(d, "absorptive.jsonl")
    if not os.path.exists(path):
        return None, None
    N = NAMES
    first, last_t, last = {}, None, {}
    for row in stream_jsonl(path):
        i = row.get(N["absorptive_id"])
        t = row.get(N["absorptive_t"])
        if i is None or t is None or t > cutoff:
            continue
        bt = births.get(i)
        if bt is not None and t >= bt and row.get(N["absorptive_density"]) is not None:
            if i not in first or t < first[i][0]:
                first[i] = (t, row.get(N["absorptive_age"]), row[N["absorptive_density"]],
                            bool(row.get(N["absorptive_dead"])), row.get(N["absorptive_y"]))
        if t <= read_t and not row.get(N["absorptive_dead"]):
            if last_t is None or t > last_t:
                last_t, last = t, {}
            if t == last_t:
                last[i] = (row.get(N["absorptive_food"]), row.get(N["absorptive_light"]))
    return first, (last_t, last)


# ---- G3 and G4: the gestation account at death

def g34(seed, L, config, ts, by_t, cutoff):
    """(G3, G4): the accounts the gestating bodies (birth row gm 1) died holding, against what
    the run banked, at the last sample; the deaths counted are those at or before it."""
    t, row, short = last_stats(ts, by_t, cutoff)
    if not L["present"]:
        return absent("G3", seed, "no lineage.jsonl"), absent("G4", seed, "no lineage.jsonl")
    banked = num(row, NAMES["stats_gest_joules"]) if row else None
    births = num(row, NAMES["stats_gest_births"]) if row else None
    living = num(row, NAMES["stats_gest_held"]) if row else None
    if banked is None:
        note = "gestatedJoules missing at the sample"
        return absent("G3", seed, note, at=t), absent("G4", seed, note, at=t)
    if not L["ga"] and not marker(config, "config_marker_cap"):
        note = pre_build("round 49", "config_marker_cap") + ", and no death row carries ga"
        return absent("G3", seed, note, at=t), absent("G4", seed, note, at=t)
    dead = [i for i, dt in L["died"].items() if dt <= t and L["gm"].get(i) == 1]
    holding = [L["ga"][i] for i in dead if L["ga"].get(i, 0) > 0]
    total = sum(holding)
    share = total / banked if banked > 0 else None
    g3 = dict(clause="G3", seed=seed, at=t, short=short, banked_J=banked, died_holding_J=total,
              gestating_deaths=len(dead), died_holding=len(holding),
              median_held_J=median(holding), share_of_banked=share, living_hold_J=living,
              held=("absent" if share is None else share >= G3_SHARE))
    price = (banked - living - total) / births if (living is not None and births) else None
    g4 = dict(clause="G4", seed=seed, at=t, short=short, banked_J=banked, living_hold_J=living,
              died_holding_J=total, gestation_births=births, price_per_birth_J=price,
              note="derived: banked less held by the living less held at death, over the "
                   "births paid from an account",
              held=("absent" if price is None else price >= G4_PRICE))
    return g3, g4


# ---- C: the senses, the checkpoints, the deaths

SENSE = re.compile('"kind":"Sensor","index":-?[0-9]+,"channel":"(Contact|Damage)"')


def senses_at(d, second):
    """(second, living, reads contact, reads damage, reads either) from the snapshot at or
    before `second`, the genomes joined; None when the record holds none."""
    seconds = [s for s in runrec.snapshot_seconds(d) if s <= second]
    if not seconds:
        return None
    snap = runrec.snapshot(d, seconds[-1])
    if snap is None:
        return None
    contact = damage = either = 0
    for line in snap.lines:
        found = set(SENSE.findall(line))
        contact += "Contact" in found
        damage += "Damage" in found
        either += bool(found)
    return seconds[-1], len(snap.lines), contact, damage, either


def c1(seed, d, ts, by_t, cutoff):
    """A reading: the share of the living touching another body at a physics step over the
    window before each second, and the share whose genome reads either sense."""
    out = dict(clause="C1", seed=seed)
    for s in C1_SECONDS:
        if s > min(cutoff, ts[-1]) + 1e-9:
            continue
        t, _ = sample_at(ts, s)
        touch = None
        if t is not None:
            t0, _ = sample_at(ts, t - C1_WINDOW)
            if t0 is not None and t0 < t:
                a, b = by_t[t0], by_t[t]
                o0, o1 = num(a, NAMES["stats_overlap_bodies"]), num(b, NAMES["stats_overlap_bodies"])
                h0, h1 = num(a, NAMES["stats_body_steps"]), num(b, NAMES["stats_body_steps"])
                if None not in (o0, o1, h0, h1) and h1 > h0:
                    touch = (o1 - o0) / (h1 - h0)
        out["touch_share_to_%d" % s] = touch
        sen = senses_at(d, s)
        if sen is not None and sen[1]:
            out["senses_at_%d" % s] = ("snapshot %d s, %d living: contact %.3f, damage %.3f, either %.3f"
                                       % (sen[0], sen[1], sen[2] / sen[1], sen[3] / sen[1], sen[4] / sen[1]))
    out["note"] = ("touch: overlapBodies over harnessBodySteps in the %g s before each second; a "
                   "sense is read when a neuron has a Sensor input on the channel" % C1_WINDOW)
    out["held"] = "reading"
    return out


def c2(seed, d, manifest, last, logs_dir, arm):
    N = NAMES
    m = manifest or {}
    err = os.path.join(logs_dir, arm + ".err")
    if not os.path.exists(err):
        # A seed launched from a worktree logs under that worktree (run-farm.ps1 writes to
        # its own tree's scratch/logs): the newest such log, named in the row.
        found = sorted(glob.glob(os.path.join(REPO, "scratch", "wt-*", "scratch", "logs", arm + ".err")),
                       key=os.path.getmtime)
        if found:
            err = found[-1]
    waits = None
    if os.path.exists(err):
        with open(err, encoding="utf-8", errors="replace") as f:
            waits = [ln.strip() for ln in f if "the checkpoint due at" in ln and " waits" in ln]
    every = m.get(N["manifest_checkpoint_every"])
    got = m.get(N["manifest_checkpoints"])
    sim = m.get(N["manifest_simulated"])
    on_disk = len(glob.glob(os.path.join(d, "checkpoints", "*.ckpt*")))
    expected = int(sim // every) if (sim and every) else None
    out = dict(clause="C2", seed=seed, err_log=err if waits is not None else "absent",
               waits=(len(waits) if waits is not None else None),
               first_wait=(waits[0][:120] if waits else None), cadence_s=every,
               checkpoints_manifest=got, expected=expected, checkpoints_on_disk=on_disk,
               parts_killed=num(last, N["stats_parts_killed"]) if last else None,
               module_rebuilds=num(last, N["stats_module_rebuilds"]) if last else None)
    if m.get(N["manifest_status"]) == "running":
        out["held"] = "absent"
        out["note"] = "the run has not ended: the manifest's count is written at the end"
    elif waits is None:
        out["held"] = "absent"
        out["note"] = "no error log at %s" % err
    else:
        out["held"] = not waits and every == C2_CADENCE and got is not None and got == expected
    return out


def c3(seed, L, config):
    if not L["present"]:
        return absent("C3", seed, "no lineage.jsonl")
    if not L["has_cause"]:
        return absent("C3", seed, "no death row carries a cause")
    if not L["res"] and not marker(config, "config_marker_cap"):
        return absent("C3", seed, pre_build("round 49", "config_marker_cap") +
                      ", and no death row carries res")
    by = {}
    for i, c in L["cause"].items():
        n, s, rs = by.get(c, (0, 0, []))
        r = L["res"].get(i)
        if r:
            s += 1
            rs.append(r)
        by[c] = (n + 1, s, rs)
    solvent = by.get(NAMES["cause_starved"], (0, 0, []))[1]
    return dict(clause="C3", seed=seed, deaths=len(L["cause"]), starved_solvent=solvent,
                by_cause="; ".join("%s: %d dead, %d solvent, median res %s J"
                                   % (c, n, s, fmt_cell(median(rs)))
                                   for c, (n, s, rs) in sorted(by.items())),
                held=solvent == 0)


# ---- V: the films, the resume, the record's size

def v1(seed, windows_root, arm):
    if not windows_root:
        return absent("V1", seed, "no --windows-root given")
    found = []
    for p in sorted(glob.glob(os.path.join(windows_root, "**", "identity.jsonl"), recursive=True)):
        last = None
        for r in stream_jsonl(p):
            last = r
        if not last or last.get("arm") != arm or "verdict" not in last:
            continue
        faithful = (last.get("verdict") == "faithful" and not last.get("sourcesDiffer")
                    and last.get("configDiffers") is None and last.get("partedAt") is None
                    and (last.get("rows") or 0) > 0 and last.get("rowsAgreed") == last.get("rows"))
        found.append((os.path.relpath(os.path.dirname(p), windows_root), last, faithful))
    if not found:
        return absent("V1", seed, "no film window of %s under %s" % (arm, windows_root))
    n = sum(1 for f in found if f[2])
    return dict(clause="V1", seed=seed, windows=len(found), faithful=n,
                listed="; ".join("%s %s-%s s %s, %s/%s rows, sources differ %d" % (
                    w, r.get("from"), r.get("to"), r.get("verdict"), r.get("rowsAgreed"),
                    r.get("rows"), len(r.get("sourcesDiffer") or [])) for w, r, _ in found[:6]),
                note="the window's exit code is not in its record; the verdict row stands for it",
                held=n >= 1)


V2_LINE = re.compile("(verify-checkpoint|compare-det) exit (-?[0-9]+)")


def v2(seed, v2_log):
    """Seed 1 only: the verdicts a V2 check wrote as `verify-checkpoint exit N` and
    `compare-det exit N` lines (the last of each counts)."""
    if not v2_log:
        return absent("V2", seed, "no --v2-log given")
    if not os.path.exists(v2_log):
        return absent("V2", seed, "no file %s" % v2_log)
    got = {}
    with open(v2_log, encoding="utf-8", errors="replace") as f:
        for ln in f:
            m = V2_LINE.search(ln)
            if m:
                got[m.group(1)] = int(m.group(2))
    if len(got) < 2:
        return absent("V2", seed, "the log names %s and not both checks" % (sorted(got) or "neither"))
    return dict(clause="V2", seed=seed, log=v2_log, verify_checkpoint_exit=got["verify-checkpoint"],
                compare_det_exit=got["compare-det"],
                held=got["verify-checkpoint"] == 0 and got["compare-det"] == 0)


def v3(seed, d, ts):
    total = 0
    for root_, _, files in os.walk(d):
        for f in files:
            try:
                total += os.path.getsize(os.path.join(root_, f))
            except OSError:
                pass
    return dict(clause="V3", seed=seed, bytes=total, gb=total / 1e9, at=ts[-1],
                short=ts[-1] < END_SECONDS, held=total < V3_BYTES)


# ---- M4: the stomach a passenger

def m4(seed, L, last_abs):
    clades = L.get("m3_clades")
    if clades is None:
        return absent("M4", seed, "M3 was not read")
    if not clades:
        return absent("M4", seed, "no M3 clade (ten or more living in a line rooted in a "
                                  "stomach bud on a leaf)")
    if last_abs is None or last_abs[0] is None:
        return absent("M4", seed, "no absorptive.jsonl sample by the second read")
    t_abs, rows = last_abs
    held, parts = True, []
    for r, mem in sorted(clades.items(), key=lambda kv: -len(kv[1])):
        shares = []
        for i in mem:
            fl = rows.get(i)
            if fl and fl[0] is not None and fl[1] is not None and fl[0] + fl[1] > 0:
                shares.append(fl[0] / (fl[0] + fl[1]))
        m = median(shares)
        if m is not None and m >= M4_SHARE:
            held = False
        parts.append("%d: %d living, %d with a stomach logged, median share %s"
                     % (r, len(mem), len(shares), fmt_cell(m)))
    return dict(clause="M4", seed=seed, absorptive_sample_at=t_abs, clades=len(clades),
                by_clade="; ".join(parts), held=held)


# ---- O, S, X: a value at the sample against its bar, round 48's same seed beside it

def bar(clause, seed, ts, by_t, cutoff, base, base_name, name, key, test, text):
    t, row, short = last_stats(ts, by_t, cutoff)
    v = num(row, name) if row else None
    if v is None:
        return absent(clause, seed, "%s missing at the sample" % name, at=t)
    bt, bv = base_value(base, name)
    out = dict(clause=clause, seed=seed, at=t, short=short)
    out[key] = v
    out.update(bar=text, r48=base_name, r48_at=bt, r48_value=bv, held=test(v))
    return out


def s2(seed, L):
    if not L["present"]:
        return absent("S2", seed, "no lineage.jsonl")
    kids = [i for i in L["birth"] if not is_founder(L, i) and L["absf"].get(i) == 1
            and not L["pho"].get(i)]
    ages = sorted(L["died"][i] - L["birth"][i] for i in kids if i in L["died"])
    out = dict(clause="S2", seed=seed, stomach_children=len(kids), dead=len(ages),
               still_alive=len(kids) - len(ages), median_age_at_death_s=median(ages),
               quartiles=(None if not ages else "%.0f to %.0f" % (
                   ages[len(ages) // 4], ages[(3 * len(ages)) // 4])))
    if len(ages) < S2_DEAD:
        out["held"] = "absent"
        out["note"] = "fewer than %d dead stomach children: the clause does not ask this seed" % S2_DEAD
    else:
        out["held"] = out["median_age_at_death_s"] < S2_AGE
    return out


def f3(seed, L, pool_reason, pos, read_t):
    if pool_reason:
        return absent("F3", seed, pool_reason)
    if not pos["present"] or pos["last_line"] is None:
        return absent("F3", seed, "no positions row at or before the second read")
    row = json.loads(pos["last_line"])
    ids = [b[0] for b in row.get(NAMES["positions_bodies"], [])]
    pf = set(L["pool_founders"])
    per_root = {}
    for i in ids:
        r = L["root"](i)
        if r in pf:
            per_root[r] = per_root.get(r, 0) + 1
    best = max(per_root, key=per_root.get) if per_root else None
    return dict(clause="F3", seed=seed, at=pos["last_t"], short=pos["last_t"] < read_t,
                living=len(ids), rooted_in_pool=sum(per_root.values()),
                pool_lines_living=len(per_root),
                largest_line=(per_root[best] if best is not None else 0),
                largest_root=best,
                largest_root_pool_index=(L["pool"].get(best) if best is not None else None),
                largest_root_capcut=(L["capcut"].get(best) if best is not None else None),
                largest_root_fsnow=(L["fsnow"].get(best) if best is not None else None),
                held=(per_root[best] if best is not None else 0) < F3_LIVING)


# ---- E1 and EK: the endowment and the founder cap

POOLED = {"EK4": [], "EK6": []}    # per-seed pieces of the two pooled clauses


def group_table(L, ids, value):
    """'group:median(n N)' of value(i) over ids, by founder group (source, or pool index)."""
    return by_index([(founder_group(L, i), value(i)) for i in ids if value(i) is not None])


def age_at_death(L, i):
    return L["died"][i] - L["birth"][i] if i in L["died"] else None


def lag_of(L, i):
    return L["first_child"][i] - L["birth"][i] if i in L["first_child"] else None


def e1(seed, L, config):
    if not L["present"]:
        return absent("E1", seed, "no lineage.jsonl")
    if not L["has_endow"]:
        note = "no founder row carries `endow`"
        if not marker(config, "config_marker_endow"):
            note = pre_build("D122", "config_marker_endow") + "; " + note
        return absent("E1", seed, note)
    founders = sorted(L["src"])
    bred = {}
    for i in founders:
        g = founder_group(L, i)
        n, b = bred.get(g, (0, 0))
        bred[g] = (n + 1, b + (L["children"].get(i, 0) > 0))
    vals = [L["endow"][i] for i in founders if i in L["endow"]]
    return dict(clause="E1", seed=seed, founders=len(founders), endowed=len(vals),
                endow_median_J=median(vals), endow_min_J=min(vals) if vals else None,
                endow_max_J=max(vals) if vals else None,
                endow_by_group=group_table(L, founders, lambda i: L["endow"].get(i)),
                age_at_death_by_group=group_table(L, founders, lambda i: age_at_death(L, i)),
                bred_by_group=" ".join("%s:%d/%d" % (g, b, n) for g, (n, b) in sorted(bred.items())),
                held="reading")


def ek1(seed, L):
    if not L["present"]:
        return absent("EK1", seed, "no lineage.jsonl")
    founders = sorted(L["src"])
    if not founders:
        return absent("EK1", seed, "no founder row")
    near = [i for i in founders if lag_of(L, i) is not None and lag_of(L, i) <= EK1_LAND]
    wide = [i for i in founders if lag_of(L, i) is not None and lag_of(L, i) <= EK1_WIDE]

    def count(ids):
        by = {}
        for i in ids:
            by[founder_group(L, i)] = by.get(founder_group(L, i), 0) + 1
        return " ".join("%s:%d" % kv for kv in sorted(by.items())) or None

    listed = ["%d %s %s lag %g s capcut %s fsnow %s" % (
        i, founder_group(L, i), diet(L, i), lag_of(L, i), fmt_cell(L["capcut"].get(i)),
        fmt_cell(L["fsnow"].get(i))) for i in near[:6]]
    return dict(clause="EK1", seed=seed, founders=len(founders), child_within_1s=len(near),
                within_1s_by_group=count(near), child_within_10s=len(wide),
                within_10s_by_group=count(wide), listed="; ".join(listed) or None,
                held=not near)


def ek2(seed, L, config, ts, by_t, cutoff):
    t, row, short = last_stats(ts, by_t, cutoff)
    if not marker(config, "config_marker_cap"):
        return absent("EK2", seed, pre_build("D124", "config_marker_cap"))
    capped = num(row, NAMES["stats_capped"]) if row else None
    joules = num(row, NAMES["stats_capped_J"]) if row else None
    if capped is None or joules is None:
        return absent("EK2", seed, "foundersCapped or founderJoulesCapped missing at the "
                                   "sample (is the cap off?)", at=t)
    rows = [i for i in L["capcut"] if L["birth"][i] <= t]
    total = sum(L["capcut"][i] for i in rows)
    one = [i for i in L["pool_founders"] if L["pool"].get(i) == ONE_PART and L["birth"][i] <= t]
    outside = [i for i in one if L["capcut"].get(i) is None
               or not EK2_CUT[0] <= L["capcut"][i] <= EK2_CUT[1]]
    cuts = [L["capcut"][i] for i in one if L["capcut"].get(i) is not None]
    count_ok = capped == len(rows)
    joules_ok = abs(joules - total) <= 1e-6 * max(abs(total), 1e-9)
    return dict(clause="EK2", seed=seed, at=t, short=short, founders_capped=capped,
                capcut_rows=len(rows), joules_capped=joules, capcut_sum=total,
                one_part_stomachs=len(one), one_part_outside_band=len(outside),
                one_part_capcut_range=("%.3f to %.3f" % (min(cuts), max(cuts))) if cuts else None,
                outside_listed=" ".join("%d:%s" % (i, fmt_cell(L["capcut"].get(i)))
                                        for i in outside[:6]) or None,
                held=count_ok and joules_ok and not outside)


def one_part(L):
    return [i for i in L["pool_founders"] if L["pool"].get(i) == ONE_PART]


def ek3(seed, L):
    ids = one_part(L)
    if not ids:
        return absent("EK3", seed, "no one-part pool stomach yet")
    ages = [age_at_death(L, i) for i in ids if i in L["died"]]
    m = median(ages)
    return dict(clause="EK3", seed=seed, one_part_stomachs=len(ids), dead=len(ages),
                still_alive=len(ids) - len(ages), median_age_at_death_s=m,
                held=("absent" if m is None else m > EK3_AGE))


def ek4(seed, L):
    ids = one_part(L)
    if not ids:
        return absent("EK4", seed, "no one-part pool stomach yet")
    lags = [lag_of(L, i) for i in ids if lag_of(L, i) is not None]
    POOLED["EK4"].append((len(ids), lags))
    return dict(clause="EK4", seed=seed, one_part_stomachs=len(ids), bred=len(lags),
                share_bred=len(lags) / len(ids), median_first_child_lag_s=median(lags),
                held="pooled")


def ek4_pooled():
    if not POOLED["EK4"]:
        return absent("EK4", "pooled", "no seed read a one-part stomach")
    n = sum(p[0] for p in POOLED["EK4"])
    lags = [g for p in POOLED["EK4"] for g in p[1]]
    share = len(lags) / n
    m = median(lags)
    return dict(clause="EK4", seed="pooled", seeds=len(POOLED["EK4"]), one_part_stomachs=n,
                bred=len(lags), share_bred=share, median_first_child_lag_s=m,
                held=share <= EK4_SHARE and (m is None or m >= EK4_LAG))


def ek5(seed, L, pool_reason):
    if pool_reason:
        return absent("EK5", seed, pool_reason)
    ids = L["pool_founders"]
    if not ids:
        return absent("EK5", seed, "no pool founder yet")
    ages = [age_at_death(L, i) for i in ids if i in L["died"]]
    m = median(ages)
    return dict(clause="EK5", seed=seed, pool_founders=len(ids), dead=len(ages),
                median_age_at_death_s=m,
                by_pool_index=group_table(L, ids, lambda i: age_at_death(L, i)),
                capcut_by_pool_index=group_table(L, ids, lambda i: L["capcut"].get(i)),
                held=("absent" if m is None else m > EK5_AGE))


def ek6(seed, L):
    ids = [i for i in L["pool_founders"] if L["fsnow"].get(i) is not None]
    if not ids:
        return absent("EK6", seed, "no pool founder with fsnow yet")
    bred = [L["fsnow"][i] for i in ids if L["children"].get(i, 0) > 0]
    not_bred = [L["fsnow"][i] for i in ids if not L["children"].get(i, 0)]
    POOLED["EK6"].append((bred, not_bred))
    return dict(clause="EK6", seed=seed, bred=len(bred), not_bred=len(not_bred),
                median_fsnow_bred=median(bred), median_fsnow_not_bred=median(not_bred),
                held="pooled")


def ek6_pooled():
    bred = [x for p in POOLED["EK6"] for x in p[0]]
    not_bred = [x for p in POOLED["EK6"] for x in p[1]]
    if not bred or not not_bred:
        return absent("EK6", "pooled", "no pool founder bred, or every one did",
                      bred=len(bred), not_bred=len(not_bred))
    mb, mn = median(bred), median(not_bred)
    return dict(clause="EK6", seed="pooled", bred=len(bred), not_bred=len(not_bred),
                median_fsnow_bred=mb, median_fsnow_not_bred=mn,
                held=(mb > mn) if len(bred) >= 5 else "reading")


def ek7(seed, L):
    founders = sorted(L["src"])
    if not founders:
        return absent("EK7", seed, "no founder row")
    capped = {}
    for i in founders:
        g = founder_group(L, i)
        n, c = capped.get(g, (0, 0))
        capped[g] = (n + 1, c + (L["capcut"].get(i) is not None))
    return dict(clause="EK7", seed=seed, founders=len(founders),
                capped_by_group=" ".join("%s:%d/%d" % (g, c, n) for g, (n, c) in sorted(capped.items())),
                capcut_by_group=group_table(L, founders, lambda i: L["capcut"].get(i)),
                endow_by_group=group_table(L, founders, lambda i: L["endow"].get(i)),
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
    reason = manifest.get(NAMES["manifest_reason"]) if manifest else None
    on_budget = status == "running" or (status == "ended" and reason == "budget")
    held = (on_budget and not censored and audit_absmax is not None and audit_absmax < L9_AUDIT
            and resid is not None and abs(resid) < L9_MATTER and div == 0)
    return dict(clause="B1", seed=seed, audit_absmax_J=audit_absmax, matter_resid_at=t,
                matter_resid=resid, short=short, diverged=div, diverged_dump_files=dumps,
                manifest_status=status, manifest_reason=reason, ends_on_budget=on_budget,
                censored=censored, provisional=status == "running",
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
            pos = dict(present=runrec.has_positions(d), l6=[])
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
                        r"ceiling \d+", r"founder cap (?:\S+ of the gate|off)"):
            m = re.search(r"(?:^| |, )(%s)" % pattern, header)
            if m:
                tokens.append(m.group(1).strip())
    last = by_t[ts[-1]]
    by_src = {}
    for s in L["src"].values():
        by_src[s] = by_src.get(s, 0) + 1
    markers = [k for k in ("config_marker_bud", "config_marker_gestation", "config_marker_senescence",
                           "config_marker_endow", "config_marker_depth", "config_marker_cap")
               if marker(config, k)]
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
        return "r48-s%s" % m.group(1)
    seed = (manifest or {}).get(NAMES["manifest_seed"])
    return ("r48-s%s" % seed) if seed is not None else None


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

    pool_births = {}
    if not pool_reason and L["present"]:
        pool_births = {i_: L["birth"][i_] for i_ in L["pool_founders"]}
    want_first = dict(pool_births)
    if L["present"]:
        for f in w1_failures(L)[1]:
            want_first[f[0]] = L["birth"][f[0]]
    pos = positions_pass(d, cutoff, read_t, reefs, bed, want_first, args.stride, args.l6_band)
    first, last_abs = absorptive_pass(d, cutoff, read_t, pool_births)

    base_name = baseline_for(arm, i, args, manifest)
    base = None
    base_l5 = base_l6 = None
    if base_name:
        try:
            bd = run_dir(base_root, base_name)
            base = read_stats(bd, float("inf"))
            if not base[0]:
                base = None
            elif not args.no_baseline_reefs:
                bconfig = load_json(os.path.join(bd, "config.json")) or {}
                bmanifest = load_json(os.path.join(bd, "run.json")) or {}
                base_l5, base_l6 = reef_readings(base_name, bd, bconfig, bmanifest,
                                                 float("inf"), END_SECONDS, args)
        except SystemExit:
            base = None

    t_last, last, _ = last_stats(ts, by_t, cutoff)
    r5, r6 = reef_readings(arm, d, config, manifest, cutoff, read_t, args, pos)
    g3, g4 = g34(arm, L, config, ts, by_t, cutoff)
    near = (arm, ts, by_t, cutoff, base, base_name)

    rows = [
        context_row(arm, ts, by_t, L, header, pool_reason, config, manifest, base_name),
        w1(arm, L, pos, bed, config),
        w2(arm, L, first),
        w3(arm, L),
        g1(arm, L, config, ts, by_t, cutoff),
        g2(arm, L, config, ts, cutoff),
        g3,
        g4,
        c1(arm, d, ts, by_t, cutoff),
        c2(arm, d, manifest, last, args.logs_dir, arm),
        c3(arm, L, config),
        b1(arm, ts, by_t, manifest, cutoff, d),
        v1(arm, args.windows_root, arm),
        (v2(arm, args.v2_log) if arm.endswith("-s1") or args.v2_any_arm
         else absent("V2", arm, "asked of seed 1 alone")),
        v3(arm, d, ts),
        m1(arm, L, config),
        m2(arm, L, config, last),
        m3(arm, L, config, pos, read_t),
        m4(arm, L, last_abs),
        bar("O1", *near, NAMES["stats_alive"], "alive",
            lambda v: O1_BAND[0] <= v <= O1_BAND[1], "4,000 to 16,000"),
        o2b(arm, ts, by_t, config, manifest, cutoff),
        bar("O3", *near, NAMES["stats_jointed"], "jointed",
            lambda v: v < O3_JOINTED, "under 100"),
        bar("S1", *near, NAMES["stats_age"], "mean_age_s",
            lambda v: v > S1_AGE, "above 1,500 s"),
        f3(arm, L, pool_reason, pos, read_t),
        e1(arm, L, config),
        as_reading(r5, "R1a", base_l5),
        as_reading(r6, "R1b", base_l6),
        p1(arm, ts, by_t, manifest, report, cutoff, args.window),
        ek1(arm, L),
        ek2(arm, L, config, ts, by_t, cutoff),
        ek3(arm, L),
        ek4(arm, L),
        ek5(arm, L, pool_reason),
        ek6(arm, L),
        ek7(arm, L),
        s2(arm, L),
        bar("X1", *near, NAMES["stats_det_cv"], "det_cv",
            lambda v: X1_BAND[0] <= v <= X1_BAND[1], "0.4 to 1.0"),
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
    "W1": "3 of 3", "W2": "3 of 3", "W3": "3 of 3", "G1": "3 of 3", "G2": "2 of 3",
    "G3": "2 of 3", "G4": "3 of 3", "C1": "a reading", "C2": "3 of 3", "C3": "3 of 3",
    "B1": "3 of 3", "V1": "a window in each of 3 seeds", "V2": "seed 1", "V3": "3 of 3",
    "M1": "3 of 3", "M2": "3 of 3", "M3": "1 of 3 or more", "M4": "every seed M3 holds in",
    "O1": "3 of 3", "O2": "3 of 3", "O3": "2 of 3", "S1": "3 of 3", "F3": "3 of 3",
    "E1": "a reading", "R1a": "a reading", "R1b": "a reading", "P1": "3 of 3",
    "EK1": "3 of 3", "EK2": "3 of 3", "EK3": "2 of 3", "EK4": "pooled over the seeds",
    "EK5": "3 of 3", "EK6": "pooled, a reading under five breeders", "EK7": "a reading",
    "S2": "every seed with ten dead", "X1": "3 of 3",
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
    ap.add_argument("--runs-root", default=_default_runs_root(),
                    help="defaults to <repo>/runs, or the main tree's from a scratch/wt-* worktree")
    ap.add_argument("--arms", nargs="+", default=DEFAULT_ARMS)
    ap.add_argument("--baseline-runs-root", default=_default_runs_root(),
                    help="where round 48's runs are, for O1, O3, S1, X1 and R1; defaults as --runs-root")
    ap.add_argument("--baseline-arms", nargs="+", default=None,
                    help="round 48's arms in the order of --arms; default r48-s<N> for seed N")
    ap.add_argument("--no-baseline-reefs", action="store_true",
                    help="skip R1's readings of round 48's run (a pass over its positions)")
    ap.add_argument("--stride", type=float, default=L6_STRIDE,
                    help="R1b reads the positions rows at multiples of this many seconds")
    ap.add_argument("--l6-band", type=float, default=L6_BAND,
                    help="R1b's depth band under the caps' underside, m")
    ap.add_argument("--window", type=float, default=WINDOW,
                    help="P1's window reading: the seconds read before the second read")
    ap.add_argument("--logs-dir", default=os.path.join(REPO, "scratch", "logs"),
                    help="where C2 finds <arm>.err; defaults to <repo>/scratch/logs")
    ap.add_argument("--windows-root", default=None,
                    help="V1: the film windows recorded from this round's checkpoints")
    ap.add_argument("--v2-log", default=None,
                    help="V2: the log of seed 1's checkpoint check and resume comparison")
    ap.add_argument("--v2-any-arm", action="store_true",
                    help="ask V2 of an arm whose name does not end -s1 (a smoke)")
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
            if r["clause"] in by_clause and r["clause"] not in POOLED:
                by_clause[r["clause"]].append(r.get("held"))
            print(show(r))
        print()

    for r in (ek4_pooled(), ek6_pooled()):
        all_rows.append(r)
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
