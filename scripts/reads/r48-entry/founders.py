"""Round 48 entry (0120): founders by source, the endowment's first child, the bud clades,
the bloom, and the leafless count. Reads lineage.jsonl and stats.jsonl only.
Usage: python founders.py <arm> [runs_root]"""
import glob, json, os, statistics, sys, collections

arm = sys.argv[1]
root = sys.argv[2] if len(sys.argv) > 2 else "runs"
d = sorted(glob.glob(os.path.join(root, arm, "*", "")))[-1]

births = {}
deaths = {}
children = collections.defaultdict(list)
with open(os.path.join(d, "lineage.jsonl"), encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        try:
            r = json.loads(line)
        except Exception:
            continue
        if r["e"] == "b":
            births[r["id"]] = r
            if r.get("p", -1) >= 0:
                children[r["p"]].append((r["t"], r["id"]))
        elif r["e"] == "d":
            deaths[r["id"]] = r["t"]

stats = []
with open(os.path.join(d, "stats.jsonl"), encoding="utf-8") as f:
    for line in f:
        try:
            stats.append(json.loads(line))
        except Exception:
            pass
last_t = stats[-1]["t"]
print(f"# {arm} {d} last stats t={last_t} births={len(births)} deaths={len(deaths)}")

# bloom: the peak by 3,000 s (the floor's close) and the trough after it, by 6,000 s
early = [s for s in stats if s["t"] <= 3000]
pk = max(early, key=lambda s: s["alive"])
after = [s for s in stats if pk["t"] < s["t"] <= 6000]
tr = min(after, key=lambda s: s["alive"])
print(f"bloom peak {pk['alive']} at {pk['t']} s; trough {tr['alive']} at {tr['t']} s; fall {(pk['alive']-tr['alive'])/pk['alive']:.2f}")
# leafless (alive - photosynthetic): max and when
lf = max(stats, key=lambda s: s["alive"] - s["photosynthetic"])
print(f"leafless max {lf['alive']-lf['photosynthetic']} at {lf['t']} s; at end {stats[-1]['alive']-stats[-1]['photosynthetic']}; absorptive at end {stats[-1]['absorptive']} (inherited {stats[-1]['absorptiveInherited']})")
jm = max(stats, key=lambda s: s["jointed"])
print(f"jointed max {jm['jointed']} at {jm['t']} s; at end {stats[-1]['jointed']}")

# founders by source
by = collections.defaultdict(list)
for i, r in births.items():
    if r.get("k") == "f":
        by[r.get("src", "?")].append(r)
for src, rows in sorted(by.items()):
    n = len(rows)
    bred = [r for r in rows if children.get(r["id"])]
    firsts = [min(children[r["id"]])[0] - r["t"] for r in bred]
    within1 = sum(1 for x in firsts if x <= 1.0)
    within10 = sum(1 for x in firsts if x <= 10.0)
    dead_ages = [deaths[r["id"]] - r["t"] for r in rows if r["id"] in deaths]
    endows = [r.get("endow", 0) for r in rows]
    kids = sum(len(children.get(r["id"], [])) for r in rows)
    med = statistics.median(firsts) if firsts else None
    print(f"founders src={src}: n={n} bred={len(bred)} ({len(bred)/n:.3f}) children={kids} "
          f"first-child lag median={med} s, within 1 s {within1}, within 10 s {within10}; "
          f"median age at death {statistics.median(dead_ages) if dead_ages else None} s of {len(dead_ages)} dead; "
          f"endow median {statistics.median(endows):.1f} J")
    if src == "pool":
        idx = collections.Counter((r.get("pool"), bool(children.get(r["id"]))) for r in rows)
        print("  pool index x bred:", dict(sorted(idx.items(), key=lambda kv: (kv[0][0], kv[0][1]))))
        # breeders' later children: children after the first 10 s
        later = sum(1 for r in bred for (t, c) in children[r["id"]] if t - r["t"] > 10)
        print(f"  pool breeders' children more than 10 s after landing: {later}")

# descendants of pool founders: generations reached
def desc(root_id):
    out = []
    stack = [root_id]
    while stack:
        x = stack.pop()
        for (t, c) in children.get(x, []):
            out.append(c)
            stack.append(c)
    return out
pool_ids = [r["id"] for r in by.get("pool", [])]
maxgen = 0
maxsize = (0, None)
for pid in pool_ids:
    ds = desc(pid)
    if ds:
        g = max(births[c]["g"] for c in ds) - births[pid]["g"]
        maxgen = max(maxgen, g)
        if len(ds) > maxsize[0]:
            maxsize = (len(ds), pid)
print(f"pool clades: deepest {maxgen} generations below the founder; largest {maxsize[0]} descendants (founder {maxsize[1]})")

# bud clades: absorptive bud on a plant (bud names absorptive, abs 1, pho 1)
alive_ids = set(i for i in births if i not in deaths)
roots = [r for r in births.values() if "absorptive" in (r.get("bud") or "") and r.get("abs") == 1 and r.get("pho") == 1]
sizes = []
for r in roots:
    ds = desc(r["id"]) + [r["id"]]
    liv = [c for c in ds if c in alive_ids]
    sizes.append((len(liv), len(ds), r["id"], r["t"]))
sizes.sort(reverse=True)
print(f"absorptive bud roots on plants: {len(roots)}; with >=10 living at the end: {sum(1 for s in sizes if s[0] >= 10)}; top five (living, ever, id, born): {sizes[:5]}")
for liv, ever, rid, t in sizes[:3]:
    ds = desc(rid) + [rid]
    liv_rows = [births[c] for c in ds if c in alive_ids]
    flags = collections.Counter((b.get("abs"), b.get("pho")) for b in liv_rows)
    gens = [births[c]["g"] - births[rid]["g"] for c in ds]
    print(f"  root {rid}: living flags (abs,pho) {dict(flags)}; generations below root {max(gens)}")
