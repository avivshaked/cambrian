"""The newborn ledger's first pass: every grazer's (and every grazer parent's) state in every frame of a run's state
stream, with the distance to the nearest living leaf, written once to a table so the ledger's reads do not re-inflate
poses.bin.

A grazer is a body born with atk 1 and ink 1 (as grazer-tally.py defines it). The tracked set is every grazer and the
parent of every bred grazer. A living leaf is a body flagged photosynthetic (flags & 4) in the frame, not remains
(flags & 8), and not itself in the tracked set. Distance is root to root, 3D and horizontal, metres. The stream
carries each body's reserve in joules (J), its seconds of reserve (S = J over its standing watts, Organism.
SecondsOfReserve), its funds over its breeding gate (breed) and its body fraction (bf).

Output columns, tab separated: id t bf J S breed x y z dleaf3 dleafh flags; and a second file of t and the frame's
leaf count.

Usage: python -I scripts/reads/newborn-extract.py RUN OUTDIR [--end 24000]
"""
import argparse
import importlib.util
import json
import math
import os
import struct

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("poses_read", os.path.join(HERE, "..", "poses-read.py"))
poses = importlib.util.module_from_spec(spec)
spec.loader.exec_module(poses)

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("out")
ap.add_argument("--end", type=float, default=24000.0)
a = ap.parse_args()

tracked = set()
grazers = set()
for line in open(os.path.join(a.run, "lineage.jsonl")):
    if not line.strip():
        continue
    r = json.loads(line)
    if r.get("e") != "b":
        continue
    if r.get("atk") == 1 and r.get("ink") == 1:
        grazers.add(r["id"])
        tracked.add(r["id"])
        if r.get("p", -1) >= 0:
            tracked.add(r["p"])

BODY = struct.Struct("<i3f16xfB3fB")   # version 4: id, xyz, (quaternion skipped), fraction, flags, S, breed, J, dof
assert BODY.size == 50

path = poses.stream_path(a.run)
size = os.path.getsize(path)
os.makedirs(a.out, exist_ok=True)
rows = open(os.path.join(a.out, "grazer-frames.tsv"), "w", encoding="utf-8", newline="\n")
leafs = open(os.path.join(a.out, "leaf-counts.tsv"), "w", encoding="utf-8", newline="\n")
rows.write("id\tt\tbf\tJ\tS\tbreed\tx\ty\tz\tdleaf3\tdleafh\tflags\n")
leafs.write("t\tleaves\ttracked\n")


def fmt(v):
    if v is None or (isinstance(v, float) and (math.isnan(v) or math.isinf(v))):
        return "nan"
    return "%.5g" % v


with open(path, "rb") as f:
    head = poses.read_header(f)
    version = head["version"]
    if version != 4:
        raise SystemExit("this pass reads a version 4 stream; this one is %d" % version)
    frames = poses.scan(f, size, version)
    for seconds, offset, payload in frames:
        if seconds > a.end + 1e-6:
            break
        f.seek(offset + 8)
        buf = f.read(payload)
        count = struct.unpack_from("<I", buf, 8)[0]
        raw = struct.unpack_from("<I", buf, 12)[0]
        rec = poses.inflate(buf[16:], raw, offset)
        at = 0
        lx, ly, lz = [], [], []
        mine = []
        for _ in range(count):
            ident, x, y, z, frac, flags, s, breed, j, dof = BODY.unpack_from(rec, at)
            at += 50 + 4 * dof
            if flags & 8:
                continue
            if ident in tracked:
                mine.append((ident, frac, j, s, breed, x, y, z, flags))
            elif flags & 4:
                lx.append(x)
                ly.append(y)
                lz.append(z)
        leafs.write("%g\t%d\t%d\n" % (seconds, len(lx), len(mine)))
        if not mine:
            continue
        LX = np.asarray(lx, dtype=np.float64)
        LY = np.asarray(ly, dtype=np.float64)
        LZ = np.asarray(lz, dtype=np.float64)
        for ident, frac, j, s, breed, x, y, z, flags in mine:
            if LX.size:
                dh2 = (LX - x) ** 2 + (LZ - z) ** 2
                d3 = math.sqrt(float(np.min(dh2 + (LY - y) ** 2)))
                dh = math.sqrt(float(np.min(dh2)))
            else:
                d3 = dh = float("nan")
            rows.write("%d\t%g\t%s\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f\t%d\n" % (
                ident, seconds, fmt(frac), fmt(j), fmt(s), fmt(breed), x, y, z, d3, dh, flags))

rows.close()
leafs.close()
print("frames read to %.0f s; tracked ids %d (grazers %d)" % (a.end, len(tracked), len(grazers)))
