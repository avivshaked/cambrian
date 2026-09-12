import json, sys

RUN = "runs/r36-s1/2026-09-11-071830-55a61291/"

# Build parent map and flags from lineage.jsonl
parent = {}
flags = {}
kind = {}
with open(RUN + "lineage.jsonl", encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        row = json.loads(line)
        if row.get("e") != "b":
            continue
        oid = row["id"]
        parent[oid] = row["p"]
        flags[oid] = (row.get("abs"), row.get("jnt"), row.get("pho"))
        kind[oid] = row.get("k")

def founder_of(oid):
    seen = set()
    cur = oid
    while True:
        if cur not in parent:
            return None
        p = parent[cur]
        if p == -1:
            return cur
        if cur in seen:
            return None
        seen.add(cur)
        cur = p

# Candidates at 30,000 s: jointed absorptive, founder 3
snap30 = RUN + "snapshots/000030000.jsonl"
cands30 = []
with open(snap30, encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        row = json.loads(line)
        oid = row["id"]
        fl = flags.get(oid)
        if fl is None:
            continue
        abs_, jnt_, pho_ = fl
        fo = founder_of(oid)
        if jnt_ == 1 and abs_ == 1 and fo == 3:
            cands30.append((oid, fo, abs_, jnt_, pho_, kind.get(oid), parent.get(oid)))

print("=== 30,000 s: jointed absorptive, founder 3 ===")
for c in cands30[:20]:
    print(c)
print("total:", len(cands30))

# Candidates at 5,000 s: jointed photosynthetic, any founder
snap5 = RUN + "snapshots/000005000.jsonl"
cands5 = []
with open(snap5, encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        row = json.loads(line)
        oid = row["id"]
        fl = flags.get(oid)
        if fl is None:
            continue
        abs_, jnt_, pho_ = fl
        if jnt_ == 1 and pho_ == 1:
            fo = founder_of(oid)
            cands5.append((oid, fo, abs_, jnt_, pho_, kind.get(oid), parent.get(oid)))

print()
print("=== 5,000 s: jointed photosynthetic ===")
for c in cands5[:20]:
    print(c)
print("total:", len(cands5))
