"""A resumed run's sources: the runs its run.json's resumedFrom names, read through to the start.

A resumed run's own lineage.jsonl holds only the rows after its checkpoint; the rows before it are in
the source's file, up to the second the resume began at. Shared by story-windows.py and guide.py (a
module whose name has a hyphen cannot be imported). Standard library only.
"""
import json
import math
import os

MAX_SOURCES = 8


def resume_sources(run_dir, max_sources=MAX_SOURCES):
    """([(source_dir, bound_seconds)] oldest first, [notes]).

    Each source is read only up to the smallest resume second met on the way from the run to it.
    A source is <runs root>/<arm>/<run>, the runs root being the run's own. The chain ends at a run
    with no resumedFrom, at max_sources, at a loop, or at a source that is missing or unreadable;
    a note says why when it ends early for any reason but the first.
    """
    out = []
    notes = []
    seen = {os.path.normcase(os.path.abspath(run_dir))}
    cur, bound = os.path.abspath(run_dir), math.inf
    while len(out) < max_sources:
        try:
            with open(os.path.join(cur, "run.json"), encoding="utf-8") as f:
                rf = json.load(f).get("resumedFrom")
        except (OSError, ValueError, AttributeError):
            if out:
                notes.append("the run.json of %s could not be read, so the chain of resumes ends there" % cur.replace("\\", "/"))
            break
        if not isinstance(rf, dict):
            break
        try:
            arm, run, sec = str(rf["arm"]), str(rf["run"]), float(rf["seconds"])
        except (KeyError, TypeError, ValueError):
            notes.append("the resumedFrom of %s is incomplete, so the chain of resumes ends there" % cur.replace("\\", "/"))
            break
        bound = min(bound, sec)
        src = os.path.abspath(os.path.join(os.path.dirname(os.path.dirname(cur)), arm, run))
        if os.path.normcase(src) in seen:
            notes.append("the chain of resumes returns to %s/%s, so it ends there" % (arm, run))
            break
        if not os.path.isdir(src):
            notes.append("the source %s/%s of the resume at %.1f s is missing, so its births before the resume are not known" % (arm, run, sec))
            break
        seen.add(os.path.normcase(src))
        out.append((src, bound))
        cur = src
    out.reverse()
    return out, notes
