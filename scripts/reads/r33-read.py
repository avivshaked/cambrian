"""Round 33's V1-V3 validity checks plus the growth reading G0-G5 (logbook/0082) per arm,
from named columns, the manifest, `stats.jsonl` and `lineage.jsonl`. Round 33 is round 32's
grid world (logbook/0079) with D087's growth in it: children are born at a fraction of their
adult body and grow, three genome dials (adult scale, investment, brood) replace the fixed
endowment, and a biomass ceiling (`maxTissue`) sits beside the population ceiling. The
reference for every clause is round 32's same seed (`runs/r32-sN.md`) -- round 32's world with
bodies born whole.

D063-amended clade scoring (the goal rule) is NOT computed here -- run
`scripts/clade-score.ps1` for that; G0 only checks the alive-count wingspan and notes the
scorer.

Reads live files with a tolerant open: a report, stats.jsonl or lineage.jsonl may still be
being written by a running arm (four of five were, at the time this was written). Never
polls, sleeps or writes outside the repo, and never touches anything under unity*.

Usage: python scripts/reads/r33-read.py [arm ...]   (default r33-s1 r33-s2 r33-s3 r33-s4 r33-s5)
"""
import glob, json, os, re, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


# -- shared with r32-read.py: the parser, num(), manifest/stats reading -------------------

def rows(arm):
    """Tolerant reader for runs/<arm>.md: a partial final row (different cell count
    than the header) is dropped rather than raising."""
    path = f"runs/{arm}.md"
    if not os.path.exists(path):
        return [], None, None
    header, out, ended = None, [], None
    try:
        f = open(path, encoding="utf-8")
    except OSError as e:
        return [], None, f"unreadable: {e}"
    with f:
        for line in f:
            if line.startswith("**Ended"):
                ended = line.strip()
            if not line.startswith("|"):
                continue
            cells = [c.strip().strip("*").strip() for c in line.strip().strip("|").split("|")]
            if header is None:
                header = cells
                continue
            if set(cells[0]) <= set("-") or len(cells) != len(header):
                continue
            out.append(dict(zip(header, cells)))
    return out, ended, None


def header_line(arm):
    """The params line (line 3 of the report): the one non-blank, non-'#', non-'|' line
    before the table. Read tolerantly; returns '' if the file is short or missing."""
    path = f"runs/{arm}.md"
    if not os.path.exists(path):
        return ""
    try:
        with open(path, encoding="utf-8") as f:
            for line in f:
                s = line.strip()
                if s and not s.startswith("#") and not s.startswith("|"):
                    return s
    except OSError:
        return ""
    return ""


def num(v):
    if v is None:
        return None
    v = v.replace("%", "").replace(",", "")
    if v in ("", "—", "-"):
        return None
    try:
        return float(v)
    except ValueError:
        return None


def manifest(arm):
    """Latest run.json for the arm (glob-sorted; the timestamped directory name sorts
    chronologically, so [-1] is the most recent launch -- the one that matters when an
    arm was relaunched, as round 32's s1 and s5 were: each carries a 'stopped manual-other'
    directory from the mislaunch beside the 'ended budget' one that actually ran, and this
    picks the latter). Returns (dict, rundir)."""
    files = sorted(glob.glob(f"runs/{arm}/*/run.json"))
    if not files:
        return {}, None
    path = files[-1]
    try:
        return json.load(open(path, encoding="utf-8")), os.path.dirname(path)
    except (OSError, json.JSONDecodeError):
        return {}, os.path.dirname(path)


def stats_last(arm):
    """Last well-formed row of the latest stats.jsonl. Tolerant of a live run: a
    truncated final line (still being written) is skipped in favour of the last line
    that parses."""
    files = sorted(glob.glob(f"runs/{arm}/*/stats.jsonl"))
    if not files:
        return {}
    last = None
    try:
        f = open(files[-1], encoding="utf-8")
    except OSError:
        return {}
    with f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                json.loads(line)
            except json.JSONDecodeError:
                continue
            last = line
    return json.loads(last) if last else {}


