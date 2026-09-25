#!/usr/bin/env python3
"""Plan the farm film windows a story's scenes are filmed from (record-and-film-spec.md, B3).

    python scripts/story-windows.py <story.json> --out <folder>
        [--runs-root <runs>] [--runs r48-s1,r48-s2] [--scenes 1,3] [--fps 30] [--tail 1]
        [--run-dir <arm>=<run directory> ...] [--guide <arm>=<guide.json> ...]

The safari's story mode films each scene from a window the farm recorded
(`Evosim.Farm --film-window <run> <from> <to> <out> --fps N`), and never steps the world in the
Editor for it. This is the first of the two steps: it reads the story, works out for every scene
the span of simulated seconds its takes will show, and writes those spans as `windows.json` in
`<folder>` ("story-windows 1"). `scripts/story-windows.ps1 <folder>` is the second step: it runs
the farm for each window in turn, and skips one already recorded over the same span.
`scripts/theatre-safari.ps1 <arm> -Story <story.json> -FromWindows <folder>` then films them.

The story is read as the director reads it (SafariStory.cs): the same keys for the scene's number,
run, station, second (or `from` and `to`), length on screen, chapter and `flexible`, and the same
reading of a station from its name, a NEW kind or the words of its description. A scene with no
second takes its neighbour's, as the director's does.

How a scene becomes a window, the rule the director follows when it films one:

- S is the scene's second; for a birth, the birth's second less the 8 s lead.
- A chapter card (8 s) on a flexible scene plays from S and the scene's takes from S + 8; on a
  scene its writer held at its second (`flexible: false`, and every birth) the card plays from
  S - 8 and the takes from S. A title card (station Card) carries no chapter card and plays from S.
- The window runs from the part's start (`start`) to the end of its last take and a tail (`--tail`,
  1 s), so the director's last frame is inside it. A time scene is two windows: `main` at its
  first second and `time-b` at its second, each one take long.
- A window that would run past the run's last row is moved back to end on it when the scene is
  flexible, and the table says so; a held scene is left where it is, and the farm will call its
  window UNVERIFIED past the row.
- A descent with no length is given the dolly's most, 120 s.

A birth is found in the run's `lineage.jsonl` before anything is filmed, so the window holds it:
the child the story names (`child body N`), else the named parent's child nearest the story's
second, else the child nearest it born to a member of the scene's line (the clade's parent clade
near its founding, else the clade itself, as the director's rehearsal takes it), within 120 s
before the second and 600 s after. The birth goes into the manifest, and the director films that
birth; a birth that cannot be found is said, and the window is planned at the story's second.

The run of each arm is the one `--run-dir` names, else the one the story's own `runs` map names
under `--runs-root`, else the newest run directory under `<runs-root>/<arm>`. A scene whose run
cannot be found, or whose arm is not in `--runs`, is listed as skipped, never guessed.

The manifest is merged: planning scenes 1 and 3 into a folder that already plans 2 keeps 2. Each
window's out directory is `story-NN-<arm>-<part>` inside the folder.
"""
import argparse
import datetime
import json
import math
import os
import re
import sys

FORMAT = "story-windows 1"
FILE_NAME = "windows.json"

CHAPTER_SECONDS = 8.0
BIRTH_LEAD = 8.0
BIRTH_TAIL = 10.0
TIME_TAKE = 8.0
DESCENT_MOST = 120.0
FOUNDING_WAIT = 600.0          # SafariOptions.MostBirthWaitSeconds
BIRTH_BEFORE = 120.0           # the rehearsal watches from the second less this
STATION_SECONDS = {
    "Arrival": 10.0, "Descent": 0.0, "Portrait": 20.0, "Floor": 10.0,
    "Birth": BIRTH_LEAD + BIRTH_TAIL, "Colony": 20.0, "Time": 2 * TIME_TAKE, "Card": CHAPTER_SECONDS,
}

# ---------------------------------------------------------------- the story, read as SafariStory reads it

