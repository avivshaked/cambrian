"""Round 31's M0-M6 (logbook/0077) per arm, from named columns, the manifest and the
controls. The control for every seed is round 30's same seed, `runs/r30-sN.md`.

Usage: python scripts/reads/r31-read.py [arm ...]   (default r31-s1 r31-s2 r31-s3b r31-s4 r31-s5)
"""
import glob, json, os, re, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
WINDOW = 6000.0
CAP = 100000


def rows(arm):
    path = f"runs/{arm}.md"
    if not os.path.exists(path):
        return [], None
    header, out, ended = None, [], None
    for line in open(path, encoding="utf-8"):
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
    return out, ended


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
    files = sorted(glob.glob(f"runs/{arm}/*/run.json"))
    return json.load(open(files[-1], encoding="utf-8")) if files else {}


def stats_last(arm):
    files = sorted(glob.glob(f"runs/{arm}/*/stats.jsonl"))
    if not files:
        return {}
    last = None
    for line in open(files[-1], encoding="utf-8"):
        if line.strip():
            last = line
    return json.loads(last) if last else {}


def last_window(r):
    if not r:
        return []
    t_end = num(r[-1]["t (s)"])
    return [x for x in r if num(x["t (s)"]) >= t_end - WINDOW]


def mean(col, rows_):
    vals = [num(x[col]) for x in rows_ if num(x[col]) is not None]
    return sum(vals) / len(vals) if vals else float("nan")


