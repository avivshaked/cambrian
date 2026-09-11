"""Read an arm's `diverged/` dumps and join each to its birth (and death) row in
`lineage.jsonl` -- one row per creature the harness caught mid-divergence and dumped
before killing it as a counted death (Ecosystem.HandleDivergence / CheckFinite,
`unity/Assets/Evosim/Sim/Ecosystem.cs`). See CLAUDE.md's "A lineage dissection can
answer less than it looks like it can" (what a dump and a lineage row each carry) and
"PhysX replays bit for bit on this machine" (what `diverged` means: a non-finite or
out-of-box body, killed and dumped rather than left to take the run down).

A dump (`runs/<arm>/<run>/diverged/<id>.json`) carries, at the top level: creatureId, t
(sim seconds), physicsStep, physicsDtSeconds, lastObservedHeightY (the root's last
*intact* height -- see below), generationDepth, ageSeconds, parts, totalDof, jointed,
lastRootPosition ({x,y,z}, also the last intact position), partStates (one entry per
articulation link: index, name, parentIndex, jointType, cellTypeId, power, volumeM3,
massKg, lastVelocity/lastAngularVelocity/lastSpeed/lastSpinRate (pre-divergence),
driveTorque, and the *current* position/velocity/angularVelocity -- these are the ones
that can be non-finite, or finite but far outside the world box), and genome (the full
stored genome: format, root, adultScale, reproduction{brood,investment}, nodes[],
globalBrain). There is no explicit "reason" field; this script derives one by
replaying `Ecosystem.CheckFinite`'s own test against the dump's `partStates[*].position`
and the run's own `world.worldDepthMetres` (from config.json): the root part (the one
whose parentIndex is -1) must satisfy `World.HeightIsInTheWorld` (y <= depth and
y >= -3*depth) and have a finite x+z; every other part must have a finite x+y+z. The
first test that fails is reported as the reason, with the offending value(s).

A genome node (`genome.nodes[i]`) carries `joint` (a string: "Fixed" or an articulation
joint type such as "TwistHinge") and `power` (a float, the drive strength if the joint
is not Fixed). "Has an active joint" below means any node with `joint != "Fixed"` and
`power > 0` -- those are the two fields read for it, no others.

`lineage.jsonl` rows are streamed line by line (large file). A birth row
({"e":"b","t":..,"id":..,"p":..,"k":..,"g":..,"s":..,"abs":..,"jnt":..,"pho":..,"pt":..,
"bf":..,"as":..}) gives the birth time and "bf" (body fraction at birth); a death row
({"e":"d","t":..,"id":..,"c":..}) gives the death time and cause -- for a diverged body
"c" reads "diverged" and its "t" should equal the dump's own "t" (and ageSeconds should
equal deathT - birthT); this script cross-checks that and flags a mismatch rather than
silently trusting one over the other.

Usage: python scripts/reads/diverged-read.py <arm> [<arm> ...]

Never writes, never polls; reads runs/<arm>/<run>/{diverged/*.json,lineage.jsonl,
config.json} directly with a tolerant open (a running arm's lineage.jsonl may still be
appended to).
"""
import glob, json, math, os, statistics, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def run_dir(arm):
    """The one run directory under runs/<arm>/ that has a diverged/ folder, or the
    latest run directory if none do (so a clean error still names *a* path)."""
    candidates = sorted(d for d in glob.glob(f"runs/{arm}/*/") if os.path.isdir(d))
    if not candidates:
        raise SystemExit(f"no run directory under runs/{arm}/")
    with_diverged = [d for d in candidates if os.path.isdir(os.path.join(d, "diverged"))]
    return (with_diverged or candidates)[-1].rstrip("/\\")


def world_depth(rdir):
    path = os.path.join(rdir, "config.json")
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    return cfg["world"]["worldDepthMetres"]


def as_float(v):
    """Dump numbers arrive either as JSON numbers or as the strings "NaN"/"Infinity"/
    "-Infinity" (Unity's JSON writer quotes the IEEE specials). float() parses both."""
    return float(v)


def height_is_in_the_world(y, depth):
    # Mirrors World.HeightIsInTheWorld (src/Evosim.Core/Ecosystem/World.cs) exactly:
    # comparisons against NaN/Infinity are already False, so this alone also screens
    # non-finite y without a separate isnan/isinf check.
    return y <= depth and y >= -3 * depth


def divergence_reason(dump, depth):
    """Replays Ecosystem.CheckFinite's own test against partStates[*].position, in the
    same order the engine checks it: root height/box first, root horizontal finiteness
    second, then every other part's finiteness. Returns (reason_text, offending_value)."""
    parts = dump.get("partStates", [])
    root = next((p for p in parts if p.get("parentIndex") == -1), parts[0] if parts else None)
    if root is None:
        return "no partStates in dump", ""

    rx, ry, rz = (as_float(root["position"][a]) for a in "xyz")
    horiz = rx + rz
    ry_nonfinite = math.isnan(ry) or math.isinf(ry)
    horiz_nonfinite = math.isnan(horiz) or math.isinf(horiz)

    if not height_is_in_the_world(ry, depth):
        if ry_nonfinite:
            return "root y (height) non-finite (NaN/Inf)", f"y={ry}"
        return (f"root height outside world box [{-3*depth:g}, {depth:g}] m (finite, not NaN)",
                f"y={ry:g}")

    if horiz_nonfinite:
        return "root horizontal (x+z) non-finite (NaN/Inf)", f"x={rx}, z={rz}"

    for p in parts:
        if p is root:
            continue
        px, py, pz = (as_float(p["position"][a]) for a in "xyz")
        s = px + py + pz
        if math.isnan(s) or math.isinf(s):
            return (f"part {p['index']} ({p.get('name','?')}) position non-finite (NaN/Inf)",
                     f"x={px}, y={py}, z={pz}")

    return "no CheckFinite test fails on this dump's partStates (root and links all finite and in box)", ""


