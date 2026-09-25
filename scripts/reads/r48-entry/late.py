"""Round 48 entry: (1) round 47 seed 2 at 13,690 s beside round 48 seed 2; (2) pool founders'
first snow reading and age at death by arrival window."""
import glob, json, os, statistics, collections
def stats_at(arm, t):
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    best = None
    for line in open(os.path.join(d, "stats.jsonl"), encoding="utf-8"):
        try: r = json.loads(line)
        except Exception: continue
        if r["t"] <= t: best = r
        else: break
    return best
for arm in ("r47-s2", "r48-s2"):
    r = stats_at(arm, 13690)
    print(arm, "t", r["t"], "alive", r["alive"], "meanAge", round(r["meanAge"]), "invest", round(r["meanBirthInvestment"], 3), "jointed", r["jointed"], "absorptive", r["absorptive"])
for arm in ("r48-s1", "r48-s2", "r48-s3"):
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    births, deaths, kids = {}, {}, collections.Counter()
    for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
        try: r = json.loads(line)
        except Exception: continue
        if r["e"] == "b":
            births[r["id"]] = r
            if r.get("p", -1) >= 0: kids[r["p"]] += 1
        else: deaths[r["id"]] = r["t"]
    pool = {i: r for i, r in births.items() if r.get("src") == "pool"}
    first = {}
    for line in open(os.path.join(d, "absorptive.jsonl"), encoding="utf-8"):
        try: a = json.loads(line)
        except Exception: continue
        if a["id"] in pool and a["id"] not in first: first[a["id"]] = a
    for lo, hi in ((3000, 10000), (10000, 20000), (20000, 30001)):
        ids = [i for i, r in pool.items() if lo <= r["t"] < hi]
        if not ids: continue
        dh = [first[i]["densityHere"] for i in ids if i in first]
        ages = [deaths[i] - pool[i]["t"] for i in ids if i in deaths]
        bred = sum(1 for i in ids if kids[i])
        print(f"{arm} pool founders born {lo}-{hi} s: n={len(ids)} first densityHere median {statistics.median(dh):.3f} (>=0.44: {sum(1 for x in dh if x >= 0.44)}), median age at death {statistics.median(ages) if ages else None} s, bred {bred}")
