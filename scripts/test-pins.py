"""The local data the tests read, as a keep list for a disk cleanup.

A test that reads a run, a checkpoint, a report or a pool that is gitignored carries [LocalDataFact(...)] (or LocalDataTheory,
LocalDataTreeFact, LocalDataTreeTheory, or an attribute derived from one) naming the path relative to the repository root
(src/TestSupport/LocalData.cs). Where the path is absent the test is skipped with the path named. This script reads those
attributes out of src/**/*.cs and prints every declared path, so that a cleanup that thins runs/ or rounds/nursery/pools/ keeps
what the tests pin, and so that a path the cleanup took shows as missing before a test run says it.

Usage: python -I scripts/test-pins.py [--missing] [--plain]

  (none)     every declared path, its state, and the tests' files that name it
  --missing  only the absent paths; exits 1 when there are any, else 0
  --plain    only the paths, one a line, for a script to read as a keep list (with --missing, only the absent ones)

A path may hold * in a segment (runs/*/*/run.json), present when anything matches. Like the attributes, a path is looked for under
this tree's root and every folder above it (a worktree under scratch/ finds the main checkout's runs/), except a path declared by a
Tree attribute, which is looked for under the root alone. Read-only; writes nothing; standard library only.
"""
import argparse, glob, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
BASE = re.compile(r"LocalData(Tree)?(Fact|Theory)Attribute")
LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')


def args_of(text, open_paren):
    """The text between the parenthesis at open_paren and its match, skipping string literals."""
    depth, i, n = 0, open_paren, len(text)
    while i < n:
        c = text[i]
        if c == '"':
            i += 1
            while i < n and text[i] != '"':
                i += 2 if text[i] == "\\" else 1
        elif c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return text[open_paren + 1:i]
        i += 1
    return ""


def line_of(text, pos):
    return text.count("\n", 0, pos) + 1


def scan():
    """Every declaration: (path, tree_only, file, line, attribute name)."""
    files = []
    for dirpath, dirnames, names in os.walk(os.path.join(ROOT, "src")):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "TestSupport")]
        files += [os.path.join(dirpath, n) for n in names if n.endswith(".cs")]
    texts = {f: open(f, encoding="utf-8-sig").read() for f in sorted(files)}

    # Attributes derived from the base ones declare their paths once, in base(...), and are used by name.
    derived = {}
    for f, text in texts.items():
        for m in re.finditer(r"class\s+(\w+?)(?:Attribute)?\s*:\s*" + BASE.pattern, text):
            tree = bool(m.group(2))
            b = text.find("base(", m.end())
            derived[m.group(1)] = (tree, [(lit, f, line_of(text, b)) for lit in LITERAL.findall(args_of(text, b + 4))])

    found, uses = [], {}
    for f, text in texts.items():
        rel = os.path.relpath(f, ROOT).replace(os.sep, "/")
        for m in re.finditer(r"\[\s*(LocalData(Tree)?(?:Fact|Theory))\s*\(", text):
            lits = LITERAL.findall(args_of(text, m.end() - 1))
            if not lits:
                print(f"warning: {rel}:{line_of(text, m.start())} names no literal path", file=sys.stderr)
            for lit in lits:
                found.append((lit, bool(m.group(2)), rel, line_of(text, m.start()), m.group(1)))
        for name in derived:
            for m in re.finditer(r"\[\s*" + re.escape(name) + r"\s*[\](]", text):
                uses[name] = uses.get(name, 0) + 1
    for name, (tree, decls) in derived.items():
        for lit, f, ln in decls:
            found.append((lit, tree, os.path.relpath(f, ROOT).replace(os.sep, "/"), ln, f"{name} x{uses.get(name, 0)}"))
    return found


def present(path, tree_only):
    roots = [ROOT]
    if not tree_only:
        d = os.path.dirname(ROOT)
        while d and d != os.path.dirname(d):
            roots.append(d)
            d = os.path.dirname(d)
        roots.append(d)
    for r in roots:
        if glob.glob(os.path.join(r, *path.split("/"))):
            return True
    return False


def main():
    a = argparse.ArgumentParser(description="Print the local data the tests read, or the part of it that is absent.")
    a.add_argument("--missing", action="store_true", help="only the absent paths; exit 1 if there are any")
    a.add_argument("--plain", action="store_true", help="only the paths, one a line")
    o = a.parse_args()

    by_path = {}
    for lit, tree, rel, ln, attr in scan():
        by_path.setdefault((lit, tree), {}).setdefault((rel, attr), []).append(ln)

    absent = 0
    for (path, tree), where in sorted(by_path.items()):
        here = present(path, tree)
        if not here:
            absent += 1
        if o.missing and here:
            continue
        if o.plain:
            print(path)
        else:
            print(f"{'present' if here else 'MISSING'}  {path}{'  (this tree only)' if tree else ''}")
            for (rel, attr), lines in sorted(where.items()):
                print(f"           {rel}:{','.join(str(n) for n in sorted(lines))} [{attr}]")
    if not o.plain:
        print(f"{len(by_path)} paths declared, {absent} missing", file=sys.stderr)
    return 1 if (o.missing and absent) else 0


if __name__ == "__main__":
    sys.exit(main())
