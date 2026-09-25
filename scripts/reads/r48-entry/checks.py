"""Round 48 entry (0120): three checks.
1. the pool founders' first absorptive rows (energy, tissue, children) by pool index, and the
   median of their first densityHere against the breeders';
2. whether the top absorptive bud roots in seed 1 are nested;
3. the stomach's share of body and income in root 48048's living mixotrophs at 30,000 s.
Usage: python checks.py <arm> [runs_root]"""
import glob, json, os, statistics, sys, collections

arm = sys.argv[1]
root = sys.argv[2] if len(sys.argv) > 2 else "runs"
d = sorted(glob.glob(os.path.join(root, arm, "*", "")))[-1]

births, deaths = {}, {}
children = collections.defaultdict(list)
for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
    try:
        r = json.loads(line)
    except Exception:
        continue
    if r["e"] == "b":
        births[r["id"]] = r
        if r.get("p", -1) >= 0:
            children[r["p"]].append(r["id"])
    else:
        deaths[r["id"]] = r["t"]

pool = {i: r for i, r in births.items() if r.get("src") == "pool"}
first = {}
last_t = 0
end_rows = {}
for line in open(os.path.join(d, "absorptive.jsonl"), encoding="utf-8"):
    try:
        a = json.loads(line)
    except Exception:
        continue
    last_t = max(last_t, a["t"])
    if a["id"] in pool and a["id"] not in first:
        first[a["id"]] = a
    end_rows.setdefault(a["t"], []).append(a) if a["t"] >= 29999 else None

print(f"# {arm}: pool founders {len(pool)}, with a first absorptive row {len(first)}")
by = collections.defaultdict(list)
for i, a in first.items():
    by[(pool[i].get("pool"), bool(children.get(i)))].append(a)
for k in sorted(by):
    rows = by[k]
    med = lambda f: statistics.median([x[f] for x in rows if x.get(f) is not None])
    print(f"  pool index {k[0]} bred={k[1]} n={len(rows)}: first row age {med('age')} s, energy {med('energy'):.1f} J, "
          f"tissue {med('tissue'):.1f} J, children {med('children')}, densityHere {med('densityHere'):.3f}, "
          f"foodW {med('foodW'):.3f}, upkeepW {med('upkeepW'):.3f}, netW {med('netW'):.3f}")

# nesting of bud roots
def ancestors(i):
    out = []
    while i in births and births[i].get("p", -1) >= 0:
        i = births[i]["p"]
        out.append(i)
    return out
for rid in (52803, 59978, 61200, 61701):
    if rid in births:
        anc = set(ancestors(rid))
        print(f"  bud root {rid}: descends from 48048? {48048 in anc}; from 52803? {52803 in anc}")

# stomach share in the 48048 clade's living mixotrophs at the end
def desc(i):
    out, st = [], [i]
    while st:
        x = st.pop()
        for c in children.get(x, []):
            out.append(c); st.append(c)
    return out
if 48048 in births:
    clade = set(desc(48048) + [48048])
    rows = [a for a in end_rows.get(30000, []) + end_rows.get(30000.0, []) if a["id"] in clade and not a.get("dead")]
    if rows:
        vs = [a["absVolume"] / a["volume"] for a in rows if a["volume"] > 0]
        fs = [a["foodW"] / (a["foodW"] + a["lightW"]) for a in rows if (a["foodW"] + a["lightW"]) > 0]
        dh = [a["densityHere"] for a in rows]
        print(f"  48048 clade at 30,000 s: {len(rows)} logged; absVolume/volume median {statistics.median(vs):.5f}; "
              f"food/(food+light) median {statistics.median(fs):.5f}; densityHere median {statistics.median(dh):.3f}")
