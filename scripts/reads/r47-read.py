#!/usr/bin/env python3
"""The clause reader for round 47 (logbook/specs/r47-prereg-draft.md, L1 to L11): round 46's
world with the mushroom reefs (logbook/specs/reef-spec.md) and the trickle's pool (D117, one
trickle founder in ten a copy of a stored stomach).

Modelled on round 46's reader (scripts/reads/r46-read.py): the same run-directory layout, the
same tolerant reading (a missing field prints 'absent' rather than raising, a half-written last
line of a live file is dropped), scripts/reads/contact_aliases.py's `field()` for every
stats.jsonl lookup, the censored-manifest rule, one row per seed per clause and a
`held in N of M` line per clause. Where a clause has two halves read from different records,
each half is its own row (L8a/L8b, L10a/L10b), so a half the record cannot answer prints
'absent' without taking the other half with it.

NAMES below is the one dictionary of what this script reads. Round 46's names were confirmed
against main's source on 2026-09-23 (src/Evosim.Farm/Row.cs, Report.cs, Manifest.cs,
src/Evosim.Core/Serialization/PositionsRow.cs, src/Evosim.Core/Ecosystem/LineageEvent.cs). The
reef's and the pool's were confirmed the same night against the two worktrees they were built
in, before either was merged: the reef in .claude/worktrees/agent-a672d6e6d5fecf25d
(uncommitted), the pool in .claude/worktrees/agent-ab8d64597f3e8e255 (commit 213b894). A merge
that renames any of them breaks this reader loudly on a round 47 run and not at all on round
46's, so the first smoke of the merged build is read with this script before the launch. The
rebuilt reef's names (D118) were confirmed against main at 73ab4ea on 2026-09-23 night:
src/Evosim.Farm/Manifest.cs's writer, src/Evosim.Core/RunConfig.cs's `reef` group and
src/Evosim.Core/Environment/ReefGeometry.cs.

The reef's geometry is the rebuilt reef of D118 (main from 9a467ce), ReefGeometry.OfReef ported
line for line, the value only and not its derivatives. Each reef has its own axis, mean radius
r, cap-top depth (positive down, so the top is at y = -depth), stem radius and lobed outline
r(theta) = r*(1 + a2 cos(2 theta + p2) + a3 cos(3 theta + p3) + a4 cos(4 theta + p4)), theta
from +x toward +z. The cap is sqrt(q^2 + v^2) - t/2 where q = (rho - r(theta))*c(theta) + t/2 is
positive and |v| - t/2 over the flat blob, with c = r/sqrt(r^2 + r'^2) the outline's slope
cosine and v the height over the cap's mid-plane (a round reef takes the exact rounded-disc
branch, which is the same arithmetic at c = 1); the stem a cylinder from below the floor to the
mid-plane; the two joined by the cubic smooth minimum with a fillet of t/2; the rock the union,
its distance the minimum over the reefs. Every reef comes from run.json's `reefs` list, which
Manifest.RecordReefs fills from the built world (x, z, r, depth, stem, a2..a4, p2..p4,
noiseSeed; `reefCover` and `reefCoverGot` after it); the thickness and the fade from
config.json's `reef` group, which carries no count (the count is the list's length). A config
with `reefCount` is the first build's round reefs, which this reader no longer reads: every
reef clause prints absent with that note. Every place is in the tank's own frame, the frame
positions.jsonl and the snow's columns share.

How each clause is read, and what it cannot be read from as the draft words it:

  - L1, L2, L3 walk lineage.jsonl. A pool founder is a founder row with `src` "pool". L1 is the
    median age at death of the pool founders that died by the second read; the ones still alive
    are counted beside it and left out of the median, which a crowd of long-lived founders would
    bias low. L3 counts the ids in the positions row at the second read whose root, walked up
    the parent chain, is a pool founder.
  - L4 places each pool founder that bred at its first positions row at or after its birth, and
    reads the snow dump at or before that second in its column: the column's joules over its
    water, where the water is the depth from fields/bed.f32 less the rock a reef puts in that
    column. Held when every breeder read 1 J/m3 or more; the share is printed beside it. The
    rock in a column is the union over the reefs of each cap's vertical extent at the column's
    centre and each stem's (floor to its cap's mid-plane) where the centre is inside the stem;
    the smooth join's fillet is left out, and the grid masks a cell by its centre, so a 1 m
    grid differs from this by under a cell.
  - L5 cannot be read as worded. The snow is dumped as column sums, and a table's column holds
    the water on the table, the cap's rock (dead cells, no snow) and the shaded room under it,
    down to the floor. So no record says how much snow lies on a table. The row prints two
    readings of the whole column against the open floor's: the column's joules over its water
    (the rock taken out) and its joules over its area. Both are the tables' snow plus the
    room's, and the second assumes nothing about where in the column the snow sits. A table
    column is one whose centre is inside any cap's outline; the open floor is the columns off
    the shelf whose centre is outside every reef's outline scaled by two plus the fade
    (rho > 2 r(theta) + fade for every reef: "a cap radius beyond the rim, and the fade past
    it", with the outline scaled as the radius was).
  - L6 counts bodies per cubic metre in a depth band under the caps and beside them. Under is
    inside any cap's outline; beside is inside no outline and inside some reef's outline
    scaled by two (between r(theta) and 2 r(theta)). The band at a place is the union, over
    the reefs it is under (or beside), of each cap's own underside down --l6-band metres (10
    by default, the lit water where round 46's crowd lived), less the rock, and never under
    the floor. The volumes are summed over the 1 m columns of the bed's grid at their
    centres, so a body count and its volume are read on the same lattice; a stem's column is
    rock through its own cap's band and adds nothing. The draft says "at the same depth" and
    names no band; the band is this script's choice and is printed on the row.
  - L7 reads "under a cap" as ReefGeometry.LightTransmission does: inside some cap's outline and
    below that cap's own top, which is where the world shades the light. A stomach is a
    body whose birth row reads `ink` 1, or `abs` 1 with `pho` 0; positions.jsonl carries the
    abs, jnt and pho bits and no `ink`, so the join to the lineage is what names a stomach.
  - L8a reads every positions row for a root inside the rock by more than --l8-tolerance metres
    (0.5 by default; the farm's own guard, Divergence.cs's guard 3b, kills a root deeper than
    the body's bounding radius, which no file records), and every divergence dump for the
    guard's reason, "inside reef". L8b is `bedOrGlassBodiesPerStep` over the last --window
    seconds against round 46's same seed at the window with the nearest mean crowd. The reef's
    contacts set the same flag as the bed's and the glass's (Contacts.cs), so the column is the
    three together and not the rock alone; it is a reading, and says so.
  - L10a is absent on every run: no record of the build carries the fastest water near a reef.
    The reading is a test's printout (ReefStreamsTests.TheFastestWaterWithinACapRadiusAtThreeFades),
    not a run's. L10b is the pace over the last --window seconds against round 46's same seed at
    the nearest crowd, from `wallTotalMs`: a timing reading, valid only when the two rounds ran
    under the same load (round 46 ran three arms at once, round 47 runs two).
  - L9 is round 46's K8, and L11 reads `meanExposure` at the second read, as K1 does.

A context row per seed prints the crowd, the trickle's and the pool's founders from the stats
and the lineage, the header's trickle, pool and reef tokens, and the reefs' count (the
manifest list's length) with the cover asked and the cover got from the manifest.

Run from anywhere:

    python scripts/reads/r47-read.py                               # r47-s1 r47-s2 r47-s3
    python scripts/reads/r47-read.py 10000 r47-s2                  # the watch's call form
    python scripts/reads/r47-read.py --runs-root scratch/r47-build/runs --arms r47smoke-s1
    python scripts/reads/r47-read.py --out logbook/specs/r47-read/clauses.tsv

The `<second> <arm>` form reads every record up to that second and no further, so a clause
asked at 30,000 s is read at the second given and marked short; it is the state at that mark
and not a verdict. A read takes the newest run directory of an arm. Round 46's runs for L8b and
L10b are found under --baseline-runs-root (runs/ by default) as r46-s<N> for r47's seed N, or
named in order by --baseline-arms. Touches nothing but reads; --out creates only the directory
of the path given, and only then.
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

DEFAULT_ARMS = ["r47-s1", "r47-s2", "r47-s3"]

END_SECONDS = 30000.0
L1_AGE = 300.0                     # L1: median age at death above 300 s
L3_LIVING = 10                     # L3: ten or more living bodies rooted in pool founders
L4_SNOW = 1.0                      # L4: a column whose snow read 1 J/m3 or more
L5_FROM = 10000.0                  # L5, L6: at 10,000 s and after
L6_BAND = 10.0                     # L6: the depth band under the underside, m (this script's)
L6_STRIDE = 500.0                  # L6: the positions rows read, every this many seconds
L8_TOLERANCE = 0.5                 # L8a: a root inside the rock by more than this, m
L8_RATIO = 2.0                     # L8b: under twice round 46's
L9_AUDIT = 0.1                     # L9: audit under 0.1 J
L9_MATTER = 1e-5                   # L9: matter residual under 1e-5 units
L10_PACE = 1.5                     # L10b: the pace within 1.5 of round 46's
L11_EXPO = 1.5                     # L11: expo above 1.5
SHELF = 12.0                       # round 46's shelf: floor within 12 m of the surface
WINDOW = 2000.0                    # L8b, L10b: the window read, s

# ---------------------------------------------------------------------- the one name dictionary
NAMES = {
    # stats.jsonl (main's Row.cs; the pool's two in the pool worktree's Row.cs, written only
    # when the config names a pool). States unless marked cumulative.
    "stats_t": "t",
    "stats_alive": "alive",
    "stats_expo": "meanExposure",            # null with light by exposure off
    "stats_trickle": "trickleSpawns",        # cumulative
    "stats_pool": "poolSpawns",              # cumulative; pool worktree Row.cs, D117
    "stats_pool_window": "poolSpawnsWindow", # the window's; pool worktree Row.cs
    "stats_audit": "auditResidual",          # joules (World.AuditResidual)
    "stats_matter_resid": "matterResidual",  # units (World.MatterResidual)
    "stats_diverged": "diverged",            # cumulative count
    # A window's bodies touching the bed or the glass per physics step (Row.cs); from the reef
    # build the rock sets the same flag (reef worktree Contacts.cs, `TouchedBedOrGlass`).
    "stats_bed_or_glass": "bedOrGlassBodiesPerStep",
    "stats_wall_total": "wallTotalMs",       # cumulative

    # config.json (RunConfigJson: the Tunable group, the key the property's camel case; found
    # by name anywhere in the tree, as r46-read.py does). The reef group: main's RunConfig.cs,
    # [Tunable("reef")], rebuilt on D118: the cover, the ceiling, the radius range, the
    # roughness, the depth and its jitter, the thickness, the stem's fraction and the fade.
    # The reader takes the thickness and the fade from it and the cover as a fallback; every
    # reef's own radius, depth and stem is in the manifest. `reefCount` is the first build's
    # key (gone from main) and marks a first-build reef config. The pool: RunConfig.cs,
    # [Tunable("population")] FoundingTricklePoolShare / FoundingTricklePoolCount.
    "config_shape": "worldShape",
    "config_area": "worldAreaSquareMetres",
    "config_reef_group": "reef",
    "config_reef_cover": "reefCover",
    "config_reef_cap_t": "reefCapThicknessMetres",
    "config_reef_fade": "reefFadeMetres",
    "config_reef_first_build": "reefCount",
    "config_pool_count": "foundingTricklePoolCount",
    "config_pool_share": "foundingTricklePoolShare",

    # run.json (main's Manifest.cs, the writer after the bed's block): `"reefs": [{"x", "z",
    # "r", "depth", "stem", "a2", "a3", "a4", "p2", "p3", "p4", "noiseSeed"}, ...]`, an empty
    # list with no reef, then "reefCover" (asked) and "reefCoverGot" (reached on the grid's
    # columns), 0 with no reef. `depth` is the cap top's depth below the surface, positive
    # (ReefGeometry.Reef.CapDepth; CapTopY = -depth); `r` the cap's mean radius r0; `stem`
    # the stem's radius in metres (0 the floating island); a_k shares of r, p_k radians.
    "manifest_reefs": "reefs",
    "manifest_reef_keys": ("x", "z", "r", "depth", "stem", "a2", "a3", "a4", "p2", "p3", "p4"),
    "manifest_reef_cover": "reefCover", "manifest_reef_cover_got": "reefCoverGot",
    "manifest_status": "status",

    # lineage.jsonl (LineageEvent.ToJson). A founder's `k` is "f" and its `p` -1; `src` is
    # "floor", "trickle" or, from D117 (pool worktree), "pool", and a pool founder's row alone
    # carries `pool`, the index of <run>/pool/NN.json. A death row is e "d" with t and id.
    "lineage_event": "e", "lineage_birth": "b", "lineage_death": "d",
    "lineage_time": "t", "lineage_id": "id", "lineage_parent": "p", "lineage_kind": "k",
    "lineage_founder_kind": "f", "lineage_src": "src", "lineage_pool_src": "pool",
    "lineage_pool_index": "pool", "lineage_trickle": "trickle", "lineage_floor": "floor",
    "lineage_ink": "ink", "lineage_abs": "abs", "lineage_pho": "pho",

    # positions.jsonl (PositionsRow.Write): {"t":..,"n":..,"b":[[id, x, y, z, flags], ...]};
    # flags: AbsorptiveBit 1, JointedBit 2, PhotosyntheticBit 4 (PositionsRow.cs).
    "positions_bodies": "b", "flag_abs": 1, "flag_jnt": 2, "flag_pho": 4,

    # fields/ (Row.cs DumpFields): layout.json's "snowColumns" and "bed" (cellsX, cellsZ,
    # cellMetres, order "ix * cellsZ + iz"); NNNNNNNNN.snow-columns.f32, each column's snow
    # in joules; bed.f32, the floor's y under each column, negative down.
    "fields_layout": "layout.json", "fields_bed": "bed.f32", "fields_snow": "snow-columns",

    # diverged/ (Simulation.cs's dump directory); the reef guard's reason (reef worktree
    # Divergence.cs, guard 3b) begins "inside reef".
    "diverged_dir": "diverged", "diverged_reef_reason": "inside reef",

    # The report: the header line starts "engine=" (Report.cs). Its tokens: the reef's is
    # ReefGeometry.HeaderToken ("reefs 15 cover 0.25 (0.251 got) cap r=6-16 m rough 0.15 at
    # 3 m ±1 t=2 m stem 0.25 fade 8 m", or "no reef"; the first build's "reefs 3 cap r=4 m at
    # 3 m t=2 m stem r=1 m fade 15 m" is still matched on its runs), the pool's Report.PoolToken ("pool 0.1 of 4", only when a pool is named),
    # the trickle's Report.TrickleToken. The footer's pace: "(2.2x real time)".
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
    """Every birth and death up to cutoff, held whole (a round's lineage is a few MB)."""
    path = os.path.join(d, "lineage.jsonl")
    N = NAMES
    L = dict(present=os.path.exists(path), birth={}, parent={}, src={}, pool={}, ink={},
             absf={}, pho={}, died={}, children={})
    if not L["present"]:
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
            L["ink"][i] = row.get(N["lineage_ink"], 0)
            L["absf"][i] = row.get(N["lineage_abs"], 0)
            L["pho"][i] = row.get(N["lineage_pho"], 0)
            if row.get(N["lineage_kind"]) == N["lineage_founder_kind"] or p is None or p < 0:
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

def positions_pass(d, cutoff, read_t, reefs, bed, L, want_l4, stride, band, tol):
    """One pass over positions.jsonl. Collects the row at the read second (L3, L7), the L6
    samples, the L8a roots in the rock (every row, when there are reefs) and each L4
    breeder's first place after its birth."""
    path = os.path.join(d, "positions.jsonl")
    out = dict(present=os.path.exists(path), last_line=None, last_t=None, l6=[],
               l8_rows=0, l8_inside=[], l8_any_negative=0, l8_deepest=None, l4_place={})
    if not out["present"]:
        return out
    pending = dict(want_l4)            # id -> birth t
    earliest_l4 = min(pending.values()) if pending else None
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
            need_l4 = bool(pending) and t >= earliest_l4
            need_l8 = reefs is not None
            if not (need_l6 or need_l4 or need_l8):
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                break                  # a half-written last line
            bodies = row.get(N["positions_bodies"], [])
            if need_l4:
                for b in bodies:
                    bt = pending.get(b[0])
                    if bt is not None and t >= bt:
                        out["l4_place"][b[0]] = (t, b[1], b[3])
                        del pending[b[0]]
                earliest_l4 = min(pending.values()) if pending else None
            if need_l8:
                out["l8_rows"] += 1
                for b in bodies:
                    x, y, z = b[1], b[2], b[3]
                    near = reefs.near(x, z, tol + 1.0)
                    if not near:
                        continue
                    s = reefs.signed_distance(x, y, z, near)
                    if s < 0.0:
                        out["l8_any_negative"] += 1
                        if out["l8_deepest"] is None or s < out["l8_deepest"]:
                            out["l8_deepest"] = s
                        if s < -tol:
                            out["l8_inside"].append((t, b[0], round(s, 3)))
            if need_l6:
                out["l6"].append(l6_count(t, bodies, reefs, bed, l6_geom))
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


# ---------------------------------------------------------------------- the clauses

def absent(clause, seed, note, **extra):
    r = dict(clause=clause, seed=seed, held="absent", note=note)
    r.update(extra)
    return r


def l1(seed, L, pool_reason):
    if pool_reason:
        return absent("L1", seed, pool_reason)
    if not L["present"]:
        return absent("L1", seed, "no lineage.jsonl")
    founders = L["pool_founders"]
    if not founders:
        return absent("L1", seed, "the config names a pool and no founder row reads src pool yet")
    ages = [L["died"][i] - L["birth"][i] for i in founders if i in L["died"]]
    alive = sum(1 for i in founders if i not in L["died"])
    m = median(ages)
    out = dict(clause="L1", seed=seed, pool_founders=len(founders), dead=len(ages),
               still_alive=alive, median_age_at_death_s=m,
               quartiles=(None if not ages else "%.0f to %.0f" % (
                   sorted(ages)[len(ages) // 4], sorted(ages)[(3 * len(ages)) // 4])))
    if m is None:
        out["held"] = "absent"
        out["note"] = "no pool founder has died yet"
    else:
        out["held"] = m > L1_AGE
    return out


def l2(seed, L, pool_reason):
    if pool_reason:
        return absent("L2", seed, pool_reason)
    founders = L["pool_founders"]
    if not founders:
        return absent("L2", seed, "no pool founder yet")
    bred = [i for i in founders if L["children"].get(i, 0) > 0]
    by_index = {}
    for i in bred:
        k = L["pool"].get(i)
        by_index[k] = by_index.get(k, 0) + 1
    return dict(clause="L2", seed=seed, pool_founders=len(founders), bred=len(bred),
                children=sum(L["children"].get(i, 0) for i in founders),
                bred_by_pool_index=(" ".join("%s:%d" % (k, v) for k, v in sorted(
                    by_index.items(), key=lambda kv: str(kv[0]))) or None),
                held=bool(bred))


def l3(seed, L, pool_reason, pos, read_t):
    if pool_reason:
        return absent("L3", seed, pool_reason)
    if not pos["present"] or pos["last_line"] is None:
        return absent("L3", seed, "no positions row at or before the second read")
    row = json.loads(pos["last_line"])
    ids = [b[0] for b in row.get(NAMES["positions_bodies"], [])]
    pf = set(L["pool_founders"])
    rooted = [i for i in ids if L["root"](i) in pf]
    by_index = {}
    for i in rooted:
        k = L["pool"].get(L["root"](i))
        by_index[k] = by_index.get(k, 0) + 1
    return dict(clause="L3", seed=seed, at=pos["last_t"], short=pos["last_t"] < read_t,
                living=len(ids), rooted_in_pool=len(rooted),
                of_them_stomachs=sum(1 for i in rooted if is_stomach(L, i)),
                by_pool_index=(" ".join("%s:%d" % kv for kv in sorted(by_index.items(),
                                                                        key=lambda kv: str(kv[0])))
                               or None),
                held=len(rooted) >= L3_LIVING)


def l4(seed, L, pool_reason, pos, bed, reefs, dumps):
    if pool_reason:
        return absent("L4", seed, pool_reason)
    bred = [i for i in L["pool_founders"] if L["children"].get(i, 0) > 0]
    if not bred:
        return absent("L4", seed, "no pool founder bred, so the clause has no subject")
    if bed is None:
        return absent("L4", seed, "no fields/bed.f32 or layout.json")
    if not dumps:
        return absent("L4", seed, "no snow-columns dump")
    sl = bed["snow_layout"]
    if not sl or sl["cellsX"] != bed["nx"] or sl["cellsZ"] != bed["nz"]:
        return absent("L4", seed, "the snow's columns and the bed's do not share a layout")
    reads = []
    unplaced = 0
    cache = {}
    for i in bred:
        place = pos["l4_place"].get(i)
        if place is None:
            unplaced += 1
            continue
        t, x, z = place
        earlier = [s for s in dumps if s <= t]
        if not earlier:
            unplaced += 1
            continue
        s = max(earlier)
        if s not in cache:
            cache[s] = read_floats(dumps[s])
        c = bed["cell"]
        ix = min(bed["nx"] - 1, max(0, int(x // c)))
        iz = min(bed["nz"] - 1, max(0, int(z // c)))
        water = column_water(bed, reefs, ix, iz)
        if not water > 0:
            unplaced += 1
            continue
        reads.append((i, t, s, cache[s][ix * bed["nz"] + iz] / (water * c * c)))
    if not reads:
        return absent("L4", seed, "no breeding pool founder has a positions row and a dump "
                                  "at or before it", bred=len(bred))
    over = sum(1 for r in reads if r[3] >= L4_SNOW)
    return dict(clause="L4", seed=seed, bred=len(bred), placed=len(reads), unplaced=unplaced,
                at_or_over_1=over, share=over / len(reads),
                snow_J_m3=" ".join("%d@%gs:%.3g" % (r[0], r[1], r[3]) for r in reads[:12]),
                note="column mean (joules over the column's water), dump at or before the "
                     "founder's first positions row",
                held=over == len(reads))


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


def l7(seed, reefs, reef_reason, pos, L, read_t):
    if reef_reason:
        return absent("L7", seed, reef_reason)
    if not pos["present"] or pos["last_line"] is None:
        return absent("L7", seed, "no positions row at or before the second read")
    row = json.loads(pos["last_line"])
    under = [b for b in row.get(NAMES["positions_bodies"], [])
             if reefs.under_cap_light(b[1], b[2], b[3])]
    photo = [b for b in under if b[4] & NAMES["flag_pho"]]
    stomachs = [b for b in under if is_stomach(L, b[0])]
    return dict(clause="L7", seed=seed, at=pos["last_t"], short=pos["last_t"] < read_t,
                under_caps=len(under), photosynthetic=len(photo), stomachs=len(stomachs),
                other=len(under) - len(stomachs),
                note="under a cap is ReefGeometry.LightTransmission's rule: inside a cap's "
                     "outline and below that cap's own top; a stomach is ink 1, or abs 1 with pho 0",
                held=not photo and len(stomachs) == len(under))


def diverged_reef_dumps(d):
    ddir = os.path.join(d, NAMES["diverged_dir"])
    if not os.path.isdir(ddir):
        return 0, 0
    files = hits = 0
    for p in glob.glob(os.path.join(ddir, "**", "*"), recursive=True):
        if not os.path.isfile(p):
            continue
        files += 1
        try:
            with open(p, encoding="utf-8", errors="replace") as f:
                if NAMES["diverged_reef_reason"] in f.read(200000):
                    hits += 1
        except OSError:
            pass
    return files, hits


def l8a(seed, reefs, reef_reason, pos, d, tol):
    if reef_reason:
        return absent("L8a", seed, reef_reason)
    if not pos["present"]:
        return absent("L8a", seed, "no positions.jsonl")
    files, hits = diverged_reef_dumps(d)
    inside = pos["l8_inside"]
    return dict(clause="L8a", seed=seed, rows_read=pos["l8_rows"], tolerance_m=tol,
                roots_inside_past_tolerance=len(inside),
                first=(("%g s id %s at %s m" % inside[0]) if inside else None),
                roots_with_negative_distance=pos["l8_any_negative"],
                deepest_m=pos["l8_deepest"], diverged_dumps=files, dumps_inside_reef=hits,
                held=not inside and hits == 0)


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


def nearest_crowd(base, crowd, width, name):
    """Round 46's window with the mean crowd nearest the given one."""
    ts, by_t = base
    best = None
    for t in ts:
        w = window_stats(ts, by_t, t, width, name)
        if w is None or w[2] is None:
            continue
        if best is None or abs(w[2] - crowd) < abs(best[2] - crowd):
            best = w
    return best


def l8b(seed, reefs, reef_reason, ts, by_t, t_end, base, base_name, width):
    w = window_stats(ts, by_t, t_end, width, NAMES["stats_bed_or_glass"])
    value = w[3] if w else None
    if reef_reason:
        return absent("L8b", seed, reef_reason, bed_or_glass_per_step=value)
    if w is None or value is None:
        return absent("L8b", seed, "bedOrGlassBodiesPerStep missing over the window")
    if base is None:
        return absent("L8b", seed, "no round 46 run for this seed (%s)" % base_name,
                      bed_or_glass_per_step=value)
    b = nearest_crowd(base, w[2], width, NAMES["stats_bed_or_glass"])
    if b is None or b[3] is None:
        return absent("L8b", seed, "round 46's run has no bedOrGlassBodiesPerStep",
                      bed_or_glass_per_step=value)
    ratio = value / b[3] if b[3] > 0 else float("inf")
    return dict(clause="L8b", seed=seed, window="%g to %g s" % (w[0], w[1]), crowd=w[2],
                bed_or_glass_per_step=value, baseline=base_name,
                baseline_window="%g to %g s" % (b[0], b[1]), baseline_crowd=b[2],
                baseline_per_step=b[3], ratio=ratio,
                note="a reading: the column counts the bed, the glass and the rock together",
                held=ratio < L8_RATIO)


def l9(seed, ts, by_t, manifest, cutoff):
    audits = [num(by_t[t], NAMES["stats_audit"]) for t in ts]
    audits = [a for a in audits if a is not None]
    audit_absmax = max(abs(a) for a in audits) if audits else None
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    resid = num(by_t[t], NAMES["stats_matter_resid"])
    div = num(by_t[ts[-1]], NAMES["stats_diverged"])
    status = manifest.get(NAMES["manifest_status"]) if manifest else None
    censored = status in ("error", "stopped")
    held = (not censored and audit_absmax is not None and audit_absmax < L9_AUDIT
            and resid is not None and abs(resid) < L9_MATTER and div == 0)
    return dict(clause="L9", seed=seed, audit_absmax_J=audit_absmax, matter_resid_at=t,
                matter_resid=resid, short=short, diverged=div, manifest_status=status,
                censored=censored, provisional=status == "running", held=held)


def l10a(seed, reef_reason):
    if reef_reason:
        return absent("L10a", seed, reef_reason)
    return absent("L10a", seed, "no run record carries the fastest water near a reef: no "
                                "stats field, report column or manifest field of the reef "
                                "build names it; the reading is ReefStreamsTests' printout")


def l10b(seed, ts, by_t, t_end, base, base_name, report, width, same_run, whole):
    w = window_stats(ts, by_t, t_end, width, None)
    footer = None
    if report and whole:
        # The footer is the run's whole wall: printed beside the window, never read as it.
        for line in report:
            m = re.search(NAMES["footer_pace"], line)
            if m and "wall clock" in line:
                footer = float(m.group(1))
    if w is None or w[4] is None:
        return absent("L10b", seed, "wallTotalMs missing over the window", footer_x_real=footer)
    if base is None:
        return absent("L10b", seed, "no round 46 run for this seed (%s)" % base_name,
                      pace_x_real=w[4], footer_x_real=footer)
    b = nearest_crowd(base, w[2], width, None)
    if b is None or b[4] is None:
        return absent("L10b", seed, "round 46's run has no wallTotalMs", pace_x_real=w[4])
    slower = b[4] / w[4] if w[4] > 0 else float("inf")
    out = dict(clause="L10b", seed=seed, window="%g to %g s" % (w[0], w[1]), crowd=w[2],
               pace_x_real=w[4], footer_x_real=footer, baseline=base_name,
               baseline_window="%g to %g s" % (b[0], b[1]), baseline_crowd=b[2],
               baseline_pace_x_real=b[4], slower_by=slower,
               note="a timing reading; valid only under the same load as round 46's",
               held=slower <= L10_PACE)
    if same_run:
        out["note2"] = "the baseline is this run itself"
    return out


def l11(seed, ts, by_t, cutoff):
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    v = num(by_t[t], NAMES["stats_expo"]) if t is not None else None
    if v is None:
        return absent("L11", seed, "meanExposure is null or missing at the sample", at=t)
    return dict(clause="L11", seed=seed, at=t, short=short, expo=v, held=v > L11_EXPO)


def context_row(seed, ts, by_t, L, header, reef_reason, pool_reason, manifest):
    tokens = []
    if header:
        for pattern in (r"trickle \S+(?: s)?", r"pool \S+ of \d+",
                        (r"reefs \d+ cover \S+ \(\S+ got\) cap r=\S+ m rough \S+ at \S+ m "
                         r"±\S+ t=\S+ m stem \S+ fade \S+ m"),
                        r"reefs \d+ cap r=\S+ m at \S+ m t=\S+ m stem r=\S+ m fade \S+ m",
                        r"no reef", r"founders (?:in their food|in matter|anywhere)"):
            m = re.search(r"(?:^| |, )(%s)" % pattern, header)
            if m:
                tokens.append(m.group(1).strip())
    listed = (manifest or {}).get(NAMES["manifest_reefs"])
    last = by_t[ts[-1]]
    by_src = {}
    for s in L["src"].values():
        by_src[s] = by_src.get(s, 0) + 1
    return dict(clause="ctx", seed=seed, last_t=ts[-1], alive=num(last, NAMES["stats_alive"]),
                trickle_spawns=num(last, NAMES["stats_trickle"]),
                pool_spawns=num(last, NAMES["stats_pool"]),
                founders_by_src=" ".join("%s:%d" % (k, v) for k, v in sorted(
                    by_src.items(), key=lambda kv: str(kv[0]))) or None,
                reef=reef_reason or "on", pool=pool_reason or "on",
                reef_count=len(listed) if isinstance(listed, list) else None,
                reef_cover=(manifest or {}).get(NAMES["manifest_reef_cover"]),
                reef_cover_got=(manifest or {}).get(NAMES["manifest_reef_cover_got"]),
                header_tokens="; ".join(tokens) if tokens else None)


# ---------------------------------------------------------------------- driving it over the arms

def read_stats(d, cutoff):
    rows = [r for r in stream_jsonl(os.path.join(d, "stats.jsonl"))
            if r.get(NAMES["stats_t"], 0) <= cutoff]
    by_t = {r[NAMES["stats_t"]]: r for r in rows}
    return sorted(by_t), by_t


def baseline_for(arm, i, args):
    if args.baseline_arms:
        if i < len(args.baseline_arms):
            return args.baseline_arms[i]
        return None
    m = re.search(r"-s(\d+)$", arm)
    return ("r46-s%s" % m.group(1)) if m else None


def read_arm(runs_root, arm, cutoff, args, base_name, base_root):
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
    dumps = snow_dumps(d, cutoff)

    want_l4 = {}
    if not pool_reason and L["present"]:
        want_l4 = {i: L["birth"][i] for i in L["pool_founders"] if L["children"].get(i, 0) > 0}
    need_pass = (reefs is not None) or (not pool_reason)
    if need_pass:
        pos = positions_pass(d, cutoff, read_t, reefs, bed, L, want_l4, args.stride,
                             args.l6_band, args.l8_tolerance)
    else:
        pos = dict(present=False)

    base = None
    same_run = False
    if base_name:
        try:
            bd = run_dir(base_root, base_name)
            base = read_stats(bd, float("inf"))
            same_run = os.path.normcase(os.path.abspath(bd)) == os.path.normcase(os.path.abspath(d))
            if not base[0]:
                base = None
        except SystemExit:
            base = None
    t_end, _ = sample_at(ts, read_t)
    if t_end is None:
        t_end = ts[-1]

    return [
        context_row(arm, ts, by_t, L, header, reef_reason, pool_reason, manifest),
        l1(arm, L, pool_reason),
        l2(arm, L, pool_reason),
        l3(arm, L, pool_reason, pos, read_t),
        l4(arm, L, pool_reason, pos, bed, reefs, dumps),
        l5(arm, reefs, reef_reason, bed, dumps),
        l6(arm, reefs, reef_reason, bed, pos, args.l6_band),
        l7(arm, reefs, reef_reason, pos, L, read_t),
        l8a(arm, reefs, reef_reason, pos, d, args.l8_tolerance),
        l8b(arm, reefs, reef_reason, ts, by_t, t_end, base, base_name, args.window),
        l9(arm, ts, by_t, manifest, cutoff),
        l10a(arm, reef_reason),
        l10b(arm, ts, by_t, t_end, base, base_name, report, args.window, same_run,
             cutoff == float("inf")),
        l11(arm, ts, by_t, cutoff),
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


CLAUSE_THRESHOLD_TEXT = {
    "L1": "3 of 3", "L2": "3 of 3", "L3": "1 of 3 or more", "L4": "3 of 3 where any breeds",
    "L5": "3 of 3", "L6": "3 of 3", "L7": "3 of 3", "L8a": "3 of 3", "L8b": "3 of 3",
    "L9": "3 of 3", "L10a": "3 of 3", "L10b": "3 of 3", "L11": "3 of 3",
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
                    help="where round 46's runs are, for L8b and L10b; defaults to <repo>/runs")
    ap.add_argument("--baseline-arms", nargs="+", default=None,
                    help="round 46's arms in the order of --arms; default r46-s<N> for seed N")
    ap.add_argument("--stride", type=float, default=L6_STRIDE,
                    help="L6 reads the positions rows at multiples of this many seconds")
    ap.add_argument("--l6-band", type=float, default=L6_BAND,
                    help="L6's depth band under the caps' underside, m")
    ap.add_argument("--l8-tolerance", type=float, default=L8_TOLERANCE,
                    help="L8a: a root inside the rock by more than this many metres")
    ap.add_argument("--window", type=float, default=WINDOW,
                    help="L8b and L10b: the seconds read before the second read")
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
        base_name = baseline_for(arm, i, args)
        try:
            arm_rows = read_arm(runs_root, arm, cutoff, args, base_name, base_root)
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
