"""Tilt of each body's root part against the vertical, from poses.jsonl and the snapshot's genome.

The root node's dimensions give its thinnest local axis (a leaf's normal); the pose row's `r`
is the root's world rotation (x, y, z, w); y is up. Tilt is the angle between the rotated
normal and the vertical, 0 = flat to the surface, 90 = on edge. Root part only.

python scripts/reads/tilt.py <run dir> <second> [<second> ...]
"""
import json, math, sys, os
from collections import Counter

def rotate(q, v):
    x, y, z, w = q
    # v' = v + 2*w*(q x v) + 2*(q x (q x v))
    cx = y * v[2] - z * v[1]; cy = z * v[0] - x * v[2]; cz = x * v[1] - y * v[0]
    dx = y * cz - z * cy; dy = z * cx - x * cz; dz = x * cy - y * cx
    return (v[0] + 2 * (w * cx + dx), v[1] + 2 * (w * cy + dy), v[2] + 2 * (w * cz + dz))

def read(run, second):
    genomes = {}
    with open(os.path.join(run, "snapshots", f"{second:09d}.jsonl"), encoding="utf-8") as f:
        for line in f:
            g = json.loads(line)
            root = g["nodes"][g["root"]]
            d = root["dimensions"]
            genomes[g["id"]] = (d["x"], d["y"], d["z"], root["cell"], len(g["nodes"]))
    pose = None
    with open(os.path.join(run, "poses.jsonl"), encoding="utf-8") as f:
        for line in f:
            if line.startswith('{"t":%d,' % second):
                pose = json.loads(line); break
    if pose is None:
        raise SystemExit(f"no pose row at {second}")
    return genomes, pose

def main():
    run = sys.argv[1]
    for second in map(int, sys.argv[2:]):
        genomes, pose = read(run, second)
        bins = Counter(); ident = 0; n = 0; thin_ratio = []
        tilts = []
        for b in pose["bodies"]:
            g = genomes.get(b["id"])
            if g is None: continue
            dims = g[:3]
            axis = min(range(3), key=lambda i: dims[i])
            local = [0.0, 0.0, 0.0]; local[axis] = 1.0
            q = b["r"]
            if q == [0, 0, 0, 1]: ident += 1
            nrm = rotate(q, local)
            tilt = math.degrees(math.acos(min(1.0, abs(nrm[1]))))
            tilts.append(tilt)
            bins["0-15 flat" if tilt < 15 else "15-45" if tilt < 45 else "45-75" if tilt < 75 else "75-90 on edge"] += 1
            thin_ratio.append(min(dims) / max(dims))
            n += 1
        tilts.sort()
        print(f"{run} t={second}: {n} bodies; root rotation identity {ident} ({ident/n:.2f}); "
              f"thin/thick median {sorted(thin_ratio)[n//2]:.2f}")
        for k in ["0-15 flat", "15-45", "45-75", "75-90 on edge"]:
            print(f"   {k:14s} {bins[k]:6d}  {bins[k]/n:.2f}")
        print(f"   median tilt {tilts[n//2]:.0f} deg; mean |cos| (the flat-projection factor, 1 = flat) "
              f"{sum(math.cos(math.radians(t)) for t in tilts)/n:.2f}; a random orientation gives 0.50")

main()
