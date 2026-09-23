#!/usr/bin/env python3
"""The clause reader for round 46 (logbook/specs/r46-prereg-draft.md, K1 to K11), on the
round's build: light by exposure (D110), the buoyancy offset (D111), the support cost (D113),
contact per part (D114), the founding trickle and founders in their food (D115, D116), the
snow's life (D112) and the beach.

Modelled on round 45's reader (scripts/reads/r45-read.py): the same run-directory layout, the
same tolerant reading (a missing field prints 'absent' rather than raising, a half-written last
line of a live file is dropped), scripts/reads/contact_aliases.py's `field()` for every
stats.jsonl lookup, the censored-manifest rule, one row per seed per clause and a
`held in N of M` line per clause. Where a clause has two halves that are read from different
records, each half is its own row (K6a/K6b, K9a/K9b, K11a/K11b), so a half that the record
cannot answer prints 'absent' without taking the other half with it.

NAMES below is the one dictionary of what this script reads, every name confirmed against the
source on 2026-09-23: src/Evosim.Farm/Report.cs (`BaseColumns`, the footer's split lines),
src/Evosim.Farm/Row.cs (the `.Field(...)` chain, and `DumpFields` for fields/*.f32 and
layout.json), src/Evosim.Farm/Profile.cs (the harness phases), src/Evosim.Core/Ecosystem/
LineageEvent.cs (`ToJson`), src/Evosim.Core/Ecosystem/AbsorptiveSample.cs (`ToJson`) and
src/Evosim.Core/Serialization/GenomeJson.cs (the snapshot row). The report columns are listed
for the words only; every number is read from stats.jsonl, as round 45's reader does.

What the record cannot answer, and so prints 'absent' on every run of this build:

  - K6b, no part farther than 6 m from its root. No file carries a part's distance from its
    root. The snapshot carries the genome (and `moduleCounts` only when a body has copied a
    module), and the developed body is not rebuilt in Python here; `meanReach` is an
    area-weighted mean and cannot bound a maximum.
  - K9b, a kill row's part against the touching link. The kill row (`e: "k"`) carries the
    victim, the attacker, `root`, `parts`, `tj`, `rj` and `ind`, and no part index; no file
    records the overlap pairs.
  - K10, the exposure and support terms against the wall. The harness split's phases are
    reconcile, finite, settle, metabolise, growth and other (Profile.cs): the exposure pass
    runs inside `metabolise` (Metabolise.cs) and the support term inside Core's step, which is
    timed as `world`, so neither has a phase of its own. The row prints the metabolise phase's
    share of the wall beside the 'absent' as a ceiling on the exposure pass alone, not as the
    clause.
  - K11a, the lowest live cell's snow density over the shelf. The farm dumps the snow as column
    sums (`fields/NNNNNNNNN.snow-columns.f32`), not by cell, so the density of a column's lowest
    cell is not recorded. The row prints the column mean over the shelf (the column's joules
    over its water depth, from `fields/bed.f32`) beside the 'absent', labelled as the column
    mean.

And one clause is read on a subset, said on its row:

  - K2 asks every living photosynthetic part's exposure factor. No file records a part's
    factor: `absorptive.jsonl`'s `exposedArea` is one number a body, summed over every part and
    not the leaves alone, and the log holds only bodies with absorptive tissue (capped at 2,000
    a sample), so the leaves are mostly not in it. What is recorded is the root link's world
    rotation in `poses.jsonl` and the root node's shape, dimensions, cell and offset in the
    snapshot, from which the root part's factor is PhenotypePart.ExposureFactor's formula
    exactly (a ratio, so the body's scale does not enter). K2 is read on root leaves only; a
    leaf part that is not a root is not read. K1's pose check is the same computation.

The windows. One stats.jsonl row is one window, as in round 45. K3's and K4's "in 20 windows"
are read as a count of windows, not a consecutive run (the draft says "in 20 windows", where
round 45's J1 and J2 said "for 20 consecutive windows"); the longest consecutive run is printed
beside the count. K4's "last 10,000 s" is the 10,000 s before the second read at. K11b's
"lie over that shelf" is read as more than half of the line's consumer births, each placed at
its first position sample after its birth.

Run from anywhere:

    python scripts/reads/r46-read.py                               # r46-s1 r46-s2 r46-s3
    python scripts/reads/r46-read.py 10000 r46-s2                  # the watch's call form
    python scripts/reads/r46-read.py --runs-root scratch/r46-build/runs --arms r46scr-s1
    python scripts/reads/r46-read.py --out logbook/specs/r46-read/clauses.tsv

The `<second> <arm>` form reads every record up to that second and no further, so a clause
asked at 30,000 s is read at the second given and marked short; it is the state at that mark
and not a verdict. A read takes the newest run directory of an arm. Touches nothing but reads;
--out creates only the directory of the path given, and only then.
"""
import argparse
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
from contact_aliases import model_note  # noqa: E402

