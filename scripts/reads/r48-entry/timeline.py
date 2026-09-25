"""Round 48 timelines from stats.jsonl, every N seconds, for the entry (0120).
Usage: python timeline.py <arm> [every] [runs_root]
Prints selected fields at each multiple of `every` and the pace over each window."""
import glob, json, os, sys

arm = sys.argv[1]
every = float(sys.argv[2]) if len(sys.argv) > 2 else 2500
root = sys.argv[3] if len(sys.argv) > 3 else "runs"
runs = sorted(glob.glob(os.path.join(root, arm, "*", "stats.jsonl")))
path = runs[-1]
cols = [
    ("t", "t"), ("alive", "alive"), ("births", "births"), ("deaths", "deaths"),
    ("abs", "absorptive"), ("absInh", "absorptiveInherited"),
    ("pho", "photosynthetic"), ("jnt", "jointed"), ("jntInh", "jointedInherited"),
    ("invest", "meanBirthInvestment"), ("bfrac", "meanBodyFraction"),
    ("age", "meanAge"), ("gestB", "gestationBirths"), ("gestHeld", "gestationJoulesHeld"),
    ("uptlim", "uptakeLimitedShare"), ("expo", "meanExposure"),
    ("detJ", "detritusJoules"), ("matLock", "matterLocked"), ("height", "meanHeight"),
    ("stillb", "stillbirths"), ("trick", "trickleSpawns"), ("pool", "poolSpawns"),
    ("ovl/b", "overlapPairsPerStep"), ("audit", "auditResidual"), ("matRes", "matterResidual"),
    ("mspd", "meanSpeed"), ("spdJ", "speedJointed"),
]
rows = []
with open(path, "r", encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        try:
            rows.append(json.loads(line))
        except Exception:
            pass
print("#", path, len(rows), "rows")
print("\t".join(c[0] for c in cols) + "\tpace")
prev = None
for r in rows:
    t = r.get("t", 0)
    if abs(t / every - round(t / every)) > 1e-6 and r is not rows[-1]:
        continue
    out = []
    for name, key in cols:
        v = r.get(key)
        if isinstance(v, float):
            out.append(f"{v:.4g}")
        else:
            out.append(str(v))
    pace = ""
    if prev is not None:
        dw = (r.get("wallTotalMs", 0) - prev.get("wallTotalMs", 0)) / 1000.0
        dt = t - prev["t"]
        if dw > 0:
            pace = f"{dt / dw:.2f}"
    out.append(pace)
    print("\t".join(out))
    prev = r
