"""Diverged-dump anatomy for round 37b: one row per dump, from the dump and its trace."""
import glob, json, math, os, sys

ROOT = r"D:\Projects\experiments\evolution-simulator"
R_TANK = math.sqrt(100.0 / math.pi)  # 5.6419 m


def num(v):
    try:
        f = float(v)
        return f if math.isfinite(f) else None
    except (TypeError, ValueError):
        return None


rows = []
for arm in ["r37b-s1", "r37b-s2", "r37b-s3", "r37b-s4", "r37b-s5"]:
    runs = glob.glob(os.path.join(ROOT, "runs", arm, "*", "diverged"))
    if not runs:
        continue
    for dump in sorted(glob.glob(os.path.join(runs[0], "*.json"))):
        if dump.endswith("-trace.json"):
            continue
        d = json.load(open(dump))
        cid = d["creatureId"]
        tr_path = dump[:-5] + "-trace.json"
        tr = json.load(open(tr_path)) if os.path.exists(tr_path) else None
        p = d.get("lastRootPosition") or {}
        x, z, y = num(p.get("x")), num(p.get("z")), num(p.get("y"))
        # the tank's axis is at (R, R): TankGeometry.Inside is (x-R)^2 + (z-R)^2 <= R^2
        rad = (math.sqrt((x - R_TANK) ** 2 + (z - R_TANK) ** 2)
               if x is not None and z is not None else None)
        # frames: how many of the ring's frames carry a finite root position
        finite_frames = 0
        max_finite_speed = None
        if tr:
            for fr in tr.get("frames", []):
                L0 = fr["links"][0]
                if num(L0["position"].get("y")) is not None:
                    finite_frames += 1
                for L in fr["links"]:
                    v = L.get("velocity", {})
                    c = [num(v.get(k)) for k in "xyz"]
                    if all(q is not None for q in c):
                        s = math.sqrt(sum(q * q for q in c))
                        max_finite_speed = s if max_finite_speed is None else max(max_finite_speed, s)
        rows.append(dict(
            arm=arm, id=cid, t=d["t"], age=d.get("ageSeconds"),
            jointed=d.get("jointed"), parts=d.get("parts"), dof=d.get("totalDof"),
            masses="|".join("%.3f" % ps["massKg"] for ps in d.get("partStates", [])),
            ratio=tr.get("maxJointMassRatio") if tr else None,
            stepsSinceResize=tr.get("stepsSinceLastResize") if tr else None,
            lastObservedY=d.get("lastObservedHeightY"),
            rootY=y, rootRadius=rad,
            withinMetreOfGlass=(rad is not None and rad >= R_TANK - 1.0),
            outsideGlass=(rad is not None and rad > R_TANK),
            reason=d.get("divergenceReason") or d.get("reason"),
            trace="yes" if tr else "NO",
            traceFinitePositionFrames=finite_frames,
            traceMaxFiniteLinkSpeed=max_finite_speed,
        ))

cols = ["arm", "id", "t", "age", "jointed", "parts", "dof", "masses", "ratio",
        "stepsSinceResize", "rootY", "lastObservedY", "rootRadius",
        "withinMetreOfGlass", "outsideGlass", "trace", "traceFinitePositionFrames",
        "traceMaxFiniteLinkSpeed", "reason"]
out = [ "\t".join(cols) ]
for r in rows:
    out.append("\t".join("" if r.get(c) is None else str(r.get(c)) for c in cols))
print("\n".join(out))
sys.stderr.write("dumps=%d  traced=%d  R_tank=%.4f\n" % (
    len(rows), sum(1 for r in rows if r["trace"] == "yes"), R_TANK))
