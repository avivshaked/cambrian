#!/usr/bin/env python3
"""Where a round's story film stands, what comes next, and the record of every verdict in it.

    python scripts/story-flow.py init   <story folder> --round 49 --runs r49-s1 r49-s2 r49-s3 [--runs-root <runs>]
    python scripts/story-flow.py status <story folder>
    python scripts/story-flow.py next   <story folder>
    python scripts/story-flow.py save   <story folder> <agent id>
    python scripts/story-flow.py owner  <story folder> arc|draft|script|timeline <file of the owner's words>
                                        --verdict approve|reject|pass|return|changes
    python scripts/story-flow.py set    <story folder> entry|prereg|film|plan <path> | clips <folder> [...]
    python scripts/story-flow.py mark   <story folder> check <run> --log <editor log> | deliver <film>
    python scripts/story-flow.py skip   <story folder> narrate        (and `unskip`)
    python scripts/story-flow.py glossary <story folder>

The story-film flow (`.claude/skills/story-film/SKILL.md`, `logbook/specs/story-film.md`) runs over
days and across sessions, and any session that runs it has to reach the same film. So nothing in it
is left to the session's judgment. Every verdict comes from one of three places: a tool (the checks
of `scripts/story-script.py`); a subagent launched with a fixed prompt from
`.claude/skills/story-film/prompts.md`, whose answer `save` copies word for word from the agent's
own transcript; or the owner's words, saved by `owner` as they wrote them. Every verdict is kept in
`flow.json` with the hash of what it was about, so an edit after it reads as stale, and every loop
has a limit after which the flow stops and asks the owner.

`next` is the one command a session runs. It does the next mechanical step itself (a check, an
apply, a page for a reader), then prints what comes after: a subagent to launch with its prompt word
for word, a command the session runs when the machine allows, or the owner's ruling to ask for.
`status` prints every stage without changing anything.
"""
import argparse
import contextlib
import datetime
import glob
import hashlib
import importlib.util
import io
import json
import os
import re
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROMPTS = os.path.join(REPO, ".claude", "skills", "story-film", "prompts.md")
FILM_FIELDS = ("run", "station", "second", "subject", "seconds", "flexible", "chapter", "chart", "canopy",
               "description", "from", "to")
DRAFT_LIMIT = 3      # drafts that fail in a row before the owner rules on one
ROUND_LIMIT = 3      # script rounds before the script goes to the owner as it stands
ATTEMPT_LIMIT = 2    # an editor's tries at one round before the scenes the check refuses are reverted
VERDICTS = {"arc": ("approve", "reject"), "draft": ("pass", "return"), "script": ("approve", "changes"),
            "timeline": ("approve", "changes")}
WRITER_FILES = ("story.md", "story.json", "checks.tsv", "make_story.py", "writer-report.md", "glossary-rows.md")
ARC_FILES = ("arc.md", "arc-reads.md", "arc-choices.md")


_SCRIPT_TOOL = []


def script_tool():
    if not _SCRIPT_TOOL:
        spec = importlib.util.spec_from_file_location("story_script", os.path.join(REPO, "scripts", "story-script.py"))
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        _SCRIPT_TOOL.append(module)
    return _SCRIPT_TOOL[0]


def now():
    return datetime.datetime.now().isoformat(timespec="seconds")


def sha_file(*paths):
    h = hashlib.sha256()
    for p in paths:
        if p and os.path.isfile(p):
            with open(p, "rb") as fh:
                h.update(os.path.basename(p).encode("utf-8"))
                h.update(fh.read())
    return h.hexdigest()[:16]


def load_json(path):
    with io.open(path, encoding="utf-8-sig") as fh:
        return json.load(fh)


def write_text(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text if text.endswith("\n") else text + "\n")


def read_text(path):
    with io.open(path, encoding="utf-8-sig") as fh:
        return fh.read()


def flow_path(folder):
    return os.path.join(folder, "flow.json")


def read_flow(folder):
    path = flow_path(folder)
    if not os.path.isfile(path):
        sys.exit("no flow.json in %s: start with `story-flow.py init`" % folder)
    flow = load_json(path)
    for key, empty in (("approvals", {}), ("marks", {}), ("owner", []), ("pending", []), ("drafts", []),
                       ("script", {"rounds": []})):
        flow.setdefault(key, empty)
    return flow