SCENE_LIST_KEYS = ["scenes", "shots", "shot_list", "shotList", "shotlist", "list", "film"]
NUMBER_KEYS = ["n", "number", "scene_n", "sceneNumber", "scene_number", "no", "num", "index", "order", "scene"]
RUN_KEYS = ["run", "arm", "seed", "run_name", "runName", "arm_name"]
CHAPTER_KEYS = ["chapter", "chapter_title", "chapterTitle", "chapter_card"]
FLEXIBLE_KEYS = ["flexible", "flex", "movable", "may_move"]
CANOPY_KEYS = ["canopy", "canopy_shot"]
WHY_KEYS = ["why", "reason"]
STATION_KEYS = ["station", "kind", "type", "shot", "shot_type", "shotType"]
SECOND_KEYS = ["second", "at", "t", "time", "run_second", "sim_second", "at_s", "second_s", "when"]
FROM_KEYS = ["from", "start", "from_s", "start_s", "second_from"]
TO_KEYS = ["to", "end", "until", "to_s", "end_s", "second_to"]
SPAN_KEYS = ["span", "window", "range", "seconds_span"]
SUBJECT_KEYS = ["subject", "who", "focus", "target", "clade"]
ROOT_KEYS = ["root", "root_id", "rootId", "founder", "founder_id", "founderId"]
BODY_KEYS = ["body", "body_id", "bodyId"]
LENGTH_KEYS = ["seconds", "length", "duration", "screen_seconds", "screenSeconds", "screen_s", "screen", "len",
               "length_s", "duration_s", "on_screen"]
DESCRIPTION_KEYS = ["description", "desc", "new", "note", "notes", "what"]


def first_of(node, keys):
    if not isinstance(node, dict):
        return None
    for k in keys:
        if k in node and node[k] is not None:
            return node[k]
    return None


def text(v):
    if v is None:
        return None
    if isinstance(v, bool):
        return None
    if isinstance(v, str):
        return v.strip() or None
    if isinstance(v, (int, float)):
        return repr(v)
    return None


NUMBER_IN_TEXT = re.compile(r"-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?")


def numbers(s):
    return [float(m.group(0).replace(",", "")) for m in NUMBER_IN_TEXT.finditer(s or "")]


def number(v):
    if isinstance(v, bool) or v is None:
        return None
    if isinstance(v, (int, float)):
        return float(v)
    if isinstance(v, str):
        all_ = numbers(v)
        return all_[0] if all_ else None
    return None


def span(v):
    if isinstance(v, list) and len(v) == 2:
        a, b = number(v[0]), number(v[1])
        if a is not None and b is not None:
            return a, b
    if isinstance(v, dict):
        a, b = number(first_of(v, FROM_KEYS)), number(first_of(v, TO_KEYS))
        if a is not None and b is not None:
            return a, b
    if isinstance(v, str):
        if not re.search(r"\bto\b|\.\.|–|—|\d\s*-\s*\d", v, re.I):
            return None
        all_ = numbers(re.sub(r"(\d)\s*-\s*(\d)", r"\1 to \2", v))
        if len(all_) == 2:
            return all_[0], all_[1]
    return None


def switch(v):
    if v is None:
        return None
    if isinstance(v, bool):
        return v
    if isinstance(v, (int, float)):
        return v != 0
    if isinstance(v, str):
        w = v.strip().lower()
        if w in ("true", "yes", "on", "1"):
            return True
        if w in ("false", "no", "off", "0"):
            return False
    return None


def whole(v):
    if isinstance(v, bool) or v is None:
        return None
    if isinstance(v, (int, float)):
        x = float(v)
    elif isinstance(v, str) and re.match(r"^\s*#?\d{1,5}\s*$", v):
        x = float(v.strip().lstrip("#"))
    else:
        return None
    if x < 0 or x >= 100000:
        return None
    return int(round(x))


ROOT_WORD = re.compile(r"\b(?:root|founder)(?:\s*(?:id|body))?\s*[:#=]?\s*(\d+)", re.I)
BODY_WORD = re.compile(r"\bbody(?:\s*id)?\s*[:#=]?\s*(\d+)", re.I)
NOT_A_BODY = re.compile(r"\b(?:founder|parent|child|root)\s+$", re.I)
PARENT_BODY_WORD = re.compile(r"\bparent\s+body\s*[:#=]?\s*(\d+)", re.I)
CHILD_BODY_WORD = re.compile(r"\bchild\s+body\s*[:#=]?\s*(\d+)", re.I)
GUIDE_INDEX_WORD = re.compile(r"\bguide\s+clade\s*[:#=]?\s*(\d+)", re.I)
BINOMIAL = re.compile(r"\b([A-Z][a-z]+ [a-z]{2,}(?: [IVX]+\b)?)")
LOOSE_NUMBER = re.compile(r"(?<![A-Za-z0-9.,])(\d+)(?![0-9.,])")
WORLD_WORDS = ["world", "the world", "tank", "the tank", "the whole tank", "everyone", "everything", "none",
               "nobody", "n/a", "-"]


def body_word(t):
    """A body named as one, and not as a founder's, a parent's, a child's or a root's (SafariStory's BodyWord)."""
    for m in BODY_WORD.finditer(t):
        if not NOT_A_BODY.search(t[:m.start()]):
            return m
    return None


