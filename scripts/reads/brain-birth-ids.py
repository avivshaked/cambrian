"""The ids file for the brain-at-birth bench (src/Evosim.SwimProbe/BrainBirth.cs, the swim probe's --brain-birth).

Population: bred grazers with a first speed and an unchanged body plan (structure diff count 0), as
newborn-followup-read.py has it, split by first speed into three groups:
  standstill   first speed under 0.01 m/s
  slow         0.01 to under 0.03 m/s
  moving       0.03 m/s and above
Every definition is loaded, not copied, from scripts/reads/genome-diff-read.py by the method newborn-followup-read.py
uses: this file executes that script's source up to its "groups" marker with the same RUN, FRAMES, --end and --window,
so grazer, bred, the first speed, the exclusions (bred grazers whose own or whose parent's genome is not in
genomes.jsonl.gz; a living grazer born in the last 60 s before --end) and the diff are that script's, unchanged. If the
marker is missing the whole script runs with its output discarded.

OUT is a tab-separated file with a header, "id parent group firstSpeed". It holds every standstill and every slow child,
and --movers moving children drawn at random, with random.Random(seed), from the sorted moving ids. Rows are written
standstill, slow, moving, each sorted by id, so the same inputs give the same file.

Usage: python -I scripts/reads/brain-birth-ids.py RUN FRAMES OUT [--end 24000] [--window 10] [--movers 200] [--seed 1]
"""
import argparse
import contextlib
import io
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
GDR = os.path.join(HERE, "genome-diff-read.py")
GROUP_MARK = "groups"
STAND = 0.01
GROUPS = ["standstill", "slow", "moving"]

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("frames")
ap.add_argument("out")
ap.add_argument("--end", type=float, default=24000.0)
ap.add_argument("--window", type=float, default=10.0)
ap.add_argument("--movers", type=int, default=200, help="moving children to draw at random")
ap.add_argument("--seed", type=int, default=1)
A = ap.parse_args()

# ------------------------------------------------------------------ load genome-diff-read.py's definitions
src = open(GDR, encoding="utf-8").read()
cut = None
pos = 0
for line in src.splitlines(keepends=True):
    s = line.strip()
    if s.startswith("# ----") and s.endswith(GROUP_MARK):
        cut = pos
        break
    pos += len(line)
head = src if cut is None else src[:cut]
ns = {"__name__": "genome_diff_read", "__file__": GDR}
argv0 = sys.argv
sys.argv = [GDR, A.run, A.frames, "--end", repr(A.end), "--window", repr(A.window)]
try:
    if cut is None:
        with contextlib.redirect_stdout(io.StringIO()):
            exec(compile(src, GDR, "exec"), ns)
    else:
        exec(compile(head, GDR, "exec"), ns)
finally:
    sys.argv = argv0

births, deaths = ns["births"], ns["deaths"]
diff_genome, speed_fn = ns["diff_genome"], ns["speed"]
genomes, bred, SLOW = ns["genomes"], ns["bred"], ns["SLOW"]
END = A.end

# ------------------------------------------------------------------ the population, as newborn-followup-read.py
young = {i for i in bred if i not in deaths and births[i]["t"] > END - 60}
speeds = {}
for i in bred:
    if i in young:
        continue
    v = speed_fn(i)
    if v is not None:
        speeds[i] = v


def parent_of(i):
    return births[i]["p"]


pop = [i for i in speeds if diff_genome(genomes[i], genomes[parent_of(i)]).n["structure"] == 0]


def group_of(v):
    return "standstill" if v < STAND else ("slow" if v < SLOW else "moving")


GM = {g: [] for g in GROUPS}
for i in pop:
    GM[group_of(speeds[i])].append(i)
for g in GROUPS:
    GM[g].sort()

moving = GM["moving"]
k = min(A.movers, len(moving))
drawn = sorted(random.Random(A.seed).sample(moving, k))

chosen = [(i, "standstill") for i in GM["standstill"]]
chosen += [(i, "slow") for i in GM["slow"]]
chosen += [(i, "moving") for i in drawn]

out_dir = os.path.dirname(os.path.abspath(A.out))
os.makedirs(out_dir, exist_ok=True)
with open(A.out, "w", encoding="utf-8", newline="\n") as f:
    f.write("id\tparent\tgroup\tfirstSpeed\n")
    for i, g in chosen:
        f.write("%d\t%d\t%s\t%.6f\n" % (i, parent_of(i), g, speeds[i]))

print("brain-birth-ids.py  run %s  frames %s" % (A.run, A.frames))
print("to %.0f s, window %g s, seed %d; bred grazers %d, young and alive left out %d, with a first speed %d" % (
    END, A.window, A.seed, len(bred), len(young), len(speeds)))
print("population (structure diff 0) n %d: %s" % (len(pop), "; ".join("%s %d" % (g, len(GM[g])) for g in GROUPS)))
print("wrote %s: %d rows (standstill %d, slow %d, moving %d of %d drawn with random.Random(%d))" % (
    A.out, len(chosen), len(GM["standstill"]), len(GM["slow"]), len(drawn), len(moving), A.seed))
