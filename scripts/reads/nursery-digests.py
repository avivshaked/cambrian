"""Compare nursery run folders file by file: every result file byte-identical.

Usage: python scripts/reads/nursery-digests.py <reference run> <run> [<run> ...]

A result file is what the registry digests (Registry.IsResult): everything but run.json,
recipe.json, timing.jsonl, profile.jsonl and *.log, *.err, *.txt. Prints one line per file and
run. Exits 0 if all are identical, 1 if any file differs or is missing on either side, and 3 if the
check could not run (wrong arguments, a folder missing or unreadable, a reference with no result
files). Python exits 2 when it cannot open this script at all, so a caller treats every code but 0
and 1 as "could not run", never as "differs": on 2026-10-05 a mangled path made Python exit 2 and a
caller that tested only for nonzero reported a result moved that had never been compared.
"""
import hashlib
import os
import sys

NOT_RESULTS = {"run.json", "run.json.tmp", "recipe.json", "timing.jsonl", "profile.jsonl"}


def results(folder):
    out = {}
    for name in sorted(os.listdir(folder)):
        low = name.lower()
        if low in NOT_RESULTS or os.path.splitext(low)[1] in (".log", ".err", ".txt"):
            continue
        with open(os.path.join(folder, name), "rb") as f:
            out[name] = hashlib.sha256(f.read()).hexdigest()
    return out


COULD_NOT_RUN = 3


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return COULD_NOT_RUN
    for folder in argv[1:]:
        if not os.path.isdir(folder):
            print(f"COULD-NOT-RUN: no folder {folder}")
            return COULD_NOT_RUN
    try:
        return compare(argv)
    except OSError as e:
        print(f"COULD-NOT-RUN: {e}")
        return COULD_NOT_RUN


def compare(argv):
    ref = results(argv[1])
    if not ref:
        print(f"COULD-NOT-RUN: no result files in {argv[1]}")
        return COULD_NOT_RUN
    bad = 0
    for run in argv[2:]:
        other = results(run)
        for name in sorted(set(ref) | set(other)):
            a, b = ref.get(name), other.get(name)
            if a is None or b is None:
                verdict = "MISSING in " + (argv[1] if a is None else run)
                bad += 1
            elif a != b:
                verdict = "DIFFERS"
                bad += 1
            else:
                verdict = "identical"
            print(f"{os.path.basename(run)} {name}: {verdict}")
    print("DIGEST-IDENTICAL" if bad == 0 else f"{bad} file(s) differ or are missing")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
