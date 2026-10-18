"""List or stage chosen hunks of one file's working-tree diff in the look worktree.

    python hunks.py list <path>
    python hunks.py stage <path> 0,2,3

Hunks are numbered from 0 in `git diff -U3` order. Staging applies only the chosen hunks to the
index with `git apply --cached`, so one file's changes can go into several commits.
"""
import subprocess
import sys

WT = r"D:\Projects\experiments\evolution-simulator\scratch\wt-look"


def diff(path):
    out = subprocess.run(["git", "-C", WT, "diff", "-U3", "--", path],
                         capture_output=True, check=True).stdout
    return out.decode("utf-8")


def split(text):
    lines = text.splitlines(keepends=True)
    header, hunks, current = [], [], None
    for line in lines:
        if line.startswith("@@"):
            current = [line]
            hunks.append(current)
        elif current is None:
            header.append(line)
        else:
            current.append(line)
    return "".join(header), ["".join(h) for h in hunks]


def main():
    mode, path = sys.argv[1], sys.argv[2]
    header, hunks = split(diff(path))
    if mode == "list":
        for i, h in enumerate(hunks):
            body = [l for l in h.splitlines() if l[:1] in "+-"]
            print(f"--- hunk {i}: {h.splitlines()[0][:90]}")
            for l in body[:6]:
                print("   ", l[:110])
        return
    chosen = [int(x) for x in sys.argv[3].split(",")]
    patch = header + "".join(hunks[i] for i in chosen)
    subprocess.run(["git", "-C", WT, "apply", "--cached", "--recount", "-"],
                   input=patch.encode("utf-8"), check=True)
    print(f"staged hunks {chosen} of {path}")


if __name__ == "__main__":
    main()
