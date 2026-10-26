"""How far and how high grazers swim, in world seconds, across worlds that differ in their clock (D239): the pace
screen's diagnostic for why the pace factor did not raise grazers' encounters (logbook/specs/r62p-read/).

A grazer is a body born mouthed (atk 1 and ink 1 on its birth row, as r61-grazer-economy defines it). From the state
stream (poses.bin), for every pair of consecutive frames that hold the same living grazer (not remains, flags & 8),
its horizontal speed is the distance between the two frames' root positions over the frames' gap in world seconds.
Frames are --gap world seconds apart at most (the run's pose cadence), so this is the net speed over that gap, not the
path, and it undercounts a body that turns within it. A world at the pace factor 3 strokes three times as often a
world second, so if search is a swim at the grazer's speed its grazers should move about three times as far.

Height is the root's y in metres. Worlds founded on the same seed share their bed, so y compares directly between
them; a lower median means grazers sit nearer the bed, which at the pace factor 3 would follow from sinking three
times as far a world second.

Per world and per --bin window: the frames read, the grazer pairs, the median and quartiles of speed, the share of
pairs that moved under --still metres per world second, and the median and quartiles of height. Grazers and leaves
are both printed, since a leaf only drifts and so shows what the current alone gives.

Usage: python -I scripts/reads/grazer-motion-read.py RUN [RUN ...] [--bin 3000] [--end 24000] [--every 1]
                                                      [--gap 10] [--still 0.005]
--every N reads every Nth frame, to keep a read of a long run light; the gap check still applies to the pair read.
"""
import argparse
import importlib.util
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("poses_read", os.path.join(HERE, "..", "poses-read.py"))
poses = importlib.util.module_from_spec(spec)
spec.loader.exec_module(poses)

ap = argparse.ArgumentParser()
ap.add_argument("runs", nargs="+")
ap.add_argument("--bin", type=float, default=3000.0)
ap.add_argument("--end", type=float, default=24000.0)
ap.add_argument("--every", type=int, default=1)
ap.add_argument("--gap", type=float, default=10.0)
ap.add_argument("--still", type=float, default=0.005)
a = ap.parse_args()


def q(xs, p):
    if not xs:
        return float("nan")
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def kinds(run):
    grazers, leaves = set(), set()
    for line in open(os.path.join(run, "lineage.jsonl")):
        if not line.strip():
            continue
        r = json.loads(line)
        if r.get("e") != "b":
            continue
        if r.get("t", 0) > a.end:
            break
        if r.get("atk") == 1 and r.get("ink") == 1:
            grazers.add(r["id"])
        elif r.get("pho") == 1:
            leaves.add(r["id"])
    return grazers, leaves


def read(run):
    grazers, leaves = kinds(run)
    path = poses.stream_path(run)
    size = os.path.getsize(path)
    bins = {}
    with open(path, "rb") as f:
        header = poses.read_header(f)
        version = header["version"]
        frames = [fr for fr in poses.scan(f, size, version) if fr[0] <= a.end]
        frames = frames[:: max(1, a.every)] if a.every > 1 else frames
        prev_t, prev = None, {}
        for seconds, offset, payload in frames:
            _, bodies = poses.read_frame(f, offset, payload, version)
            now = {}
            for b in bodies:
                if b["flags"] & 8:
                    continue
                kind = "grazer" if b["id"] in grazers else ("leaf" if b["id"] in leaves else None)
                if kind is None:
                    continue
                now[b["id"]] = (kind, b["p"])
            key = int(seconds // a.bin)
            slot = bins.setdefault(key, {"frames": 0, "grazer": ([], []), "leaf": ([], [])})
            slot["frames"] += 1
            for ident, (kind, p) in now.items():
                slot[kind][1].append(p[1])
            if prev_t is not None and 0 < seconds - prev_t <= a.gap * max(1, a.every) + 1e-6:
                dt = seconds - prev_t
                for ident, (kind, p) in now.items():
                    if ident in prev:
                        q0 = prev[ident][1]
                        slot[kind][0].append(math.hypot(p[0] - q0[0], p[2] - q0[2]) / dt)
            prev_t, prev = seconds, now
    return bins


for run in a.runs:
    m = json.load(open(os.path.join(run, "run.json")))
    k = m.get("paceFactor") or 1
    print(f"\n== {m.get('arm')}  pace factor {k}  {run}")
    print(f"{'bin to':>7} {'frames':>6} | {'kind':>6} {'pairs':>7} {'v med':>7} {'v q1':>7} {'v q3':>7} {'still':>6} | "
          f"{'y med':>7} {'y q1':>7} {'y q3':>7}")
    for key, slot in sorted(read(run).items()):
        for kind in ("grazer", "leaf"):
            v, y = slot[kind]
            still = sum(1 for x in v if x < a.still) / len(v) if v else float("nan")
            print(f"{(key + 1) * a.bin:7.0f} {slot['frames']:6d} | {kind:>6} {len(v):7d} {q(v, .5):7.4f} {q(v, .25):7.4f} "
                  f"{q(v, .75):7.4f} {still:6.3f} | {q(y, .5):7.2f} {q(y, .25):7.2f} {q(y, .75):7.2f}")
