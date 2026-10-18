"""Read a nursery exam per body: own brain against its best constant, each knockout and the kinesis.

Paired differences over the exam's held-out draws, with standard errors (D145's bar: two).
Usage: python scripts/reads/nursery-exam-per-body.py <run dir>
"""
import json
import math
import sys
from collections import defaultdict

run = sys.argv[1]
rows = [json.loads(l) for l in open(f"{run}/tank-exam.jsonl", encoding="utf-8") if l.strip()]
by_genome = defaultdict(dict)
for r in rows:
    by_genome[r["genome"]][r["brain"]] = r


def paired(a, b):
    d = [x - y for x, y in zip(a, b)]
    n = len(d)
    m = sum(d) / n
    sd = math.sqrt(sum((x - m) ** 2 for x in d) / (n - 1))
    return m, sd / math.sqrt(n)


def short(brain):
    return brain.replace("knockout ", "-").replace("JointAngle,JointAngularVelocity", "Joint").replace("Stroke,Hold,Ramp", "Drive")


print("| Genome | Body | Own J | Best constant | Own − constant | SEs | " +
      " | ".join(short(b) for b in by_genome[0] if b.startswith("knockout") or b in ("frozen", "brainless")) +
      " | Own − best kinesis |")
for k in sorted(by_genome):
    g = by_genome[k]
    own = g["own"]
    consts = {b: r for b, r in g.items() if b.startswith("constant")}
    best = max(consts, key=lambda b: consts[b]["net"])
    m, se = paired(own["per"], consts[best]["per"])
    cells = [str(k), str(own["body"]), f"{own['net']:.0f}", f"{best[9:]} ({consts[best]['net']:.0f})",
             f"{m:+.0f} ± {se:.0f}", f"{m / se:.1f}"]
    for b in g:
        if b.startswith("knockout") or b in ("frozen", "brainless"):
            m2, se2 = paired(own["per"], g[b]["per"])
            cells.append(f"{m2:+.0f} ± {se2:.0f}")
    kin = {b: r for b, r in g.items() if b.startswith("kinesis")}
    bk = max(kin, key=lambda b: kin[b]["net"])
    m3, se3 = paired(own["per"], kin[bk]["per"])
    cells.append(f"{m3:+.0f} ± {se3:.0f}")
    print("| " + " | ".join(cells) + " |")

# With a second run (the control), pair the two runs' own brains body by body. That holds only when
# both exams swam the same held-out draws, which the identical constants prove.
if len(sys.argv) > 2:
    other = defaultdict(dict)
    for l in open(f"{sys.argv[2]}/tank-exam.jsonl", encoding="utf-8"):
        if l.strip():
            r = json.loads(l)
            other[r["body"]][r["brain"]] = r
    mine = {g["own"]["body"]: g for g in by_genome.values()}
    same = all(mine[b][x]["per"] == other[b][x]["per"]
               for b in mine for x in mine[b] if x.startswith("constant") and b in other and x in other[b])
    print(f"\nconstants identical across the two runs: {same}")
    print("| Body | Own minus the other run's own | SEs |")
    for b in sorted(mine):
        if b in other:
            m, se = paired(mine[b]["own"]["per"], other[b]["own"]["per"])
            print(f"| {b} | {m:+.0f} ± {se:.0f} | {m / se:+.1f} |")