class Shot:
    def __init__(self, position):
        self.position = position
        self.number = position + 1
        self.run = None
        self.station_text = None
        self.description = None
        self.chapter = None
        self.flexible = None
        self.canopy = None
        self.why = None
        self.second = math.nan
        self.frm = math.nan
        self.to = math.nan
        self.seconds = math.nan
        self.subject_text = None
        self.root = self.body = self.loose = self.guide_index = self.parent_body = self.child_body = -1
        self.name = None
        self.world = False


def parse_subject_text(t, shot):
    t = t.strip()
    lower = t.lower()
    root = ROOT_WORD.search(t)
    body = body_word(t)
    parent = PARENT_BODY_WORD.search(t)
    child = CHILD_BODY_WORD.search(t)
    guide = GUIDE_INDEX_WORD.search(t)
    if root:
        shot.root = int(root.group(1))
    if body:
        shot.body = int(body.group(1))
    if parent:
        shot.parent_body = int(parent.group(1))
    if child:
        shot.child_body = int(child.group(1))
    if guide:
        shot.guide_index = int(guide.group(1))

    world_word = any(lower == w or lower.startswith(w + " ") or lower.startswith(w + ",") or
                     lower.startswith(w + "(") or lower.startswith(w + ":") for w in WORLD_WORDS)
    if world_word and not root and not body:
        shot.world = True
        return

    b = BINOMIAL.search(re.sub(r"\([^)]*\)", " ", t)) or BINOMIAL.search(t)
    if b:
        shot.name = b.group(1)
    else:
        name = re.sub(r"\([^)]*\)", " ", t)
        for r in (ROOT_WORD, BODY_WORD, PARENT_BODY_WORD, CHILD_BODY_WORD, GUIDE_INDEX_WORD):
            name = r.sub(" ", name)
        name = LOOSE_NUMBER.sub(" ", name)
        name = re.sub(r"\s+", " ", name).strip(" ,;:-./")
        if name and len(name) <= 40 and len(name.split(" ")) <= 4 and name[0].isupper() and name.lower() not in WORLD_WORDS:
            shot.name = name

    if not root and not body and not guide and not parent and shot.name is None:
        loose = LOOSE_NUMBER.search(t)
        if loose:
            shot.loose = int(loose.group(1))


def read_shot(node, position):
    shot = Shot(position)
    n = whole(first_of(node, NUMBER_KEYS))
    if n is not None:
        shot.number = n
    shot.run = text(first_of(node, RUN_KEYS))
    shot.station_text = text(first_of(node, STATION_KEYS))
    shot.description = text(first_of(node, DESCRIPTION_KEYS))
    shot.chapter = text(first_of(node, CHAPTER_KEYS))
    shot.flexible = switch(first_of(node, FLEXIBLE_KEYS))
    shot.canopy = switch(first_of(node, CANOPY_KEYS))
    shot.why = text(first_of(node, WHY_KEYS))

    second = first_of(node, SECOND_KEYS)
    sp = span(second)
    if sp:
        shot.frm, shot.to = sp
    else:
        s = number(second)
        if s is not None:
            shot.second = s
    f = number(first_of(node, FROM_KEYS))
    if f is not None:
        shot.frm = f
    t = number(first_of(node, TO_KEYS))
    if t is not None:
        shot.to = t
    sp = span(first_of(node, SPAN_KEYS))
    if sp:
        shot.frm, shot.to = sp
    if math.isnan(shot.second) and not math.isnan(shot.frm):
        shot.second = shot.frm

    length = number(first_of(node, LENGTH_KEYS))
    if length is not None and length > 0:
        shot.seconds = length

    subject = first_of(node, SUBJECT_KEYS)
    if isinstance(subject, dict):
        r = number(first_of(subject, ROOT_KEYS))
        b = number(first_of(subject, BODY_KEYS))
        i = number(first_of(subject, ["id"]))
        if r is not None:
            shot.root = int(round(r))
        if b is not None:
            shot.body = int(round(b))
        if i is not None and shot.root < 0 and shot.body < 0:
            shot.loose = int(round(i))
        shot.subject_text = text(first_of(subject, ["name", "clade", "text", "label"]))
        if shot.subject_text:
            parse_subject_text(shot.subject_text, shot)
        return shot

    if isinstance(subject, (int, float)) and not isinstance(subject, bool):
        shot.loose = int(round(subject))
        shot.subject_text = str(shot.loose)
    else:
        shot.subject_text = text(subject)
        if shot.subject_text:
            parse_subject_text(shot.subject_text, shot)

    if shot.root < 0:
        r = number(first_of(node, ROOT_KEYS))
        if r is not None:
            shot.root = int(round(r))
    b = number(first_of(node, BODY_KEYS))
    if b is not None:
        shot.body = int(round(b))
        shot.world = False
    if subject is None and shot.root < 0 and shot.body < 0:
        shot.world = True
    return shot