def diverged_count(m, rundir):
    if "divergedTotal" in m and m["divergedTotal"] is not None:
        return m["divergedTotal"], "manifest divergedTotal"
    d = os.path.join(rundir, "diverged") if rundir else None
    if d and os.path.isdir(d):
        try:
            n = len([x for x in os.listdir(d) if os.path.isfile(os.path.join(d, x))])
            return n, "counted runs/<arm>/<run>/diverged/*"
        except OSError:
            return None, "diverged/ unreadable"
    return (0 if rundir else None), "no diverged/ dir found (0 assumed)"


def row_at(r, target_t):
    """Exact match on 't (s)' if present; else the closest row at or before target_t;
    else None (target not yet reached, e.g. a running arm still short of it)."""
    if not r:
        return None
    exact = [x for x in r if num(x["t (s)"]) == target_t]
    if exact:
        return exact[0]
    before = [x for x in r if num(x["t (s)"]) is not None and num(x["t (s)"]) <= target_t]
    return before[-1] if before else None


def r32_control(seed):
    return f"r32-s{seed}"


V1_TOKENS = [
    ("field grid", "field grid"),
    ("cell=1", "cell=1"),
    ("mcell=5", "mcell=5"),
    ("corpse=0.005/s", "corpse=0.005/s"),
    ("mixing 0.02 m2/s", "mixing 0.02 m2/s"),
    ("h-mix 0.02 m2/s", "h-mix 0.02 m2/s"),
    ("addedMass 0.5", "addedMass 0.5"),
    ("dt=0.01", "dt=0.01"),
    ("maxTissue=30000", "maxTissue=30000"),
    ("growth reserve=0.2 floor=0.1 minkg=0.5 step=10 invest=0.25-1 scale/invest chance=0.08/0.08",
     "growth reserve=0.2 floor=0.1 minkg=0.5 step=10 invest=0.25-1 scale/invest chance=0.08/0.08"),
]

RUNAWAY_FAIL_REASONS = {"ceiling-population", "ceiling-tissue"}

DIALS = ["adult scale", "invest", "brood", "body frac"]


def sign(last_v, base_v):
    """down/up/flat within 5% of the base value (t=1000s)."""
    if last_v is None or base_v is None:
        return "?"
    if base_v == 0:
        return "flat" if last_v == 0 else ("up" if last_v > 0 else "down")
    change = (last_v - base_v) / abs(base_v)
    if abs(change) < 0.05:
        return "flat"
    return "up" if change > 0 else "down"


def lineage_dist(arm, rundir, final):
    """Birth-fraction (bf) and adult-scale (as) distributions over child birth rows
    (kind 'r', i.e. not a founder 'f') in lineage.jsonl. Streamed with a tolerant open --
    the file can run into the hundreds of MB and a live run may be mid-write on its last
    line. Only computed for ended arms, per the task: a running arm's lineage is still
    growing and its distribution is not yet the round's answer."""
    if not final:
        return None, "skipped, running"
    if not rundir:
        return None, "no run directory found"
    path = os.path.join(rundir, "lineage.jsonl")
    if not os.path.exists(path):
        return None, "lineage.jsonl not found"
    bf_vals, as_vals = [], []
    try:
        f = open(path, encoding="utf-8", errors="replace")
    except OSError as e:
        return None, f"unreadable: {e}"
    with f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except json.JSONDecodeError:
                continue
            if d.get("e") != "b" or d.get("k") != "r":
                continue
            if "bf" in d and isinstance(d["bf"], (int, float)):
                bf_vals.append(d["bf"])
            if "as" in d and isinstance(d["as"], (int, float)):
                as_vals.append(d["as"])
    return (bf_vals, as_vals), None


def percentile(vals, p):
    if not vals:
        return None
    s = sorted(vals)
    if len(s) == 1:
        return s[0]
    k = (len(s) - 1) * (p / 100.0)
    f, c = int(k), min(int(k) + 1, len(s) - 1)
    if f == c:
        return s[f]
    return s[f] + (s[c] - s[f]) * (k - f)


def median(vals):
    return percentile(vals, 50)


def dist_line(vals, label):
    if not vals:
        return f"     {label}: n=0 (no child birth rows with '{label}' read)"
    n = len(vals)
    med = median(vals)
    p10 = percentile(vals, 10)
    p90 = percentile(vals, 90)
    at_one = sum(1 for v in vals if v >= 0.999999) / n
    return (f"     {label}: n={n}  median={med:.4f}  p10={p10:.4f}  p90={p90:.4f}"
            f"  share@1.0={at_one*100:.1f}%")


