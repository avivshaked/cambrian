"""Round 48 entry: pool founders' first-row crowding share and age at death by pool index."""
import glob, json, os, statistics, sys, collections
arm = sys.argv[1]
d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
births, deaths = {}, {}
for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
    try: r = json.loads(line)
    except Exception: continue
    if r["e"] == "b": births[r["id"]] = r
    else: deaths[r["id"]] = r["t"]
pool = {i: r for i, r in births.items() if r.get("src") == "pool"}
first = {}
for line in open(os.path.join(d, "absorptive.jsonl"), encoding="utf-8"):
    try: a = json.loads(line)
    except Exception: continue
    if a["id"] in pool and a["id"] not in first: first[a["id"]] = a
sh = [a["share"] for a in first.values()]
print(arm, "first-row share: median", statistics.median(sh), "share<1:", sum(1 for s in sh if s < 0.999), "of", len(sh))
by = collections.defaultdict(list)
for i, r in pool.items():
    if i in deaths: by[r.get("pool")].append(deaths[i] - r["t"])
for k in sorted(by): print("  pool index", k, "dead", len(by[k]), "median age at death", statistics.median(by[k]))
