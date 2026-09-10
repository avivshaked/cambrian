"""Round 32's V1-V3 and M0-M6 (logbook/0079) per arm, from named columns, the manifest
and the controls. Round 32 is the grid world's first count; the reference for M0 and M3
is round 31's same seed (the vertex world, `runs/r31-sN.md`, except seed 3 whose finished
run is `r31-s3b` — `r31-s3` errored at 11,533 s), and for M5 round 30's same seed
(`runs/r30-sN.md`). V1/V2/V3 are the validity checks from 0079's "Validity checks"
section; the D063-amended clade scoring in M0's second clause is NOT computed here — run
`scripts/clade-score.ps1` for that.

Reads live files with a tolerant open: a report or stats.jsonl may still be being written
by a running arm. Never polls, sleeps or writes outside the repo, and never touches
anything under unity*.

Usage: python scripts/reads/r32-read.py [arm ...]   (default r32-s1 r32-s2 r32-s3 r32-s4 r32-s5)
"""
import glob, json, os, re, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
WINDOW = 6000.0


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
            if not line.startswith("|") and header is None and out == [] and line.strip() and not line.startswith("#"):
                # first non-blank, non-heading, non-table line: the params header line
                pass
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
    chronologically, so [-1] is the most recent launch — the one that matters when an
    arm was relaunched, as all five of round 32 were). Returns (dict, rundir)."""
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


def last_window(r):
    if not r:
        return []
    t_end = num(r[-1]["t (s)"])
    return [x for x in r if num(x["t (s)"]) is not None and num(x["t (s)"]) >= t_end - WINDOW]


def r31_control(seed):
    return "r31-s3b" if seed == "3" else f"r31-s{seed}"


def r30_control(seed):
    return f"r30-s{seed}"


V1_TOKENS = [
    ("field grid", "field grid"),
    ("cell=1", "cell=1"),
    ("mcell=5", "mcell=5"),
    ("corpse=0.005/s", "corpse=0.005/s"),
    ("mixing 0.02 m2/s", "mixing 0.02 m2/s"),
    ("h-mix 0.02 m2/s", "h-mix 0.02 m2/s"),
    ("addedMass 0.5", "addedMass 0.5"),
    ("dt=0.01", "dt=0.01"),
]


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
    final = status == "ended"

    r31c, _, _ = rows(r31_control(seed))
    r30c, _, _ = rows(r30_control(seed))
    r31_last = r31c[-1] if r31c else None
    r30_last = r30c[-1] if r30c else None

    e = r[-1]
    t_end = num(e["t (s)"])
    alive = num(e["alive"])

    held = {}
    print(f"== {arm}  t={t_end:.0f}  status={status} reason={m.get('reason', '')}"
          f"  wall={m.get('wallClockMinutes', 'n/a')} min  (r31 control {r31_control(seed)}, r30 control {r30_control(seed)})"
          + ("" if final else "  [RUNNING]"))

    # -- V1: header tokens --------------------------------------------------
    v1_found = {}
    for label, tok in V1_TOKENS:
        v1_found[label] = tok in hl
    held["V1"] = all(v1_found.values())
    print("  V1 header tokens: " + "; ".join(f"{k}={'found' if v else 'MISSING'}" for k, v in v1_found.items()))
    print(f"     -> {'PASS' if held['V1'] else 'FAIL'}")

    # -- V2: identities across all rows -------------------------------------
    aud_vals = [num(x["audit"]) for x in r if num(x["audit"]) is not None]
    resid_vals = [num(x["mat resid"]) for x in r if num(x["mat resid"]) is not None]
    aud_max = max((abs(v) for v in aud_vals), default=float("nan"))
    resid_max = max((abs(v) for v in resid_vals), default=float("nan"))
    held["V2"] = aud_max == aud_max and aud_max < 1e-4 and resid_max == resid_max and resid_max == 0.0
    print(f"  V2 max |audit| {aud_max:.4f}%  max |mat resid| {resid_max}"
          f"  -> {'PASS' if held['V2'] else 'FAIL'}")

    # -- V3: manifest facts ---------------------------------------------------
    src = m.get("source", {})
    sim8 = (src.get("simHash") or "")[:8]
    core8 = (src.get("coreHash") or "")[:8]
    div_n, div_src = diverged_count(m, rundir)
    drive_lim = m.get("driveImpulsesLimited")
    drag_lim = m.get("dragImpulsesLimited")
    # "any drag limiter count from the last stats row" -- stats.jsonl carries no
    # per-step drag/drive-limiter field in this build; the cumulative counts live only
    # in the manifest (written at an orderly end). Search the last stats row anyway in
    # case a future build adds one, so this does not silently go stale.
    stats_drag_keys = {k: v for k, v in st.items() if "drag" in k.lower() or ("limit" in k.lower())}
    print(f"  V3 status={status} reason={m.get('reason')} physicsJobWorkers={m.get('physicsJobWorkers')}"
          f" gitDirty={src.get('gitDirty')} simHash={sim8} coreHash={core8}")
    print(f"     diverged={div_n} ({div_src}); driveImpulsesLimited={drive_lim}; dragImpulsesLimited={drag_lim}"
          f" (manifest, written at an orderly end only)")
    if stats_drag_keys:
        print(f"     stats.jsonl last row also carries: {stats_drag_keys}")
    else:
        print("     stats.jsonl last row carries no drag/drive-limiter field in this build")
    if final:
        held["V3"] = (m.get("physicsJobWorkers") == 0 and src.get("gitDirty") is False
                       and status == "ended" and m.get("reason") == "budget"
                       and bool(sim8) and bool(core8))
    else:
        held["V3"] = None
    print(f"     -> {'PASS' if held['V3'] else ('FAIL' if held['V3'] is False else 'PROVISIONAL (running)')}")

    def prov(label, ok):
        if final:
            return ok
        return None

    # -- M0: the grid stands --------------------------------------------------
    c_alive = num(r31_last["alive"]) if r31_last else None
    ratio0 = alive / c_alive if c_alive else float("nan")
    ok0 = c_alive is not None and 0.5 <= ratio0 <= 1.5
    held["M0"] = prov("M0", ok0)
    print(f"  M0 alive {alive:.0f} vs {r31_control(seed)} {c_alive if c_alive is None else f'{c_alive:.0f}'}"
          f" (ratio {ratio0:.2f})  -> {'PASS' if ok0 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}"
          f"  (D063 3-of-5 clause: scorer: run clade-score.ps1)")

    # -- M1: identities plus neither impulse limiter binds ---------------------
    ok1 = held["V2"] and (drive_lim == 0 and drag_lim == 0) if final else False
    held["M1"] = prov("M1", ok1) if final else None
    print(f"  M1 identities {'closed' if held['V2'] else 'OPEN'}; impulse limiters drive={drive_lim} drag={drag_lim}"
          f"  -> {'PASS' if ok1 and final else ('FAIL' if final else 'PROVISIONAL (running, limiter totals unknown until ended)')}")

    # -- M2: the loop closes, only in a seed that passes M0 ---------------------
    last6k = last_window(r)
    out_sum = sum((num(x["det out"]) or 0) for x in last6k)
    exu_sum = sum((num(x["det exuded"]) or 0) for x in last6k)
    ratio2 = out_sum / exu_sum if exu_sum else float("nan")
    ok2 = exu_sum > 0 and 0.5 <= ratio2 <= 2
    if ok0 is not True:
        held["M2"] = None
        print(f"  M2 (columns used: 'det out', 'det exuded') not applicable -- M0 did not hold for this seed")
    else:
        held["M2"] = prov("M2", ok2)
        print(f"  M2 det out/exuded over last {WINDOW:.0f} s: {ratio2:.2f} (out {out_sum:.0f}, exuded {exu_sum:.0f})"
              f"  (columns used: 'det out', 'det exuded')"
              f"  -> {'PASS' if ok2 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}")

    # -- M3: the matter economy is the vertex world's --------------------------
    ml_end = num(e["mat locked"])
    ml_ctrl = num(r31_last["mat locked"]) if r31_last else None
    ratio3 = ml_end / ml_ctrl if ml_ctrl else float("nan")
    ok3 = ml_ctrl is not None and abs(ratio3 - 1.0) <= 0.10
    held["M3"] = prov("M3", ok3)
    print(f"  M3 mat locked {ml_end} vs {r31_control(seed)} {ml_ctrl} (ratio {ratio3:.3f})"
          f"  -> {'PASS' if ok3 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}")

    # -- M4: corpses hold a small standing share --------------------------------
    corpse_j = st.get("corpseJoules")
    detritus_j = st.get("detritusJoules")
    if corpse_j is not None and detritus_j is not None and (detritus_j + corpse_j) > 0:
        share4 = corpse_j / (detritus_j + corpse_j)
        ok4 = share4 < 0.10
    else:
        share4 = None
        ok4 = False
    held["M4"] = prov("M4", ok4)
    print(f"  M4 corpseJoules {corpse_j} / (detritusJoules {detritus_j} + corpseJoules) = "
          f"{share4 if share4 is None else f'{share4*100:.2f}%'}"
          f"  (stats.jsonl fields used: 'corpseJoules', 'detritusJoules')"
          f"  -> {'PASS' if ok4 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}")

    # -- M5: the larder still goes deep -----------------------------------------
    dd_end = num(e["det deep"])
    dd_ctrl = num(r30_last["det deep"]) if r30_last else None
    ok5 = dd_ctrl is not None and dd_end is not None and dd_end > dd_ctrl
    held["M5"] = prov("M5", ok5)
    print(f"  M5 det deep {dd_end} vs {r30_control(seed)} {dd_ctrl}"
          f"  -> {'PASS' if ok5 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}")

    # -- M6: no swimmer, still ---------------------------------------------------
    jnt_end = num(e["jnt inh"])
    ok6 = jnt_end == 0
    held["M6"] = prov("M6", ok6)
    print(f"  M6 jnt inh {jnt_end}  -> {'PASS' if ok6 else 'FAIL'}{'' if final else '  [PROVISIONAL, running]'}")

    return held


results = {}
sim_hashes, core_hashes = set(), set()
for a in (sys.argv[1:] or ["r32-s1", "r32-s2", "r32-s3", "r32-s4", "r32-s5"]):
    m, _ = manifest(a)
    src = m.get("source", {})
    if src.get("simHash"):
        sim_hashes.add(src["simHash"][:8])
    if src.get("coreHash"):
        core_hashes.add(src["coreHash"][:8])
    results[a] = read(a)
    print()

print("== summary ==")
cols = ["V1", "V2", "V3", "M0", "M1", "M2", "M3", "M4", "M5", "M6"]
print("  arm      " + "  ".join(f"{c:>4}" for c in cols))
for a, h in results.items():
    row = []
    for c in cols:
        v = h.get(c) if h else None
        row.append(" n/a" if v is None else ("PASS" if v else "FAIL"))
    print(f"  {a:<8} " + "  ".join(f"{x:>4}" for x in row))

print()
print(f"  simHash across arms: {sim_hashes if sim_hashes else '(none read)'}"
      f"  -> {'one build' if len(sim_hashes) <= 1 else 'MULTIPLE — arms are not comparable'}")
print(f"  coreHash across arms: {core_hashes if core_hashes else '(none read)'}"
      f"  -> {'one build' if len(core_hashes) <= 1 else 'MULTIPLE — arms are not comparable'}")
print()

CLAUSES = {
    "V1": "every arm's header carries every token (0079 Validity checks)",
    "V2": "0.0000% audit and 0 mat resid on every row, every arm (0079 Validity checks)",
    "V3": "ended budget, physicsJobWorkers 0, gitDirty false, one simHash/coreHash, every arm (0079 Validity checks)",
    "M0": "alive within 0.5-1.5x in every arm (D063 3-of-5 clause not computed here)",
    "M1": "identities close and neither limiter binds, at every sample of every arm",
    "M2": "det out/exuded in [0.5, 2] in every seed that passes M0",
    "M3": "mat locked within 10% of r31 in at least 4 of 5",
    "M4": "corpseJoules under 10% of detritus+corpse in every arm",
    "M5": "det deep above r30 in at least 4 of 5",
    "M6": "jnt inh reads 0 in at least 4 of 5",
}
for pred in cols:
    vals = [h.get(pred) for h in results.values() if h]
    passed = sum(1 for v in vals if v is True)
    failed = sum(1 for v in vals if v is False)
    na = sum(1 for v in vals if v is None)
    total = len(vals)
    print(f"  {pred}: {passed} PASS, {failed} FAIL, {na} n/a/provisional of {total}   [{CLAUSES[pred]}]")
