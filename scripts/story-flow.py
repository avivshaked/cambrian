#!/usr/bin/env python3
"""Where a round's story film stands, and what to do next.

    python scripts/story-flow.py init <story folder> --round 49 --runs r49-s1 r49-s2 r49-s3
                                      [--runs-root <runs>] [--clips <folder> ...]
    python scripts/story-flow.py status <story folder>
    python scripts/story-flow.py approve <story folder> arc|script|timeline [--note "..."]
    python scripts/story-flow.py mark <story folder> check <run> | deliver [--note "..."]
    python scripts/story-flow.py skip <story folder> narrate [--note "..."]
    python scripts/story-flow.py set <story folder> clips <folder> [...] | plan <folder> | film <mp4>
                                  | entry <the round's logbook entry> | prereg <its pre-registration>

The story-film flow (`.claude/skills/story-film/SKILL.md`, `logbook/specs/story-film.md`) runs
over days and across sessions, so its state is kept where any agent can read it: the story's folder
(`scratch/story/<round>/`). Most of a stage's state is read from the files it leaves (a guide per
run, `arc.md`, `story.json`, `script.md`, `narration/narration.json`, `story.filmed.json`, the clips,
the Resolve plan). What no file shows is kept in `flow.json`: the owner's three approvals, each
with a hash of what was approved so that a later edit shows as stale (the script's without its
timing, since the narration comes after that review and retimes it), the director's check per run,
the narration stage skipped, and delivery. `status` prints every stage and the next step.
"""
import argparse
import datetime
import glob
import hashlib
import importlib.util
import io
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIP_PREFIX = "story-%02d-"
FILM_FIELDS = ("run", "station", "second", "subject", "seconds", "flexible", "chapter", "chart", "canopy",
               "description", "from", "to")


