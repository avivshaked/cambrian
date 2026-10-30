"""Keep the index table at the top of DECISIONS.md complete: one row for every `### Dnnn` entry, in number order.

A row reads `| [Dnnn](#dnnn) | title | date | status |`. The script never touches a row that already exists, since the
older rows carry hand-written statuses. For an entry with no row it builds one: the title is the entry's bold title (the
text between the first pair of `**`, with a date prefix of the form `**2026-09-08 - Title**` or a closing full stop
dropped), the date is the first YYYY-MM-DD after it (or in the title or first paragraph where the entry has no
` · date`), and the status is the first sentence of the `**Status:**` paragraph, cut to about 200 characters at a word.
An entry with no Status paragraph reads `active`. If a later entry says it supersedes, replaces or reverses an entry by
number, or the entry's own text carries a struck-through passage marked "Superseded by Dnnn", the cell ends with
` · superseded by Dnnn` (` · partly superseded by Dnnn` when the later entry names a clause, a part or a ruling of it, as
in "supersedes D138's first ruling") or ` · reversal proposed in Dnnn` (a "would reverse" or "proposes to reverse").

Usage: python -I scripts/decisions-index.py [--write | --check] [--file DECISIONS.md]

With no flag, prints the rows that would be added and writes nothing. --write rewrites only the table's rows, in number
order, leaving every other byte of the file as found (line endings included). --check writes nothing and exits 1,
naming them, when an entry has no row or a row names an entry that does not exist; it exits 0 otherwise. Standard library
only.
"""
import argparse, os, re, sys

CUT = 200
ISO = re.compile(r"\d{4}-\d\d-\d\d")
ROW = re.compile(r"^\| \[D(\d+)\]\(#d\d+\) \|")
HEAD = re.compile(r"^### (D\d+)[ \t]*$", re.M)
VERBS = re.compile(r"(?i)\b(would reverse|proposes? to reverse|reverses|supersedes|replaces)\b")


def parse_entries(text):
    """Return {number: body} for every `### Dnnn` heading, bodies with line endings normalised to LF."""
    t = text.replace("\r\n", "\n")
    parts = HEAD.split(t)
    return {int(parts[i][1:]): parts[i + 1] for i in range(1, len(parts), 2)}


def squash(s):
    return re.sub(r"\s+", " ", s).strip()


def parse_head(body):
    """(title, date) from the top of an entry, whichever of the forms it uses; None for what is not found."""
    b = body.lstrip("\n")
    m = re.match(r"\*\*(.+?)\*\*", b, re.S)
    if not m:
        return None, None
    title = squash(m.group(1))
    after = b[m.end():]
    date = None
    dm = re.match(r"[ \t]*·[ \t]*(\d{4}-\d\d-\d\d)", after)
    if dm:
        date = dm.group(1)
    pm = re.match(r"(\d{4}-\d\d-\d\d)\W+(?=\S)", title)  # "2026-09-08 - Title" form
    if pm:
        date = date or pm.group(1)
        title = title[pm.end():]
    if date is None:
        first = b.split("\n\n", 1)[0]
        dm = ISO.search(first)
        date = dm.group(0) if dm else None
    # a bold lead-in sentence ("**The footprint world.** Ruled by ...") loses its full stop; a title ending in
    # an abbreviation-free full stop is a sentence either way
    title = title.rstrip(".").strip()
    return title, date


def status_sentence(body):
    m = re.search(r"^\*\*Status:\*\*[ \t]*(.*?)(?:\n[ \t]*\n|\Z)", body, re.M | re.S)
    if not m:
        return None
    s = squash(m.group(1))
    m2 = re.search(r"\.(?=\s+[A-Z`(*\[~])|\.$", s)
    if m2:
        s = s[: m2.start()]
    s = s.strip()
    if len(s) > CUT:
        cut = s[:CUT].rsplit(" ", 1)[0].rstrip(",;:( ")
        s = cut + "…"
    return s