def read_story(path):
    with open(path, encoding="utf-8") as f:
        root = json.load(f)
    scenes = []
    top_run = None
    top_runs = {}
    if isinstance(root, list):
        scenes = list(root)
    elif isinstance(root, dict):
        top_run = text(first_of(root, RUN_KEYS))
        lst = first_of(root, SCENE_LIST_KEYS)
        if isinstance(lst, list):
            scenes.extend(lst)
        acts = root.get("acts")
        if isinstance(acts, list):
            for act in acts:
                inner = first_of(act, SCENE_LIST_KEYS) if isinstance(act, dict) else None
                if isinstance(inner, list):
                    scenes.extend(inner)
        if isinstance(root.get("runs"), dict):
            top_runs = {k: v for k, v in root["runs"].items() if isinstance(v, str)}
    shots = [read_shot(s, i) for i, s in enumerate(scenes) if isinstance(s, dict)]
    return shots, top_run, top_runs


# ---------------------------------------------------------------- the station, as SafariStory maps it

NEAREST = [
    ("Birth", ["birth", "born", "bud", "budding", "offspring", "newborn", "child", "split"]),
    ("Time", ["time", "timelapse", "time-lapse", "then and now", "before and after", "compare", "comparison"]),
    ("Floor", ["floor", "bed", "sand", "seabed", "bottom", "sediment", "detritus", "snow", "corpse", "corpses",
               "grave", "graveyard"]),
    ("Descent", ["descent", "descend", "descending", "dive", "diving", "sink", "sinking", "depth", "depths"]),
    ("Colony", ["colony", "colonies", "crowd", "swarm", "population", "census", "flock", "school", "herd",
                "cluster", "pull-back", "pullback", "pull back"]),
    ("Portrait", ["portrait", "close", "closeup", "close-up", "orbit", "follow", "macro", "detail", "anatomy",
                  "face", "swim", "swimmer", "joint", "joints", "individual"]),
    ("Arrival", ["arrival", "establishing", "overview", "tank", "glass", "surface", "aerial", "canopy", "window",
                 "wide"]),
]
CARD_WORDS = ["title", "card", "chapter", "intertitle", "credit", "credits", "caption card", "text card"]


def has_word(t, word):
    return bool(t) and re.search(r"(?<![a-z])" + re.escape(word) + r"(?![a-z])", t, re.I) is not None


def named(word):
    w = (word or "").strip().lower()
    return {
        "arrival": "Arrival", "arrive": "Arrival",
        "descent": "Descent", "descend": "Descent", "dive": "Descent",
        "portrait": "Portrait", "floor": "Floor", "bed": "Floor", "birth": "Birth", "colony": "Colony",
        "time": "Time", "timelapse": "Time", "time-lapse": "Time",
        "card": "Card", "title": "Card", "chapter": "Card", "title card": "Card", "chapter card": "Card",
    }.get(w)


def station_of(shot, has_subject):
    raw = (shot.station_text or "").strip()
    n = named(raw)
    if n:
        return n
    m = re.match(r"^[A-Za-z]+", raw)
    if m and not raw.lower().startswith("new"):
        n = named(m.group(0))
        if n:
            return n
    kind = raw
    description = shot.description
    is_new = raw.lower().startswith("new") and (len(raw) == 3 or not raw[3].isalpha())
    if is_new:
        rest = raw[3:].lstrip(" :-—–")
        m = re.match(r"^(?P<kind>[^—–:(,;]*?)\s*(?:[—–:(,;]|\s-\s|$)(?P<desc>.*)$", rest, re.S)
        kind = m.group("kind").strip() if m else rest.strip()
        desc = m.group("desc").strip().rstrip(")").strip() if m else ""
        if desc:
            description = desc if not description else desc + "; " + description
        n = named(kind)
        if n and n != "Card":
            return n
    if any(has_word(kind, w) for w in CARD_WORDS):
        return "Card"
    for station, words in NEAREST:
        if any(has_word(kind, w) for w in words):
            return station
    if any(has_word(description, w) for w in CARD_WORDS):
        return "Card"
    for station, words in NEAREST:
        if any(has_word(description, w) for w in words):
            return station
    return "Portrait" if has_subject else "Arrival"