def write_flow(folder, flow):
    flow["version"] = 2
    with io.open(flow_path(folder), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(flow, fh, ensure_ascii=False, indent=2)
        fh.write("\n")


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
    comes after the review and rewrites every length and caption time, so those are left out; a word,
    a scene, a subject or a chart changed after the review reads stale."""
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


def filmed_fields_hash(scene):
    return hashlib.sha256(json.dumps({k: scene.get(k) for k in FILM_FIELDS if k != "seconds"}, sort_keys=True,
                                     ensure_ascii=False).encode("utf-8")).hexdigest()[:16]


def film_changes(story, filmed):
    """Scenes whose filmed fields changed since story.filmed.json, or which now run longer than they
    were filmed: they need filming again. A scene that got shorter keeps its clip."""
    was = {s["n"]: s for s in scenes(filmed)}
    changed = []
    for s in scenes(story):
        w = was.get(s["n"])
        if w is None or filmed_fields_hash(s) != filmed_fields_hash(w) or \
                float(s.get("seconds") or 0) > float(w.get("seconds") or 0) + 1e-6:
            changed.append(s["n"])
    return changed


# ------------------------------------------------------------------------------------------------
# The prompts, and the answers.

def prompts():
    """{role: {"agent", "model", "text"}} from prompts.md: a `## <role>` heading, an `agent:` line and a
    fenced block holding the prompt."""
    text = read_text(PROMPTS)
    found = {}
    for m in re.finditer(r"^## ([a-z-]+)[ \t]*\n(.*?)(?=^## |\Z)", text, re.M | re.S):
        role, body = m.group(1), m.group(2)
        agent = re.search(r"^agent:\s*(\S+)\s*[·|]\s*model:\s*(\S+)", body, re.M)
        block = re.search(r"^```[a-z]*\n(.*?)^```", body, re.M | re.S)
        if agent and block:
            found[role] = {"agent": agent.group(1), "model": agent.group(2), "text": block.group(1).rstrip("\n")}
    return found


def prompt_for(role, folder, k=None):
    p = prompts().get(role)
    if not p:
        sys.exit("no prompt for '%s' in %s" % (role, PROMPTS))
    text = p["text"].replace("<checkout>", REPO).replace("<folder>", folder)
    if k is not None:
        text = text.replace("<k>", str(k)).replace("<n>", str(k))
    return p["agent"], p["model"], text


def norm(text):
    return " ".join(str(text).split())


def transcripts_root():
    return os.environ.get("EVOSIM_TRANSCRIPTS") or os.path.join(os.path.expanduser("~"), ".claude", "projects")


def find_transcript(agent_id):
    slug = re.sub(r"[^A-Za-z0-9]", "-", REPO)
    found = glob.glob(os.path.join(transcripts_root(), slug, "**", "agent-%s.jsonl" % agent_id), recursive=True)
    if not found:
        found = glob.glob(os.path.join(transcripts_root(), "*", "**", "agent-%s.jsonl" % agent_id), recursive=True)
    return sorted(found, key=os.path.getmtime)[-1] if found else None


def answer_of(agent_id):
    """(the agent's prompt, its final answer) from its transcript, word for word: the message of its last
    SubagentHandback call, or else the text of its last message that has any."""
    path = find_transcript(agent_id)
    if not path:
        sys.exit("no transcript for agent %s under %s" % (agent_id, transcripts_root()))
    first, handback, last_text = None, None, None
    with io.open(path, encoding="utf-8") as fh:
        for line in fh:
            try:
                r = json.loads(line)
            except ValueError:
                continue
            content = (r.get("message") or {}).get("content")
            if r.get("type") == "user" and first is None:
                if isinstance(content, str):
                    first = content
                elif isinstance(content, list):
                    texts = [b.get("text", "") for b in content if isinstance(b, dict) and b.get("type") == "text"]
                    if texts:
                        first = "\n".join(texts)
            if r.get("type") == "assistant" and isinstance(content, list):
                for b in content:
                    if not isinstance(b, dict):
                        continue
                    if b.get("type") == "tool_use" and b.get("name") == "SubagentHandback":
                        handback = (b.get("input") or {}).get("message")
                    elif b.get("type") == "text" and b.get("text", "").strip():
                        last_text = b["text"]
    answer = handback if handback is not None else last_text
    if not answer:
        sys.exit("agent %s has no answer yet (its transcript is %s)" % (agent_id, path))
    return first or "", answer


# ------------------------------------------------------------------------------------------------
# What each verdict reads.

def parse_listen(text):
    m = re.findall(r"^\s*VERDICT:\s*(PASS|FAIL)\s*$", text, re.M)
    if not m:
        return None
    faults = re.findall(r"^\s*scene\s+\d+,\s*line\s+\S+\s*\|", text, re.M | re.I)
    if (m[-1] == "PASS") != (not faults):
        return None
    return {"verdict": m[-1], "faults": len(faults)}


def parse_facts(text):
    m = re.findall(r"^\s*VERDICT:\s*(\d+)\s+false\s+of\s+(\d+)\s*$", text, re.M | re.I)
    if not m:
        return None
    false, of = int(m[-1][0]), int(m[-1][1])
    scenes_false = sorted({int(x) for x in re.findall(r"^\s*scene\s+(\d+),[^|]*\|\s*FALSE", text, re.M | re.I)})
    rows_false = sorted({int(x) for x in re.findall(r"^\s*row\s+(\d+)\s*\|\s*FALSE", text, re.M | re.I)})
    if false != len(re.findall(r"^[^|\n]*\|\s*FALSE", text, re.M | re.I)):
        return None
    return {"false": false, "of": of, "scenes": scenes_false, "rows": rows_false,
            "verdict": "PASS" if false == 0 else "FAIL"}


def parse_cold(text):
    m = re.search(r"STUMBLES:\s*(\d+)", text)
    summary = re.search(r"(?ms)^\s*(?:\*\*)?3\.(?:\*\*)?\s*(.+)$", text)
    if not m or not summary:
        return None
    return {"stumbles": int(m.group(1)), "summary": summary.group(1).strip()}


def parse_arc(text):
    m = re.search(r"TURN:\s*(TOLD|MISSED)\s*(?:\|\s*(.*))?", text)
    if not m:
        return None
    return {"told": m.group(1) == "TOLD", "missing": (m.group(2) or "").strip()}


ROLES = {
    # role: (the file its answer is saved as, in the draft's or the round's folder; its parser)
    "listener": ("listen.md", parse_listen),
    "facts-draft": ("facts.md", parse_facts),
    "facts-script": ("facts.md", parse_facts),
    "cold": ("cold.md", parse_cold),
    "arc-compare": ("arc.md", parse_arc),
}


# ------------------------------------------------------------------------------------------------
# The state.

class Step(Exception):
    """What the session does next: kind is 'agents', 'run', 'owner', 'wait' or 'done'; rows are the
    stages walked to reach it."""

    def __init__(self, kind, text, rows, agents=None):
        Exception.__init__(self, text)
        self.kind, self.text, self.rows, self.agents = kind, text, rows, agents or []


def f(folder, *parts):
    return os.path.join(folder, *parts)


def arc_hash(folder):
    owner = sorted(glob.glob(f(folder, "owner", "arc-*.md")))
    return sha_file(f(folder, "arc.md"), f(folder, "arc-choices.md"), *owner)


def owner_files(folder, stage):
    return sorted(glob.glob(f(folder, "owner", "%s-*.md" % stage)),
                  key=lambda p: int(re.search(r"-(\d+)-", os.path.basename(p)).group(1)))


def draft_story(folder):
    """The writer's story: story.draft.json once the script stage has kept it, story.json before."""
    p = f(folder, "story.draft.json")
    if os.path.isfile(p):
        return load_json(p)
    return load_json(f(folder, "story.json")) if os.path.isfile(f(folder, "story.json")) else None


def pending_role(flow, role, k=None):
    return next((p for p in flow["pending"] if p["role"] == role and (k is None or p.get("k") == k)), None)


def launch(flow, folder, role, k=None, extra=None):
    agent, model, text = prompt_for(role, folder, k)
    p = {"role": role, "k": k, "at": now(), "prompt": text, "agent": agent, "model": model}
    p.update(extra or {})
    flow["pending"] = [q for q in flow["pending"] if not (q["role"] == role and q.get("k") == k)] + [p]
    return p


def ask(flow, folder, act, role, k=None, extra=None):
    """The pending launch of a role, or a new one when acting, or a placeholder for status."""
    p = pending_role(flow, role, k)
    if p:
        return p
    return launch(flow, folder, role, k, extra) if act else {"role": role, "k": k}


def tool(*args):
    """Runs a story-script.py command in this process and returns what it printed. A new process per
    command made a test run start hundreds of them; the machine froze twice on 2026-09-26 while bursts
    of process starts were running, cause unproven, so the flow starts none it does not need."""
    buf, code, argv = io.StringIO(), 0, sys.argv
    sys.argv = ["story-script.py"] + list(args)
    try:
        with contextlib.redirect_stdout(buf):
            script_tool().main()
    except SystemExit as e:
        code = e.code if isinstance(e.code, int) else (0 if e.code is None else 2)
        if isinstance(e.code, str):
            buf.write(e.code + "\n")
    finally:
        sys.argv = argv
    if code not in (0, 1):
        sys.exit("story-script.py %s failed:\n%s" % (" ".join(args), buf.getvalue()))
    return buf.getvalue()


def check_story(folder, out_path):
    """Runs story-script.py check on the folder and writes its output; returns the error count and the
    scenes with an ERROR (0 for the title and checks.tsv)."""
    text = tool("check", folder)
    write_text(out_path, text)
    m = re.search(r"check: (\d+) error", text)
    if not m:
        sys.exit("story-script.py check printed no count:\n" + text)
    bad = set()
    for line in text.splitlines():
        if line.startswith("ERROR"):
            s = re.match(r"ERROR\s+scene (\d+)", line)
            bad.add(int(s.group(1)) if s else 0)
    return int(m.group(1)), sorted(bad)


def report_stamp(folder):
    p = f(folder, "writer-report.md")
    return os.path.getmtime(p) if os.path.isfile(p) else None


def draft_failed(d):
    return d["check"]["errors"] > 0 or d.get("listener", {}).get("verdict") == "FAIL" or \
        d.get("facts-draft", {}).get("verdict") == "FAIL"


def draft_reason(d):
    parts = []
    if d["check"]["errors"]:
        parts.append("%d check error(s)" % d["check"]["errors"])
    if d.get("listener", {}).get("verdict") == "FAIL":
        parts.append("the listener named %d fault(s)" % d["listener"].get("faults", 0))
    if d.get("facts-draft", {}).get("verdict") == "FAIL":
        parts.append("%d row(s) false" % d["facts-draft"]["false"])
    return ", ".join(parts)


def advance(folder, flow, act):
    """Walks the flow from the start, doing each mechanical step it can when `act` is true, and raises
    the Step that comes next. `status` walks it with act false and changes nothing."""
    runs_root = flow.get("runs_root") or os.path.join(REPO, "runs")
    rows = []

    def row(stage, state, why):
        rows.append((stage, state, why))

    # open
    missing = [k for k in ("entry", "prereg") if not flow.get(k)]
    if missing:
        row("open", "open", "no " + " or ".join(missing))
        raise Step("run", "name the round's logbook entry and its pre-registration: `story-flow.py set <folder> entry "
                   "<logbook/NNNN-....md>` and `set <folder> prereg <...>` (the entry that reports the round and the "
                   "entry that pre-registered it)", rows)
    rels = [os.path.relpath(flow[k], REPO).replace(os.sep, "/") for k in ("entry", "prereg")]
    tracked = set(subprocess.run(["git", "-C", REPO, "ls-files", "--"] + rels, capture_output=True, text=True,
                                 encoding="utf-8").stdout.split())
    changed = subprocess.run(["git", "-C", REPO, "status", "--porcelain", "--"] + rels, capture_output=True, text=True,
                             encoding="utf-8").stdout
    dirty = [r for r in rels if r not in tracked or r in changed]
    running = []
    for arm in flow.get("runs", []):
        d = run_dir(runs_root, arm)
        if not d or load_json(os.path.join(d, "run.json")).get("status") in (None, "running"):
            running.append(arm)
    if dirty or running:
        row("open", "open", "; ".join(filter(None, ["not committed: " + ", ".join(dirty) if dirty else "",
                                                     "still running or missing: " + ", ".join(running) if running else ""])))
        raise Step("wait", "a story starts from committed entries and ended runs", rows)
    row("open", "done", "entry and prereg committed; %d runs ended" % len(flow.get("runs", [])))

    # guides
    need = []
    for arm in flow.get("runs", []):
        g = os.path.join(run_dir(runs_root, arm), "guide", "guide.json")
        if not os.path.isfile(g) or load_json(g).get("status") == "running":
            need.append(arm)
    if need:
        row("guides", "open", "no final guide for " + ", ".join(need))
        raise Step("run", "`python scripts/guide.py <arm> --no-economics` for " + ", ".join(need), rows)
    row("guides", "done", "a guide for every run")

    # arc: the writer's first pass
    if not all(os.path.isfile(f(folder, n)) for n in ARC_FILES):
        row("arc", "open", "missing: " + ", ".join(n for n in ARC_FILES if not os.path.isfile(f(folder, n))))
        p = pending_role(flow, "writer-arc")
        raise Step("agents", "the writer's first pass" + (" (launched %s; launch it again only if its task has ended "
                                                          "without writing its three files)" % p["at"] if p else ""),
                   rows, [ask(flow, folder, act, "writer-arc")])
    flow["pending"] = [p for p in flow["pending"] if p["role"] != "writer-arc"]
    row("arc", "done", ", ".join(ARC_FILES))

    # arc approved
    a = flow["approvals"].get("arc")
    if not a or a.get("hash") != arc_hash(folder):
        row("arc approved", "stale" if a else "open", "changed since the owner approved it" if a else "not approved")
        pkg = owner_package(folder, flow, "arc") if act else "the arc"
        raise Step("owner", "the owner rules on the arc: give them %s and set out arc-choices.md's choices in full; save "
                   "their reply word for word to a file and run `story-flow.py owner <folder> arc <file> --verdict "
                   "approve|reject`, with the verdict they gave (ask them for it if they did not give one)" % pkg, rows)
    row("arc approved", "done", "approved %s (%s)" % (a["at"], a.get("owner", "")))

    # write: the writer's second pass
    story = draft_story(folder)
    current = script_hash(story) if story is not None else None
    drafts = flow["drafts"]
    last = drafts[-1] if drafts else None
    writer = pending_role(flow, "writer-story")
    have = [n for n in WRITER_FILES if os.path.isfile(f(folder, n))]
    # The writer has handed back when writer-report.md was written after it was launched; a draft it
    # hands back is reviewed as a new one even when its words did not change, so a writer that
    # changes nothing still uses up a try.
    fresh = writer is None or report_stamp(folder) != writer.get("report_mtime")
    arc_moved = last is not None and last.get("arc") != a.get("hash") and writer is None
    if len(have) < len(WRITER_FILES) or not fresh or arc_moved:
        if len(have) < len(WRITER_FILES):
            why = "missing: " + ", ".join(n for n in WRITER_FILES if n not in have)
        elif arc_moved:
            why = "the arc was approved again after the last draft"
        else:
            why = "the writer, launched %s, has not written a new story.json and writer-report.md" % writer["at"]
        row("write", "open", why)
        text = "the writer's second pass" + (" (launch it again only if its task has ended without its files)"
                                              if writer else "")
        raise Step("agents", text, rows, [ask(flow, folder, act, "writer-story",
                                              extra={"report_mtime": report_stamp(folder)})])
    handed_back = writer is not None
    flow["pending"] = [p for p in flow["pending"] if p["role"] != "writer-story"]
    row("write", "done", "the writer's six files")

    # draft: the tool's check, then the listener and the fact checker, on every draft the writer writes
    if last is None or last.get("hash") != current or (handed_back and act):
        if os.path.isfile(f(folder, "story.draft.json")):
            row("draft", "stale", "story.draft.json changed after the script stage kept it")
            raise Step("owner", "the writer's draft changed under the script stage; show the owner `status` and ask", rows)
        if not act:
            row("draft", "open", "draft %d not reviewed yet" % (len(drafts) + 1))
            raise Step("run", "`story-flow.py next` reviews the draft", rows)
        k = len(drafts) + 1
        write_text(f(folder, "drafts", str(k), "page.md"), script_tool().render(story))
        errors, _ = check_story(folder, f(folder, "drafts", str(k), "check.txt"))
        last = {"k": k, "hash": current, "arc": a.get("hash"), "at": now(), "check": {"errors": errors}}
        drafts.append(last)
    k = last["k"]
    failed = 0
    for d in drafts:
        if d.get("owner", {}).get("verdict") == "return":
            failed = 0
        elif draft_failed(d):
            failed += 1
    passed = last.get("owner", {}).get("verdict") == "pass" or (
        last["check"]["errors"] == 0 and last.get("listener", {}).get("verdict") == "PASS" and
        last.get("facts-draft", {}).get("verdict") == "PASS")
    if not passed:
        if draft_failed(last):
            row("draft", "open", "draft %d failed: %s" % (k, draft_reason(last)))
            if failed >= DRAFT_LIMIT and last.get("owner", {}).get("verdict") != "return":
                pkg = owner_package(folder, flow, "draft") if act else "the draft"
                raise Step("owner", "%d drafts in a row failed; the owner rules on draft %d: give them %s; save their "
                           "reply word for word and run `story-flow.py owner <folder> draft <file> --verdict pass|return`"
                           % (failed, k, pkg), rows)
            raise Step("agents", "draft %d failed; the writer's second pass again, which reads drafts/%d/" % (k, k), rows,
                       [ask(flow, folder, act, "writer-story", extra={"report_mtime": report_stamp(folder)})])
        need = [r for r in ("listener", "facts-draft") if r not in last]
        row("draft", "open", "draft %d: the check is clean; waiting on the %s" % (k, " and the ".join(need)))
        raise Step("agents", "draft %d: the listener and the fact checker, together" % k, rows,
                   [ask(flow, folder, act, r, k) for r in need])
    row("draft", "done", "draft %d passed%s" % (k, " on the owner's word" if last.get("owner") else ""))

    script_stage(folder, flow, act, row, rows)

    # script approved
    story_now = load_json(f(folder, "story.json"))
    s = flow["approvals"].get("script")
    if not s or s.get("hash") != script_hash(story_now):
        row("script approved", "stale" if s else "open", "changed since the owner approved it" if s else "not approved")
        pkg = owner_package(folder, flow, "script") if act else "the script"
        raise Step("owner", "the owner reads the script: give them %s; save their reply word for word and run "
                   "`story-flow.py owner <folder> script <file> --verdict approve|changes`" % pkg, rows)
    row("script approved", "done", "approved %s (%s)" % (s["at"], s.get("owner", "")))

    later_stages(folder, flow, story_now, row, rows)


def script_stage(folder, flow, act, row, rows):
    """Rounds of the editor, apply and check, the fact checker and the cold reader, and the arc comparer
    (the story-script skill). A round that ends clean closes the stage; so does the third round, or an
    owner's round, whatever it ends with, and the owner reads what remains."""
    rounds = flow["script"]["rounds"]
    if not rounds or (rounds[-1].get("done") and not rounds[-1].get("final")):
        n = len(rounds) + 1
        if not act:
            row("script", "open", "round %d to begin" % n)
            raise Step("run", "`story-flow.py next` begins round %d" % n, rows)
        if not rounds:
            tool("render", folder, "--force")
            write_text(f(folder, "script", "0", "check.txt"), tool("check", folder, "--stats"))
        rounds.append({"n": n, "attempt": 1, "owner": None, "at": now()})
    r = rounds[-1]
    n = r["n"]
    rd = f(folder, "script", str(n))
    if r.get("final") and r.get("for_writer"):
        # The owner asked for changes only the writer may make. The script stage so far is set aside,
        # the writer gets the owner's words and the editor's list as changes.md, and the draft review
        # and the script stage run again on what the writer hands back.
        if not act:
            row("script", "open", "round %d closed with changes for the writer" % n)
            raise Step("run", "`story-flow.py next` sends the changes to the writer", rows)
        j = len(glob.glob(f(folder, "superseded", "script-*"))) + 1
        gone = f(folder, "superseded", "script-%d" % j)
        os.makedirs(gone, exist_ok=True)
        text = "# Changes the owner asked for that only the writer may make\n\n" + read_text(os.path.join(rd, "for-writer.md"))
        if os.path.isfile(os.path.join(rd, "owner.md")):
            text += "\n# The owner's words, as they wrote them\n\n" + read_text(os.path.join(rd, "owner.md"))
        for name in ("story.draft.json", "edits.tsv", "script.md", "script"):
            if os.path.exists(f(folder, name)):
                shutil.move(f(folder, name), os.path.join(gone, name))
        write_text(f(folder, "changes.md"), text)
        flow["script"].setdefault("superseded", []).append(flow["script"]["rounds"])
        flow["script"]["rounds"] = []
        story_now = load_json(f(folder, "story.json"))
        row("script", "open", "the script stage is set aside in superseded/script-%d; the writer makes the changes" % j)
        raise Step("agents", "the writer's second pass, to make the changes in changes.md", rows,
                   [launch(flow, folder, "writer-story", extra={"report_mtime": report_stamp(folder)})])
    if r.get("final"):
        story_now = load_json(f(folder, "story.json"))
        if r.get("hash") != script_hash(story_now):
            row("script", "stale", "story.json changed after round %d closed the stage" % n)
            raise Step("owner", "story.json changed outside the flow after the script stage closed; show the owner "
                       "`story-flow.py status` and ask how to go on", rows)
        row("script", "done", "round %d closed the stage (%s)" % (n, r["final"]))
        return
    if "check" not in r:
        editor = os.path.join(rd, "editor.md")
        if not os.path.isfile(editor):
            row("script", "open", "round %d: the editor, try %d%s" % (n, r["attempt"], " (the owner's round)"
                                                                      if r.get("owner") else ""))
            raise Step("agents", "round %d: the editor" % n, rows, [ask(flow, folder, act, "editor", n)])
        if not act:
            row("script", "open", "round %d: the editor has written its table" % n)
            raise Step("run", "`story-flow.py next` applies and checks round %d" % n, rows)
        flow["pending"] = [p for p in flow["pending"] if p["role"] != "editor"]
        tool("apply", folder, "--round", str(n), "--author", "editor")
        errors, bad = check_story(folder, os.path.join(rd, "check.txt"))
        if errors and r["attempt"] < ATTEMPT_LIMIT:
            t = r["attempt"]
            os.replace(os.path.join(rd, "check.txt"), os.path.join(rd, "check-%d.txt" % t))
            os.replace(editor, os.path.join(rd, "editor-%d.md" % t))
            r["attempt"] = t + 1
            row("script", "open", "round %d: %d check error(s) after try %d" % (n, errors, t))
            raise Step("agents", "round %d: the editor again, to fix the ERRORs in script/%d/check-%d.txt" % (n, n, t),
                       rows, [launch(flow, folder, "editor", n)])
        if errors:
            tool("revert", folder, "--scenes", ",".join(str(x) for x in bad), "--round", str(n))
            errors, bad = check_story(folder, os.path.join(rd, "check.txt"))
            r["reverted_for_check"] = bad
        r["check"] = {"errors": errors}
        if errors:
            row("script", "open", "round %d: %d check error(s) remain with the writer's words restored" % (n, errors))
            raise Step("owner", "the check still refuses the script with the writer's words restored (script/%d/check.txt)"
                       "; show the owner the check and ask how to go on" % n, rows)
        tool("edits", folder, "--round", str(n), "--out", os.path.join(rd, "edits.tsv"))
        tool("cold", folder, "--out", os.path.join(rd, "cold-page.md"))
    need = [x for x in ("facts-script", "cold") if x not in r]
    if need:
        row("script", "open", "round %d: waiting on the %s" % (n, " and the ".join(need)))
        raise Step("agents", "round %d: the fact checker and the cold reader, together" % n, rows,
                   [ask(flow, folder, act, x, n) for x in need])
    if "arc-compare" not in r:
        if act and not os.path.isfile(os.path.join(rd, "arc-pair.md")):
            write_text(os.path.join(rd, "arc-pair.md"), arc_pair(folder, r["cold"]["summary"]))
        row("script", "open", "round %d: waiting on the arc comparer" % n)
        raise Step("agents", "round %d: the arc comparer" % n, rows, [ask(flow, folder, act, "arc-compare", n)])
    counted = len([x for x in rounds if not x.get("owner")])
    last_round = counted >= ROUND_LIMIT or bool(r.get("owner"))
    facts_ok = r["facts-script"]["false"] == 0
    if not facts_ok and last_round:
        if act and "reverted_for_facts" not in r:
            tool("revert", folder, "--scenes", ",".join(str(x) for x in r["facts-script"]["scenes"]), "--round", str(n))
            errors, _ = check_story(folder, os.path.join(rd, "check-after-revert.txt"))
            r["reverted_for_facts"] = r["facts-script"]["scenes"]
            if errors:
                raise Step("owner", "restoring the writer's words where the fact checker found a false line left %d "
                           "check error(s) (script/%d/check-after-revert.txt); show the owner and ask" % (errors, n), rows)
        facts_ok = True
    clean = facts_ok and r["cold"]["stumbles"] == 0 and r["arc-compare"]["told"]
    if clean or last_round:
        if act:
            r["final"] = "clean" if clean else ("the owner's round" if r.get("owner") else "the round limit")
            r["done"] = True
            r["hash"] = script_hash(load_json(f(folder, "story.json")))
            fw = os.path.join(rd, "for-writer.md")
            r["for_writer"] = os.path.isfile(fw) and bool(read_text(fw).strip())
            if r["for_writer"]:
                row("script", "open", "round %d closed with changes for the writer" % n)
                raise Step("run", "`story-flow.py next` sends the changes to the writer", rows)
        row("script", "done" if act else "open", "round %d closes the stage (%s)" % (n, round_reason(r)))
        return
    if act:
        r["done"] = True
    row("script", "open", "round %d done (%s); round %d next" % (n, round_reason(r), n + 1))
    raise Step("run", "`story-flow.py next` begins round %d" % (n + 1), rows)


def round_reason(r):
    parts = []
    if r["facts-script"]["false"]:
        parts.append("%d false%s" % (r["facts-script"]["false"], ", reverted" if r.get("reverted_for_facts") else ""))
    if r["cold"]["stumbles"]:
        parts.append("%d stumble(s)" % r["cold"]["stumbles"])
    if not r["arc-compare"]["told"]:
        parts.append("the turn missed: " + r["arc-compare"]["missing"])
    return ", ".join(parts) or "clean"


def arc_pair(folder, summary):
    story = load_json(f(folder, "story.json"))
    parts = story.get("arc_parts") or {}
    return "\n".join([
        "# The arc, as the film's writer set it down", "",
        norm(story.get("arc", "")), "",
        "- Followed: " + norm(parts.get("followed", "(not given)")),
        "- Turn: " + norm(parts.get("turn", "(not given)")),
        "- Kind: " + norm(parts.get("kind", "(not given)")), "",
        "# The listener's summary", "", summary.strip(), ""])


def later_stages(folder, flow, story, row, rows):
    marks = flow["marks"]
    narration = f(folder, "narration", "narration.json")
    if marks.get("narrate", {}).get("skipped"):
        row("narrate", "skipped", marks["narrate"].get("note", ""))
    elif os.path.isfile(narration):
        current = load_json(narration).get("story_captions") == captions_hash(story)
        timed = isinstance(story, dict) and (story.get("narration") or {}).get("manifest") == hashlib.sha256(
            open(narration, "rb").read()).hexdigest()[:16]
        if not current:
            row("narrate", "stale", "narration.json is older than the captions")
            raise Step("run", "story-narration.py segments, narrate the changed paragraphs, then timing", rows)
        if not timed:
            row("narrate", "open", "narrated, not timed")
            raise Step("run", "`python scripts/story-narration.py timing <folder>`", rows)
        row("narrate", "done", "narrated and timed")
    else:
        row("narrate", "open", "no narration.json")
        raise Step("run", "narrate (logbook/specs/story-narration.md); until story-narration.py can call the service, "
                   "`story-flow.py skip <folder> narrate`", rows)

    runs_in_story = sorted({s.get("run") for s in scenes(story)})
    checks = marks.get("check", {})
    stale = [r for r in runs_in_story if r in checks and checks[r].get("scenes") != arm_scenes_hash(story, r)]
    missing = [r for r in runs_in_story if r not in checks]
    if stale or missing:
        row("check", "stale" if stale and not missing else "open", "%d of %d runs checked%s" % (
            len(runs_in_story) - len(missing) - len(stale), len(runs_in_story),
            ("; changed since: " + ", ".join(stale)) if stale else ""))
        raise Step("run", "`theatre-safari.ps1 <arm> -Story story.json -Check` for %s, then `story-flow.py mark <folder> "
                   "check <arm> --log <its editor log>`" % ", ".join(missing + stale), rows)
    row("check", "done", "%d runs checked" % len(runs_in_story))

    clips = flow.get("clip_record", {})
    if not os.path.isfile(f(folder, "story.filmed.json")):
        row("film", "open", "no story.filmed.json")
        raise Step("run", "copy story.json to story.filmed.json, film by the window route (logbook/specs/story-film.md "
                   "section 5), then `story-flow.py set <folder> clips <clip folders>`", rows)
    refilm = film_changes(story, load_json(f(folder, "story.filmed.json")))
    have = [s["n"] for s in scenes(story) if str(s["n"]) in clips and
            clips[str(s["n"])].get("fields") == filmed_fields_hash(s)]
    lacking = [s["n"] for s in scenes(story) if s["n"] not in have]
    if refilm or lacking:
        row("film", "stale" if refilm else "open", "%d of %d scenes have a current clip%s" % (
            len(have), len(scenes(story)), ("; changed since filming: " + ", ".join(map(str, refilm))) if refilm else ""))
        raise Step("run", "film scenes %s (the window route), then `story-flow.py set <folder> clips <clip folders>`" %
                   ", ".join(map(str, sorted(set(refilm) | set(lacking)))), rows)
    row("film", "done", "every scene has a clip")

    plan = os.path.join(flow.get("plan") or f(folder, "resolve"), "plan.json")
    build = os.path.join(os.path.dirname(plan), "build.json")
    film = flow.get("film")
    if os.path.isfile(plan) and os.path.isfile(build) and not load_json(build).get("errors") and \
            os.path.getmtime(build) >= os.path.getmtime(plan) >= os.path.getmtime(f(folder, "story.json")):
        row("assemble", "done", "the Resolve timeline %s" % load_json(build).get("timeline", "?"))
    elif film and os.path.isfile(film) and os.path.getmtime(film) >= os.path.getmtime(f(folder, "story.json")):
        row("assemble", "done", "the ffmpeg film " + os.path.basename(film))
    else:
        row("assemble", "open", "no build without errors newer than story.json")
        raise Step("run", "story-resolve.py <story.json> <plan folder> <clip folders> (the story-resolve skill); without "
                   "Resolve, story-assemble.py and `story-flow.py set <folder> film <mp4>`", rows)

    t = flow["approvals"].get("timeline")
    if not t or t.get("hash") != timeline_hash(flow, folder):
        row("timeline approved", "stale" if t else "open", "changed since the owner approved it" if t else "not approved")
        raise Step("owner", "the owner watches the timeline in Resolve, or the film; save their reply word for word and "
                   "run `story-flow.py owner <folder> timeline <file> --verdict approve|changes`", rows)
    row("timeline approved", "done", "approved %s" % t["at"])

    if not marks.get("glossary"):
        row("glossary", "open", "the writer's new rows are not in the glossary yet")
        raise Step("run", "`story-flow.py glossary <folder>`", rows)
    row("glossary", "done", "appended %s" % marks["glossary"]["at"])

    d = marks.get("deliver")
    if not d or d.get("timeline") != t.get("hash"):
        row("deliver", "stale" if d else "open", "delivered before the last approval" if d else "not delivered")
        raise Step("run", "render the approved timeline, copy the film and story.md to scratch/owner/, give the owner "
                   "the path; then `story-flow.py mark <folder> deliver <film>`", rows)
    row("deliver", "done", "delivered %s" % d["at"])
    raise Step("done", "the film is delivered", rows)


def arm_scenes_hash(story, arm):
    return hashlib.sha256(json.dumps([filmed_fields_hash(s) for s in scenes(story) if s.get("run") == arm]).encode(
        "utf-8")).hexdigest()[:16]


def timeline_hash(flow, folder):
    plan = os.path.join(flow.get("plan") or f(folder, "resolve"), "plan.json")
    if os.path.isfile(plan):
        return sha_file(plan)
    film = flow.get("film")
    return sha_file(film) if film and os.path.isfile(film) else ""


def owner_package(folder, flow, stage):
    """One file for the owner's review, in scratch/owner/, so the session hands over a path and composes
    nothing."""
    parts = []
    if stage == "arc":
        parts = [read_text(f(folder, "arc.md")), "\n---\n\n", read_text(f(folder, "arc-choices.md"))]
    elif stage == "draft":
        k = flow["drafts"][-1]["k"]
        dd = f(folder, "drafts", str(k))
        parts = ["# Draft %d, which failed its review\n\n" % k, read_text(os.path.join(dd, "page.md"))]
        for name, title in (("check.txt", "The tool's check"), ("listen.md", "The listener"),
                            ("facts.md", "The fact checker")):
            if os.path.isfile(os.path.join(dd, name)):
                parts += ["\n---\n\n## %s\n\n" % title, read_text(os.path.join(dd, name))]
    elif stage == "script":
        r = flow["script"]["rounds"][-1]
        rd = f(folder, "script", str(r["n"]))
        parts = [read_text(f(folder, "script.md")), "\n---\n\n## The writer's counts against the script's\n\n```\n",
                 tool("check", folder, "--stats"), "```\n"]
        for name, title in (("facts.md", "The fact checker, round %d" % r["n"]),
                            ("cold.md", "The cold reader, round %d" % r["n"]),
                            ("arc.md", "The arc comparer, round %d" % r["n"]),
                            ("for-writer.md", "What the editor could not change, for the writer")):
            if os.path.isfile(os.path.join(rd, name)):
                parts += ["\n---\n\n## %s\n\n" % title, read_text(os.path.join(rd, name))]
    out = os.path.join(REPO, "scratch", "owner", "story-%s-%s.md" % (flow.get("round", "x"), stage))
    write_text(out, "".join(parts))
    return out


# ------------------------------------------------------------------------------------------------
# Commands.

def cmd_status(folder, act=False):
    flow = read_flow(folder)
    try:
        advance(folder, flow, act)
        step = Step("done", "nothing", [])
    except Step as s:
        step = s
    if act:
        write_flow(folder, flow)
    print("story flow: round %s, runs %s, folder %s" % (flow.get("round"), ", ".join(flow.get("runs", [])), folder))
    print("%-18s %-8s %s" % ("stage", "state", "evidence"))
    for stage, state, why in step.rows:
        print("%-18s %-8s %s" % (stage, state, why))
    print("")
    print("next (%s): %s" % (step.kind, step.text))
    launches = [p for p in step.agents if "prompt" in p]
    for p in launches:
        print("")
        print("launch the %s: a `%s` subagent on %s, in the background, with this prompt word for word:" % (
            p["role"], p["agent"], p["model"]))
        print("-----")
        print(p["prompt"])
        print("-----")
    if launches:
        print("")
        print("when a reader's task ends (listener, fact checker, cold reader, arc comparer): `story-flow.py save "
              "<folder> <its agent id>`; after the writer or the editor, which write their own files, and after every "
              "save: `story-flow.py next`")
    elif step.kind == "agents":
        print("(`story-flow.py next` prints the prompts)")
    if step.kind == "owner":
        print("this waits on the owner: lead with \"I am blocked on ... from you\", set the decision out in full, and "
              "keep working on what is not gated")


def cmd_save(folder, agent_id):
    flow = read_flow(folder)
    prompt, answer = answer_of(agent_id)
    match = [p for p in flow["pending"] if norm(p["prompt"]) in norm(prompt)]
    if not match:
        sys.exit("agent %s was not launched with a prompt the flow is waiting on; its prompt began: %s" % (
            agent_id, norm(prompt)[:200]))
    p = match[0]
    role, k = p["role"], p.get("k")
    if role not in ROLES:
        sys.exit("the %s writes its own files; run `story-flow.py next` after it" % role)
    name, parse = ROLES[role]
    base = f(folder, "drafts", str(k)) if role in ("listener", "facts-draft") else f(folder, "script", str(k))
    verdict = parse(answer)
    flow["pending"] = [q for q in flow["pending"] if q is not p]
    if verdict is None:
        bad = name.replace(".md", ".invalid-%s.md" % agent_id)
        write_text(os.path.join(base, bad), answer)
        write_flow(folder, flow)
        sys.exit("the %s's answer has no verdict in the form its brief asks for; it is saved as %s, and `next` "
                 "launches the %s again" % (role, bad, role))
    write_text(os.path.join(base, name), answer)
    verdict.update({"file": os.path.relpath(os.path.join(base, name), folder), "agent": agent_id, "at": now()})
    if role in ("listener", "facts-draft"):
        next(d for d in flow["drafts"] if d["k"] == k)[role] = verdict
    else:
        next(r for r in flow["script"]["rounds"] if r["n"] == k)[role] = verdict
    write_flow(folder, flow)
    shown = {x: verdict[x] for x in verdict if x not in ("file", "agent", "at", "summary")}
    print("saved the %s's answer as %s: %s" % (role, verdict["file"], json.dumps(shown)))


def cmd_owner(folder, stage, words, verdict):
    flow = read_flow(folder)
    if verdict not in VERDICTS[stage]:
        sys.exit("the %s review takes --verdict %s" % (stage, " or ".join(VERDICTS[stage])))
    if not os.path.isfile(words) or not read_text(words).strip():
        sys.exit("%s holds none of the owner's words" % words)
    k = len(owner_files(folder, stage)) + 1
    dest = f(folder, "owner", "%s-%d-%s.md" % (stage, k, verdict))
    write_text(dest, read_text(words))
    rel = os.path.relpath(dest, folder)
    flow["owner"].append({"stage": stage, "k": k, "verdict": verdict, "file": rel, "at": now()})
    # The owner's words are also copied beside what they rule on, as owner.md, so that the subagent that
    # acts on them finds them in its own folder and nobody has to say which file is meant.
    if stage == "arc":
        if verdict == "approve":
            flow["approvals"]["arc"] = {"at": now(), "hash": arc_hash(folder), "owner": rel}
        else:
            gone = f(folder, "superseded", "arc-%d" % k)
            os.makedirs(gone, exist_ok=True)
            for n in ARC_FILES:
                if os.path.isfile(f(folder, n)):
                    shutil.move(f(folder, n), os.path.join(gone, n))
            shutil.copyfile(dest, os.path.join(gone, "owner.md"))
            flow["approvals"].pop("arc", None)
    elif stage == "draft":
        if not flow["drafts"]:
            sys.exit("no draft to rule on")
        flow["drafts"][-1]["owner"] = {"verdict": verdict, "file": rel}
        shutil.copyfile(dest, f(folder, "drafts", str(flow["drafts"][-1]["k"]), "owner.md"))
    elif verdict == "approve":
        flow["approvals"][stage] = {"at": now(), "owner": rel,
                                    "hash": script_hash(load_json(f(folder, "story.json"))) if stage == "script"
                                    else timeline_hash(flow, folder)}
    else:
        rounds = flow["script"]["rounds"]
        n = len(rounds) + 1
        rounds.append({"n": n, "attempt": 1, "owner": rel, "at": now()})
        write_text(f(folder, "script", str(n), "owner.md"), read_text(dest))
        flow["approvals"].pop("script", None)
    write_flow(folder, flow)
    print("the owner's words are %s (%s, %s)" % (rel, stage, verdict))
    cmd_status(folder)


def cmd_glossary(folder, flow):
    rows_path = f(folder, "glossary-rows.md")
    text = read_text(rows_path).strip() if os.path.isfile(rows_path) else ""
    glossary = os.path.join(REPO, "logbook", "specs", "story-glossary.md")
    if text:
        with io.open(glossary, "a", encoding="utf-8", newline="\n") as g:
            g.write("\n## Rows from the story of round %s, as its writer handed them back\n\n%s\n" % (flow.get("round"), text))
    flow["marks"]["glossary"] = {"at": now(), "rows": sha_file(rows_path)}


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
    for name in ("status", "next", "glossary"):
        p = sub.add_parser(name)
        p.add_argument("folder")
    p = sub.add_parser("save")
    p.add_argument("folder")
    p.add_argument("agent")
    p = sub.add_parser("owner")
    p.add_argument("folder")
    p.add_argument("stage", choices=tuple(VERDICTS))
    p.add_argument("words")
    p.add_argument("--verdict", required=True)
    p = sub.add_parser("mark")
    p.add_argument("folder")
    p.add_argument("what", choices=("check", "deliver"))
    p.add_argument("value")
    p.add_argument("--log")
    for name in ("skip", "unskip"):
        p = sub.add_parser(name)
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
        write_flow(folder, {"round": a.round, "runs": a.runs, "made": now(),
                            "runs_root": os.path.abspath(a.runs_root) if a.runs_root else os.path.join(REPO, "runs"),
                            "clips": [], "plan": os.path.join(folder, "resolve"), "approvals": {}, "marks": {}})
        cmd_status(folder)
        return
    if a.command in ("status", "next"):
        cmd_status(folder, act=a.command == "next")
        return
    if a.command == "save":
        cmd_save(folder, a.agent)
        return
    if a.command == "owner":
        cmd_owner(folder, a.stage, a.words, a.verdict)
        return
    flow = read_flow(folder)
    if a.command == "glossary":
        cmd_glossary(folder, flow)
    elif a.command == "mark":
        if a.what == "check":
            if not a.log or not os.path.isfile(a.log):
                sys.exit("mark check <arm> --log <the check's editor log>")
            log = read_text(a.log)
            if "[Theatre] safari check: PASS" not in log:
                sys.exit("the log has no `[Theatre] safari check: PASS` line")
            unread = [l for l in log.splitlines() if "[Theatre] safari story:" in l]
            if unread:
                sys.exit("the director could not read %d field(s) of the story:\n%s" % (len(unread), "\n".join(unread[:10])))
            story = load_json(f(folder, "story.json"))
            flow["marks"].setdefault("check", {})[a.value] = {"at": now(), "scenes": arm_scenes_hash(story, a.value),
                                                            "log": os.path.abspath(a.log)}
        else:
            if not os.path.isfile(a.value):
                sys.exit("no film at %s" % a.value)
            flow["marks"]["deliver"] = {"at": now(), "film": os.path.abspath(a.value), "sha": sha_file(a.value),
                                        "timeline": flow["approvals"].get("timeline", {}).get("hash")}
    elif a.command == "skip":
        flow["marks"][a.what] = {"skipped": True, "at": now(), "note": a.note}
    elif a.command == "unskip":
        flow["marks"].pop(a.what, None)
    elif a.command == "set":
        if a.what == "clips":
            flow["clips"] = [os.path.abspath(v) for v in a.values]
            found = clips_by_scene(flow["clips"])
            twice = {n: c for n, c in found.items() if len(c) > 1}
            if twice:
                sys.exit("more than one clip for scene(s) %s: keep one per scene" % ", ".join(map(str, sorted(twice))))
            filmed = {s["n"]: s for s in scenes(load_json(f(folder, "story.filmed.json")))} \
                if os.path.isfile(f(folder, "story.filmed.json")) else {}
            flow["clip_record"] = {str(n): {"clip": c[0], "fields": filmed_fields_hash(filmed.get(n, {}))}
                                   for n, c in found.items()}
        else:
            flow[a.what] = os.path.abspath(a.values[0])
    write_flow(folder, flow)
    cmd_status(folder)


if __name__ == "__main__":
    main()