def supersessions(entries):
    """{target: {"by": {Dnnn: partial?}, "prop": {...}}} from later entries and struck-through passages."""
    out = {}

    def add(target, by, partial, proposed=False):
        if target == by:
            return
        out.setdefault(target, {}).setdefault("prop" if proposed else "by", {})
        d = out[target]["prop" if proposed else "by"]
        d[by] = d.get(by, True) and partial

    for n, body in entries.items():
        flat = squash(body)
        for sent in re.split(r"(?<=[.;])\s+", flat):
            for vm in VERBS.finditer(sent):
                rest = re.split(r",\s+whose\b|;|\.\s", sent[vm.end():])[0]
                proposed = vm.group(1).lower().startswith(("would", "propose"))
                for tm in re.finditer(r"(part of |clause of )?\bD(\d{3})\b('s)?", rest):
                    partial = bool(tm.group(1) or tm.group(3))
                    add(int(tm.group(2)), n, partial, proposed)
        for sm in re.finditer(r"~~\s+Superseded by D(\d{3})", flat):
            add(n, int(sm.group(1)), True)
    return out


def build_row(n, entries, sup):
    title, date = parse_head(entries[n])
    st = status_sentence(entries[n]) or "active"
    notes = []
    s = sup.get(n, {})
    if s.get("by"):
        ids = sorted(s["by"])
        partly = all(s["by"][i] for i in ids)
        notes.append(("partly superseded by " if partly else "superseded by ") + ", ".join("D%03d" % i for i in ids))
    if s.get("prop"):
        notes.append("reversal proposed in " + ", ".join("D%03d" % i for i in sorted(s["prop"])))
    cell = " · ".join([st] + notes)
    esc = lambda x: x.replace("|", "\\|")
    return "| [D%03d](#d%03d) | %s | %s | %s |" % (n, n, esc(title or "(untitled)"), date or "(undated)", esc(cell)), title, date


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--file", default="DECISIONS.md")
    a = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    with open(a.file, "rb") as f:
        text = f.read().decode("utf-8")
    entries = parse_entries(text)
    lines = re.findall(r"[^\n]*\n|[^\n]+", text)
    idx = [i for i, l in enumerate(lines) if ROW.match(l)]
    if not idx:
        print("no index rows found in " + a.file)
        return 2
    if idx != list(range(idx[0], idx[-1] + 1)):
        print("the index rows are not one contiguous block; first break after line %d" % (idx[0] + 1))
        return 2
    existing = {int(ROW.match(lines[i]).group(1)): lines[i] for i in idx}
    missing = sorted(n for n in entries if n not in existing)
    orphan = sorted(n for n in existing if n not in entries)
    if a.check:
        if missing:
            print("entries with no row: " + ", ".join("D%03d" % n for n in missing))
        if orphan:
            print("rows naming no entry: " + ", ".join("D%03d" % n for n in orphan))
        if not (missing or orphan):
            print("index complete: %d rows, %d entries" % (len(existing), len(entries)))
        return 1 if (missing or orphan) else 0
    sup = supersessions(entries)
    eol = "\r\n" if lines[idx[-1]].endswith("\r\n") else "\n"
    built = {}
    for n in missing:
        row, title, date = build_row(n, entries, sup)
        built[n] = row + eol
        if title is None or date is None:
            print("WARNING D%03d: title or date not parsed (title=%r, date=%r)" % (n, title, date))
        if not a.write:
            print(row)
    if not a.write:
        print("%d rows would be added to %d existing" % (len(built), len(existing)))
        return 0
    rows = {**existing, **built}
    new_lines = lines[: idx[0]] + [rows[n] for n in sorted(rows)] + lines[idx[-1] + 1:]
    with open(a.file, "wb") as f:
        f.write("".join(new_lines).encode("utf-8"))
    print("wrote %d rows (%d new) to %s" % (len(rows), len(built), a.file))
    return 0


if __name__ == "__main__":
    sys.exit(main())