def same_run(story_run, arm):
    if not story_run or not arm:
        return False
    a, b = story_run.strip().lower(), arm.strip().lower()
    if a == b or re.sub(r"[\s_/]+", "-", a) == b:
        return True
    m = re.match(r"^(?:seed\s*|s)?(\d+)$", a)
    return bool(m) and b.endswith("-s" + m.group(1))


# ---------------------------------------------------------------- the run

def newest_run(arm_dir):
    if not os.path.isdir(arm_dir):
        return None
    runs = [os.path.join(arm_dir, d) for d in os.listdir(arm_dir)
            if os.path.isfile(os.path.join(arm_dir, d, "run.json")) or os.path.isfile(os.path.join(arm_dir, d, "config.json"))]
    return max(runs) if runs else None


def resolve_run(arm, runs_root, story_runs, named_dirs):
    if arm in named_dirs:
        d = named_dirs[arm]
        return (os.path.abspath(d), "named on the command line") if os.path.isdir(d) else (None, "--run-dir names " + d + ", which is not a directory")
    if runs_root and arm in story_runs:
        rel = story_runs[arm].replace("\\", "/")
        rel = rel[len("runs/"):] if rel.startswith("runs/") else rel
        d = os.path.join(runs_root, rel)
        if os.path.isdir(d):
            return os.path.abspath(d), "the story's own runs map"
    if runs_root:
        d = newest_run(os.path.join(runs_root, arm))
        if d:
            return os.path.abspath(d), "the newest run under " + os.path.join(runs_root, arm)
    return None, "no run for " + arm + (" under " + runs_root if runs_root else " (no --runs-root)")


def last_row_second(run_dir):
    """The run's last stats row's second: a window past it is UNVERIFIED (FilmWindow)."""
    path = os.path.join(run_dir, "stats.jsonl")
    if os.path.isfile(path):
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            size = f.tell()
            f.seek(max(0, size - 65536))
            tail = f.read().decode("utf-8", "replace").splitlines()
        for line in reversed(tail):
            line = line.strip()
            if line.startswith("{") and line.endswith("}"):
                try:
                    return float(json.loads(line)["t"])
                except (ValueError, KeyError):
                    continue
    try:
        with open(os.path.join(run_dir, "run.json"), encoding="utf-8") as f:
            run = json.load(f)
        for k in ("simulatedSeconds", "requestedSeconds"):
            if isinstance(run.get(k), (int, float)):
                return float(run[k])
    except (OSError, ValueError):
        pass
    return math.inf


def checkpoints(run_dir):
    d = os.path.join(run_dir, "checkpoints")
    out = []
    if os.path.isdir(d):
        for f in os.listdir(d):
            if f.endswith(".ckpt"):
                try:
                    out.append(float(f[:-5]))
                except ValueError:
                    pass
    return sorted(out)


BIRTH_ROW = re.compile(r'"e"\s*:\s*"b"')


class Lineage:
    """Every birth row's parent, second and flags, and the clade walk the guide and SafariClades use."""

    def __init__(self, run_dir):
        self.born = {}          # id -> (parent, t, flags)
        self.clade = {}         # id -> founder of its clade
        self.children = {}      # parent -> [ids]
        path = os.path.join(run_dir, "lineage.jsonl")
        self.present = os.path.isfile(path)
        if not self.present:
            return
        with open(path, encoding="utf-8", errors="replace") as f:
            for line in f:
                if '"b"' not in line or BIRTH_ROW.search(line) is None:
                    continue
                try:
                    row = json.loads(line)
                except ValueError:
                    continue
                i, p, t = int(row["id"]), int(row.get("p", -1)), float(row["t"])
                flags = (1 if row.get("abs") else 0) | (2 if row.get("jnt") else 0) | (4 if row.get("pho") else 0)
                self.born[i] = (p, t, flags)
                c = i
                if p >= 0 and p in self.born and self.born[p][2] == flags:
                    c = self.clade[p]
                self.clade[i] = c
                self.children.setdefault(p, []).append(i)

    def clade_info(self, founder):
        """The clade's founding second and its parent clade's founder (-1 for none)."""
        if founder not in self.born:
            return math.nan, -1
        p, t, _ = self.born[founder]
        return t, (self.clade.get(p, -1) if p >= 0 else -1)


def read_guide(path):
    try:
        with open(path, encoding="utf-8") as f:
            g = json.load(f)
    except (OSError, ValueError):
        return None
    cards = g.get("cards") if isinstance(g, dict) else None
    return cards if isinstance(cards, list) else None


