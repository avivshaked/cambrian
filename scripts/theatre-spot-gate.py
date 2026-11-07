"""Computes a world's eyespot gate, so the theatre's eyespot lands on a true share of the bodies that would carry one in that world.

The mix's eyespot gate passes a body when 0.5 * (chroma + (1 - zone)) is above a threshold, 0.667 by default, and the bodies above
it are the bearers. The share dial (EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT_SHARE) raises the threshold by a formula that holds for two
independent ranks spread evenly, and the ranks in a world are neither, so the share it keeps differs from world to world. Given a
Unity log holding the skin map lines of one world's bodies, this scores each body, takes the bearers, and prints the threshold above
which exactly the wanted share of the bearers stay (default one half, the owner's ruling, D324), halfway between the last bearer kept
and the first dropped. It also prints how many the share dial would keep at the same share, for comparison.

Usage: python -I scripts/theatre-spot-gate.py LOG [--share 0.5]
       python -I scripts/theatre-spot-gate.py LOG [--share 0.5] --write WORLD --frame "TEXT" [--note "TEXT"]

The log's lines have the form "[Theatre] skin map: id N v ... f ... value X chroma X sheen X zone X [family N] [mix ...]", one per
body and possibly repeated for the same id across takes; each id is counted once, at its first line. A body whose chroma or zone is
missing or not a finite number is skipped. The score is s = 0.5 * (chroma + (1 - zone)), and a bearer is a body with s above 0.667.
With k = round(share * bearers) kept, the threshold is halfway between the k-th and (k+1)-th highest scores; with none kept it is the
highest score, and with all kept it is 0.667. Ties across the cut mean the count kept can differ from k, and the output says so.

--write adds or replaces WORLD's row in theatre-world-spots.tsv beside this script (created with a comment line and a header if
absent), and keeps every other row and the comment as they were. A film's launcher sets EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT_GATE
from the world's row. Standard library only; reads the log and writes nothing unless --write is given.
"""
import argparse
import math
import os
import re
import sys

EYESPOT = 0.667
ID_RE = re.compile(r"\bskin map: id (\d+)\b")
CHROMA_RE = re.compile(r"\bchroma (\S+)")
ZONE_RE = re.compile(r"\bzone (\S+)")
TABLE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "theatre-world-spots.tsv")
COMMENT = ("# The eyespot gate per world, set so a true share of the bodies that would carry an eyespot keep it in that world. "
           "A film's launcher sets EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT_GATE from its world's row. "
           "Computed with scripts/theatre-spot-gate.py from the world's skin map lines.")
HEADER = "world\tgate\tshare\tbearers\tkept\tframe\tnote"


def number(text):
    try:
        v = float(text)
    except (TypeError, ValueError):
        return None
    return v if math.isfinite(v) else None


def read_bodies(path):
    """Returns ({id: score}, number of ids whose later lines differ from their first, lines skipped for a bad chroma or zone)."""
    scores = {}
    differ = 0
    skipped = 0
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            m = ID_RE.search(line)
            if not m:
                continue
            c = CHROMA_RE.search(line)
            z = ZONE_RE.search(line)
            chroma = number(c.group(1)) if c else None
            zone = number(z.group(1)) if z else None
            if chroma is None or zone is None:
                skipped += 1
                continue
            s = 0.5 * (chroma + (1.0 - zone))
            bid = int(m.group(1))
            if bid in scores:
                if abs(scores[bid] - s) > 1e-9:
                    differ += 1
            else:
                scores[bid] = s
    return scores, differ, skipped


def half_up(x):
    return int(math.floor(x + 0.5))


def find_gate(bearer_scores, share):
    """Returns (threshold, k, note) for the bearers' scores sorted descending."""
    n = len(bearer_scores)
    k = half_up(share * n)
    if n == 0:
        return EYESPOT, 0, "no bearers; the threshold stays at %.3f" % EYESPOT
    if k <= 0:
        return bearer_scores[0], 0, "none kept; the threshold is the highest bearer's score"
    if k >= n:
        return EYESPOT, n, "all kept; the threshold stays at %.3f" % EYESPOT
    return 0.5 * (bearer_scores[k - 1] + bearer_scores[k]), k, ""


def write_row(world, gate, share, bearers, kept, frame, note):
    clean = lambda t: " ".join(str(t).split())
    row = "\t".join([clean(world), "%.6f" % gate, "%g" % share, str(bearers), str(kept), clean(frame), clean(note)])
    lines = []
    if os.path.exists(TABLE):
        with open(TABLE, "r", encoding="utf-8") as f:
            lines = f.read().splitlines()
    else:
        lines = [COMMENT, HEADER]
    if not lines or not lines[0].startswith("#"):
        lines.insert(0, COMMENT)
    if HEADER not in lines:
        lines.insert(1, HEADER)
    replaced = False
    out = []
    for ln in lines:
        if not ln.startswith("#") and ln.split("\t")[0] == world and ln != HEADER:
            if not replaced:
                out.append(row)
                replaced = True
            continue
        out.append(ln)
    if not replaced:
        out.append(row)
    with open(TABLE, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    return replaced


def main():
    ap = argparse.ArgumentParser(description="Eyespot gate threshold for one world, from its skin map log.")
    ap.add_argument("log")
    ap.add_argument("--share", type=float, default=0.5, help="share of the bearers that keep the eyespot (default 0.5)")
    ap.add_argument("--write", metavar="WORLD", help="add or replace WORLD's row in theatre-world-spots.tsv")
    ap.add_argument("--frame", default="", help="what the log is: the run, the second, the shot (required with --write)")
    ap.add_argument("--note", default="", help="a note for the row")
    a = ap.parse_args()
    if not 0.0 <= a.share <= 1.0:
        sys.exit("--share must be between 0 and 1 (%s)" % a.share)
    if a.write and not a.frame:
        sys.exit("--write needs --frame, to say what the log is")
    if not os.path.isfile(a.log):
        sys.exit("no such log: %s" % a.log)

    scores, differ, skipped = read_bodies(a.log)
    bearer_scores = sorted((s for s in scores.values() if s > EYESPOT), reverse=True)
    n = len(bearer_scores)
    gate, k, why = find_gate(bearer_scores, a.share)
    kept = sum(1 for s in bearer_scores if s > gate)

    print("bodies parsed: %d (lines skipped for a bad chroma or zone: %d; ids whose lines differ across takes: %d)" % (len(scores), skipped, differ))
    print("bearers (s above %.3f): %d" % (EYESPOT, n))
    print("share asked: %g" % a.share)
    print("kept at the threshold: %d (aimed at %d)%s" % (kept, k, "" if kept == k else "; ties across the cut"))
    if why:
        print("note: " + why)
    print("threshold: %.6f" % gate)
    print("EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT_GATE=%.6f" % gate)
    old = 1.0 - (1.0 - EYESPOT) * math.sqrt(a.share)
    old_kept = sum(1 for s in bearer_scores if s > old)
    print("share dial at the same share: threshold %.6f keeps %d of %d bearers (%.1f%%)" % (old, old_kept, n, 100.0 * old_kept / n if n else 0.0))

    if a.write:
        replaced = write_row(a.write, gate, a.share, n, kept, a.frame, a.note)
        print("%s row for %s in %s" % ("replaced" if replaced else "added", a.write, TABLE))


if __name__ == "__main__":
    main()
