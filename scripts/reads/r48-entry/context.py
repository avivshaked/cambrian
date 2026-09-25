"""Round 48 entry (0120): shading, matter and size fields of stats.jsonl at 10,000 s, 20,000 s
and the last sample, rounds 48 and 47. Usage: python context.py > context.txt"""
import glob, json, os
keys = ["t", "alive", "shading", "meanExposure", "uptakeLimitedShare", "matterLocked", "matterStanding",
        "matterInfluxedTotal", "meanAdultScale", "meanBodyFraction", "meanReach", "detritusJoules",
        "corpseJoules", "lightJoules", "spendJoules", "meanHeight", "heightSd"]
for arm in ("r48-s1", "r48-s3", "r47-s1", "r47-s3", "r48-s2"):
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    rows = []
    for line in open(os.path.join(d, "stats.jsonl"), encoding="utf-8"):
        try: rows.append(json.loads(line))
        except Exception: pass
    for t in sorted({10000, 20000, rows[-1]["t"]}):
        r = min(rows, key=lambda x: abs(x["t"] - t))
        print(arm, " ".join(f"{k}={r.get(k):.4g}" if isinstance(r.get(k), float) else f"{k}={r.get(k)}" for k in keys))
