"""Round 48 entry (0120): the median birth fraction (`bf`, a child's body at birth over its
adult body) of the children born early and late, rounds 48 and 47, founders left out.
Usage: python birthfraction.py   (writes nothing; run from the repository root)"""
import glob, json, os, statistics
for arm, windows in (("r48-s1", ((3000, 8000), (25000, 30001))), ("r48-s2", ((3000, 8000), (8690, 13691))), ("r48-s3", ((3000, 8000), (25000, 30001))),
                     ("r47-s1", ((3000, 8000), (25000, 30001))), ("r47-s2", ((3000, 8000), (25000, 30001))), ("r47-s3", ((3000, 8000), (25000, 30001)))):
    d = sorted(glob.glob(os.path.join("runs", arm, "*", "")))[-1]
    bf = {w: [] for w in windows}
    for line in open(os.path.join(d, "lineage.jsonl"), encoding="utf-8"):
        try: r = json.loads(line)
        except Exception: continue
        if r["e"] != "b" or r.get("k") == "f": continue
        for w in windows:
            if w[0] <= r["t"] < w[1] and "bf" in r: bf[w].append(r["bf"])
    print(arm, "; ".join(f"{w[0]}-{w[1]} s: n={len(v)} median bf {statistics.median(v):.3f}, under 0.1: {sum(1 for x in v if x < 0.1)/len(v):.2f}" for w, v in bf.items() if v))
