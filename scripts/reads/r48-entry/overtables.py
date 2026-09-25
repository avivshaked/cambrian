"""Round 48 entry: where pool founders stood at their first positions row, against the reefs
(the plain radius, lobes ignored): over a table (within r of an axis, above the cap top), under a
cap, or open water; with the first snow reading by place."""
import glob, json, math, os, statistics, sys, collections
for arm in sys.argv[1:]:
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    reefs = json.load(open(os.path.join(d, "run.json")))["reefs"]
    pool = {}
    for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
        try: r = json.loads(line)
        except Exception: continue
        if r["e"] == "b" and r.get("src") == "pool": pool[r["id"]] = r["t"]
    first_pos = {}
    want = set(pool)
    for line in open(os.path.join(d, "positions.jsonl"), encoding="utf-8"):
        try: row = json.loads(line)
        except Exception: continue
        for b in row["b"]:
            if b[0] in want and b[0] not in first_pos and row["t"] >= pool[b[0]]:
                first_pos[b[0]] = (row["t"], b[1], b[2], b[3])
        if len(first_pos) == len(want): break
    first_dh = {}
    for line in open(os.path.join(d, "absorptive.jsonl"), encoding="utf-8"):
        try: a = json.loads(line)
        except Exception: continue
        if a["id"] in want and a["id"] not in first_dh: first_dh[a["id"]] = a["densityHere"]
    place = collections.defaultdict(list)
    for i, (t, x, y, z) in first_pos.items():
        where = "open"
        for rf in reefs:
            if math.hypot(x - rf["x"], z - rf["z"]) <= rf["r"]:
                where = "over table" if y > -rf["depth"] else ("under cap" if y < -(rf["depth"] + 2) else "in rock band")
                break
        place[where].append(first_dh.get(i))
    for k, v in sorted(place.items()):
        vv = [x for x in v if x is not None]
        print(f"{arm} {k}: {len(v)} founders, first densityHere median {statistics.median(vv):.3f}")
