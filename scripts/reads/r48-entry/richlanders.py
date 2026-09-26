"""Round 48 entry: pool founders whose first snow reading was at or above 1 J/m3, whether they
bred after their first ten seconds, and their lines' sizes; and the nesting of seed 1's bud
roots 59978 and 61200."""
import glob, json, os, collections, sys
for arm in ("r48-s1", "r48-s2", "r48-s3"):
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    births, deaths, kids = {}, {}, collections.defaultdict(list)
    for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
        try: r = json.loads(line)
        except Exception: continue
        if r["e"] == "b":
            births[r["id"]] = r
            if r.get("p", -1) >= 0: kids[r["p"]].append(r["id"])
        else: deaths[r["id"]] = r["t"]
    pool = {i: r for i, r in births.items() if r.get("src") == "pool"}
    first = {}
    for line in open(os.path.join(d, "absorptive.jsonl"), encoding="utf-8"):
        try: a = json.loads(line)
        except Exception: continue
        if a["id"] in pool and a["id"] not in first: first[a["id"]] = a
    def desc(i):
        out, st = [], [i]
        while st:
            x = st.pop()
            for c in kids.get(x, []): out.append(c); st.append(c)
        return out
    rich = [i for i in pool if i in first and first[i]["densityHere"] >= 1.0]
    print(arm, "pool founders", len(pool), "first reading >= 1 J/m3:", len(rich))
    for i in sorted(rich, key=lambda i: pool[i]["t"]):
        ds = desc(i)
        late = sum(1 for c in kids.get(i, []) if births[c]["t"] - pool[i]["t"] > 10)
        print(f"  {i} idx {pool[i].get('pool')} landed {pool[i]['t']} first {first[i]['densityHere']:.2f} J/m3, children {len(kids.get(i, []))} (after 10 s: {late}), descendants {len(ds)}, lived {deaths.get(i, float('nan')) - pool[i]['t']:.0f} s")
    if arm == "r48-s1":
        def anc(i):
            out = set()
            while i in births and births[i].get("p", -1) >= 0:
                i = births[i]["p"]; out.add(i)
            return out
        print("  61200 descends from 59978?", 59978 in anc(61200), "; 59978 from 61200?", 61200 in anc(59978))