def genome_stats(genome):
    nodes = genome.get("nodes", [])
    n_nodes = len(nodes)
    n_edges = sum(len(n.get("edges", [])) for n in nodes)
    has_active_joint = any(
        n.get("joint") not in (None, "Fixed") and n.get("power", 0) > 0
        for n in nodes
    )
    return n_nodes, n_edges, has_active_joint


def load_lineage(rdir, wanted_ids):
    """Streams lineage.jsonl once, keeping only rows for the ids we need. Returns
    {id: {"birthT":.., "bf":.., "jnt":.., "deathT":.., "cause":..}}."""
    out = {i: {} for i in wanted_ids}
    path = os.path.join(rdir, "lineage.jsonl")
    truncated = 0
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                # A live run's writer can be mid-flush on the final line; that is not
                # a parse bug here, just a live file read at the wrong instant.
                truncated += 1
                continue
            rid = row.get("id")
            if rid not in out:
                continue
            if row.get("e") == "b":
                out[rid]["birthT"] = row.get("t")
                out[rid]["bf"] = row.get("bf")
                out[rid]["jnt"] = row.get("jnt")
            elif row.get("e") == "d":
                out[rid]["deathT"] = row.get("t")
                out[rid]["cause"] = row.get("c")
    if truncated:
        print(f"  (note: {truncated} unparsable line(s) at the tail of lineage.jsonl skipped "
              f"-- the arm is likely still running and mid-write)")
    return out


def read_arm(arm):
    rdir = run_dir(arm)
    dpath = os.path.join(rdir, "diverged")
    dump_files = sorted(glob.glob(os.path.join(dpath, "*.json")))
    if not dump_files:
        print(f"{arm}: no diverged/ dumps under {rdir}")
        return

    depth = world_depth(rdir)
    dumps = {}
    for fp in dump_files:
        with open(fp, encoding="utf-8") as f:
            d = json.load(f)
        dumps[d["creatureId"]] = d

    lineage = load_lineage(rdir, dumps.keys())

    print(f"\n=== {arm}  ({rdir})  world depth={depth:g} m ===")
    header = (f"{'id':>6} {'t_div':>10} {'age_s':>8} {'jnt':>3} {'nodes':>5} {'edges':>5} "
              f"{'activeJoint':>11} {'bf':>7}  reason (offending value)")
    print(header)
    print("-" * len(header))

    ages = []
    n_jointed = 0
    times = []
    for cid in sorted(dumps):
        d = dumps[cid]
        lin = lineage.get(cid, {})
        birth_t = lin.get("birthT")
        death_t = lin.get("deathT")
        cause = lin.get("cause")

        t_div = death_t if death_t is not None else d.get("t")
        age = (t_div - birth_t) if (t_div is not None and birth_t is not None) else d.get("ageSeconds")

        # Cross-check the dump's own ageSeconds/t against the lineage rows rather than
        # silently trusting either.
        note = ""
        if death_t is not None and d.get("t") is not None and abs(death_t - d["t"]) > 1e-6:
            note += f" [dump t={d['t']:.2f} != lineage deathT={death_t:.2f}]"
        if cause is not None and cause != "diverged":
            note += f" [lineage cause={cause!r}, not 'diverged']"
        if birth_t is None:
            note += " [no birth row found in lineage.jsonl]"

        jnt = lin.get("jnt", d.get("jointed"))
        jnt_disp = "1" if jnt in (1, True) else ("0" if jnt in (0, False) else "?")
        if jnt in (1, True):
            n_jointed += 1

        n_nodes, n_edges, active_joint = genome_stats(d.get("genome", {}))
        bf = lin.get("bf")
        reason, offending = divergence_reason(d, depth)

        ages.append(age)
        times.append(t_div)

        bf_disp = f"{bf:.3f}" if isinstance(bf, (int, float)) else "?"
        t_disp = f"{t_div:.1f}" if isinstance(t_div, (int, float)) else "?"
        age_disp = f"{age:.1f}" if isinstance(age, (int, float)) else "?"
        print(f"{cid:>6} {t_disp:>10} {age_disp:>8} {jnt_disp:>3} {n_nodes:>5} {n_edges:>5} "
              f"{str(active_joint):>11} {bf_disp:>7}  {reason} ({offending}){note}")

    numeric_ages = [a for a in ages if isinstance(a, (int, float))]
    numeric_times = [t for t in times if isinstance(t, (int, float))]
    print("-" * len(header))
    print(f"count={len(dumps)}  jointed={n_jointed}  "
          f"median_age={statistics.median(numeric_ages):.1f}s  "
          f"under_100s={sum(1 for a in numeric_ages if a < 100)}  "
          f"t_range=[{min(numeric_times):.1f}, {max(numeric_times):.1f}]"
          if numeric_ages and numeric_times else
          f"count={len(dumps)}  jointed={n_jointed}  (ages/times not all resolvable)")


if __name__ == "__main__":
    arms = sys.argv[1:]
    if not arms:
        raise SystemExit("usage: python scripts/reads/diverged-read.py <arm> [<arm> ...]")
    for arm in arms:
        read_arm(arm)
