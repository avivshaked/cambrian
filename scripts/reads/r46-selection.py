"""Did the flat-born leaves out-breed the edge-born inside a crowd? Round 46's selection read.

python scripts/reads/r46-selection.py <run dir> [--windows 3000-6000,6000-10000,10000-15000]

A root whose thinnest genome dimension is y is born lying flat in the developer's frame; one
whose thinnest is x or z is born on edge. For every body with a genome in any snapshot, the
read takes its trait from the genome, and in each window counts the body-seconds it was
alive and the births it had, by trait, and prints births per body-hour for each. The ratio
between the two rows is the selection differential the ledger's doubled income predicts;
equal rows say the pose paid nothing in that window.
"""
import glob, json, os, sys


def main():
    run = sys.argv[1]
    windows = [(3000, 6000), (6000, 10000), (10000, 15000), (15000, 20000), (20000, 30000)]
    if "--windows" in sys.argv:
        windows = [tuple(int(x) for x in w.split("-")) for w in sys.argv[sys.argv.index("--windows") + 1].split(",")]
    thin = {}
    for path in sorted(glob.glob(os.path.join(run, "snapshots", "*.jsonl"))):
        with open(path, encoding="utf-8") as f:
            for line in f:
                g = json.loads(line)
                if g["id"] in thin:
                    continue
                d = g["nodes"][g["root"]]["dimensions"]
                thin[g["id"]] = d["y"] <= d["x"] and d["y"] <= d["z"]
    birth = {}; died = {}; kids = {}
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            if r["e"] == "b":
                birth[r["id"]] = r["t"]
                if r["p"] != -1:
                    kids.setdefault(r["p"], []).append(r["t"])
            elif r["e"] == "d":
                died[r["id"]] = r["t"]
    end = max(died.values()) if died else 0
    print("%s: %d bodies with a genome; births per body-hour by the root's birth pose" % (run, len(thin)))
    print("  %-14s %-22s %7s %13s %7s %10s" % ("window", "trait", "bodies", "body-seconds", "births", "per hour"))
    for lo, hi in windows:
        for flag, name in ((True, "born flat (thin on y)"), (False, "born on edge")):
            secs = 0.0; n = 0; births = 0
            for i, t in thin.items():
                if t != flag or i not in birth:
                    continue
                a = max(birth[i], lo); b = min(died.get(i, end), hi)
                if b <= a:
                    continue
                n += 1; secs += b - a
                births += sum(1 for kt in kids.get(i, []) if lo <= kt < hi)
            print("  %5d-%-8d %-22s %7d %13.0f %7d %10.2f" % (lo, hi, name, n, secs, births, births / secs * 3600 if secs else 0))


main()
