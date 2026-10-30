"""r63s's floaters: the diverged deaths by root shape, against how long bodies of each shape lived.

For each run: every diverged record (diverged/<id>.json) is classed as a living body (a birth row in lineage.jsonl) or
remains (an "r" row), and counted by its root part's shape, its part count and its upward speed. The exposure is the
living bodies in every snapshot (every 100 s) by their genome's root shape, and by root shape for bodies grown to one
part (the record's "parts" for the diverged, the birth row's "pt" for the living). Remains are not in the snapshots and
have no genome row, so their exposure is the remains founded from bodies of each root shape, a proxy marked as such.

    python -I scripts/reads/shape/r63s-floaters.py RUN [RUN ...]

Its read is logbook/specs/r63s-read/floaters.txt, which scripts/reads/shape/r63s-read.ps1 makes again.
"""
import collections
import glob
import gzip
import json
import os
import sys


def read_run(run):
    births, remains_from = {}, {}
    with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            e = r.get("e")
            if e == "b":
                births[r["id"]] = r.get("pt")
            elif e == "r":
                remains_from[r["id"]] = r.get("from")
    root_shape = {}
    with gzip.open(os.path.join(run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
        for line in f:
            g = json.loads(line)
            nodes = g.get("nodes") or []
            root = g.get("root", 0)
            if 0 <= root < len(nodes):
                root_shape[g["id"]] = nodes[root].get("shape")
    exposure = collections.Counter()
    exposure_one = collections.Counter()
    snaps = sorted(glob.glob(os.path.join(run, "snapshots", "*.jsonl.gz")))
    for s in snaps:
        with gzip.open(s, "rt", encoding="utf-8") as f:
            for line in f:
                i = json.loads(line)["id"]
                sh = root_shape.get(i, "?")
                exposure[sh] += 1
                if births.get(i) == 1:
                    exposure_one[sh] += 1
    founded = collections.Counter(root_shape.get(src, "?") for src in remains_from.values())
    div = []
    for p in sorted(glob.glob(os.path.join(run, "diverged", "*.json"))):
        with open(p, encoding="utf-8") as f:
            d = json.load(f)
        i = d["creatureId"]
        kind = "living" if i in births else ("remains" if i in remains_from else "?")
        parts = d.get("partStates") or []
        root = parts[0] if parts else {}
        vy = (root.get("velocity") or {}).get("y")
        src_shape = root_shape.get(remains_from.get(i)) if kind == "remains" else None
        div.append(dict(id=i, t=d["t"], kind=kind, shape=root.get("shapeId"), parts=d.get("parts"),
                        cell=root.get("cellTypeId"), vy=vy, src_shape=src_shape))
    return dict(snaps=len(snaps), births=len(births), remains=len(remains_from), exposure=exposure,
                exposure_one=exposure_one, founded=founded, div=div)


def main(runs):
    for run in runs:
        r = read_run(run)
        print(f"== {run}")
        print(f"   snapshots {r['snaps']} (100 s apart), births {r['births']}, remains founded {r['remains']}, "
              f"diverged records {len(r['div'])}")
        kinds = collections.Counter(d["kind"] for d in r["div"])
        print(f"   diverged by kind: {dict(kinds)}")
        print("   diverged by kind, root shape, parts, root cell:")
        c = collections.Counter((d["kind"], d["shape"], d["parts"], d["cell"]) for d in r["div"])
        for k, n in c.most_common():
            print(f"     {n:4d}  {k[0]:8s} {k[1]:15s} parts {k[2]}  {k[3]}")
        vys = sorted(d["vy"] for d in r["div"] if d["vy"] is not None)
        if vys:
            print(f"   root upward speed at the guard, m/s: min {vys[0]:.2f}, median {vys[len(vys) // 2]:.2f}, "
                  f"max {vys[-1]:.2f}")
        times = sorted(d["t"] for d in r["div"])
        same_t = sum(1 for a, b in zip(times, times[1:]) if a == b)
        print(f"   records sharing a second with the one before: {same_t} of {len(times)}")
        print("   living bodies, exposure in body-snapshots (x 100 s) and diverged per million body-seconds, by root shape:")
        living = collections.Counter(d["shape"] for d in r["div"] if d["kind"] == "living")
        living_one = collections.Counter(d["shape"] for d in r["div"] if d["kind"] == "living" and d["parts"] == 1)
        for sh in sorted(set(r["exposure"]) | set(living)):
            e = r["exposure"][sh]
            e1 = r["exposure_one"][sh]
            rate = living[sh] / (e * 100) * 1e6 if e else float("nan")
            rate1 = living_one[sh] / (e1 * 100) * 1e6 if e1 else float("nan")
            print(f"     {str(sh):15s} all: exposure {e:9d}, diverged {living[sh]:4d}, rate {rate:8.3f}   "
                  f"one-part at birth: exposure {e1:8d}, diverged one-part {living_one[sh]:4d}, rate {rate1:8.3f}")
        print("   remains (proxy: the source body's root shape), founded and diverged:")
        rem = collections.Counter(d["src_shape"] for d in r["div"] if d["kind"] == "remains")
        for sh in sorted(set(r["founded"]) | set(rem), key=str):
            n = r["founded"][sh]
            share = rem[sh] / n if n else float("nan")
            print(f"     {str(sh):15s} founded {n:7d}, diverged {rem[sh]:4d}, per founded {share:.5f}")
        print()


if __name__ == "__main__":
    main(sys.argv[1:])
