"""Copy an ids file and add S0, each child's seconds of reserve at its first recorded frame.

The ids file is brain-birth-ids.py's; the frames file is the newborn ledger's grazer-frames.tsv (columns id, t, S).
A child with no frame gets NaN. Usage: add-s0.py IDS FRAMES OUT
"""
import csv, sys

src, frames, out = sys.argv[1:4]
with open(src, encoding="utf-8", newline="") as f:
    reader = csv.DictReader(f, delimiter="\t")
    fields = list(reader.fieldnames)
    rows = list(reader)
want = {r["id"] for r in rows}
first = {}
with open(frames, encoding="utf-8") as f:
    h = f.readline().rstrip("\n").split("\t")
    ci, ct, cs = h.index("id"), h.index("t"), h.index("S")
    for line in f:
        p = line.rstrip("\n").split("\t")
        i = p[ci]
        if i in want:
            t = float(p[ct])
            if i not in first or t < first[i][0]:
                first[i] = (t, p[cs])
if "S0" not in fields:
    fields.append("S0")
with open(out, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=fields, delimiter="\t", lineterminator="\n")
    w.writeheader()
    for r in rows:
        r["S0"] = first[r["id"]][1] if r["id"] in first else "NaN"
        w.writerow(r)
missing = sum(1 for r in rows if r["id"] not in first)
print("rows %d, with S0 %d, NaN %d" % (len(rows), len(rows) - missing, missing))
