"""D239's step-size check (rounds/check-pace-step.ps1): the same world and seed founded at physics steps of 0.01, 0.02
and 0.025 s, compared as distributions, since a step over 0.01 s is a new realisation of the world.

Per run, over the window --from to the end:
- the pace: simulated seconds per wall second, from run.json;
- the swim speed: stats speedJointed and speedRigid (the mean over the window's rows), and the poses' own speeds of
  jointed and rigid bodies over 10 s, horizontal;
- the stroke: each jointed body's joint angles over the window's frames, their spread (standard deviation) and their
  mean change between frames, both in radians. Frames are 0.5 s apart at most (the state stream's cadence is raised to
  the metabolic step), so this is the stroke's amplitude and not its shape within a stroke;
- the contacts: stats overlapPairs over the window per simulated second and per physics step, the held share
  (overlapPairsHeld over overlapPairs) and the jointed share, all from cumulative columns differenced;
- the population and the diverged count at the end.

Usage: python -I scripts/reads/pace-step-read.py RUNS_ROOT [--from 100]
"""
import argparse
import glob
import importlib.util
import json
import math
import os
import statistics

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("poses_read", os.path.join(HERE, "..", "poses-read.py"))
poses = importlib.util.module_from_spec(spec)
spec.loader.exec_module(poses)

ap = argparse.ArgumentParser()
ap.add_argument("root")
ap.add_argument("--from", dest="start", type=float, default=100.0)
a = ap.parse_args()


def latest(arm_dir):
    runs = sorted(glob.glob(os.path.join(arm_dir, "*", "run.json")), key=os.path.getmtime)
    return os.path.dirname(runs[-1]) if runs else None


def find(d, key):
    if isinstance(d, dict):
        if key in d:
            return d[key]
        for v in d.values():
            got = find(v, key)
            if got is not None:
                return got
    return None


def mean(xs):
    return sum(xs) / len(xs) if xs else float("nan")


def read_run(run):
    out = {}
    manifest = json.load(open(os.path.join(run, "run.json")))
    config = json.load(open(os.path.join(run, "config.json")))
    out["step"] = find(config, "physicsStepSeconds")
    out["config"] = config.get("configHash")
    out["status"] = f"{manifest.get('status')} {manifest.get('ending') or manifest.get('reason') or ''}".strip()
    rows = [json.loads(l) for l in open(os.path.join(run, "stats.jsonl")) if l.strip()]
    rows = [r for r in rows if r.get("t") is not None]
    last = rows[-1]
    wall = (last.get("wallTotalMs") or 0) / 1000.0
    out["pace"] = last["t"] / wall if wall else float("nan")
    win = [r for r in rows if r["t"] >= a.start]
    first = win[0]
    out["speedJointed"] = mean([r["speedJointed"] for r in win if r.get("speedJointed") is not None])
    out["speedRigid"] = mean([r["speedRigid"] for r in win if r.get("speedRigid") is not None])
    span = last["t"] - first["t"]
    pairs = (last.get("overlapPairs") or 0) - (first.get("overlapPairs") or 0)
    held = (last.get("overlapPairsHeld") or 0) - (first.get("overlapPairsHeld") or 0)
    jnt = (last.get("overlapPairsJointed") or 0) - (first.get("overlapPairsJointed") or 0)
    steps = span / out["step"] if out["step"] else float("nan")
    out["pairs/s"] = pairs / span if span else float("nan")
    out["pairs/step"] = pairs / steps if steps else float("nan")
    out["alive"] = mean([r["alive"] for r in win])
    out["pairs/body/s"] = out["pairs/s"] / out["alive"] if out["alive"] else float("nan")
    out["held share"] = held / pairs if pairs else float("nan")
    out["jointed share"] = jnt / pairs if pairs else float("nan")
    out["jointed"] = last.get("jointed")
    out["alive end"] = last.get("alive")
    out["diverged"] = last.get("diverged")

    path = poses.stream_path(run)
    size = os.path.getsize(path)
    spreads, changes, vj, vr = [], [], [], []
    with open(path, "rb") as f:
        header = poses.read_header(f)
        version = header["version"]
        frames = [fr for fr in poses.scan(f, size, version) if fr[0] >= a.start]
        series = {}
        place = {}
        for seconds, offset, payload in frames:
            _, bodies = poses.read_frame(f, offset, payload, version)
            for b in bodies:
                series.setdefault(b["id"], []).append((seconds, b["q"], b["flags"], b["p"]))
        for ident, s in series.items():
            if s[0][2] & 8:
                continue   # remains (D220): dead bodies drift, and neither stroke nor swim
            jointed = bool(s[0][2] & 2)
            if jointed and len(s) >= 8 and s[0][1]:
                dof = len(s[0][1])
                for d in range(dof):
                    qs = [x[1][d] for x in s if len(x[1]) == dof]
                    if len(qs) >= 8:
                        spreads.append(statistics.pstdev(qs))
                        changes.append(mean([abs(qs[i + 1] - qs[i]) for i in range(len(qs) - 1)]))
            for i in range(len(s)):
                for j in range(i + 1, len(s)):
                    if s[j][0] - s[i][0] >= 10.0 - 1e-6:
                        dx = s[j][3][0] - s[i][3][0]
                        dz = s[j][3][2] - s[i][3][2]
                        v = math.hypot(dx, dz) / (s[j][0] - s[i][0])
                        (vj if jointed else vr).append(v)
                        break
                break
    out["stroke spread"] = statistics.median(spreads) if spreads else float("nan")
    out["stroke change"] = statistics.median(changes) if changes else float("nan")
    out["pose v jointed"] = statistics.median(vj) if vj else float("nan")
    out["pose v rigid"] = statistics.median(vr) if vr else float("nan")
    out["frames"] = len(frames)
    return out


arms = sorted(d for d in glob.glob(os.path.join(a.root, "pacestep-dt*")) if os.path.isdir(d))
results = []
for arm in arms:
    run = latest(arm)
    if run:
        results.append((os.path.basename(arm), run, read_run(run)))

keys = ["step", "config", "status", "pace", "speedJointed", "speedRigid", "pose v jointed", "pose v rigid",
        "stroke spread", "stroke change", "pairs/s", "pairs/step", "pairs/body/s", "held share", "jointed share",
        "alive", "alive end", "jointed", "diverged", "frames"]
print(f"window from {a.start:.0f} s to each run's end")
print(f"{'':16}" + "".join(f"{name:>22}" for name, _, _ in results))
for k in keys:
    cells = []
    for _, _, r in results:
        v = r.get(k)
        cells.append(f"{v:22.4g}" if isinstance(v, float) else f"{str(v):>22}")
    print(f"{k:16}" + "".join(cells))
for name, run, _ in results:
    print(f"{name}: {run}")