def place(shot, lineage, cards, notes):
    """The clade's founder a subject names, or -1: by its root, a body, a loose number, the guide's number or its name."""
    by_founder = {c.get("founder"): c for c in cards or [] if isinstance(c, dict)}
    by_index = {c.get("clade"): c for c in cards or [] if isinstance(c, dict)}
    root, body = shot.root, shot.body
    if root < 0 and body < 0 and shot.loose >= 0:
        if shot.loose in by_founder or lineage.clade.get(shot.loose) == shot.loose:
            root = shot.loose
        else:
            body = shot.loose
    if root >= 0:
        if root in by_founder:
            return root
        if root in lineage.clade:
            return lineage.clade[root]
        notes.append("root %d is in neither the guide nor the lineage" % root)
    if body >= 0 and body in lineage.clade:
        return lineage.clade[body]
    if shot.guide_index >= 0 and shot.guide_index in by_index:
        return by_index[shot.guide_index].get("founder", -1)
    if shot.name:
        want = re.sub(r"\s+", " ", shot.name).strip().lower()
        for c in cards or []:
            if isinstance(c, dict) and re.sub(r"\s+", " ", str(c.get("name", ""))).strip().lower() == want:
                return c.get("founder", -1)
    return -1


def find_birth(shot, second, lineage, founder, notes):
    """(parent, child, second) of the birth the scene films, or None, by the rule in the module's notes."""
    if not lineage.present:
        notes.append("no lineage.jsonl, so no birth can be found before filming")
        return None
    lo, hi = second - BIRTH_BEFORE, second + FOUNDING_WAIT

    if shot.child_body >= 0:
        if shot.child_body in lineage.born:
            p, t, _ = lineage.born[shot.child_body]
            if shot.parent_body >= 0 and p != shot.parent_body:
                notes.append("the story names parent body %d and the lineage gives child body %d parent %d: the lineage's is filmed"
                             % (shot.parent_body, shot.child_body, p))
            notes.append("the birth of child body %d, which the story names, at %.1f s" % (shot.child_body, t))
            return p, shot.child_body, t
        notes.append("child body %d is not in the lineage" % shot.child_body)

    def nearest(ids):
        best = None
        for i in ids:
            t = lineage.born[i][1]
            if lo - 1e-6 <= t <= hi + 1e-6 and (best is None or abs(t - second) < abs(lineage.born[best][1] - second)):
                best = i
        return best

    if shot.parent_body >= 0:
        c = nearest(lineage.children.get(shot.parent_body, []))
        if c is not None:
            notes.append("the child of parent body %d nearest the story's second, body %d at %.1f s"
                         % (shot.parent_body, c, lineage.born[c][1]))
            return shot.parent_body, c, lineage.born[c][1]
        notes.append("parent body %d has no child between %.1f and %.1f s: the line's is looked for" % (shot.parent_body, lo, hi))

    if founder < 0:
        notes.append("the birth's clade could not be placed, so no line to look in")
        return None
    founded_at, parent_clade = lineage.clade_info(founder)
    founding = parent_clade >= 0 and not math.isnan(founded_at) and abs(second - founded_at) <= FOUNDING_WAIT
    line = parent_clade if founding else founder
    candidates = [i for i, (p, t, _) in lineage.born.items()
                  if p >= 0 and lo - 1e-6 <= t <= hi + 1e-6 and lineage.clade.get(p) == line]
    c = nearest(candidates)
    if c is None:
        notes.append("no birth to a member of the clade founded by body %d between %.1f and %.1f s" % (line, lo, hi))
        return None
    notes.append("the line's birth nearest the story's second (%s, the clade founded by body %d): body %d to body %d at %.1f s"
                 % ("the founding" if founding else "inside the clade", line, c, lineage.born[c][0], lineage.born[c][1]))
    return lineage.born[c][0], c, lineage.born[c][1]


# ---------------------------------------------------------------- the plan

def scene_length(station, shot, canopy):
    if station == "Descent":
        if shot.seconds > 0:
            return shot.seconds, None
        return DESCENT_MOST, "a descent with no length: the dolly's most, %.0f s" % DESCENT_MOST
    if station == "Birth":
        return (max(BIRTH_LEAD + 2, shot.seconds) if shot.seconds > 0 else BIRTH_LEAD + BIRTH_TAIL), None
    if station == "Time":
        return (0.5 * shot.seconds if shot.seconds > 0 else TIME_TAKE), None
    return (shot.seconds if shot.seconds > 0 else STATION_SECONDS[station]), None


