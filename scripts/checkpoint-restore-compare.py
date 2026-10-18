"""Compare a run and a restore of it, row by row, after the second the restore started at.

Usage: python compare.py <original run dir> <resumed run dir> <t1 seconds>
"""
import json
import os
import sys

KEYS = {
    "lineage.jsonl": ("t", "birth", "bornAt", "time"),
    "positions.jsonl": ("t", "seconds", "time"),
    "poses.jsonl": ("t", "seconds", "time"),
    "stats.jsonl": ("t", "seconds", "time"),
    "absorptive.jsonl": ("t", "seconds", "time"),
}


def seconds_of(row, names):
    for name in names:
        if name in row:
            return float(row[name])
    return None


# What a restored run legitimately does not carry: the wall clock this process has spent, and
# the two counts of work it has done that the wall split divides by. They measure the machine,
# not the world, and a run that started at 400 s has spent less of all of them.
PROCESS_FIELDS = {
    "harnessBodySteps",
    "fluidLinkSteps",
    "wallPhysicsMs",
    "wallWorldMs",
    "wallHarnessMs",
    "wallWritersMs",
    "wallTotalMs",
}


def is_process_field(key):
    return key in PROCESS_FIELDS or key.startswith("wallHarness") or key.startswith("wallFluid") or key.startswith("wallStep")


def strip_process_fields(line):
    try:
        row = json.loads(line)
    except json.JSONDecodeError:
        return line
    if not isinstance(row, dict):
        return line
    return json.dumps(
        {k: v for k, v in row.items() if not is_process_field(k)}, sort_keys=True)


def rows_after(path, names, t1):
    kept = []
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.rstrip("\n")
            if not line.strip():
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                kept.append(line)
                continue
            seconds = seconds_of(row, names)
            if seconds is None or seconds > t1 + 1e-9:
                kept.append(line)
    return kept


def main():
    a_dir, b_dir, t1 = sys.argv[1], sys.argv[2], float(sys.argv[3])
    bad = 0

    for name, names in KEYS.items():
        a_path, b_path = os.path.join(a_dir, name), os.path.join(b_dir, name)
        if not os.path.exists(a_path) or not os.path.exists(b_path):
            print(f"{name:20s} SKIP (one side is missing)")
            continue

        a = rows_after(a_path, names, t1)
        b = rows_after(b_path, names, t1)

        if a == b:
            print(f"{name:20s} IDENTICAL, {len(a)} rows after t={t1:g}")
            continue

        # Second chance for stats.jsonl alone, and only for the process's own instruments.
        if name == "stats.jsonl":
            a2 = [strip_process_fields(line) for line in a]
            b2 = [strip_process_fields(line) for line in b]
            if a2 == b2:
                print(
                    f"{name:20s} IDENTICAL, {len(a)} rows after t={t1:g} "
                    "(bar the wall clock and its two denominators)")
                continue
            a, b = a2, b2

        bad += 1
        print(f"{name:20s} DIFFERS: {len(a)} rows against {len(b)}")
        for i in range(min(len(a), len(b))):
            if a[i] != b[i]:
                print(f"  first difference at row {i}")
                print(f"    original {a[i][:400]}")
                print(f"    restored {b[i][:400]}")
                break
        else:
            shorter, longer = (a, b) if len(a) < len(b) else (b, a)
            print(f"  the shorter side ends; the next extra row is:")
            print(f"    {longer[len(shorter)][:400]}")

    print("PASS" if bad == 0 else f"FAIL ({bad} file(s) differ)")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
