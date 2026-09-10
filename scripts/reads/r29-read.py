"""Round 29's M1-M6 (logbook/0072) per arm, from named columns, the manifest and round 28's ending.

Usage: python scripts/reads/r29-read.py [arm ...]   (default r29-s1..s5; control r28-sN by seed)
"""
import glob, json, os, re, sys

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
WINDOW = 6000.0


def rows(arm):
    path = f"runs/{arm}.md"
    if not os.path.exists(path):
        return None, [], None
    header = None
    out = []
    ended = None
    for line in open(path, encoding="utf-8"):
        if line.startswith("**Ended"):
            ended = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip().strip("*").strip() for c in line.strip().strip("|").split("|")]
        if header is None:
            header = cells
            continue
        if set(cells[0]) <= set("-"):
            continue
        if len(cells) != len(header):
            continue
        out.append(dict(zip(header, cells)))
    return header, out, ended


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
    if not files:
        return {}
    return json.load(open(files[-1], encoding="utf-8"))


def read(arm):
    header, r, ended = rows(arm)
    if not r:
        print(f"{arm}: no report")
        return
    m = manifest(arm)
    t_end = num(r[-1]["t (s)"])
    last = [x for x in r if num(x["t (s)"]) >= t_end - WINDOW]
    alive_end = num(r[-1]["alive"])
    seed = re.search(r"s(\d+)$", arm).group(1)
    _, r28, _ = rows(f"r28-s{seed}")
    alive28 = num(r28[-1]["alive"]) if r28 else None

    sense_share = [num(x["sense"]) / num(x["alive"]) for x in last if num(x["sense"]) is not None and num(x["alive"])]
    jnt = [num(x["jnt inh"]) for x in last]
    fj = [num(x["food jnt"]) for x in last if num(x["food jnt"]) is not None]
    fr = [num(x["food rig"]) for x in last if num(x["food rig"]) is not None]
    sj = [num(x["spd jnt"]) for x in last if num(x["spd jnt"]) is not None]
    audit = max(abs(num(x["audit"]) or 0.0) for x in last)
    div = num(r[-1]["diverged"])
    limited = m.get("dragImpulsesLimited", m.get("footer", {}).get("dragImpulsesLimited") if isinstance(m.get("footer"), dict) else None)

    status = m.get("status", "?")
    print(f"== {arm}  t={t_end:.0f}  status {status} {m.get('reason','')}  ({ended or 'running'})")
    print(f"  M1 sense/alive over last {WINDOW:.0f} s: min {min(sense_share):.3f} max {max(sense_share):.3f}  -> {'held' if min(sense_share) >= 0.2 else 'FAILED'} (>= 0.20 at every sample)")
    print(f"  M2 jnt inh over last window: min {min(jnt):.0f} max {max(jnt):.0f}  -> {'held' if min(jnt) >= 10 else 'FAILED'} (>= 10 at every sample)")
    if fj and fr:
        mj, mr = sum(fj) / len(fj), sum(fr) / len(fr)
        ms = sum(sj) / len(sj) if sj else float('nan')
        print(f"  M3 food jnt {mj:.2f} vs rig {mr:.2f} ({(mj/mr-1)*100:+.0f}%), spd jnt {ms:.3f}  -> {'held' if mj > 1.2*mr and ms >= 0.05 else 'FAILED'}")
    else:
        print(f"  M3 no jointed guild in the last window (food jnt absent in {len(last)-len(fj)} of {len(last)} samples) -> not applicable")
    if alive28 is not None:
        ratio = alive_end / alive28
        print(f"  M5 alive {alive_end:.0f} vs r28-s{seed} {alive28:.0f} = {ratio:.2f}, diverged {div:.0f}  -> {'held' if 0.75 <= ratio <= 1.25 and div == 0 else 'FAILED'}")
    else:
        print(f"  M5 alive {alive_end:.0f}, diverged {div:.0f}; r28-s{seed} report not found")
    print(f"  M6 max |audit| over last window {audit:.4f}%, dragImpulsesLimited {limited}  -> {'held' if audit < 0.0001 and (limited in (0, None)) else 'check'}")
    print(f"  end: alive {alive_end:.0f} absorpt {r[-1]['absorpt']} inherit {r[-1]['inherit']} depth {r[-1]['depth m']} det {r[-1]['detritus J']}")


arms = sys.argv[1:] or [f"r29-s{i}" for i in range(1, 6)]
for a in arms:
    read(a)