def plan_arm(arm, shots, run_dir, fps, tail, guide_path, out_dir):
    """Every window of one arm's scenes, and the scenes it skipped, in the story's order."""
    windows, skipped = [], []
    run_end = last_row_second(run_dir)
    lineage = None
    cards = read_guide(guide_path) if guide_path else read_guide(os.path.join(run_dir, "guide", "guide.json"))

    # A scene with no second is filmed at its neighbour's (SafariStory.Trip).
    seconds = [s.second for s in shots]
    for i, s in enumerate(seconds):
        if not math.isnan(s):
            continue
        later = [x for x in seconds[i + 1:] if not math.isnan(x) and x != 0]
        earlier = [x for x in seconds[:i] if not math.isnan(x) and x != 0]
        cks = checkpoints(run_dir)
        seconds[i] = later[0] if later else earlier[-1] if earlier else (cks[0] if cks else 0.0)

    for shot, second in zip(shots, seconds):
        notes = []
        asked_for_subject = not shot.world and (shot.root >= 0 or shot.body >= 0 or shot.loose >= 0 or shot.name is not None)
        station = station_of(shot, asked_for_subject)

        # A station that needs a clade, without one, is filmed as the world (SafariStory.SceneOf).
        needs_clade = station in ("Colony", "Birth") or (station == "Portrait" and shot.body < 0 and shot.loose < 0)
        founder = -1
        if needs_clade or station == "Birth":
            if lineage is None:
                lineage = Lineage(run_dir)
            founder = place(shot, lineage, cards, notes)
            if needs_clade and founder < 0 and not (station == "Portrait" and shot.body >= 0):
                notes.append("a %s whose subject could not be placed in a clade: the director films it as an Arrival" % station)
                station = "Arrival"

        flexible = station != "Birth" and shot.flexible is not False
        canopy = shot.canopy
        length, note = scene_length(station, shot, canopy)
        if note:
            notes.append(note)
        card = CHAPTER_SECONDS if shot.chapter and station != "Card" else 0.0

        birth = None
        if station == "Birth":
            found = find_birth(shot, second, lineage, founder, notes)
            if found:
                birth = {"parent": found[0], "child": found[1], "at": found[2]}
                scene_second = found[2] - BIRTH_LEAD
            else:
                scene_second = second - BIRTH_LEAD
                notes.append("no birth found: the window is planned at the story's second, and the director takes the first birth in the line inside it")
        else:
            scene_second = second

        second_at = math.nan
        if station == "Time":
            if not math.isnan(shot.frm) and not math.isnan(shot.to) and shot.to > shot.frm:
                scene_second, second_at = shot.frm, shot.to
            else:
                # SafariStory.LastAfter: the run's last checkpoint, or its end.
                cks = checkpoints(run_dir)
                last = cks[-1] if cks else run_end
                second_at = last if last > second + 1 else max(second + 1, run_end)
                notes.append("a time scene with one second: its second take at %.1f s, the run's last checkpoint" % second_at)

        parts = [("main", scene_second, card, length)]
        if station == "Time":
            parts.append(("time-b", second_at, 0.0, length))

        for part, s, part_card, part_length in parts:
            part_notes = list(notes) if part == "main" else ["the time scene's second take"]
            held = not flexible and part == "main"
            start = s - part_card if (part_card > 0 and held) else s
            takes_from = start + part_card
            end = takes_from + part_length + tail

            if start < 0:
                part_notes.append("the part would open at %.1f s, before the run began: moved to 0" % start)
                end -= start
                start = 0.0

            if end > run_end + 1e-6:
                over = end - run_end
                if flexible or station == "Card" or part == "time-b":
                    start -= over
                    end -= over
                    part_notes.append("moved back %.1f s to end on the run's last row at %.1f s, so the farm can compare it" % (over, run_end))
                else:
                    part_notes.append("runs %.1f s past the run's last row at %.1f s: the farm will call the window UNVERIFIED" % (over, run_end))

            out = "story-%02d-%s-%s" % (shot.number, re.sub(r"[^a-z0-9]+", "-", arm.lower()).strip("-"), part)
            w = {
                "scene": shot.number, "part": part, "arm": arm, "station": station,
                "run": run_dir.replace("\\", "/"),
                "from": round(max(0.0, start), 6), "to": round(end, 6), "fps": fps,
                "start": round(start, 6), "card": part_card, "seconds": part_length,
                "out": out,
                "notes": part_notes,
            }
            if birth and part == "main":
                w["birth"] = birth
            windows.append(w)
    return windows, skipped


