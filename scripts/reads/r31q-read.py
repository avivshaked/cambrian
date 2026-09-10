"""Round 31's fast-step screen against 0077's sequencing section, beside round 30's screen.

Usage: python scripts/reads/r31q-read.py
"""
import glob, json, os

os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def rows(arm):
    header, out = None, []
    path = f"runs/{arm}.md"
    if not os.path.exists(path):
        return out
    for line in open(path, encoding="utf-8"):
        if not line.startswith("|"):
            continue
        c = [x.strip().strip("*").strip() for x in line.strip().strip("|").split("|")]
        if header is None:
            header = c
            continue
        if set(c[0]) <= set("-") or len(c) != len(header):
            continue
        out.append(dict(zip(header, c)))
    return out


def num(v):
    try:
        return float(v.replace("%", "").replace(",", ""))
    except (ValueError, AttributeError):
        return None


def manifest(arm):
    f = sorted(glob.glob(f"runs/{arm}/*/run.json"))
    return json.load(open(f[-1], encoding="utf-8")) if f else {}


def stats_last(arm):
    f = sorted(glob.glob(f"runs/{arm}/*/stats.jsonl"))
    if not f:
        return {}
    last = None
    for line in open(f[-1], encoding="utf-8"):
        if line.strip():
            last = line
    return json.loads(last) if last else {}


print("arm      alive(r30q)  inherit(r30q)  jnt-inh end/peak@t   food rig end (r30q)  layer mean (r30q)  det kJ end (r30q)  out/exu last 4000 s  vtx det/mat  resid!=0  short  audit>0  merged  status")
for s in range(1, 6):
    arm, ctl = f"r31q-s{s}", f"r30q-s{s}"
    r, c = rows(arm), rows(ctl)
    if not r:
        print(f"{arm} no report")
        continue
    e, ce = r[-1], (c[-1] if c else {})
    last = [x for x in r if num(x["t (s)"]) >= num(e["t (s)"]) - 4000]
    ratio = sum(num(x["det out"]) for x in last) / max(1e-9, sum(num(x["det exuded"]) for x in last))
    peak = max((num(x["jnt inh"]) or 0, num(x["t (s)"])) for x in r)
    m = manifest(arm)
    st = stats_last(arm)
    resid = sum(1 for x in r if num(x["mat resid"]) != 0)
    aud = sum(1 for x in r if abs(num(x["audit"]) or 0) > 0)
    fr, cfr = num(e["food rig"]), num(ce.get("food rig", ""))
    lm, clm = num(e["J/m3 here"]), num(ce.get("J/m3 here", ""))
    print(
        f"{arm}  {e['alive']:>5} ({ce.get('alive','?'):>5})  {e['inherit']:>4} ({ce.get('inherit','?'):>4})  "
        f"{e['jnt inh']:>3}/{peak[0]:.0f}@{peak[1]:.0f}  "
        f"{fr if fr is not None else float('nan'):>6.2f} ({cfr if cfr is not None else float('nan'):.2f})  "
        f"{lm if lm is not None else float('nan'):>6.2f} ({clm if clm is not None else float('nan'):.2f})  "
        f"{num(e['detritus J'])/1000:>6.1f} ({num(ce.get('detritus J','0'))/1000:.1f})  {ratio:>6.2f}  "
        f"{e['vtx']:>11}  {resid:>3}  {e['mat short']:>4}  {aud:>3}  {st.get('verticesMerged')}  {m.get('status')} {m.get('reason','')} t={e['t (s)']}"
    )
