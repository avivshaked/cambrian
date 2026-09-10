"""Row-for-row identity check between two run reports, modulo appended columns.

Transient: the replay-identity validation for the perception build (D075 item 1). The new
build's report carries four columns the baseline does not, so a byte diff of the files is
meaningless; what has to be identical is every column the baseline had, in every row.

    python scripts/reads/compare-rows.py runs/pv-base.md runs/pv-replay.md
"""
import io
import sys


def table(path):
    header = None
    rows = []
    for line in io.open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line.startswith("| "):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if header is None:
            header = cells
            continue
        if set("".join(cells)) <= set("-"):
            continue
        rows.append(cells)
    return header, rows


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    a_head, a_rows = table(a_path)
    b_head, b_rows = table(b_path)

    n = len(a_head)
    print("baseline columns: %d   new columns: %d   appended: %s"
          % (n, len(b_head), b_head[n:]))
    print("baseline rows: %d   new rows: %d" % (len(a_rows), len(b_rows)))

    if b_head[:n] != a_head:
        print("HEADER MISMATCH")
        for i, (x, y) in enumerate(zip(a_head, b_head)):
            if x != y:
                print("  col %d: %r vs %r" % (i, x, y))
        return 1

    compared = min(len(a_rows), len(b_rows))
    bad = 0
    for r in range(compared):
        x, y = a_rows[r], b_rows[r][:n]
        if x != y:
            bad += 1
            for i, (p, q) in enumerate(zip(x, y)):
                if p != q:
                    print("row %d (t=%s) col %d %r: %r vs %r"
                          % (r, x[0], i, a_head[i], p, q))
    print("rows compared: %d   differing: %d" % (compared, bad))
    return 1 if bad else 0


sys.exit(main())