def main(argv=None):
    ap = argparse.ArgumentParser(description="Plan the farm film windows a story's scenes are filmed from.")
    ap.add_argument("story")
    ap.add_argument("--out", required=True, help="the folder windows.json and the windows go into")
    ap.add_argument("--runs-root", default=None, help="the runs directory (the main tree's runs/)")
    ap.add_argument("--runs", default="", help="the arms to plan, comma separated (all the story's by default)")
    ap.add_argument("--scenes", default="", help="the story's scene numbers to plan, comma separated (all by default)")
    ap.add_argument("--fps", type=float, default=30.0)
    ap.add_argument("--tail", type=float, default=1.0, help="seconds kept after a part's last take (1)")
    ap.add_argument("--run-dir", action="append", default=[], help="<arm>=<run directory>, taken before any other")
    ap.add_argument("--guide", action="append", default=[], help="<arm>=<guide.json>, when the run's own is not beside it")
    a = ap.parse_args(argv)

    story_path = os.path.abspath(a.story)
    shots, top_run, story_runs = read_story(story_path)
    if not shots:
        sys.exit("the story at %s has no scenes" % story_path)
    for s in shots:
        if s.run is None:
            s.run = top_run
    want_arms = [x.strip() for x in a.runs.split(",") if x.strip()]
    want_scenes = {int(x) for x in re.split(r"[,\s]+", a.scenes) if x.strip()}
    named_dirs = dict(x.split("=", 1) for x in a.run_dir)
    guides = dict(x.split("=", 1) for x in a.guide)
    runs_root = os.path.abspath(a.runs_root) if a.runs_root else None

    arms = []
    for s in sorted(shots, key=lambda k: (k.number, k.position)):
        if s.run and s.run not in arms:
            arms.append(s.run)

    out_dir = os.path.abspath(a.out)
    os.makedirs(out_dir, exist_ok=True)
    windows, skipped = [], []

    for story_run in arms:
        arm = story_run
        mine = [s for s in sorted(shots, key=lambda k: (k.number, k.position)) if same_run(s.run, story_run)]
        chosen = [s for s in mine if not want_scenes or s.number in want_scenes]
        if not chosen:
            continue
        if want_arms and not any(same_run(story_run, w) or story_run == w for w in want_arms):
            continue
        run_dir, how = resolve_run(arm, runs_root, story_runs, named_dirs)
        if run_dir is None:
            for s in chosen:
                skipped.append({"scene": s.number, "arm": arm, "why": how})
            continue
        print("%s: %s (%s)" % (arm, run_dir, how))
        # A scene with no second looks at its neighbours in the whole arm, so plan the arm and keep the chosen.
        w, sk = plan_arm(arm, mine, run_dir, a.fps, a.tail, guides.get(arm), out_dir)
        numbers_ = {s.number for s in chosen}
        windows += [x for x in w if x["scene"] in numbers_]
        skipped += [x for x in sk if x["scene"] in numbers_]

    for n in sorted(want_scenes - {w["scene"] for w in windows} - {s["scene"] for s in skipped}):
        skipped.append({"scene": n, "arm": None, "why": "the story has no scene %d on the runs asked for" % n})

    # Merged with what the folder plans already: a scene planned again replaces its entries.
    path = os.path.join(out_dir, FILE_NAME)
    kept_w, kept_s = [], []
    if os.path.isfile(path):
        try:
            with open(path, encoding="utf-8") as f:
                old = json.load(f)
            if old.get("format") == FORMAT:
                replaced = {(w["scene"], w["arm"]) for w in windows} | {(s["scene"], s["arm"]) for s in skipped}
                kept_w = [w for w in old.get("windows", []) if (w.get("scene"), w.get("arm")) not in replaced]
                kept_s = [s for s in old.get("skipped", []) if (s.get("scene"), s.get("arm")) not in replaced]
        except (OSError, ValueError):
            pass

    all_w = sorted(kept_w + windows, key=lambda w: (w["scene"], w["part"] != "main"))
    all_s = sorted(kept_s + skipped, key=lambda s: s["scene"])
    manifest = {
        "format": FORMAT,
        "story": story_path.replace("\\", "/"),
        "written": datetime.datetime.now().isoformat(timespec="seconds"),
        "windows": all_w,
        "skipped": all_s,
    }
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print("\n%-5s %-7s %-8s %-9s %10s %10s %10s  %s" % ("scene", "part", "arm", "station", "from", "to", "start", "out"))
    for w in all_w:
        print("%-5d %-7s %-8s %-9s %10.1f %10.1f %10.1f  %s" % (w["scene"], w["part"], w["arm"], w["station"], w["from"], w["to"], w["start"], w["out"]))
        for n in w.get("notes", []):
            print("      " + n)
    for s in all_s:
        print("%-5d skipped: %s" % (s["scene"], s["why"]))
    print("\n%d window(s), %d scene(s) skipped; written to %s" % (len(all_w), len(all_s), path))
    return 0


if __name__ == "__main__":
    sys.exit(main())
