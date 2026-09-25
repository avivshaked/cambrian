"""D111's one-off: bring the format-7 inocula to format 8 at offset 0.

The previous reader and the new one differ by one node field, so the conversion is the
format number and `"buoyancyOffset":0` placed after each node's `"toughness"`, done on the
text so every other byte (every float as the run wrote it) is untouched. The check that it
is the same genome is check/Program.cs: the new reader loads each file, the new writer
writes it, and compare() below asks that the two parse to the same object.

Usage: python convert_inocula.py convert | compare <rewritten dir>
"""
import json
import os
import re
import sys

ROOT = r"D:\Projects\experiments\evolution-simulator"
FILES = [
    "bush-16-r41d-s2-15000.json",
    "growth-ledger-genome.json",
    "knot-16-jointed-r41b-s1-1632.json",
    "knot-16-r41c-s3-1341.json",
    "knot-9-r41c-s3-937.json",
    "r45s1-stomach-100.json",
]


def convert():
    for name in FILES:
        path = os.path.join(ROOT, "inocula", name)
        with open(path, "r", encoding="utf-8", newline="") as f:
            text = f.read()

        nodes = len(json.loads(text)["nodes"])
        if text.count('"format":7') != 1:
            raise SystemExit(f"{name}: not one format-7 marker")

        out = text.replace('"format":7', '"format":8', 1)
        out, n = re.subn(r'("toughness":-?[0-9.eE+-]+)', r'\1,"buoyancyOffset":0', out)
        if n != nodes:
            raise SystemExit(f"{name}: {n} toughness fields for {nodes} nodes")

        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(out)
        print(f"{name}: {nodes} nodes, format 8")


def as_float32(value):
    """Numbers compared as the float the genome holds: the older files were written by Mono's
    nine-digit formatter and .NET 8 writes the shortest round trip, so 0.219846547 and
    0.21984655 are one float in two spellings, and 1.0 and 1 one value."""
    import struct
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return struct.unpack("<f", struct.pack("<f", float(value)))[0]
    if isinstance(value, dict):
        return {k: as_float32(v) for k, v in value.items()}
    if isinstance(value, list):
        return [as_float32(v) for v in value]
    return value


def compare(rewritten):
    bad = 0
    for name in FILES:
        with open(os.path.join(ROOT, "inocula", name), encoding="utf-8") as f:
            a = json.load(f)
        with open(os.path.join(rewritten, name), encoding="utf-8") as f:
            b = json.load(f)
        same = as_float32(a) == as_float32(b)
        bad += 0 if same else 1
        print(f"{name}: {'same genome' if same else 'DIFFERS'}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    if sys.argv[1] == "convert":
        convert()
    else:
        compare(sys.argv[2])