def read(arm):
    r, ended, err = rows(arm)
    if not r:
        print(f"== {arm}: no report{' (' + err + ')' if err else ''}")
        return {}
    hl = header_line(arm)
    m, rundir = manifest(arm)
    st = stats_last(arm)
    seedmatch = re.search(r"s(\d+)", arm)
    seed = seedmatch.group(1) if seedmatch else "?"
    status = m.get("status", "?")
    reason = m.get("reason")
    final = status == "ended"

    r32c, _, _ = rows(r32_control(seed))
    r32_last = r32c[-1] if r32c else None

    e = r[-1]
    t_end = num(e["t (s)"])
    alive = num(e["alive"])

    held = {}
    print(f"== {arm}  t={t_end:.0f}  status={status} reason={reason}"
          f"  wall={m.get('wallClockMinutes', 'n/a')} min  (r32 control {r32_control(seed)})"
          + ("" if final else "  [RUNNING]"))

    # -- V1: header tokens ----------------------------------------------------------------
    v1_found = {}
    for label, tok in V1_TOKENS:
        v1_found[label] = tok in hl
    held["V1"] = all(v1_found.values())
    print("  V1 header tokens:")
    for k, v in v1_found.items():
        print(f"     {'found' if v else 'MISSING'}: {k}")
    print(f"     -> {'PASS' if held['V1'] else 'FAIL'}")

    # -- V2: identities across all rows ----------------------------------------------------
    aud_vals = [num(x["audit"]) for x in r if num(x["audit"]) is not None]
    resid_vals = [num(x["mat resid"]) for x in r if num(x["mat resid"]) is not None]
    orphan_vals = [num(x["mat orphan"]) for x in r if "mat orphan" in x and num(x["mat orphan"]) is not None]
    aud_max = max((abs(v) for v in aud_vals), default=float("nan"))
    resid_max = max((abs(v) for v in resid_vals), default=float("nan"))
    held["V2"] = aud_max == aud_max and aud_max < 1e-4 and resid_max == resid_max and resid_max == 0.0
    orphan_str = (f"{max(abs(v) for v in orphan_vals)}" if orphan_vals else "column absent")
    print(f"  V2 max |audit| {aud_max:.4f}%  max |mat resid| {resid_max}"
          f"  max |mat orphan| {orphan_str} (printed, not judged)"
          f"  -> {'PASS' if held['V2'] else 'FAIL'}")

    # -- V3: manifest facts + resize instruments -------------------------------------------
    src = m.get("source", {})
    sim8 = (src.get("simHash") or "")[:8]
    core8 = (src.get("coreHash") or "")[:8]
    div_n, div_src = diverged_count(m, rundir)
    drive_lim = m.get("driveImpulsesLimited")
    drag_lim = m.get("dragImpulsesLimited")
    resizes = st.get("resizes")
    resize_jump = st.get("resizeJumpMetres")
    resize_step = st.get("resizeStepMetres")
    print(f"  V3 status={status} reason={reason} physicsJobWorkers={m.get('physicsJobWorkers')}"
          f" gitDirty={src.get('gitDirty')} simHash={sim8} coreHash={core8}")
    print(f"     diverged={div_n} ({div_src}); driveImpulsesLimited={drive_lim}; dragImpulsesLimited={drag_lim}"
          f" (manifest, written at an orderly end only)")
    print(f"     stats.jsonl last row: resizes={resizes}  resizeJumpMetres={resize_jump} (PASS at 0)"
          f"  resizeStepMetres={resize_step} (printed against 0.15 m current-drift per half-second, not judged)")
    if final:
        runaway_fail = reason in RUNAWAY_FAIL_REASONS
        held["V3"] = (m.get("physicsJobWorkers") == 0 and src.get("gitDirty") is False
                       and status == "ended" and not runaway_fail
                       and bool(sim8) and bool(core8)
                       and (resize_jump == 0))
        if runaway_fail:
            print(f"     reason '{reason}' is a runaway ending -> FAIL for the round")
    else:
        held["V3"] = None
    print(f"     -> {'PASS' if held['V3'] else ('FAIL' if held['V3'] is False else 'PROVISIONAL (running)')}")

    def prov(ok):
        return ok if final else None

    # -- G0: the world stands ---------------------------------------------------------------
    c_alive = num(r32_last["alive"]) if r32_last else None
    ratio0 = alive / c_alive if c_alive else float("nan")
    ok0 = c_alive is not None and 0.5 <= ratio0 <= 1.5
    held["G0"] = prov(ok0)
    print(f"  G0 alive {alive:.0f} vs {r32_control(seed)} {c_alive if c_alive is None else f'{c_alive:.0f}'}"
          f" (ratio {ratio0:.2f})  -> {'PASS' if ok0 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}"
          f"  (scorer: run clade-score.ps1 for the goal rule)")

    # -- G1: the dials -------------------------------------------------------------------------
    r1000 = row_at(r, 1000.0)
    r5000 = row_at(r, 5000.0)
    print(f"  G1 dials at 1,000 s / 5,000 s / last ({t_end:.0f} s), and sign of change 1,000s -> last:")
    signs = {}
    for dial in DIALS:
        v1000 = num(r1000[dial]) if r1000 and dial in r1000 else None
        v5000 = num(r5000[dial]) if r5000 and dial in r5000 else None
        vend = num(e[dial]) if dial in e else None
        s = sign(vend, v1000)
        signs[dial] = s
        print(f"     {dial:<12} 1000s={v1000}  5000s={v5000}  last={vend}  -> {s}")
    held["G1"] = "printed"

    # -- G2: the eaters ----------------------------------------------------------------------
    inh_end = num(e["inherit"])
    c_inh_end = num(r32_last["inherit"]) if r32_last else None
    ratio2 = inh_end / c_inh_end if c_inh_end else float("nan")
    r32_5000 = row_at(r32c, 5000.0) if r32c else None
    inh_5000 = num(r5000["inherit"]) if r5000 else None
    c_inh_5000 = num(r32_5000["inherit"]) if r32_5000 else None
    print(f"  G2 inherit last: {inh_end} vs {r32_control(seed)} {c_inh_end} (ratio {ratio2:.2f})")
    print(f"     inherit @5000s: {inh_5000} vs {r32_control(seed)} {c_inh_5000}")
    held["G2"] = "printed"

    # -- G3: growth counters against births ----------------------------------------------------
    births_end = num(e["births"])
    cum_mf = st.get("conceptionsUnderMassFloor")
    cum_short = st.get("growthShortOfMatter")
    ratio_mf = cum_mf / births_end if (cum_mf is not None and births_end) else None
    ratio_short = cum_short / births_end if (cum_short is not None and births_end) else None
    print(f"  G3 conceptionsUnderMassFloor={cum_mf}  growthShortOfMatter={cum_short}  births={births_end:.0f}")
    print(f"     ratio conceptionsUnderMassFloor/births = {ratio_mf}")
    print(f"     ratio growthShortOfMatter/births = {ratio_short}")
    held["G3"] = "printed"

    # -- G4: joints ---------------------------------------------------------------------------
    r1500 = row_at(r, 1500.0)
    jnt_1000 = num(r1000["jnt inh"]) if r1000 else None
    jnt_1500 = num(r1500["jnt inh"]) if r1500 else None
    jnt_5000 = num(r5000["jnt inh"]) if r5000 else None
    jnt_end = num(e["jnt inh"])
    print(f"  G4 jnt inh at 1000s={jnt_1000}  1500s={jnt_1500}  5000s={jnt_5000}  last={jnt_end}")
    held["G4"] = "printed"

    # -- G5: identities (mat blk vs births in the same last window) ----------------------------
    if len(r) >= 2:
        births_prev = num(r[-2]["births"])
        births_window = births_end - births_prev if births_prev is not None else None
    else:
        births_window = None
    matblk_end = num(e["mat blk"])
    if r32c and len(r32c) >= 2:
        c_births_window = num(r32c[-1]["births"]) - num(r32c[-2]["births"])
        c_matblk_end = num(r32c[-1]["mat blk"])
    else:
        c_births_window, c_matblk_end = None, None
    print(f"  G5 mat blk (last window) {matblk_end} vs births (same window) {births_window}"
          f"  ratio={matblk_end/births_window if births_window else None}")
    print(f"     {r32_control(seed)}: mat blk {c_matblk_end} vs births {c_births_window}"
          f"  ratio={c_matblk_end/c_births_window if c_births_window else None}")
    held["G5"] = "printed"

    # -- lineage.jsonl: bf/as distribution over child birth rows --------------------------------
    dist, dist_err = lineage_dist(arm, rundir, final)
    if dist is None:
        print(f"  lineage: {dist_err}")
    else:
        bf_vals, as_vals = dist
        print(f"  lineage (child birth rows, kind 'r'):")
        print(dist_line(bf_vals, "bf"))
        print(dist_line(as_vals, "as"))

    return held, signs if held.get("G1") == "printed" else {}


