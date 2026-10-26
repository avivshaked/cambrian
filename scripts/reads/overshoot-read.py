"""The pace screen's overshoot marks (logbook/specs/r62p-prereg/predictions.md, "The overshoot, and what follows it"), per
3,000 s bin, for one world against its control on the same seed:

- leaves: the stats column photosynthetic (living bodies holding photosynthetic tissue) at the bin's end, in the
  world and in the control at the same second, and their ratio. The mark is a ratio under 0.5;
- grazers lead the fall: of the leaves that died in the bin (a body born photosynthetic, pho 1, whose death row falls
  in the bin), the share killed whole by a grazer (a kill row with root 1 whose attacker was born mouthed, atk 1 and
  ink 1), against each other cause of death. The mark is that grazers' kills are the largest single cause. This reads
  "the largest share of what the leaves lose" by deaths, not joules: the record does not book a living leaf's lost
  tissue by who took it;
- breeding: the replacement of the bin before's cohort, the mouthed children born to grazers born in the bin before,
  counted to this bin's end, per grazer of that cohort. The mark is at least 1.

The third mark, the oases' island share at or above 0.6, is r60-read.py's on-isl column read with --year 3000, which
this script does not compute. A bin overshoots when all three hold.

Usage: python -I scripts/reads/overshoot-read.py WORLD_RUN CONTROL_RUN [--bin 3000] [--end 24000]
"""
import argparse
import json
import os

ap = argparse.ArgumentParser()
ap.add_argument("world")
ap.add_argument("control")
ap.add_argument("--bin", type=float, default=3000.0)
ap.add_argument("--end", type=float, default=24000.0)
a = ap.parse_args()


def stats_at(run):
    out = {}
    for line in open(os.path.join(run, "stats.jsonl")):
        if line.strip():
            r = json.loads(line)
            if r.get("t") is not None:
                out[r["t"]] = r
    return out


def nearest(rows, t):
    keys = [k for k in rows if k <= t + 1e-6]
    return rows[max(keys)] if keys else None


births, deaths, kills = {}, [], {}
for line in open(os.path.join(a.world, "lineage.jsonl")):
    if not line.strip():
        continue
    r = json.loads(line)
    if r.get("t", 0) > a.end:
        break
    e = r.get("e")
    if e == "b":
        births[r["id"]] = r
    elif e == "d":
        deaths.append(r)
    elif e == "k" and r.get("root") == 1:
        kills[r["id"]] = r


def grazer(b):
    return b is not None and b.get("atk") == 1 and b.get("ink") == 1


def leaf(b):
    return b is not None and b.get("pho") == 1


ws, cs = stats_at(a.world), stats_at(a.control)
print(f"world   {a.world}\ncontrol {a.control}\nbins of {a.bin:.0f} s to {a.end:.0f} s")
print(f"{'bin end':>8} {'leaves':>7} {'control':>8} {'ratio':>6} {'<0.5':>5} | {'leaf deaths':>11} {'by grazer':>9} "
      f"{'share':>6} {'largest cause':>16} {'lead':>5} | {'cohort':>6} {'kids':>5} {'repl':>6} {'>=1':>4}")
t = a.bin
while t <= a.end + 1e-6:
    w, c = nearest(ws, t), nearest(cs, t)
    lw = w.get("photosynthetic") if w else None
    lc = c.get("photosynthetic") if c else None
    ratio = lw / lc if lw is not None and lc else float("nan")

    causes = {}
    for d in deaths:
        if t - a.bin < d["t"] <= t and leaf(births.get(d["id"])):
            k = kills.get(d["id"])
            cause = "grazer kill" if k is not None and grazer(births.get(k.get("by"))) else (
                "other kill" if k is not None else d.get("c") or "unknown")
            causes[cause] = causes.get(cause, 0) + 1
    total = sum(causes.values())
    by_grazer = causes.get("grazer kill", 0)
    top = max(causes.items(), key=lambda kv: kv[1])[0] if causes else "none"

    lo, hi = t - 2 * a.bin, t - a.bin
    cohort = [i for i, b in births.items() if grazer(b) and lo < b["t"] <= hi]
    ids = set(cohort)
    kids = sum(1 for b in births.values() if grazer(b) and b.get("p") in ids and b["t"] <= t)
    repl = kids / len(cohort) if cohort else float("nan")

    print(f"{t:8.0f} {lw if lw is not None else '-':>7} {lc if lc is not None else '-':>8} {ratio:6.3f} "
          f"{'yes' if ratio < 0.5 else 'no':>5} | {total:11d} {by_grazer:9d} {by_grazer / total if total else float('nan'):6.3f} "
          f"{top:>16} {'yes' if top == 'grazer kill' else 'no':>5} | {len(cohort):6d} {kids:5d} {repl:6.3f} "
          f"{'yes' if repl >= 1 else 'no':>4}")
    t += a.bin
