import json

# Same transform round 34's rigid.json used on jointed.json: for every node whose
# joint is not "Fixed", set joint = "Fixed", power = 0, jointLimits = [].

def make_rigid(path_in, path_out):
    with open(path_in, encoding="utf-8") as f:
        line = f.readline().strip()
    row = json.loads(line)
    changed = 0
    for node in row["nodes"]:
        if node["joint"] != "Fixed":
            node["joint"] = "Fixed"
            node["power"] = 0
            node["jointLimits"] = []
            changed += 1
    with open(path_out, "w", encoding="utf-8") as out:
        out.write(json.dumps(row) + "\n")
    print(path_in, "->", path_out, "changed", changed, "node(s)")

make_rigid("scratch/reads-r36/30000_absorptive_founder3.json", "scratch/reads-r36/30000_absorptive_founder3_rigid.json")
make_rigid("scratch/reads-r36/5000_photosynthetic.json", "scratch/reads-r36/5000_photosynthetic_rigid.json")