results = {}
sign_tally = {d: {"down": 0, "up": 0, "flat": 0, "?": 0} for d in DIALS}
sim_hashes, core_hashes = set(), set()
for a in (sys.argv[1:] or ["r33-s1", "r33-s2", "r33-s3", "r33-s4", "r33-s5"]):
    m, _ = manifest(a)
    src = m.get("source", {})
    if src.get("simHash"):
        sim_hashes.add(src["simHash"][:8])
    if src.get("coreHash"):
        core_hashes.add(src["coreHash"][:8])
    held, signs = read(a)
    results[a] = held
    for d in DIALS:
        s = signs.get(d, "?")
        sign_tally[d][s] = sign_tally[d].get(s, 0) + 1
    print()

print("== summary ==")
cols = ["V1", "V2", "V3", "G0", "G1", "G2", "G3", "G4", "G5"]
print("  arm      " + "  ".join(f"{c:>4}" for c in cols))
for a, h in results.items():
    row = []
    for c in cols:
        v = h.get(c) if h else None
        if v == "printed":
            row.append("prnt")
        elif v is None:
            row.append("PROV")
        else:
            row.append("PASS" if v else "FAIL")
    print(f"  {a:<8} " + "  ".join(f"{x:>4}" for x in row))

print()
print(f"  simHash across arms: {sim_hashes if sim_hashes else '(none read)'}"
      f"  -> {'one build' if len(sim_hashes) <= 1 else 'MULTIPLE — arms are not comparable'}")