DEFAULT_ARMS = ["r46-s1", "r46-s2", "r46-s3"]

END_SECONDS = 30000.0
WINDOW_COUNT = 20                  # K3, K4: "in 20 windows"
K1_EXPO = 1.3                      # K1: expo above 1.3
K1_FOUNDING = (0.9, 1.1)           # K1: "from a founding at 1.0 +- 0.1"
K2_OFFSET = 0.2                    # K2: |buoyancyOffset| above 0.2
K2_MARGIN = 0.3                    # K2: by 0.3 or more
K4_TAIL_SECONDS = 10000.0          # K4: inside the last 10,000 s
K5_CROWD = 500                     # K5: with 500 alive or more
K5_THRESHOLD = 0.01                # K5: attack % / prot % above 1%
K6_REACH = 2.0                     # K6a: reach m below 2 m
K6_AFTER = 5000.0                  # K6a: at every sample after 5,000 s
K7_ALIVE = 1000                    # K7
K8_AUDIT = 0.1                     # K8: audit under 0.1 J
K8_MATTER = 1e-5                   # K8: matter residual under 1e-5 units
K9_OVL = 0.01                      # K9a: ovl/body under round 45's 0.01
K11_FROM = 10000.0                 # K11a: at 10,000 s and after
K11_SHELF = 12.0                   # K11: floor within 12 m of the surface
K11_SNOW = 0.4                     # K11a: 0.4 J/m3, the stomach's break-even