def read(arm):
    r, ended = rows(arm)
    if not r:
        print(f"{arm}: no report")
        return {}
    m = manifest(arm)
    st = stats_last(arm)
    seedmatch = re.search(r"s(\d+)", arm)
    seed = seedmatch.group(1) if seedmatch else "?"
    c, _ = rows(f"r30-s{seed}")
    c_last = last_window(c) if c else []
    last = last_window(r)
    e = r[-1]
    ce = c[-1] if c else None
    t_end = num(e["t (s)"])
    alive = num(e["alive"])

    held = {}

    print(f"== {arm}  t={t_end:.0f}  {m.get('status', '?')} {m.get('reason', '')}  wall {m.get('wallClockMinutes', 0):.0f} min  (control r30-s{seed})")

    # M0 the world stands
    c_alive = num(ce["alive"]) if ce else None
    ratio = alive / c_alive if c_alive else float("nan")
    held["M0"] = c_alive is not None and 0.5 <= ratio <= 1.5
    print(f"  M0 alive {alive:.0f} vs r30-s{seed} {c_alive if c_alive is None else f'{c_alive:.0f}'} (ratio {ratio:.2f})"
          f"  -> {'held' if held['M0'] else 'FAILED'}  (the 3-of-5 D063 clause is the scorer's, not computed here)")

    # M1 a still stomach eats a hole
    fa = mean("food rig", last)
    fc = mean("food rig", c_last)
    ja = mean("J/m3 here", last)
    jc = mean("J/m3 here", c_last)
    drop_food = (fc - fa) / fc if fc else float("nan")
    drop_j = (jc - ja) / jc if jc else float("nan")
    held["M1"] = fc == fc and fc != 0 and drop_food >= 0.30 and drop_j < drop_food
    print(f"  M1 food rig mean {fa:.3f} vs r30 {fc:.3f} (drop {drop_food*100:.1f}%); J/m3 here mean {ja:.3f} vs r30 {jc:.3f} (drop {drop_j*100:.1f}%)"
          f"  -> {'held' if held['M1'] else 'FAILED'}")

    # M2 a jointed guild persists
    jnt = [num(x["jnt inh"]) for x in last]
    peak = max((num(x["jnt inh"]), num(x["t (s)"])) for x in r)
    held["M2"] = bool(jnt) and min(jnt) >= 10
    print(f"  M2 jnt inh last window: min {min(jnt):.0f} max {max(jnt):.0f}; whole run peak {peak[0]:.0f} at {peak[1]:.0f} s"
          f"  -> {'held' if held['M2'] else 'FAILED'}")

    # M3 movement pays where it exists (only where a jointed guild exists at the end)
    if held["M2"]:
        fj = mean("food jnt", last)
        fr = mean("food rig", last)
        sj = mean("spd jnt", last)
        sr = mean("spd rig", last)
        held["M3"] = fr == fr and fr != 0 and fj > fr * 1.2 and sr == sr and sj >= sr * 1.5
        print(f"  M3 food jnt {fj:.3f} vs food rig {fr:.3f} (+{(fj/fr-1)*100 if fr else float('nan'):.1f}%);"
              f" spd jnt {sj:.4f} vs spd rig {sr:.4f} (x{sj/sr if sr else float('nan'):.2f})"
              f"  -> {'held' if held['M3'] else 'FAILED'}")
    else:
        held["M3"] = None
        print("  M3 not applicable (no jointed guild at the end)")

    # M4 the loop still closes
    out_sum = sum(num(x["det out"]) for x in last)
    exu_sum = sum(num(x["det exuded"]) for x in last)
    ratio4 = out_sum / exu_sum if exu_sum else float("nan")
    d_end = num(e["detritus J"])
    d_ctrl_end = num(ce["detritus J"]) if ce else None
    held["M4"] = (0.5 <= ratio4 <= 2) and d_ctrl_end is not None and d_end < 3 * d_ctrl_end
    print(f"  M4 det out/exuded last window {ratio4:.2f} (out {out_sum:.0f}, exuded {exu_sum:.0f}); detritus at end {d_end:.0f} J vs 3x r30 {(3*d_ctrl_end) if d_ctrl_end is not None else float('nan'):.0f}"
          f"  -> {'held' if held['M4'] else 'FAILED'}")

    # M5 the larder stays shallow
    dd_end = num(e["det deep"])
    dd_ctrl = num(ce["det deep"]) if ce else None
    floor_pct = [num(x["% on floor"]) for x in last]
    held["M5"] = (dd_ctrl is not None and dd_end < dd_ctrl) and bool(floor_pct) and max(floor_pct) < 5
    print(f"  M5 det deep at end {dd_end:.4f} vs r30 {dd_ctrl if dd_ctrl is None else f'{dd_ctrl:.4f}'}; % on floor last window max {max(floor_pct) if floor_pct else float('nan'):.2f}%"
          f"  -> {'held' if held['M5'] else 'FAILED'}")

    # M6 the identities close
    resid_nonzero = sum(1 for x in r if num(x["mat resid"]) != 0)
    short_nonzero = sum(1 for x in r if num(x["mat short"]) != 0)
    aud_max = max(abs(num(x["audit"]) or 0) for x in r)
    drag = m.get("dragImpulsesLimited")
    drive = m.get("driveImpulsesLimited")
    diverged = m.get("divergedTotal")
    merged = st.get("verticesMerged")
    maxv = max(max(int(a) for a in x["vtx"].split("/")) for x in r)
    held["M6"] = (resid_nonzero == 0 and short_nonzero == 0 and aud_max < 1e-4
                  and drag == 0 and drive == 0 and diverged == 0
                  and maxv < CAP and merged == 0)
    print(f"  M6 mat resid nonzero rows {resid_nonzero}; mat short nonzero rows {short_nonzero}; max |audit| {aud_max:.4f}%;"
          f" drag/drive limited {drag}/{drive}; diverged {diverged}; peak vtx {maxv} of cap {CAP}; verticesMerged {merged}"
          f"  -> {'held' if held['M6'] else 'FAILED'}")

    print(f"  end: alive {e['alive']} (r30 {ce['alive'] if ce else '?'})"
          f" inherit {e['inherit']} (r30 {ce['inherit'] if ce else '?'})"
          f" jnt inh {e['jnt inh']} (r30 {ce['jnt inh'] if ce else '?'})"
          f" sense {e['sense']} (r30 {ce['sense'] if ce else '?'})"
          f" depth m {e['depth m']} (r30 {ce['depth m'] if ce else '?'})"
          f" food rig {e['food rig']} (r30 {ce['food rig'] if ce else '?'})"
          f" J/m3 here {e['J/m3 here']} (r30 {ce['J/m3 here'] if ce else '?'})")

    return held


results = {}
for a in (sys.argv[1:] or ["r31-s1", "r31-s2", "r31-s3b", "r31-s4", "r31-s5"]):
    results[a] = read(a)
    print()

print("== summary ==")
for pred in ["M0", "M1", "M2", "M3", "M4", "M5", "M6"]:
    vals = [h.get(pred) for h in results.values() if h]
    held_n = sum(1 for v in vals if v is True)
    na_n = sum(1 for v in vals if v is None)
    total = len(vals)
    note = f", {na_n} not applicable" if na_n else ""
    extra = "  (D063 3-of-5 clause scored separately)" if pred == "M0" else ""
    print(f"  {pred}: {held_n} of {total} held{note}{extra}")
