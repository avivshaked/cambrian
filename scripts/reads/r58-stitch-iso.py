"""Join round 58's isolated twins into one run directory each, for r58-read.py.

r58-iso stopped on a heat stop at 2,960 to 3,350 s and r58-isor resumed it from the common 1,500 s
checkpoint. Over the overlap the two are byte-identical (scratch/logs/r58-isor-overlap-compare.txt), and a
resumed run's files begin after the second it resumed from. So each world is the original's rows up to
1,500 s followed by the resumed run's rows, and the resumed run's run.json, whose ending is the world's.

    python -I r58-stitch-iso.py <out dir>

writes <out dir>/r58-s1i, r58-s2i and r58-s3i, each with run.json, stats.jsonl and lineage.jsonl.
"""
import glob
import json
import os
import shutil
import sys

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "runs")
AT = 1500.0


def run_dir(arm):
    dirs = sorted(d for d in glob.glob(os.path.join(RUNS, arm, "*")) if os.path.exists(os.path.join(d, "run.json")))
    if len(dirs) != 1:
        sys.exit(f"{arm}: expected one run directory, found {len(dirs)}")
    return dirs[0]


def stitch(name, before, after, out):
    first_after = None
    with open(out, "w", encoding="utf-8", newline="\n") as w:
        kept = 0
        for line in open(before, encoding="utf-8"):
            if not line.strip():
                continue
            if json.loads(line)["t"] <= AT:
                w.write(line if line.endswith("\n") else line + "\n")
                kept += 1
        added = 0
        for line in open(after, encoding="utf-8"):
            if not line.strip():
                continue
            t = json.loads(line)["t"]
            if first_after is None:
                first_after = t
            if t <= AT:
                sys.exit(f"{name}: the resumed file holds a row at t {t}, at or before {AT}")
            w.write(line if line.endswith("\n") else line + "\n")
            added += 1
    print(f"  {name}: {kept} rows to {AT:g} s, then {added} from {first_after:g} s")


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    root = sys.argv[1]
    for seed in (1, 2, 3):
        orig, res = run_dir(f"r58-s{seed}i"), run_dir(f"r58-s{seed}ir")
        out = os.path.join(root, f"r58-s{seed}i")
        os.makedirs(out, exist_ok=True)
        print(f"r58-s{seed}i: {orig} to {AT:g} s, then {res}")
        shutil.copyfile(os.path.join(res, "run.json"), os.path.join(out, "run.json"))
        for name in ("stats.jsonl", "lineage.jsonl"):
            stitch(name, os.path.join(orig, name), os.path.join(res, name), os.path.join(out, name))


if __name__ == "__main__":
    main()