def script_tool():
    spec = importlib.util.spec_from_file_location("story_script", os.path.join(REPO, "scripts", "story-script.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def now():
    return datetime.datetime.now().isoformat(timespec="seconds")


def sha_file(*paths):
    h = hashlib.sha256()
    for p in paths:
        if p and os.path.isfile(p):
            with open(p, "rb") as f:
                h.update(f.read())
    return h.hexdigest()[:16]


def load_json(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def flow_path(folder):
    return os.path.join(folder, "flow.json")


def read_flow(folder):
    path = flow_path(folder)
    if not os.path.isfile(path):
        sys.exit("no flow.json in %s: start with `story-flow.py init`" % folder)
    return load_json(path)


def write_flow(folder, flow):
    with io.open(flow_path(folder), "w", encoding="utf-8", newline="\n") as f:
        json.dump(flow, f, ensure_ascii=False, indent=2)
        f.write("\n")


def scenes(story):
    return story["scenes"] if isinstance(story, dict) else story


def captions_hash(story):
    items = [(s["n"], [(c.get("text") if isinstance(c, dict) else c) for c in (s.get("captions") or [])])
             for s in scenes(story)]
    return hashlib.sha256(json.dumps(items, ensure_ascii=False).encode("utf-8")).hexdigest()[:16]


TIMING = {"story": ("narration", "screen_seconds"), "scene": ("seconds", "least_seconds", "narration"),
          "caption": ("at", "seconds"), "chart": ("at", "until")}


def script_hash(story):
    """What the owner approves at the script review: story.json without its timing. The narration
    comes after the review (the owner, 2026-09-26) and rewrites every length and caption time, so
    those are left out; a word, a scene, a subject or a chart changed after the review reads stale."""
    story = json.loads(json.dumps(story))
    if isinstance(story, dict):
        for k in TIMING["story"]:
            story.pop(k, None)
    for s in scenes(story):
        for k in TIMING["scene"]:
            s.pop(k, None)
        for c in s.get("captions") or []:
            if isinstance(c, dict):
                for k in TIMING["caption"]:
                    c.pop(k, None)
        if isinstance(s.get("chart"), dict):
            for k in TIMING["chart"]:
                s["chart"].pop(k, None)
    return hashlib.sha256(json.dumps(story, ensure_ascii=False, sort_keys=True).encode("utf-8")).hexdigest()[:16]


def run_dir(runs_root, arm):
    """The newest run directory of an arm (the one with a run.json), or None."""
    found = sorted(d for d in glob.glob(os.path.join(runs_root, arm, "*")) if os.path.isfile(os.path.join(d, "run.json")))
    return found[-1] if found else None


def clips_by_scene(folders):
    found = {}
    for folder in folders:
        if not os.path.isdir(folder):
            continue
        for name in os.listdir(folder):
            if name.startswith("story-") and name.endswith(".mp4") and name[6:8].isdigit():
                found.setdefault(int(name[6:8]), []).append(os.path.join(folder, name))
    return found


def film_changes(story, filmed):
    """Scenes whose filmed fields changed since story.filmed.json: they need filming again."""
    was = {s["n"]: s for s in scenes(filmed)}
    changed = []
    for s in scenes(story):
        w = was.get(s["n"])
        if w is None or any(s.get(k) != w.get(k) for k in FILM_FIELDS):
            changed.append(s["n"])
    return changed


def stages(folder, flow):
    """[(stage, state, evidence, next step)], state one of done, skipped, stale, open, waiting."""
    f = lambda name: os.path.join(folder, name)
    out = []
    approvals, marks = flow.get("approvals", {}), flow.get("marks", {})
    story = load_json(f("story.json")) if os.path.isfile(f("story.json")) else None

    def gate(name, digest, what):
        a = approvals.get(name)
        if not a:
            return "open", "not approved", "show the owner %s and ask for a ruling; then `story-flow.py approve %s`" % (what, name)
        if a.get("hash") != digest:
            return "stale", "approved %s, changed since" % a.get("at", "?"), "show the owner what changed in %s" % what
        return "done", "approved %s%s" % (a.get("at", "?"), (": " + a["note"]) if a.get("note") else ""), ""

    runs_root = flow.get("runs_root") or os.path.join(REPO, "runs")
    have = [a for a in flow.get("runs", []) if run_dir(runs_root, a) and
            os.path.isfile(os.path.join(run_dir(runs_root, a), "guide", "guide.json"))]
    out.append(("guides", "done" if len(have) == len(flow.get("runs", [])) else "open",
                "%d of %d runs have a guide" % (len(have), len(flow.get("runs", []))),
                "python scripts/guide.py <arm> --no-economics for each run without one"))

    out.append(("arc", "done" if os.path.isfile(f("arc.md")) else "open",
                "arc.md" if os.path.isfile(f("arc.md")) else "no arc.md",
                "the writer's first pass: arc.md (the arc, its kind, who we follow, the chapters)"))
    out.append(("arc approved",) + gate("arc", sha_file(f("arc.md")), "arc.md"))

    wrote = all(os.path.isfile(f(n)) for n in ("story.md", "story.json", "checks.tsv"))
    out.append(("write", "done" if wrote else "open",
                "story.md, story.json, checks.tsv" if wrote else "missing: " + ", ".join(
                    n for n in ("story.md", "story.json", "checks.tsv") if not os.path.isfile(f(n))),
                "the writer's second pass, from the approved arc"))

    if story is not None:
        tool = script_tool()
        edited = os.path.isfile(f("story.draft.json")) and os.path.isfile(f("edits.tsv"))
        synced = tool.in_sync(folder)
        errors = [x for x in tool.findings(folder) if x[0] == "ERROR"]
        state = "done" if edited and synced and not errors else "open"
        why = "; ".join(filter(None, [None if edited else "not edited yet", None if synced else "script.md not applied",
                                      "%d check error(s)" % len(errors) if errors else None])) or "edited, applied, checked"
        out.append(("script", state, why, "the story-script skill: render, edit, apply, check, cold read"))
    else:
        out.append(("script", "open", "no story.json", "after the writer"))

    out.append(("script approved",) + gate("script", script_hash(story) if story is not None else "",
                                            "script.md, the words before any narration"))

    narration = f(os.path.join("narration", "narration.json"))
    script_ok = approvals.get("script") and story is not None and approvals["script"].get("hash") == script_hash(story)
    if marks.get("narrate", {}).get("skipped"):
        out.append(("narrate", "skipped", marks["narrate"].get("note", ""), ""))
    elif not script_ok:
        out.append(("narrate", "waiting", "the script is not approved yet; nothing is narrated before it is",
                    "after the owner approves the script"))
    elif os.path.isfile(narration):
        current = load_json(narration).get("story_captions") == captions_hash(story)
        timed = isinstance(story, dict) and (story.get("narration") or {}).get("manifest") == hashlib.sha256(
            open(narration, "rb").read()).hexdigest()[:16]
        if not current:
            out.append(("narrate", "stale", "narration.json is older than the captions",
                        "story-narration.py segments, narrate the changed paragraphs, then timing"))
        else:
            out.append(("narrate", "done" if timed else "open", "narrated and timed" if timed else "narrated, not timed",
                        "python scripts/story-narration.py timing <folder>"))
    else:
        out.append(("narrate", "waiting", "no narration.json; the service is not built yet",
                    "narrate (logbook/specs/story-narration.md), or `story-flow.py skip narrate`"))

    runs_in_story = sorted({s.get("run") for s in scenes(story)}) if story else []
    checked = [r for r in runs_in_story if r in marks.get("check", {})]
    out.append(("check", "done" if story and len(checked) == len(runs_in_story) else "open",
                "%d of %d runs checked" % (len(checked), len(runs_in_story)),
                "theatre-safari.ps1 <arm> -Story story.json -Check per run; then `story-flow.py mark check <arm>`"))

    if story is not None and os.path.isfile(f("story.filmed.json")):
        found = clips_by_scene(flow.get("clips", []))
        missing = [s["n"] for s in scenes(story) if s["n"] not in found]
        refilm = film_changes(story, load_json(f("story.filmed.json")))
        state = "stale" if refilm else ("done" if not missing else "open")
        why = "%d of %d scenes have a clip" % (len(scenes(story)) - len(missing), len(scenes(story)))
        if refilm:
            why += "; scenes %s changed since filming" % ", ".join(map(str, refilm))
        out.append(("film", state, why, "film the scenes named (the procedure's section 5); a caption edit alone needs no filming"))
    else:
        out.append(("film", "open", "no story.filmed.json", "copy story.json to story.filmed.json, then film"))

    plan = os.path.join(flow.get("plan") or f("resolve"), "plan.json")
    if os.path.isfile(plan) and story is not None:
        fresh = os.path.getmtime(plan) >= os.path.getmtime(f("story.json"))
        out.append(("assemble", "done" if fresh else "stale", "plan.json" + ("" if fresh else ", older than story.json"),
                    "story-resolve.py (plan and build a new vK); or story-assemble.py without Resolve"))
    else:
        out.append(("assemble", "open", "no plan", "story-resolve.py <story.json> <plan folder> <clip folders>"))
    out.append(("timeline approved",) + gate("timeline", sha_file(plan), "the Resolve timeline"))

    d = marks.get("deliver")
    out.append(("deliver", "done" if d else "open", ("delivered %s" % d.get("at")) if d else "not delivered",
                "render, copy the film and story.md to scratch/owner/, give the owner the path; `story-flow.py mark deliver`"))
    return out


def cmd_status(folder):
    flow = read_flow(folder)
    rows = stages(folder, flow)
    print("story flow: round %s, runs %s, folder %s" % (flow.get("round"), ", ".join(flow.get("runs", [])), folder))
    print("%-18s %-8s %s" % ("stage", "state", "evidence"))
    for stage, state, why, _ in rows:
        print("%-18s %-8s %s" % (stage, state, why))
    pending = [r for r in rows if r[1] not in ("done", "skipped")]
    if not pending:
        print("next: nothing; the film is delivered")
        return
    stage, state, _, step = pending[0]
    print("next: %s (%s): %s" % (stage, state, step))
    if stage.endswith("approved"):
        print("      this stage waits on the owner: say so plainly, with the decision in full, and keep working on the rest")


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    sub = ap.add_subparsers(dest="command", required=True)
    p = sub.add_parser("init")
    p.add_argument("folder")
    p.add_argument("--round", required=True)
    p.add_argument("--runs", nargs="+", required=True)
    p.add_argument("--runs-root")
    p.add_argument("--clips", nargs="*", default=[])
    p = sub.add_parser("status")
    p.add_argument("folder")
    p = sub.add_parser("approve")
    p.add_argument("folder")
    p.add_argument("what", choices=("arc", "script", "timeline"))
    p.add_argument("--note", default="")
    p = sub.add_parser("mark")
    p.add_argument("folder")
    p.add_argument("what", choices=("check", "deliver"))
    p.add_argument("run", nargs="?")
    p.add_argument("--note", default="")
    p = sub.add_parser("skip")
    p.add_argument("folder")
    p.add_argument("what", choices=("narrate",))
    p.add_argument("--note", default="")
    p = sub.add_parser("set")
    p.add_argument("folder")
    p.add_argument("what", choices=("clips", "plan", "film", "entry", "prereg"))
    p.add_argument("values", nargs="+")
    a = ap.parse_args()
    folder = os.path.abspath(a.folder)

    if a.command == "init":
        if os.path.isfile(flow_path(folder)):
            sys.exit("%s already has a flow.json; `status` reads it" % folder)
        os.makedirs(folder, exist_ok=True)
        write_flow(folder, {"version": 1, "round": a.round, "runs": a.runs, "made": now(),
                            "runs_root": os.path.abspath(a.runs_root) if a.runs_root else os.path.join(REPO, "runs"),
                            "clips": [os.path.abspath(c) for c in a.clips], "plan": os.path.join(folder, "resolve"),
                            "approvals": {}, "marks": {}})
        cmd_status(folder)
        return
    flow = read_flow(folder)
    if a.command == "status":
        cmd_status(folder)
        return
    if a.command == "approve":
        f = lambda name: os.path.join(folder, name)
        digest = {"arc": lambda: sha_file(f("arc.md")),
                  "script": lambda: script_hash(load_json(f("story.json"))),
                  "timeline": lambda: sha_file(os.path.join(flow.get("plan") or f("resolve"), "plan.json"))}[a.what]()
        flow.setdefault("approvals", {})[a.what] = {"at": now(), "hash": digest, "note": a.note}
    elif a.command == "mark":
        marks = flow.setdefault("marks", {})
        if a.what == "check":
            if not a.run:
                sys.exit("mark check <run>")
            marks.setdefault("check", {})[a.run] = {"at": now(), "note": a.note}
        else:
            marks["deliver"] = {"at": now(), "note": a.note}
    elif a.command == "skip":
        flow.setdefault("marks", {})[a.what] = {"skipped": True, "at": now(), "note": a.note}
    elif a.command == "set":
        if a.what == "clips":
            flow["clips"] = [os.path.abspath(v) for v in a.values]
        else:
            flow[a.what] = os.path.abspath(a.values[0])
    write_flow(folder, flow)
    cmd_status(folder)


if __name__ == "__main__":
    main()
