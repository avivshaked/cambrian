"""Row-for-row identity between two run reports, matched by column NAME.

`compare-rows.py` assumes the new build's columns are a prefix of the baseline's plus a
suffix. That stopped being true with the sea-bed build: `floor con` is appended after
`contacts`, which is the end of BaseColumns, while the per-patch columns (p0..pK-1) are
appended after *that* for every run — so the new table interleaves rather than extends.
CLAUDE.md's rule for reading a report is "never index columns positionally"; this is that
rule applied to the identity check itself.

Every column the baseline has must exist in the new report and hold the same text in every
row compared. Columns only the new report has are listed and ignored.

    python scripts/reads/compare-rows-by-name.py runs/fp-replay3.md runs/fl-replay.md
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

    a_index = {name: i for i, name in enumerate(a_head)}
    b_index = {name: i for i, name in enumerate(b_head)}

    if len(a_index) != len(a_head) or len(b_index) != len(b_head):
        print("a report has a duplicate column name; the join is ambiguous")
        return 2

    missing = [n for n in a_head if n not in b_index]
    added = [n for n in b_head if n not in a_index]

    print("baseline columns: %d   new columns: %d" % (len(a_head), len(b_head)))
    print("added: %s" % (added or "none"))
    print("missing from the new report: %s" % (missing or "none"))

    if missing:
        return 1

    n = min(len(a_rows), len(b_rows))
    print("baseline rows: %d   new rows: %d   compared: %d" % (len(a_rows), len(b_rows), n))

    differing = 0
    for r in range(n):
        for name in a_head:
            av = a_rows[r][a_index[name]]
            bv = b_rows[r][b_index[name]]
            if av != bv:
                differing += 1
                if differing <= 20:
                    print("  row %d, column %r: %r vs %r" % (r, name, av, bv))

    print("differing cells: %d" % differing)
    return 0 if differing == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