# ---------------------------------------------------------------------- the one name dictionary
NAMES = {
    # Report.cs BaseColumns: the table's words, for the printed rows only.
    "col_expo": "expo", "col_float_off": "float off", "col_support_w": "support W",
    "col_reach_m": "reach m", "col_trickle": "**trickle**", "col_floor": "**floor**",
    "col_killed": "killed", "col_attack_pct": "attack %", "col_prot_pct": "prot %",
    "col_alive": "alive", "col_audit": "audit", "col_mat_resid": "**mat resid**",
    "col_diverged": "diverged", "col_ovl_body": "ovl/body", "col_corpse_eat": "corpse eat",

    # stats.jsonl (Row.cs). States unless marked cumulative.
    "stats_t": "t",
    "stats_alive": "alive",
    "stats_expo": "meanExposure",            # null with the tunable off or no leaf posed
    "stats_float_off": "meanBuoyancyOffset", # null with the offset's price at 0
    "stats_support_w": "supportWatts",       # null with the support price at 0
    "stats_reach": "meanReach",              # null only with no part alive
    "stats_trickle": "trickleSpawns",        # cumulative (trickleSpawnsWindow beside it)
    "stats_floor": "floorSpawns",            # cumulative (floorSpawnsWindow beside it)
    "stats_killed": "partsKilled",           # cumulative -> `killed`
    "stats_corpse_eat": "corpsesEaten",      # cumulative -> `corpse eat`
    "stats_attack": "attackShare",           # a share 0..1 -> `attack %`
    "stats_prot": "protectionShare",         # a share 0..1 -> `prot %`
    "stats_audit": "auditResidual",          # joules (World.AuditResidual)
    "stats_matter_resid": "matterResidual",  # units (World.MatterResidual)
    "stats_diverged": "diverged",            # cumulative count
    # `ovl/body` is not a field: the column is overlapPairsPerStep / alive (Row.cs, the row's
    # own arithmetic), so it is computed here the same way. contact_aliases resolves the Unity
    # name `contactPairsPerStep` to it.
    "stats_ovl_per_step": "overlapPairsPerStep",
    "stats_wall_total": "wallTotalMs",       # cumulative
    "stats_wall_harness": "wallHarnessMs",   # cumulative
    "stats_wall_metabolise": "wallHarnessMetaboliseMs",  # cumulative, Profile.cs's phase

    # config.json (found by name anywhere in the group tree, as positions-read.py does).
    "config_shape": "worldShape",
    "config_area": "worldAreaSquareMetres",

    # lineage.jsonl (LineageEvent.ToJson). A birth row is e "b"; a founder's `k` is "f" and its
    # `p` is -1, and from D115's build every founder row carries `src` "floor" or "trickle".
    "lineage_event": "e", "lineage_birth": "b", "lineage_death": "d", "lineage_kill": "k",
    "lineage_time": "t", "lineage_id": "id", "lineage_parent": "p", "lineage_kind": "k",
    "lineage_founder_kind": "f", "lineage_reproduction_kind": "r",
    "lineage_src": "src", "lineage_trickle": "trickle", "lineage_floor": "floor",
    "lineage_ink": "ink", "lineage_atk": "atk", "lineage_abs": "abs",
    # The kill row's fields: id, by, root, parts, tj, rj, ind. No part index.

    # absorptive.jsonl (AbsorptiveSample.ToJson): per body, not per part. Not read by any
    # clause; named for the docstring's reason K2 cannot use it.
    "absorptive_photo_area": "photoArea", "absorptive_exposed_area": "exposedArea",

    # poses.jsonl (Row.cs's pose row): {"t":..,"bodies":[{"id","p","r":[x,y,z,w],"q"}]}.
    "poses_bodies": "bodies", "poses_rotation": "r",

    # positions.jsonl: {"t":..,"n":..,"b":[[id, x, y, z, flags], ...]}.
    "positions_bodies": "b",

    # snapshots/NNNNNNNNN.jsonl (GenomeJson.Write): "id", "root", "nodes"; each node "cell",
    # "shape", "buoyancyOffset", "dimensions" {x,y,z}; "moduleCounts" only when present.
    "snap_id": "id", "snap_root": "root", "snap_nodes": "nodes", "node_cell": "cell",
    "node_shape": "shape", "node_offset": "buoyancyOffset", "node_dims": "dimensions",
    "cell_photosynthetic": "photosynthetic", "shape_sphere": "sphere",

    # fields/ (Row.cs DumpFields): layout.json's "snowColumns" and "bed" groups (cellsX,
    # cellsZ, cellMetres, order "ix * cellsZ + iz"); NNNNNNNNN.snow-columns.f32, each column's
    # summed snow stock in joules (the charged field); bed.f32, the floor's y under each
    # column, negative down, written once and only for a shaped floor.
    "fields_layout": "layout.json", "fields_bed": "bed.f32", "fields_snow": "snow-columns",

    # The report's footer lines (Report.cs).
    "footer_wall_split": "wall split:", "footer_harness_split": "harness split:",
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
    """The run's markdown report, <runs root>/<arm>.md, as a list of lines, or None."""
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


def windowed(ts, by_t, name):
    """[(t, delta)] of a cumulative field over consecutive samples, or None if absent."""
    if not ts or num(by_t[ts[0]], name) is None:
        return None
    out = []
    prev = num(by_t[ts[0]], name)
    for t in ts[1:]:
        v = num(by_t[t], name)
        if v is None:
            return None
        out.append((t, v - prev))
        prev = v
    return out


def longest_run(bools):
    best = cur = 0
    for b in bools:
        cur = cur + 1 if b else 0
        best = max(best, cur)
    return best


# ---------------------------------------------------------------------- the lineage, streamed

def read_lineage(d, cutoff):
    """One pass over lineage.jsonl up to `cutoff` seconds.

    Every birth row is reduced to a few bits (its own `ink`, and whether its founder, walked up
    the parent chain, is a trickle founder): a parent is born before its child, so the founder
    is known from the parent's entry at the child's row, which is the ancestor walk done once
    per birth rather than once per question."""
    path = os.path.join(d, "lineage.jsonl")
    out = dict(present=os.path.exists(path), births=0, founders_floor=0, founders_trickle=0,
               founders_no_src=0, trickle_rooted_births=0, k3_line=[], kills=0,
               kill_rows_with_part=0, src_seen=False)
    if not out["present"]:
        return out
    info = {}  # id -> (ink, trickle_rooted)
    N = NAMES
    for row in stream_jsonl(path):
        t = row.get(N["lineage_time"], 0.0)
        if t > cutoff:
            continue
        e = row.get(N["lineage_event"])
        if e == N["lineage_kill"]:
            out["kills"] += 1
            if "part" in row or "pi" in row:
                out["kill_rows_with_part"] += 1
            continue
        if e != N["lineage_birth"]:
            continue
        out["births"] += 1
        i = row.get(N["lineage_id"])
        p = row.get(N["lineage_parent"], -1)
        ink = bool(row.get(N["lineage_ink"], 0))
        src = row.get(N["lineage_src"])
        if src is not None:
            out["src_seen"] = True
        if row.get(N["lineage_kind"]) == N["lineage_founder_kind"] or p is None or p < 0:
            if src == N["lineage_trickle"]:
                out["founders_trickle"] += 1
                rooted = True
            else:
                if src == N["lineage_floor"]:
                    out["founders_floor"] += 1
                else:
                    out["founders_no_src"] += 1
                rooted = False
            info[i] = (ink, rooted)
            continue
        parent = info.get(p)
        rooted = bool(parent and parent[1])
        info[i] = (ink, rooted)
        if rooted:
            out["trickle_rooted_births"] += 1
            # K3: an inherited consumer birth, `ink` on the child and on its parent's own row.
            if ink and parent[0]:
                out["k3_line"].append((i, t, bool(row.get(N["lineage_abs"], 0))))
    return out


# ---------------------------------------------------------------------- the poses at a snapshot

def snapshot_seconds(d):
    snaps = glob.glob(os.path.join(d, "snapshots", "*.jsonl"))
    out = []
    for s in snaps:
        m = re.match(r"^(\d{9})\.jsonl$", os.path.basename(s))
        if m:
            out.append(int(m.group(1)))
    return sorted(out)


def root_nodes(d, second):
    """id -> (cell, shape, offset, hx, hy, hz) of each living body's root node at a snapshot."""
    path = os.path.join(d, "snapshots", "%09d.jsonl" % second)
    N = NAMES
    out = {}
    for g in stream_jsonl(path):
        nodes = g.get(N["snap_nodes"])
        if not nodes:
            continue
        root = nodes[g.get(N["snap_root"], 0)]
        dims = root.get(N["node_dims"], {})
        out[g.get(N["snap_id"])] = (root.get(N["node_cell"]), root.get(N["node_shape"]),
                                    root.get(N["node_offset"]),
                                    abs(dims.get("x", 0.0)), abs(dims.get("y", 0.0)),
                                    abs(dims.get("z", 0.0)))
    return out


def pose_row(d, second):
    path = os.path.join(d, "poses.jsonl")
    if not os.path.exists(path):
        return None
    prefix = '{"t":%d,' % second
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith(prefix):
                try:
                    return json.loads(line)
                except json.JSONDecodeError:
                    return None
    return None


def exposure_factor(shape, hx, hy, hz, q):
    """PhenotypePart.ExposureFactor on the part's world rotation q = (x, y, z, w): the world's up
    on each of the part's axes is the second row of the rotation matrix."""
    if shape == NAMES["shape_sphere"]:
        return 1.0
    x, y, z, w = q
    ux = 2.0 * (x * y + w * z)
    uy = 1.0 - 2.0 * (x * x + z * z)
    uz = 2.0 * (y * z - w * x)
    faces = hx * hy + hy * hz + hx * hz
    if not faces > 0.0:
        return 1.0
    shadow = hy * hz * abs(ux) + hx * hz * abs(uy) + hx * hy * abs(uz)
    return 2.0 * shadow / faces


def root_leaf_exposures(d, cutoff):
    """(second, [(exposure, offset, |cos tilt|)]) over root parts of photosynthetic cell at the
    last snapshot at or before cutoff that has a pose row, or (None, None)."""
    # At most three snapshots are tried: each try is a scan of poses.jsonl, which runs to
    # gigabytes over a full round, and a run that records poses writes one at every sample.
    for second in list(reversed([s for s in snapshot_seconds(d) if s <= cutoff]))[:3]:
        pose = pose_row(d, second)
        if pose is None:
            continue
        roots = root_nodes(d, second)
        out = []
        for b in pose.get(NAMES["poses_bodies"], []):
            r = roots.get(b.get("id"))
            if r is None or r[0] != NAMES["cell_photosynthetic"]:
                continue
            cell, shape, offset, hx, hy, hz = r
            q = b.get(NAMES["poses_rotation"])
            if not q or len(q) != 4:
                continue
            e = exposure_factor(shape, hx, hy, hz, q)
            # tilt.py's reading: the thinnest local axis against the vertical.
            dims = (hx, hy, hz)
            axis = min(range(3), key=lambda i: dims[i])
            x, y, z, w = q
            up_on_axis = (2.0 * (x * y + w * z), 1.0 - 2.0 * (x * x + z * z),
                          2.0 * (y * z - w * x))[axis]
            out.append((e, offset, abs(up_on_axis)))
        return second, out
    return None, None


# ---------------------------------------------------------------------- the clauses

def k1(seed, ts, by_t, cutoff, leaves):
    name = NAMES["stats_expo"]
    vals = [(t, num(by_t[t], name)) for t in ts]
    vals = [(t, v) for t, v in vals if v is not None]
    if not vals:
        return dict(clause="K1", seed=seed, held="absent",
                    note="meanExposure is null or missing at every sample (light by exposure "
                         "off, or a run before D110)")
    t_end, short = sample_at([t for t, _ in vals], min(cutoff, END_SECONDS))
    expo_end = dict(vals)[t_end]
    founding_t, founding = vals[0]
    lo, hi = K1_FOUNDING
    off = num(by_t[ts[-1]], NAMES["stats_float_off"])
    out = dict(clause="K1", seed=seed, expo_at=t_end, short=short, expo=expo_end,
               expo_founding_at=founding_t, expo_founding=founding,
               founding_in_band=lo <= founding <= hi, float_off_at_end=off,
               held=expo_end > K1_EXPO)
    second, rows = leaves
    if rows:
        out["pose_check_at"] = second
        out["pose_root_leaves"] = len(rows)
        out["pose_root_leaf_mean_expo"] = sum(r[0] for r in rows) / len(rows)
        out["pose_mean_abs_cos_tilt"] = sum(r[2] for r in rows) / len(rows)
        out["pose_note"] = ("root leaves only, unweighted; tilt.py's flat factor is the "
                            "mean |cos tilt| (0.5 random, 1 flat)")
    return out


def k2(seed, leaves, cutoff):
    second, rows = leaves
    if rows is None:
        return dict(clause="K2", seed=seed, held="absent",
                    note="no snapshot with a pose row at or before the second read")
    if rows and all(r[1] is None for r in rows):
        return dict(clause="K2", seed=seed, held="absent", at=second,
                    note="the snapshot's genomes carry no buoyancyOffset (format before 8)")
    hi = [r[0] for r in rows if r[1] is not None and abs(r[1]) > K2_OFFSET]
    lo = [r[0] for r in rows if r[1] is not None and abs(r[1]) <= K2_OFFSET]
    out = dict(clause="K2", seed=seed, at=second, short=second < min(cutoff, END_SECONDS),
               scope="root leaf parts only (docstring)",
               offset_leaves=len(hi), plain_leaves=len(lo),
               offset_mean_expo=(sum(hi) / len(hi)) if hi else None,
               plain_mean_expo=(sum(lo) / len(lo)) if lo else None)
    if not hi or not lo:
        out["held"] = "absent"
        out["note"] = ("one side is empty: %d root leaves with |offset| > %g and %d without"
                       % (len(hi), K2_OFFSET, len(lo)))
        return out
    diff = out["offset_mean_expo"] - out["plain_mean_expo"]
    out["difference"] = diff
    out["held"] = diff >= K2_MARGIN
    return out


def k3(seed, ts, by_t, lineage, cutoff):
    if not lineage["present"]:
        return dict(clause="K3", seed=seed, held="absent", note="no lineage.jsonl")
    if not lineage["src_seen"]:
        return dict(clause="K3", seed=seed, held="absent",
                    note="no founder row carries `src` (a run before D115's build)")
    wins = windowed([t for t in ts if t < END_SECONDS] or ts, by_t, NAMES["stats_corpse_eat"])
    if wins is None:
        return dict(clause="K3", seed=seed, held="absent",
                    note="corpsesEaten is not on this report")
    bools = [dv > 0 for _, dv in wins]
    n_win = sum(bools)
    line = lineage["k3_line"]
    return dict(clause="K3", seed=seed, trickle_founders=lineage["founders_trickle"],
                trickle_rooted_births=lineage["trickle_rooted_births"],
                inherited_consumer_births_of_trickle=len(line),
                of_them_with_abs=sum(1 for _, _, a in line if a),
                first_such_birth_at=(line[0][1] if line else None),
                corpse_eat_windows=n_win, corpse_eat_longest_run=longest_run(bools),
                held=bool(line) and n_win >= WINDOW_COUNT)


def k4(seed, ts, by_t, cutoff):
    t_end = min(cutoff, END_SECONDS, ts[-1])
    lo = t_end - K4_TAIL_SECONDS
    kills = windowed(ts, by_t, NAMES["stats_killed"])
    if kills is None or num(by_t[ts[0]], NAMES["stats_attack"]) is None:
        return dict(clause="K4", seed=seed, held="absent",
                    note="partsKilled or attackShare is not on this report")
    bools = []
    for t, dv in kills:
        if t <= lo or t > t_end:
            continue
        a = num(by_t[t], NAMES["stats_attack"]) or 0.0
        bools.append(a > 0 and dv > 0)
    return dict(clause="K4", seed=seed, window_from=max(lo, 0.0), window_to=t_end,
                short=t_end < END_SECONDS, windows_attack_and_kill=sum(bools),
                longest_run=longest_run(bools),
                kills_total=num(by_t[ts[-1]], NAMES["stats_killed"]),
                held=sum(bools) >= WINDOW_COUNT)


def k5(seed, ts, by_t):
    A, P, L = NAMES["stats_attack"], NAMES["stats_prot"], NAMES["stats_alive"]
    if num(by_t[ts[0]], A) is None or num(by_t[ts[0]], P) is None:
        return dict(clause="K5", seed=seed, held="absent",
                    note="attackShare/protectionShare are not on this report")
    first_a = first_p = None
    for t in ts:
        r = by_t[t]
        if (num(r, L) or 0) < K5_CROWD:
            continue
        if first_a is None and (num(r, A) or 0) > K5_THRESHOLD:
            first_a = t
        if first_p is None and (num(r, P) or 0) > K5_THRESHOLD:
            first_p = t
    out = dict(clause="K5", seed=seed, first_attack_over_1pct=first_a,
               first_prot_over_1pct=first_p)
    if first_a is None:
        out["held"] = "no attack"
        out["note"] = ("attack % never above 1% with 500 alive; the seed is outside the "
                       "clause's denominator (\"with attack present\")")
    elif first_p is None:
        out["held"] = True
        out["note"] = "protection never above 1% with 500 alive; not violated, not confirmed"
    else:
        out["held"] = first_p > first_a
    return out


def k6a(seed, ts, by_t, cutoff):
    name = NAMES["stats_reach"]
    after = [t for t in ts if t > K6_AFTER]
    if not after:
        return dict(clause="K6a", seed=seed, held="pending",
                    note="no sample after 5,000 s yet", reach_now=num(by_t[ts[-1]], name))
    vals = [(t, num(by_t[t], name)) for t in after]
    if any(v is None for _, v in vals):
        return dict(clause="K6a", seed=seed, held="absent", note="meanReach missing or null")
    t_max, v_max = max(vals, key=lambda tv: tv[1])
    return dict(clause="K6a", seed=seed, samples=len(vals), reach_max=v_max,
                reach_max_at=t_max, reach_end=vals[-1][1],
                support_w_end=num(by_t[ts[-1]], NAMES["stats_support_w"]),
                short=ts[-1] < END_SECONDS, held=v_max < K6_REACH)


def k6b(seed):
    return dict(clause="K6b", seed=seed, held="absent",
                note="no file records a part's distance from its root; the snapshot is the "
                     "genome and development is not rebuilt here")


def k7(seed, ts, by_t, cutoff):
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    alive = num(by_t[t], NAMES["stats_alive"])
    return dict(clause="K7", seed=seed, at=t, short=short, alive=alive,
                held=alive is not None and alive > K7_ALIVE)


def k8(seed, ts, by_t, manifest, cutoff):
    audits = [num(by_t[t], NAMES["stats_audit"]) for t in ts]
    audits = [a for a in audits if a is not None]
    audit_absmax = max(abs(a) for a in audits) if audits else None
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    resid = num(by_t[t], NAMES["stats_matter_resid"])
    div = num(by_t[ts[-1]], NAMES["stats_diverged"])
    status = manifest.get("status") if manifest else None
    censored = status in ("error", "stopped")
    held = (not censored and audit_absmax is not None and audit_absmax < K8_AUDIT
            and resid is not None and abs(resid) < K8_MATTER and div == 0)
    return dict(clause="K8", seed=seed, audit_absmax_J=audit_absmax, matter_resid_at=t,
                matter_resid=resid, short=short, diverged=div, manifest_status=status,
                censored=censored, provisional=status == "running", held=held)


def k9a(seed, ts, by_t, cutoff, header):
    t, short = sample_at(ts, min(cutoff, END_SECONDS))
    per_step = num(by_t[t], NAMES["stats_ovl_per_step"])
    alive = num(by_t[t], NAMES["stats_alive"])
    if per_step is None or not alive:
        return dict(clause="K9a", seed=seed, held="absent",
                    note="overlapPairsPerStep or alive missing at the sample")
    ovl = per_step / alive
    return dict(clause="K9a", seed=seed, at=t, short=short, ovl_per_body=ovl,
                contact_model=model_note(header_line=header) if header else None,
                held=ovl < K9_OVL)


def k9b(seed, lineage):
    return dict(clause="K9b", seed=seed, held="absent", kill_rows=lineage["kills"],
                note="the kill row carries no part index (id, by, root, parts, tj, rj, ind) "
                     "and no file records the overlap pairs")


def k10(seed, ts, by_t, report, cutoff):
    out = dict(clause="K10", seed=seed, held="absent",
               note="the harness split has no exposure or support phase: exposure runs inside "
                    "`metabolise`, support inside Core's step (`world`)")
    tot = num(by_t[ts[-1]], NAMES["stats_wall_total"])
    met = num(by_t[ts[-1]], NAMES["stats_wall_metabolise"])
    if tot and met is not None:
        out["metabolise_share_of_wall"] = met / tot
        out["metabolise_note"] = "a ceiling on the exposure pass alone, not the clause"
    if report and cutoff == float("inf"):
        # The footer is the run's whole wall; at a milestone it is not the state at that mark.
        for line in report:
            if line.startswith(NAMES["footer_wall_split"]):
                out["wall_split"] = line[len(NAMES["footer_wall_split"]):].strip()
            elif line.startswith(NAMES["footer_harness_split"]):
                out["harness_split"] = line[len(NAMES["footer_harness_split"]):].strip()
    return out


def shelf_mask(d, config):
    """(layout, floor_y list, mask list) for the snow's columns: live, and floor within 12 m;
    or None when the run wrote no bed."""
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
    live = []
    shelf = []
    for ix in range(nx):
        for iz in range(nz):
            ok = floor[ix * nz + iz] < 0.0
            if radius is not None:
                ok = ok and (((ix + 0.5) * cell - radius) ** 2 +
                             ((iz + 0.5) * cell - radius) ** 2 <= radius * radius)
            live.append(ok)
            shelf.append(ok and floor[ix * nz + iz] >= -K11_SHELF)
    return dict(nx=nx, nz=nz, cell=cell, floor=floor, live=live, shelf=shelf,
                snow_layout=layout.get("snowColumns"))


def k11a(seed, d, shelf, cutoff):
    out = dict(clause="K11a", seed=seed, held="absent",
               note="the snow is dumped as column sums, not by cell; the lowest live cell's "
                    "density is not recorded")
    if shelf is None:
        out["note2"] = "no fields/bed.f32 (a flat bed, or a run before the beach build)"
        return out
    n_shelf = sum(shelf["shelf"])
    out["shelf_columns"] = n_shelf
    out["shelf_share_of_live"] = n_shelf / max(1, sum(shelf["live"]))
    dumps = {}
    for p in glob.glob(os.path.join(d, "fields", "*.%s.f32" % NAMES["fields_snow"])):
        m = re.match(r"^(\d{9})\.", os.path.basename(p))
        if m and int(m.group(1)) <= cutoff:
            dumps[int(m.group(1))] = p
    if not dumps:
        return out
    sl = shelf["snow_layout"]
    if not sl or sl["cellsX"] != shelf["nx"] or sl["cellsZ"] != shelf["nz"]:
        out["note2"] = "the snow's columns and the bed's do not share a layout"
        return out
    t = max(dumps)
    snow = read_floats(dumps[t])
    area = shelf["cell"] * shelf["cell"]
    dens = [snow[i] / (-shelf["floor"][i] * area) for i in range(len(snow)) if shelf["shelf"][i]]
    out["column_mean_at"] = t
    out["before_10000s"] = t < K11_FROM
    if dens:
        out["shelf_column_mean_J_m3"] = sum(dens) / len(dens)
        out["shelf_columns_mean_above_0.4"] = sum(1 for v in dens if v > K11_SNOW) / len(dens)
        out["column_note"] = "column mean (joules over water depth), not the clause's lowest cell"
    return out


def k11b(seed, d, shelf, lineage, k3_row, cutoff):
    if k3_row.get("held") is not True:
        return dict(clause="K11b", seed=seed, held="no line",
                    note="K3 does not hold on this seed, so there is no line to place")
    if shelf is None:
        return dict(clause="K11b", seed=seed, held="absent", note="no fields/bed.f32")
    pending = {i: t for i, t, _ in lineage["k3_line"]}
    placed = {}
    earliest = min(pending.values())
    path = os.path.join(d, "positions.jsonl")
    if not os.path.exists(path):
        return dict(clause="K11b", seed=seed, held="absent", note="no positions.jsonl")
    for row in stream_jsonl(path):
        t = row.get("t", 0.0)
        if t < earliest:
            continue
        if t > cutoff or not pending:
            break
        for b in row.get(NAMES["positions_bodies"], []):
            i = b[0]
            bt = pending.get(i)
            if bt is not None and t >= bt:
                placed[i] = (b[1], b[3])
                del pending[i]
    nx, nz, cell = shelf["nx"], shelf["nz"], shelf["cell"]
    over = 0
    for x, z in placed.values():
        ix = min(nx - 1, max(0, int(x // cell)))
        iz = min(nz - 1, max(0, int(z // cell)))
        if shelf["shelf"][ix * nz + iz]:
            over += 1
    share = over / len(placed) if placed else None
    return dict(clause="K11b", seed=seed, line_births=len(lineage["k3_line"]),
                placed=len(placed), over_shelf=over, share_over_shelf=share,
                shelf_share_of_live=sum(shelf["shelf"]) / max(1, sum(shelf["live"])),
                held=(share is not None and share > 0.5))


def context_row(seed, ts, by_t, lineage, header):
    """Not a clause: the founding sources, the header's rule tokens."""
    tokens = []
    if header:
        # The rule tokens the prereg asks the header to carry (Report.cs's settings line).
        for pattern in (r"light (?:by exposure|averaged)", r"buoyancy offset (?:off|\S+ W/m3)",
                        r"trickle \S+(?: s)?", r"founders (?:in their food|in matter|anywhere)",
                        r"support (?:off|\S+ W/m2/m2)", r"contact per (?:part|body)",
                        r"tilt \S+ m shore \S+ m fade \S+ m", r"remin \S+ /s",
                        r"floor closes \S+ s"):
            m = re.search(r"(?:^| )(%s)" % pattern, header)
            if m:
                tokens.append(m.group(1).strip())
    last = by_t[ts[-1]]
    births = lineage["births"]
    founders = lineage["founders_floor"] + lineage["founders_trickle"] + lineage["founders_no_src"]
    return dict(clause="ctx", seed=seed, last_t=ts[-1],
                alive=num(last, NAMES["stats_alive"]),
                floor_spawns=num(last, NAMES["stats_floor"]),
                trickle_spawns=num(last, NAMES["stats_trickle"]),
                lineage_births=births, founders_floor=lineage["founders_floor"],
                founders_trickle=lineage["founders_trickle"],
                founders_share_of_births=(founders / births) if births else None,
                header_tokens="; ".join(tokens) if tokens else None)


# ---------------------------------------------------------------------- driving it over the arms

def read_arm(runs_root, arm, cutoff):
    d = run_dir(runs_root, arm)
    rows = [r for r in stream_jsonl(os.path.join(d, "stats.jsonl"))
            if r.get(NAMES["stats_t"], 0) <= cutoff]
    if not rows:
        raise SystemExit("no stats.jsonl rows (or none by the second asked) for %r" % arm)
    by_t = {r[NAMES["stats_t"]]: r for r in rows}
    ts = sorted(by_t)
    manifest = load_json(os.path.join(d, "run.json"))
    config = load_json(os.path.join(d, "config.json")) or {}
    report = report_lines(runs_root, arm)
    header = None
    if report:
        header = next((ln for ln in report if ln.startswith("engine=")), None)
    lineage = read_lineage(d, cutoff)
    leaves = root_leaf_exposures(d, min(cutoff, END_SECONDS))
    shelf = shelf_mask(d, config)

    k3row = k3(arm, ts, by_t, lineage, cutoff)
    return [
        context_row(arm, ts, by_t, lineage, header),
        k1(arm, ts, by_t, cutoff, leaves),
        k2(arm, leaves, cutoff),
        k3row,
        k4(arm, ts, by_t, cutoff),
        k5(arm, ts, by_t),
        k6a(arm, ts, by_t, cutoff),
        k6b(arm),
        k7(arm, ts, by_t, cutoff),
        k8(arm, ts, by_t, manifest, cutoff),
        k9a(arm, ts, by_t, cutoff, header),
        k9b(arm, lineage),
        k10(arm, ts, by_t, report, cutoff),
        k11a(arm, d, shelf, cutoff),
        k11b(arm, d, shelf, lineage, k3row, cutoff),
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
    "K1": "2 of 3", "K2": "2 of 3", "K3": "2 of 3", "K4": "1 of 3 or more",
    "K5": "3 of 3 with attack present", "K6a": "3 of 3", "K6b": "3 of 3", "K7": "3 of 3",
    "K8": "3 of 3", "K9a": "3 of 3", "K9b": "3 of 3", "K10": "3 of 3", "K11a": "3 of 3",
    "K11b": "2 of 3 where K3 holds",
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
    ap.add_argument("--out", default=None,
                    help="write the TSV here (creates its directory; nothing is written "
                         "without this flag)")
    ap.add_argument("milestone", nargs="*",
                    help="the watch's `<second> <arm>`; reads that arm alone, up to that second")
    args = ap.parse_args()

    runs_root = args.runs_root
    if not os.path.isabs(runs_root):
        runs_root = os.path.join(os.getcwd(), runs_root)

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
    for arm in args.arms:
        try:
            arm_rows = read_arm(runs_root, arm, cutoff)
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
        print("%-4s held in %d of %d%s -- needs %s"
              % (c + ":", n_true, len(results), note, CLAUSE_THRESHOLD_TEXT[c]))

    if args.out:
        write_tsv(args.out, all_rows)
        print()
        print("wrote", args.out)


if __name__ == "__main__":
    main()
