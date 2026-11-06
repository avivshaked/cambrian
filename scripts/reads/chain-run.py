"""Join a world's first run and its resume into one run folder, so that scripts that read one run (what-evolved.py) see
the world's founders from 0 s. The first run's lineage and stats rows are kept up to the resumed run's first row of each
file (strict), and its genomes for the bodies whose birth rows are kept; the resumed run's rows follow whole. config.json
and the snapshot asked for come from the resumed run; run.json is the resumed run's without resumedFrom, with a note.
A resume replays exactly, so the joined folder is the world from 0 s. Counts birth ids seen twice and prints them. Round
67 joined each world this way for readouts 5 and 6 (logbook/specs/r67-read/looks.md); write the folder under scratch/.

Usage: python -I scripts/reads/chain-run.py <first run dir> <resumed run dir> <out dir> --snapshot 24000
"""
import argparse
import gzip
import json
import os
import shutil

ap = argparse.ArgumentParser()
ap.add_argument("first")
ap.add_argument("resumed")
ap.add_argument("out")
ap.add_argument("--snapshot", type=int, required=True)
a = ap.parse_args()
os.makedirs(os.path.join(a.out, "snapshots"), exist_ok=True)


def first_t(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.strip():
                return json.loads(line)["t"]
    raise SystemExit(f"{path} is empty")


def rows(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            if not line.strip():
                continue
            try:
                yield line if line.endswith("\n") else line + "\n", json.loads(line)
            except ValueError:
                pass  # a torn line, from a program ended from outside or still writing


kept_births, dup = set(), 0
for name in ("lineage.jsonl", "stats.jsonl"):
    cut = first_t(os.path.join(a.resumed, name))
    n = [0, 0]
    with open(os.path.join(a.out, name), "w", encoding="utf-8", newline="") as out:
        for k, run in enumerate((a.first, a.resumed)):
            for line, r in rows(os.path.join(run, name)):
                if k == 0 and r["t"] >= cut:
                    break
                if name == "lineage.jsonl" and r.get("e") == "b":
                    if r["id"] in kept_births:
                        dup += 1
                    if k == 0:
                        kept_births.add(r["id"])
                out.write(line)
                n[k] += 1
    print(f"{name}: {n[0]} rows from the first run (before {cut:g} s), {n[1]} from the resumed run")

first_ids = set(kept_births)
n = [0, 0]
with gzip.open(os.path.join(a.out, "genomes.jsonl.gz"), "wt", encoding="utf-8", newline="") as out:
    for k, run in enumerate((a.first, a.resumed)):
        with gzip.open(os.path.join(run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
            try:
                for line in f:
                    try:
                        g = json.loads(line)
                    except ValueError:
                        continue
                    if k == 0 and g.get("id") not in first_ids:
                        continue
                    out.write(line if line.endswith("\n") else line + "\n")
                    n[k] += 1
            except EOFError:
                pass  # a gzip cut mid-stream by a run still writing
print(f"genomes.jsonl.gz: {n[0]} from the first run, {n[1]} from the resumed run")

shutil.copy2(os.path.join(a.resumed, "config.json"), os.path.join(a.out, "config.json"))
snap = "%09d.jsonl.gz" % a.snapshot
shutil.copy2(os.path.join(a.resumed, "snapshots", snap), os.path.join(a.out, "snapshots", snap))
with open(os.path.join(a.resumed, "run.json"), encoding="utf-8") as f:
    m = json.load(f)
was = m.pop("resumedFrom", None)
m["chainedNote"] = (f"joined by chain-run.py: {a.first} to its resume second {was and was.get('seconds')}, then "
                    f"{a.resumed}")
with open(os.path.join(a.out, "run.json"), "w", encoding="utf-8") as f:
    json.dump(m, f, indent=2)
print(f"birth ids seen in both runs: {dup}")