print(f"  coreHash across arms: {core_hashes if core_hashes else '(none read)'}"
      f"  -> {'one build' if len(core_hashes) <= 1 else 'MULTIPLE — arms are not comparable'}")
print()

print("== G1 cross-seed tally (sign of change, 1,000 s -> last, per dial) ==")
for d in DIALS:
    t = sign_tally[d]
    print(f"  {d:<12} down={t.get('down', 0)}  up={t.get('up', 0)}  flat={t.get('flat', 0)}  unknown={t.get('?', 0)}")

CLAUSES = {
    "V1": "every arm's header carries every round-32 token plus the growth tokens and maxTissue=30000 (0082 V1)",
    "V2": "0.0000% audit and 0 mat resid on every row, every arm; mat orphan printed only (0082 V2)",
    "V3": "ended budget (not a ceiling-population/ceiling-tissue runaway), physicsJobWorkers 0, gitDirty false,"
          " one simHash/coreHash, resizeJumpMetres 0 (0082 V3)",
    "G0": "alive within 0.5-1.5x of round 32's same seed (goal-rule clade scoring not computed here)",
    "G1": "adult scale / invest / brood / body frac at 1000s, 5000s, last, and their sign of change (0082 G1)",
    "G2": "inherit at 5000s and at the end, against round 32 (0082 G2)",
    "G3": "conceptionsUnderMassFloor and growthShortOfMatter against births (0082 G3)",
    "G4": "jnt inh at 1000, 1500, 5000s and the end (0082 G4)",
    "G5": "mat blk in the last window against births in the same window, against round 32 (0082 G5)",
}
for pred in cols:
    vals = [h.get(pred) for h in results.values() if h]
    passed = sum(1 for v in vals if v is True)
    failed = sum(1 for v in vals if v is False)
    prnt = sum(1 for v in vals if v == "printed")
    na = sum(1 for v in vals if v is None)
    total = len(vals)
    print(f"  {pred}: {passed} PASS, {failed} FAIL, {prnt} printed, {na} n/a/provisional of {total}   [{CLAUSES[pred]}]")
