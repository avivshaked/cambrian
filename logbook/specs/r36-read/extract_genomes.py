import json

RUN = "runs/r36-s1/2026-09-11-071830-55a61291/"

def find_in_snapshot(path, target_id):
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            if row["id"] == target_id:
                return line
    return None

targets = [
    ("30000_absorptive_founder3", RUN + "snapshots/000030000.jsonl", 12772),
    ("5000_photosynthetic", RUN + "snapshots/000005000.jsonl", 168),
]

for name, path, oid in targets:
    line = find_in_snapshot(path, oid)
    if line is None:
        print(name, oid, "NOT FOUND")
        continue
    row = json.loads(line)
    nodes = row["nodes"]
    joints = [(i, n["cell"], n["joint"], n["power"]) for i, n in enumerate(nodes)]
    print(name, "id", oid, "nodes:", joints)
    outpath = f"scratch/reads-r36/{name}.json"
    with open(outpath, "w", encoding="utf-8") as out:
        out.write(line + "\n")
    print("  wrote", outpath)
