"""The scaling audit's cheapest measurements 1 to 4 (logbook/specs/scaling-audit.md section 4), per bin, per world.

Reads each run's stats.jsonl (one row a sample) and lineage.jsonl. A RUN is a run directory or an arm folder, of which
the latest dated run is read. A grazer is a body born mouthed (atk 1 and ink 1), as grazer-tally.py defines it, and
grazer-seconds are the time grazers were alive inside the bin.

1. Kill conversion: blows, finishing blows and their share in the bin (mouthBlows and mouthBlowsFinishing are
   cumulative counts; the bin's are differences). A run recorded before those columns prints a dash.
2. Encounters: blows a grazer-second, overlapPairsPerStep (the bin's mean of the per-row figure), and grazer kills
   (lineage kill rows whose attacker is a grazer) a grazer-second.
3. Sinking: the bin's means of depthRigid, foodRigid, meanHeight and depthJointed, and of bedOrGlassBodiesPerStep.
4. Speed and work: the bin's mean of speedJointed, and of workJoulesPerSecond over jointed (per jointed body).
   workJoulesPerSecond is the sample's last metabolic step's stroke work over that step's world seconds
   (src/Evosim.Farm/Row.cs); whether it is before or after the work price is not checked here. speedJointed is the
   farm's sampled speed; its time base under the pace factor is not checked here, and grazer-motion-read.py gives
   speed in metres a world second from the poses.

Usage: python -I scripts/reads/scaling-audit-read.py RUN [RUN ...] [--bin 3000] [--end 24000]
"""
import argparse
import json
import os
import sys


def latest(path):
    if os.path.exists(os.path.join(path, "stats.jsonl")):
        return path
    subs = sorted(d for d in os.listdir(path) if os.path.isdir(os.path.join(path, d)))
    for d in reversed(subs):
        p = os.path.join(path, d)
        if os.path.exists(os.path.join(p, "stats.jsonl")):
            return p
    sys.exit(f"{path}: no stats.jsonl in it or its dated runs")


def mean(xs):
    return sum(xs) / len(xs) if xs else float("nan")


def fmt(v, spec):
    if v is None or v != v:
        return "-"
    return spec.format(v)


def read(run, a):
    edges = []
    lo = 0.0
    while lo < a.end - 1e-9:
        edges.append((lo, min(a.end, lo + a.bin)))
        lo += a.bin

    def binof(t):
        for i, (l, h) in enumerate(edges):
            if l < t <= h + 1e-9:
                return i
        return None

    rows = [[] for _ in edges]
    for line in open(os.path.join(run, "stats.jsonl")):
        if not line.strip():
            continue
        r = json.loads(line)
        k = binof(r.get("t", 0))
        if k is not None:
            rows[k].append(r)

    born, died, kills = {}, {}, []
    for line in open(os.path.join(run, "lineage.jsonl")):
        if not line.strip():
            continue
        r = json.loads(line)
        if r.get("t", 0) > a.end:
            break
        e = r.get("e")
        if e == "b" and r.get("atk") == 1 and r.get("ink") == 1:
            born[r["id"]] = r["t"]
        elif e == "d":
            died[r["id"]] = r["t"]
        elif e == "k":
            kills.append(r)

    gs = [0.0] * len(edges)
    for i, t0 in born.items():
        t1 = died.get(i, a.end)
        for k, (l, h) in enumerate(edges):
            s = max(0.0, min(t1, h) - max(t0, l))
            gs[k] += s
    kb = [0] * len(edges)
    for r in kills:
        if r.get("by") in born:
            k = binof(r["t"])
            if k is not None:
                kb[k] += 1

    out = []
    prev = None
    for k, rs in enumerate(rows):
        if not rs:
            out.append(None)
            continue
        last = rs[-1]
        has = "mouthBlows" in last
        if has:
            b0 = prev["mouthBlows"] if prev and "mouthBlows" in prev else 0
            f0 = prev["mouthBlowsFinishing"] if prev and "mouthBlowsFinishing" in prev else 0
            blows = last["mouthBlows"] - b0
            fin = last["mouthBlowsFinishing"] - f0
        else:
            blows = fin = None
        prev = last
        per_j = [r["workJoulesPerSecond"] / r["jointed"] for r in rs if r.get("jointed")]
        out.append(dict(
            blows=blows, fin=fin, share=(fin / blows) if blows else None,
            blows_gs=(blows / gs[k]) if (blows is not None and gs[k] > 0) else None,
            overlap=mean([r.get("overlapPairsPerStep", 0) for r in rs]),
            kills_gs=(kb[k] / gs[k]) if gs[k] > 0 else None, gs=gs[k],
            depthRigid=mean([r["depthRigid"] for r in rs if "depthRigid" in r]),
            foodRigid=mean([r["foodRigid"] for r in rs if "foodRigid" in r]),
            meanHeight=mean([r["meanHeight"] for r in rs if "meanHeight" in r]),
            depthJointed=mean([r["depthJointed"] for r in rs if "depthJointed" in r]),
            bedStep=mean([r.get("bedOrGlassBodiesPerStep", 0) for r in rs]),
            speedJointed=mean([r["speedJointed"] for r in rs if "speedJointed" in r]),
            workPerJointed=mean(per_j)))
    return edges, out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--bin", type=float, default=3000.0)
    ap.add_argument("--end", type=float, default=24000.0)
    a = ap.parse_args()
    for path in a.runs:
        run = latest(path)
        cfg = {}
        rj = os.path.join(run, "run.json")
        if os.path.exists(rj):
            cfg = json.load(open(rj, encoding="utf-8"))
        print(f"\n== {run}  configHash {cfg.get('configHash', '?')}, commit {str((cfg.get('source') or {}).get('gitCommit'))[:8]}, "
              f"paceFactor {cfg.get('paceFactor', 1)}, to {a.end:.0f} s")
        edges, out = read(run, a)
        print(f"   {'bin':>11} | {'blows':>6} {'finish':>6} {'share':>5} {'blow/gs':>7} {'kill/gs':>8} {'overlap':>7} {'graz-s':>7}"
              f" | {'dRigid':>6} {'food':>5} {'meanH':>6} {'dJnt':>6} {'bed/st':>6} | {'spJnt':>6} {'W/jnt':>6}")
        for (l, h), o in zip(edges, out):
            if o is None:
                print(f"   {l:5.0f}-{h:<5.0f} | no rows")
                continue
            print(f"   {l:5.0f}-{h:<5.0f} | {fmt(o['blows'], '{:6d}'):>6} {fmt(o['fin'], '{:6d}'):>6} "
                  f"{fmt(o['share'], '{:5.2f}'):>5} {fmt(o['blows_gs'], '{:7.4f}'):>7} {fmt(o['kills_gs'], '{:8.5f}'):>8} "
                  f"{o['overlap']:7.2f} {o['gs']:7.0f} | {o['depthRigid']:6.2f} {o['foodRigid']:5.2f} {o['meanHeight']:6.2f} "
                  f"{o['depthJointed']:6.2f} {o['bedStep']:6.3f} | {o['speedJointed']:6.4f} {o['workPerJointed']:6.4f}")


if __name__ == "__main__":
    main()
