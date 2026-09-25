"""Two farm runs' stats.jsonl compared field by field, sample by sample.

The config hash differs by construction — D106 added four tunables and a tunable is part of the
hash whatever its default — so the two runs cannot be compared by their manifests. What must be
identical is the world: a zero-chance, zero-threshold module gene is the recorded world, so every
field the two runs share has to read the same at every sample.

Fields only one side has (the five D106 added) are listed and skipped; everything else is compared
exactly, and a float is compared on its repr so that a difference of one ulp is a difference.
"""

import json
import sys


def rows(path):
    out = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                out.append(json.loads(line))
    return out


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    watch = sys.argv[3].split(",") if len(sys.argv) > 3 else None

    a, b = rows(a_path), rows(b_path)
    print(f"reference {a_path}: {len(a)} samples")
    print(f"candidate {b_path}: {len(b)} samples")

    if len(a) != len(b):
        print(f"DIFFER: sample counts {len(a)} against {len(b)}")

    only_a = sorted(set(a[0]) - set(b[0]))
    only_b = sorted(set(b[0]) - set(a[0]))
    if only_a:
        print(f"fields only in the reference ({len(only_a)}): {', '.join(only_a)}")
    if only_b:
        print(f"fields only in the candidate ({len(only_b)}): {', '.join(only_b)}")

    shared = sorted(set(a[0]) & set(b[0]))

    # The wall-clock split is a reading of the machine and not of the world: two runs of one
    # trajectory differ in it by a millisecond a row on a loaded box. It is listed and left out.
    clocks = [k for k in shared if k.startswith("wall")]
    shared = [k for k in shared if not k.startswith("wall")]
    print(f"not compared, a reading of the machine ({len(clocks)}): {', '.join(clocks)}")

    if watch:
        missing = [w for w in watch if w not in shared]
        if missing:
            print(f"DIFFER: watched field(s) not shared: {', '.join(missing)}")
        shared = [w for w in watch if w in shared]

    print(f"comparing {len(shared)} field(s) over {min(len(a), len(b))} samples")

    bad = 0
    for i in range(min(len(a), len(b))):
        for key in shared:
            x, y = a[i][key], b[i][key]
            if repr(x) == repr(y):
                continue
            bad += 1
            if bad <= 20:
                print(f"DIFFER at sample {i} (t={a[i].get('t')}): {key} {x!r} != {y!r}")

    if bad:
        print(f"DIFFER: {bad} field-sample difference(s)")
        return 1

    print("IDENTICAL: every shared field at every sample")
    return 0


if __name__ == "__main__":
    sys.exit(main())
