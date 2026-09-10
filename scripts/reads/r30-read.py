"""Round 30's M0-M7 (logbook/0075) per arm, from named columns, the manifest and the controls.

Usage: python scripts/reads/r30-read.py [arm ...]   (default r30-s1..s5; controls r29-sN and r28-sN)
"""
import glob, json, os, re, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
WINDOW = 6000.0


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
    t_end = num(r[-1]["t (s)"])
    return [x for x in r if num(x["t (s)"]) >= t_end - WINDOW]


def read(arm):
    r, ended = rows(arm)
    if not r:
        print(f"{arm}: no report")
        return
    m = manifest(arm)
    st = stats_last(arm)
    seed = re.search(r"s(\d+)$", arm).group(1)
    r29, _ = rows(f"r29-s{seed}")
    r28, _ = rows(f"r28-s{seed}")
    last = last_window(r)
    e = r[-1]
    t_end = num(e["t (s)"])
    alive = num(e["alive"])

    print(f"== {arm}  t={t_end:.0f}  {m.get('status','?')} {m.get('reason','')}  wall {m.get('wallClockMinutes', 0):.0f} min")

    # M0 the world stands
    a29 = num(r29[-1]["alive"]) if r29 else None
    a28 = num(r28[-1]["alive"]) if r28 else None
    ratio = alive / a29 if a29 else float("nan")
    print(f"  M0 alive {alive:.0f} vs r29-s{seed} {a29:.0f} ({ratio:.2f}), r28-s{seed} {a28:.0f}  -> {'held' if 0.5 <= ratio <= 1.5 else 'FAILED'} (scorer separately)")

    # M1 senses
    share = [num(x["sense"]) / num(x["alive"]) for x in last if num(x["alive"])]
    s29 = [num(x["sense"]) / num(x["alive"]) for x in last_window(r29)] if r29 else []
    print(f"  M1 sense/alive last window: min {min(share):.3f} max {max(share):.3f} (r29 min {min(s29):.3f})  -> {'held' if min(share) >= 0.2 else 'FAILED'}")

    # M2 jointed guild
    jnt = [num(x["jnt inh"]) for x in last]
    peak = max((num(x["jnt inh"]), num(x["t (s)"])) for x in r)
    print(f"  M2 jnt inh last window: min {min(jnt):.0f} max {max(jnt):.0f}; whole run peak {peak[0]:.0f} at {peak[1]:.0f} s  -> {'held' if min(jnt) >= 10 else 'FAILED'}")

    # M3
    fj = [num(x["food jnt"]) for x in last if num(x["food jnt"]) is not None]
    fr = [num(x["food rig"]) for x in last if num(x["food rig"]) is not None]
    if fj and fr and min(jnt) >= 10:
        print(f"  M3 food jnt {sum(fj)/len(fj):.2f} vs rig {sum(fr)/len(fr):.2f}")
    else:
        print(f"  M3 not applicable (no jointed guild at the end)")

    # M4 detritus loop
    out = sum(num(x["det out"]) for x in last)
    exu = sum(num(x["det exuded"]) for x in last)
    d_end = num(e["detritus J"])
    d29 = num(r29[-1]["detritus J"]) if r29 else None
    print(f"  M4 det out/exuded last window {out/exu:.2f}; detritus at end {d_end:.0f} J vs r29 {d29:.0f}  -> {'held' if 0.5 <= out/exu <= 2 and d_end < d29 else 'FAILED'}")

    # M5 vertex budget
    maxv = max(max(int(a) for a in x["vtx"].split("/")) for x in r)
    merged = st.get("verticesMerged")
    print(f"  M5 peak vertices {maxv} of cap 100000; verticesMerged {merged}  -> {'held' if maxv < 100000 and merged == 0 else 'FAILED'}")

    # M6 identities
    resid = sum(1 for x in r if num(x["mat resid"]) != 0)
    aud = max(abs(num(x["audit"]) or 0) for x in r)
    short = num(e["mat short"])
    print(f"  M6 mat resid nonzero rows {resid}; max |audit| {aud:.4f}%; mat short {short:.0f}; drag/drive limited {m.get('dragImpulsesLimited')}/{m.get('driveImpulsesLimited')}; diverged {m.get('divergedTotal')}  -> {'held' if resid == 0 and aud < 1e-4 and short == 0 and m.get('dragImpulsesLimited') == 0 and m.get('driveImpulsesLimited') == 0 else 'FAILED'}")

    # M7 matter ceiling
    def per_birth(rr):
        # `mat blk` is a per-window count (CLAUDE.md); births is cumulative, so each row's
        # refusals are read against the births that row's window added.
        w = last_window(rr)
        vals = []
        for i in range(1, len(w)):
            b = num(w[i]["births"]) - num(w[i - 1]["births"])
            if b > 0:
                vals.append(num(w[i]["mat blk"]) / b)
        return sum(vals) / len(vals) if vals else float("nan")
    blk = per_birth(r)
    blk29 = per_birth(r29) if r29 else float("nan")
    lock, lock29 = num(e["mat locked"]), (num(r29[-1]["mat locked"]) if r29 else None)
    print(f"  M7 mat blk per birth {blk:.0f} vs r29 {blk29:.0f}; mat locked {lock:.0f} vs {lock29:.0f}  -> {'held' if blk >= 3 * blk29 and lock > lock29 else 'FAILED'}")

    print(f"  end: absorpt {e['absorpt']} inherit {e['inherit']} (r29 {r29[-1]['inherit'] if r29 else '?'}) photo inh {e['photo inh']} depth {e['depth m']} sd {e['depth sd']} sense {e['sense']}")


for a in (sys.argv[1:] or [f"r30-s{i}" for i in range(1, 6)]):
    read(a)
