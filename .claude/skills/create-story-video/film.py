#!/usr/bin/env python3
"""The filming step's tool, for the create-story-video skill (filming.md).

    python film.py plan <folder> <k>               plan pass k: the director's story and the farm's windows
    python film.py machine <folder>                say whether the machine and the render worker are free
    python film.py start <folder> <k>              start pass k detached: record its windows, then film them
    python film.py run <folder> <k>                the detached job itself; only start runs it
    python film.py wait <folder> <k> <minutes>     wait for pass k to end, for at most the minutes given
    python film.py collect <folder> <k>            copy pass k's clips into clips/, make their sheets, write clips.json
    python film.py slides <folder> <k>             build a deck of pass k's collected takes, film/pass-<k>/slides/index.html
    python film.py stills <folder> <j> <dir>       measure the stills in <dir> (a manifest.json and its PNGs) and make a deck, film/stills-<j>/slides/index.html
    python film.py encode <folder> <k>             encode the takes of a pass whose theatre ended at its wall, film/pass-<k>/encode.log
    python film.py stop <folder> <k> <reason...>   end a running pass cleanly: the scene under way completes, then the theatre writes scenes.tsv

The first line each command prints is its verdict, as filming.md reads it. The last line says how long the
command took. Exit status: 0 on READY, NOTHING TO FILM, FREE, STARTED, DONE or PASS, 1 otherwise, 2 on a
wrong command. It uses the standard library, ffmpeg and ffprobe, and PowerShell to start the detached job.
"""

import hashlib
import importlib.util
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
SETTINGS = "film-settings.json"
SCHEMA = "create-story-video/clips/1"
BIRTH_LEAD_S = 8.0                 # the director opens a birth's take this long before it (SafariTripBuilder)
TAIL_S = 1.0                       # what story-windows.py records after a take's last frame
ALIVE_AFTER_S = 1.0                # a named body's window opens this long after its birth: two metabolic steps
EPS = 0.0015
WORLD = {"Arrival", "Descent", "Floor", "Card"}
SUBJECTS = ("plants",)     # what subject_by_shot may name for a world shot, besides the default "world"
CLOSE = {"Portrait", "Birth"}      # the cameras the theatre focuses on a subject (SafariPlans' focusOn)
# What a close shot's own focus may hold, as the theatre's story reader takes it (SafariStory.ReadFocus).
FOCUS_RANGES = {"aperture": (1, 32), "format": (8, 120), "radius": (0.5, 1.5), "start": (1, 20), "end": (1.1, 40),
                "supersample": (1, 3)}
SLUG = re.compile(r"[A-Za-z0-9._-]+")
SIZE = re.compile(r"([0-9]+)x([0-9]+)")
# What the theatre costs a run, measured on round 67's pass 2 at 3840x2160 (2026-11-07): Unity's start and first
# load, each window's load and skinning, and each frame. cmd_plan estimates a run's time from them.
THEATRE_COSTS = {"theatre_start_s": 300, "theatre_window_s": 240, "theatre_frame_s": 0.45}
FRAME_COST_AT = 3840 * 2160        # the pixel count theatre_frame_s is given for
FRAME_COST_FLOOR = 0.25            # a smaller frame costs a quarter of it at the least
JUMP_FLAG = 6.0                    # a frame-to-frame change this many times the clip's median is printed
FOG_RANGE = (0.002, 0.08)          # the story look's fog dial's range, which a far shot's own fog keeps to
# The snow's dials under the underwater look, each the theatre's own range: the motes' brightness, their
# number as a multiple of the look's, and the least share of the reference density a column counts as.
SNOW_RANGES = {"gain": (0, 4), "quantity": (0.25, 3), "least": (0, 1), "ambient": (0, 6)}
SNOW_ENV = {"gain": "EVOSIM_THEATRE_SHELL_GAIN", "quantity": "EVOSIM_THEATRE_SNOW_QUANTITY",
            "least": "EVOSIM_THEATRE_SNOW_LEAST", "ambient": "EVOSIM_THEATRE_SNOW_AMBIENT"}
# The skin's dials a film may set: the tank's glass and the earth at it drawn or not, and the caustic net's
# strength on the bed, the reef and the bodies as a multiple of the shaders' own.
CAUSTICS_RANGE = (0, 2)
SKIN_ENV = {"glass": "EVOSIM_THEATRE_GLASS", "earth": "EVOSIM_THEATRE_EARTH", "caustics": "EVOSIM_THEATRE_CAUSTIC_SCALE"}
LOOK_RATE = 3                      # frames a second read for a clip's tone and edges
# A shot's colour: the theatre turns the hue of the bodies a map names, read once when its Editor starts, so a
# pass films one colour. colours.json in the film's folder names the colours a film may use (filming.md).
COLOURS = "colours.json"
COLOUR_NAME = re.compile(r"[a-z0-9-]+")
COLOUR_FIELDS = {"shows", "file", "sha256", "runs"}
TEAMS_ENV = "EVOSIM_THEATRE_TEAMS"
TEAMS_LINE = re.compile(r"\[Theatre\] teams: (\d+) bodies turned from (.+?) \(EVOSIM_THEATRE_TEAMS\)")


class Refused(Exception):
    """A command that cannot go on; its lines say why."""

    def __init__(self, *lines):
        super().__init__(lines[0] if lines else "")
        self.lines = list(lines)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + chr(10), encoding="utf-8")


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def number(x):
    return isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)


def now():
    return datetime.now().isoformat(timespec="seconds")


def repo_path(value):
    p = Path(value)
    return p if p.is_absolute() else REPO / p


def film_settings(folder, lines):
    """The film's filming settings, first copied from the skill's if the film has none."""
    if not (folder / SETTINGS).is_file():
        shutil.copyfile(HERE / SETTINGS, folder / SETTINGS)
        lines.append(f"copied the skill's {SETTINGS} into the film's folder")
    s = read_json(folder / SETTINGS)
    problems = []
    if not number(s.get("handles_s")) or s["handles_s"] < 0:
        problems.append("handles_s is seconds, 0 or more")
    tb = s.get("takes_by_shot", {})
    if not isinstance(tb, dict) or not all(isinstance(k, str) and isinstance(v, int) and v >= 1 for k, v in tb.items()):
        problems.append("takes_by_shot is an object of shot ids and take numbers, 1 or more")
    sj = s.get("subject_by_shot", {})
    if not isinstance(sj, dict) or not all(isinstance(k, str) and isinstance(v, str) and v in SUBJECTS for k, v in sj.items()):
        problems.append("subject_by_shot is an object of shot ids and subjects, each one of " + ", ".join(SUBJECTS))
    sb = s.get("size_by_shot", {})
    if not isinstance(sb, dict) or not all(isinstance(k, str) and SIZE.fullmatch(str(v)) for k, v in sb.items()):
        problems.append("size_by_shot is an object of shot ids and sizes, <width>x<height>")
    hb = s.get("handles_by_shot", {})
    if not isinstance(hb, dict) or not all(isinstance(k, str) and number(v) and v >= 0 for k, v in hb.items()):
        problems.append("handles_by_shot is an object of shot ids and seconds, 0 or more")
    if not isinstance(s.get("fps"), int) or not 1 <= s["fps"] <= 60:
        problems.append("fps is a whole number, 1 to 60")
    m = SIZE.fullmatch(str(s.get("size", "")))
    if not m or int(m.group(1)) % 2 or int(m.group(2)) % 2 or not 64 <= int(m.group(1)) <= 4096:
        problems.append("size is <width>x<height>, both even, 64 to 4096")
    if not isinstance(s.get("worker"), int) or s["worker"] < 2:
        problems.append("worker is the number of a unity-w<n> copy, 2 or more")
    if not isinstance(s.get("farm_threads"), int) or s["farm_threads"] < 1:
        problems.append("farm_threads is a whole number, 1 or more")
    if not isinstance(s.get("wall_minutes"), int) or not 10 <= s["wall_minutes"] <= 2880:
        problems.append("wall_minutes is a whole number, 10 to 2880")
    if s.get("farm_cores") is not None and not re.fullmatch(r"\d+(-\d+)?(,\d+(-\d+)?)*", str(s["farm_cores"])):
        problems.append("farm_cores is a list of logical processors such as \"16-31\", or null for none")
    if s.get("theatre_cores") is not None and not re.fullmatch(r"\d+(-\d+)?(,\d+(-\d+)?)*", str(s["theatre_cores"])):
        problems.append("theatre_cores is a list of logical processors such as \"16-19\", or null for none")
    if s.get("overlap") is not None and not isinstance(s["overlap"], bool):
        problems.append("overlap is true or false: false records every window before the theatre films any")
    tr = s.get("theatre_root")
    if tr is not None and not (isinstance(tr, str) and tr and (repo_path(tr) / "scripts" / "theatre-safari.ps1").is_file()):
        problems.append("theatre_root is null (this checkout) or the path of an existing checkout that holds "
                        "scripts/theatre-safari.ps1, absolute or from the checkout's root")
    if s.get("guard", "standard") not in ("card terms", "film terms", "standard", None):
        problems.append("guard is \"card terms\" (D258's stops and the card's wider margin), \"film terms\" (D258's stops and "
                        "CPU 95 C once or 90 C twice in a row), \"standard\" (D258's own "
                        "stops) or null for no heat guard")
    gm = s.get("guard_minutes", 600)
    if not isinstance(gm, int) or isinstance(gm, bool) or gm < 1:
        problems.append("guard_minutes is a whole number, 1 or more: how long the heat guard watches")
    tc = s.get("theatre_commit")
    if tc is not None and not (isinstance(tc, str) and tc.strip() and not tc.startswith("-")):
        problems.append("theatre_commit is null or a commit or branch name of the theatre_root checkout")
    fb = s.get("farm_by_run", {})
    if not isinstance(fb, dict) or not all(isinstance(k, str) and isinstance(v, str) and v and repo_path(v).is_file()
                                           for k, v in fb.items()):
        problems.append("farm_by_run is an object of run names and the paths of an existing Evosim.Farm.exe, "
                        "absolute or from the checkout's root")
    rb = s.get("runs_root_by_run", {})
    if not isinstance(rb, dict) or not all(isinstance(k, str) and isinstance(v, str) and v and repo_path(v).is_dir()
                                           for k, v in rb.items()):
        problems.append("runs_root_by_run is an object of run names and the paths of an existing folder that holds "
                        "the run, absolute or from the checkout's root")
    ro = s.get("runs_only")
    if ro is not None and not (isinstance(ro, list) and ro and all(isinstance(r, str) and r for r in ro)):
        problems.append("runs_only is null (a pass films every run's shots) or a list of run names, the only runs "
                        "the next pass films")
    so = s.get("shots_only")
    if so is not None and not (isinstance(so, list) and so and all(isinstance(r, str) and r for r in so)):
        problems.append("shots_only is null (a pass films every shot it would) or a list of shot ids, the only shots "
                        "the next pass films")
    ms = s.get("max_scenes")
    if ms is not None and not (isinstance(ms, int) and not isinstance(ms, bool) and ms >= 1):
        problems.append("max_scenes is null (a pass films every shot it plans) or a whole number of 1 or more, the "
                        "most scenes one pass films")
    if not isinstance(s.get("record_only", False), bool):
        problems.append("record_only is true (a pass records the farm windows of every shot and films nothing) or "
                        "false, the default: a pass that films")
    for key, default in THEATRE_COSTS.items():
        v = s.get(key, default)
        if not number(v) or v < 0:
            problems.append(f"{key} is seconds, 0 or more (default {default}): what the theatre costs a run, which the "
                            "plan checks against wall_minutes")
    if not isinstance(s.get("theatre_look"), int) or not 0 <= s["theatre_look"] <= 4:
        problems.append("theatre_look is the theatre's underwater look, 0 to 4 (EVOSIM_THEATRE_LOOK): 4 is both "
                        "batches, the look the owner chose; 0 the old look")
    sn = s.get("snow")
    if sn is not None and not (isinstance(sn, dict) and set(sn) <= set(SNOW_RANGES) and sn
                               and all(number(sn[k]) and SNOW_RANGES[k][0] <= sn[k] <= SNOW_RANGES[k][1] for k in sn)):
        problems.append("snow is null (the look's own snow) or any of "
                        + ", ".join(f"{k} {a} to {b}" for k, (a, b) in SNOW_RANGES.items()))
    for key in ("glass", "earth"):
        if not isinstance(s.get(key, True), bool):
            problems.append(f"{key} is true (the theatre draws the tank's {key}, its default) or false")
    if s.get("caustics") is not None and not (number(s["caustics"]) and CAUSTICS_RANGE[0] <= s["caustics"] <= CAUSTICS_RANGE[1]):
        problems.append(f"caustics is null (the shaders' own) or a multiple of it, {CAUSTICS_RANGE[0]} to {CAUSTICS_RANGE[1]}")
    if not isinstance(s.get("theatre_stream", False), bool):
        problems.append("theatre_stream is true (each take's frames straight to the card's encoder, no PNG; "
                        "EVOSIM_THEATRE_STREAM=nvenc) or false, the PNG path; a film's settings without it are false")
    for key in ("runs_root", "reference"):
        if not isinstance(s.get(key), str) or not s[key]:
            problems.append(f"{key} is a path, absolute or from the checkout's root")
    if s.get("far_fog") is not None and not (number(s["far_fog"]) and FOG_RANGE[0] <= s["far_fog"] <= FOG_RANGE[1]):
        problems.append(f"far_fog is null (the look's fog) or a fog density, {FOG_RANGE[0]} to {FOG_RANGE[1]}")
    f = s.get("close_focus")
    if f is not None and not (isinstance(f, dict) and f.get("mode") in ("bokeh", "gaussian", "off")
                              and set(f) <= {"mode"} | set(FOCUS_RANGES)
                              and all(number(f[k]) and FOCUS_RANGES[k][0] <= f[k] <= FOCUS_RANGES[k][1]
                                      for k in f if k != "mode")):
        problems.append("close_focus is null (the grade's own) or {\"mode\": bokeh, gaussian or off, and any of "
                        + ", ".join(f"{k} {a} to {b}" for k, (a, b) in FOCUS_RANGES.items()) + "}")
    if problems:
        raise Refused(f"the film's {SETTINGS} is not in its form:", *("  " + p for p in problems))
    s["look_dials"] = look_dials()     # read once for the command; shot_key takes it from here
    return s


def theatre_cost(s, key):
    """One of the theatre's cost settings; a film's older copy without it takes the default."""
    return s[key] if s.get(key) is not None else THEATRE_COSTS[key]


def frame_cost_s(s, size):
    """The theatre's seconds a frame at this size: theatre_frame_s at 3840x2160 scaled by the pixel count, never
    below a quarter of it."""
    m = SIZE.fullmatch(str(size))
    share = int(m.group(1)) * int(m.group(2)) / FRAME_COST_AT if m else 1.0
    return theatre_cost(s, "theatre_frame_s") * max(share, FRAME_COST_FLOOR)


def run_estimates(s, entries, size):
    """Each run's time in the theatre, in minutes: start + windows x per window + frames x per frame. A run's windows
    are its shots in the pass and its frames are the sum of each shot's round(seconds x fps) + 1.
    Returns {run: (windows, frames, minutes)}."""
    found = {}
    for e in entries:
        w, f = found.get(e["arm"], (0, 0))
        found[e["arm"]] = (w + 1, f + round(e["seconds"] * s["fps"]) + 1)
    return {arm: (w, f, (theatre_cost(s, "theatre_start_s") + w * theatre_cost(s, "theatre_window_s")
                         + f * frame_cost_s(s, size)) / 60) for arm, (w, f) in found.items()}


def wall_lines(s, entries, size):
    """The plan's line a run, and the refusals: a run whose estimate is over wall_minutes would time out, and the
    theatre encodes a run's takes only after Unity exits, so a time-out loses every take of the run."""
    lines, over = [], []
    for arm, (w, f, minutes) in sorted(run_estimates(s, entries, size).items()):
        lines.append(f"theatre {arm}: {w} windows, {f} frames, about {minutes:.0f} min against the wall of "
                     f"{s['wall_minutes']} min")
        if minutes > s["wall_minutes"]:
            over.append(f"run {arm}: the theatre needs about {minutes:.0f} min ({w} windows, {f} frames at {size}), over the "
                        f"wall of {s['wall_minutes']} min, and a time-out loses every take of the run: raise wall_minutes "
                        "or split the run's shots across passes")
    return lines, over


def runs_root_for(s, arm):
    """The folder that holds a run: its entry in the settings' runs_root_by_run, else the settings' runs_root."""
    return repo_path((s.get("runs_root_by_run") or {}).get(arm) or s["runs_root"])


def run_dir(runs_root, arm):
    """The newest directory of a run under the runs root, as story-windows.py takes it."""
    base = runs_root / arm
    found = sorted(d for d in base.iterdir() if d.is_dir() and ((d / "run.json").is_file()
                                                                or (d / "config.json").is_file())) if base.is_dir() else []
    return found[-1] if found else None


def farm_of(rd):
    """The farm program that recorded a run, as its manifest names it, or None when it names none. A window is
    recorded on that build: a later one refuses the run's config once a tunable has been added since."""
    path = ((read_json(rd / "run.json") or {}).get("source") or {}).get("programPath")
    return str(Path(path) / "Evosim.Farm.exe") if path else None


def farm_for(s, arm, rd):
    """The farm program a pass records a run's windows on: the run's entry in the settings' farm_by_run, else the
    one its manifest names (farm_of). Every place the pass takes a run's farm takes it here."""
    own = (s.get("farm_by_run") or {}).get(arm)
    return str(repo_path(own)) if own else farm_of(rd)


SIM_SOURCE = ("src/Evosim.Core", "src/Evosim.Dynamics", "src/Evosim.Farm", "src/Evosim.Farm.Gpu")


def git(top, *args):
    return subprocess.run(["git", "-C", str(top), *args], capture_output=True, text=True, encoding="utf-8")


def source_check(arm, rd, farm):
    """One plan line saying whether the checkout that holds a run's farm program still has the simulation source
    the run was recorded at, or a Refused. A program path names a folder in a worktree that can be rebuilt after
    the run, and its windows would then be recorded on another build. The simulation source is compared with the
    run's commit as the working tree stands, so a moved HEAD and uncommitted edits both show."""
    source = (read_json(rd / "run.json") or {}).get("source") or {}
    commit = source.get("gitCommit")
    if not isinstance(commit, str) or not commit or commit == "unknown":
        return f"{arm}: the source of {farm} could not be checked, since the run's manifest names no commit"
    folder = Path(farm).parent
    top = git(folder, "rev-parse", "--show-toplevel") if folder.is_dir() else None
    if top is None or top.returncode != 0 or not top.stdout.strip():
        raise Refused(f"{arm}: the farm program {farm} is in no git checkout, so its source cannot be compared "
                      f"with the run's commit {commit}")
    top = top.stdout.strip()
    if git(top, "cat-file", "-e", f"{commit}^{{commit}}").returncode != 0:
        raise Refused(f"{arm}: the checkout {top} that holds the farm program {farm} does not hold the run's "
                      f"commit {commit}")
    diff = git(top, "diff", "--name-only", commit, "--", *SIM_SOURCE)
    if diff.returncode != 0:
        raise Refused(f"{arm}: git could not compare {top} with the run's commit {commit}: "
                      + (diff.stderr.strip().splitlines() or ["no message"])[-1])
    files = [f for f in diff.stdout.splitlines() if f.strip()]
    if files:
        raise Refused(f"{arm}: the farm program {farm} is not the source the run was recorded at. The checkout {top} "
                      f"differs from the run's commit {commit} in {len(files)} file(s) of the simulation source "
                      "(committed since, or edited and not committed), so a window recorded on it could be "
                      "another realisation:", *("  " + f for f in files[:10]),
                      *([f"  and {len(files) - 10} more"] if len(files) > 10 else []))
    return (f"{arm}: the farm program {farm} is at the run's commit {commit}, and its checkout's simulation "
            "source matches it" + (" (the run was recorded from a tree with uncommitted edits, which the commit "
                                   "does not hold)" if source.get("gitDirty") is True else ""))


def last_second(rd):
    """The run's last recorded second: its last stats row, else what its manifest says."""
    stats = rd / "stats.jsonl"
    if stats.is_file():
        with open(stats, "rb") as f:
            f.seek(0, 2)
            f.seek(max(0, f.tell() - 65536))
            rows = [r for r in f.read().decode("utf-8", "replace").splitlines() if r.strip().startswith("{")]
        for row in reversed(rows):
            try:
                return float(json.loads(row)["t"])
            except (ValueError, KeyError):
                continue
    manifest = read_json(rd / "run.json") or {}
    for key in ("simulatedSeconds", "requestedSeconds"):
        if number(manifest.get(key)):
            return float(manifest[key])
    raise Refused(f"cannot tell where the run in {rd} ends")


def births(rd, parent, a, b):
    """The seconds at which a body's children were born inside [a, b], from the run's lineage."""
    out = []
    with open(rd / "lineage.jsonl", encoding="utf-8") as f:
        for line in f:
            if '"p":' + parent + "," not in line:
                continue
            row = json.loads(line)
            if row.get("e") == "b" and str(row.get("p")) == parent and a - EPS <= row["t"] <= b + EPS:
                out.append(float(row["t"]))
    return sorted(out)


def born_at(rd, body):
    """The second a body was born, from the run's lineage, or None when the lineage has no birth row for it."""
    key = '"id":' + str(body) + ","
    with open(rd / "lineage.jsonl", encoding="utf-8") as f:
        for line in f:
            if key not in line:
                continue
            row = json.loads(line)
            if row.get("e") == "b" and str(row.get("id")) == str(body):
                return float(row["t"])
    return None


def lead_in(rd, shot, h):
    """Where a shot's take opens, with its handle h before the stretch, and why it cannot be filmed, or None.

    A shot of a named body opens no earlier than two metabolic steps after the body's birth: the director finds
    no subject in a window that opens before the body is drawn, and misses the scene. Round 57's founder, body
    11630, has its birth row at 5,177.5 s and is first drawn at 5,178.04 s, the frame after the step that placed
    it: its portrait's window opened at 5,177 s in the look test and at 5,178 s, one step after the birth, in the
    4K pass, and both missed it (2026-10-10)."""
    a = shot["run_from_s"]
    start = a - min(h, a)
    borns = [t for t in (born_at(rd, body) for body in shot.get("bodies") or []) if t is not None]
    if borns:
        alive = max(borns) + ALIVE_AFTER_S
        if a < alive - EPS:
            return start, (f"its stretch starts at {a:g} s, before its body is alive in a window, at {alive:g} s "
                           f"(born at {max(borns):g} s)")
        start = max(start, alive)
    return start, None


def far_fog(shot, s):
    """The fog a shot is filmed in when it is not the look's: a far shot's, when the settings give one."""
    return s.get("far_fog") if shot["camera"] in WORLD else None


def close_focus(shot, s):
    """The focus a close shot is filmed in when it is not the grade's: the settings' close_focus."""
    return s.get("close_focus") if shot["camera"] in CLOSE else None


def side_of(shot):
    """The side a portrait under a graphic puts its subject: the left, since the graphic's card takes the right
    third. Any other shot leaves it to the director."""
    return "left" if shot.get("kind") == "graphic" and shot["camera"] == "Portrait" else None


def size_of(shot, s):
    """A shot's size: its own from size_by_shot, an offline pass filmed small for review, or the film's."""
    return (s.get("size_by_shot") or {}).get(shot["id"], s["size"])


def handle_of(shot, s):
    """A shot's handle: its own from handles_by_shot, or the film's."""
    return (s.get("handles_by_shot") or {}).get(shot["id"], s["handles_s"])


LOOK_SECTIONS = ("wide", "events")     # the sections of theatre-film-look.json the safari reads


def look_file():
    """The main checkout's scripts/theatre-film-look.json, found from any worktree as theatre-film-look.ps1 finds it
    (the parent of git's common directory), else this checkout's own."""
    own = REPO / "scripts" / "theatre-film-look.json"
    try:
        r = subprocess.run(["git", "-C", str(REPO), "rev-parse", "--git-common-dir"], capture_output=True, text=True,
                           encoding="utf-8", timeout=30)
        common = r.stdout.strip().splitlines()[0] if r.returncode == 0 and r.stdout.strip() else None
    except (OSError, subprocess.SubprocessError):
        common = None
    if common:
        c = Path(common)
        c = c if c.is_absolute() else REPO / c
        main = c.resolve().parent / "scripts" / "theatre-film-look.json"
        if main.is_file():
            return main
    return own


def look_dials():
    """The first 16 hex characters of the sha256 of the canonical JSON (sorted keys, no spaces) of the look's
    sections the safari reads. Refuses when the look file is missing or unreadable."""
    path = look_file()
    if not path.is_file():
        raise Refused(f"the theatre's film look was not found at {path}; a clip's key cannot be made without it")
    try:
        look = json.loads(path.read_text(encoding="utf-8-sig"))
        sections = {k: look[k] for k in LOOK_SECTIONS}
    except (OSError, ValueError, KeyError) as e:
        raise Refused(f"the theatre's film look at {path} cannot be read ({type(e).__name__}: {e}); it needs the "
                      f"sections {', '.join(LOOK_SECTIONS)}")
    text = json.dumps(sections, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def seat_of(s):
    """The theatre build a take is filmed on: theatre_commit's first 8 characters, lower-cased."""
    return str(s.get("theatre_commit") or "")[:8].lower()


def shot_key(shot, s, colours=None):
    """What a clip was filmed from: a clip whose key differs from its shot's is filmed again. A far shot's own
    fog is in its key, so setting far_fog films the far shots again and no others."""
    fields = {k: shot[k] for k in ("run", "bodies", "camera", "run_from_s", "run_to_s")}
    fields.update({k: s[k] for k in ("handles_s", "fps", "size")})
    fields["handles_s"] = handle_of(shot, s)
    fields["size"] = size_of(shot, s)
    fields["look"] = s["theatre_look"]
    fields["look_dials"] = s.get("look_dials") or look_dials()   # a take on other dials is never kept silently
    fields["seat"] = seat_of(s)                                  # nor one filmed on another theatre build
    take =(s.get("takes_by_shot") or {}).get(shot["id"])
    if take is not None:
        fields["take"] = take          # raised to film a shot again when nothing in its key changed, a camera's fix
    if s.get("theatre_stream"):
        fields["stream"] = "nvenc"     # only when on, so a film filmed before the switch keeps its keys
    if (s.get("subject_by_shot") or {}).get(shot["id"]):
        fields["subject"] = s["subject_by_shot"][shot["id"]]    # likewise: only a shot with an entry
    if s.get("snow"):
        fields["snow"] = s["snow"]     # only when set, so a film filmed before the setting keeps its keys
    skin = skin_of(s)
    if skin:
        fields["skin"] = skin          # likewise: only what differs from the theatre's defaults
    if shot.get("colour") is not None:
        if shot["colour"] not in (colours or {}):
            raise Refused(f"shot {shot['id']}: colour '{shot['colour']}' is not a colour {COLOURS} names")
        fields["colour"] = shot["colour"]      # likewise: a shot with no colour keeps its key
        fields["colour_sha256"] = colours[shot["colour"]]["sha256"]
    if shot.get("canopy"):
        fields["canopy"] = True        # likewise: only a canopy shot, so no other shot is filmed again
    if far_fog(shot, s) is not None:
        fields["fog"] = far_fog(shot, s)
    if close_focus(shot, s) is not None:
        fields["focus"] = close_focus(shot, s)
    if side_of(shot) is not None:
        fields["side"] = side_of(shot)
    return hashlib.sha256(json.dumps(fields, sort_keys=True).encode()).hexdigest()[:16]


def skin_of(s):
    """The skin's settings that differ from the theatre's defaults (glass and earth on, caustics as the shaders')."""
    skin = {}
    if s.get("glass", True) is False:
        skin["glass"] = False
    if s.get("earth", True) is False:
        skin["earth"] = False
    if s.get("caustics") is not None and s["caustics"] != 1:
        skin["caustics"] = s["caustics"]
    return skin


def skin_env(s):
    """The environment the theatre reads the skin's settings from."""
    skin = skin_of(s)
    env = {}
    for key in ("glass", "earth"):
        if key in skin:
            env[SKIN_ENV[key]] = "0"
    if "caustics" in skin:
        env[SKIN_ENV["caustics"]] = str(skin["caustics"])
    return env


def skin_pattern(s):
    """What the theatre's skin line says when the settings reached it (TheatreSkin's dress)."""
    skin = skin_of(s)
    glass = "off" if "glass" in skin else "on"
    earth = "off" if "earth" in skin else "on"
    scale = dial_text(skin.get("caustics", 1))
    return re.escape(f"skin: glass {glass}, earth {earth}, caustic scale x{scale}") + r"(?!\d|\.\d)"


def dial_text(v):
    """A dial as the theatre's log writes it (C#'s 0.##)."""
    return f"{v:.2f}".rstrip("0").rstrip(".")


def read_colours(folder):
    """The film's colours.json as (colours, problems). colours is None when the film has none, else the entries
    that are sound, name by name; problems is every way the file departs from its form, as lines."""
    path = folder / COLOURS
    if not path.is_file():
        return None, []
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except ValueError as exc:
        return {}, [f"{COLOURS} is not JSON: {exc}"]
    if not isinstance(data, dict):
        return {}, [f"{COLOURS} is an object of colour names"]
    colours, problems = {}, []
    for name, entry in data.items():
        where = f"{COLOURS}, colour '{name}'"
        if not COLOUR_NAME.fullmatch(name):
            problems.append(f"{where}: a name is lower-case letters, digits and hyphens")
            continue
        if not isinstance(entry, dict) or set(entry) != COLOUR_FIELDS:
            problems.append(f"{where}: holds exactly shows, file, sha256 and runs")
            continue
        runs = entry["runs"]
        if not all(isinstance(entry[k], str) and entry[k].strip() for k in ("shows", "file")):
            problems.append(f"{where}: shows and file are words and a path")
        elif not (isinstance(runs, list) and runs and all(isinstance(r, str) and r for r in runs)):
            problems.append(f"{where}: runs is a list of at least one run")
        elif not (isinstance(entry["sha256"], str) and re.fullmatch("[0-9a-f]{64}", entry["sha256"])):
            problems.append(f"{where}: sha256 is 64 lower-case hex digits")
        elif not repo_path(entry["file"]).is_file():
            problems.append(f"{where}: its file {entry['file']} is not there")
        elif sha256_of(repo_path(entry["file"])) != entry["sha256"]:
            problems.append(f"{where}: its sha256 is not the file's: the map changed after it was named")
        else:
            colours[name] = entry
    return colours, problems


def colour_faults(shot, colours):
    """Why a shot's colour cannot be filmed, as lines without the shot's name; none when it has none or is sound."""
    if "colour" not in shot:
        return []
    name = shot["colour"]
    if not isinstance(name, str):
        return ["colour is the name of a colour in colours.json, a string"]
    out = []
    if "run" not in shot:
        out.append(f"colour '{name}' is on a shot with no footage; only footage is coloured")
    if colours is None:
        out.append(f"colour '{name}' is given and the film has no {COLOURS}")
    elif name not in colours:
        out.append(f"colour '{name}' is not a colour {COLOURS} names")
    elif "run" in shot and shot["run"] not in colours[name]["runs"]:
        out.append(f"colour '{name}' is not for run {shot['run']}, only for " + ", ".join(colours[name]["runs"]))
    return out


def colours_of(folder, shots):
    """The film's colours, read for filming: refuses on a fault in the file or in any shot's colour."""
    colours, problems = read_colours(folder)
    for x in shots:
        problems += [f"shot {x.get('id')}: {m}" for m in colour_faults(x, colours)]
    if problems:
        raise Refused(*problems)
    return colours


def colour_of(shot, colours):
    """What a pass of this shot records of its colour: {name, file, sha256}, or None."""
    name = shot.get("colour")
    if name is None:
        return None
    return {"name": name, "file": colours[name]["file"], "sha256": colours[name]["sha256"]}


def same_path(a, b):
    return str(a).replace(chr(92), "/").lower() == str(b).replace(chr(92), "/").lower()


def colour_log_fault(colour, log):
    """Why a theatre log does not show the colour its pass asked for, or None. A coloured pass's log has a teams
    line naming its file with bodies turned; an uncoloured pass's has none (the theatre's TEAMS line)."""
    lines = TEAMS_LINE.findall(log)
    if colour is None:
        return "the theatre's log has a teams line, and this pass was asked for none" if lines else None
    want = repo_path(colour["file"])
    if not any(int(n) > 0 and same_path(path, want) for n, path in lines):
        return (f"the theatre's log does not show colour '{colour['name']}': no teams line with bodies turned "
                f"from {want}")
    return None


LOOK_WARNINGS = (
    ("the main checkout's theatre-film-look.json was not found",
     "the look was not the main checkout's: the launcher read its own copy of theatre-film-look.json"),
    ("has no row in scripts/theatre-world-spots.tsv",
     "the eyespot gate is the look's stand-in: the world has no row in scripts/theatre-world-spots.tsv"),
    ("has no row in scripts/theatre-world-stops.tsv",
     "the blade stops are the look's stand-in: the world has no row in scripts/theatre-world-stops.tsv"),
)


def look_log_fault(log):
    """Why a take's logs show a look other than the main checkout's calibrated one, or None. theatre-film-look.ps1
    warns when it falls back to its own copy of the dials, and when a world has no row in a per-world table."""
    for text, why in LOOK_WARNINGS:
        if text in log:
            return why
    return None


def snow_text(snow):
    return ", ".join(f"{k} {dial_text(v)}" for k, v in sorted(snow.items()))


def snow_patterns(snow):
    """What the theatre's look line says when each snow dial reached it (TheatreLook.Describe)."""
    words = {"gain": "shell gain ", "quantity": "quantity x", "least": "least ", "ambient": "ambient "}
    return [re.escape(words[k] + dial_text(v)) + r"(?!\d|\.\d)" for k, v in snow.items()]


def fog_text(fog):
    """A fog density as the theatre's log writes it (C#'s 0.####)."""
    return f"{fog:.4f}".rstrip("0").rstrip(".")


def tree_hash(root):
    h = hashlib.sha256()
    if root.is_dir():
        for p in sorted(root.rglob("*")):
            if p.is_file() and p.suffix != ".meta":
                h.update(str(p.relative_to(root)).replace(chr(92), "/").encode())
                h.update(p.read_bytes())
    return h.hexdigest()


def theatre_root(s):
    """The checkout that holds the render worker and the theatre-safari.ps1 a pass runs: the settings' theatre_root,
    else the repository's root."""
    return repo_path(s["theatre_root"]) if s.get("theatre_root") else REPO


def stale_worker(s):
    """Why the render worker cannot film this pass, or None: missing, or carrying another theatre."""
    root = theatre_root(s)
    worker = root / f"unity-w{s['worker']}"
    if not worker.is_dir():
        return f"there is no render worker {worker.name}" + (f" in {root}" if root != REPO else "")
    if tree_hash(root / "unity" / "Assets" / "Theatre") != tree_hash(worker / "Assets" / "Theatre"):
        if root != REPO:
            return (f"{worker.name} carries another theatre than {root}'s unity/Assets/Theatre; its seat is the lane's "
                    "that keeps it")
        return (f"{worker.name} carries another theatre than unity/Assets/Theatre; refresh it from PowerShell with "
                f"./scripts/new-worker.ps1 -Workers {s['worker']}")
    return None


def commit_fault(s):
    """Why the theatre_root checkout is not at the settings' theatre_commit, or None: the lane reseats that checkout
    between films, and both its workers compile its src."""
    want = s.get("theatre_commit")
    if not want:
        return None
    root = theatre_root(s)

    def rev(name):
        r = subprocess.run(["git", "-C", str(root), "rev-parse", "--verify", "-q", name + "^{commit}"],
                           capture_output=True, text=True, encoding="utf-8")
        return r.stdout.strip() if r.returncode == 0 and r.stdout.strip() else None

    head, named = rev("HEAD"), rev(want)
    if head is None or named is None:
        return (f"theatre_commit {want}: git could not name {'HEAD' if head is None else want} in {root}"
                f" (HEAD {head}, {want} {named})")
    if head != named:
        return f"the theatre checkout {root} is at {head}, and theatre_commit {want} is {named}"
    return None


def subject(shot, s=None):
    named = ((s or {}).get("subject_by_shot") or {}).get(shot["id"])
    if named:
        return named
    if shot["camera"] in WORLD or not shot["bodies"]:
        return "world"
    # "parent body N" names no family the window planner can place, and it films such a Birth as an Arrival.
    return "body " + shot["bodies"][0]


def pass_dir(folder, k):
    return folder / "film" / f"pass-{k}"


CANOPY_CAMERAS = ("Arrival", "Descent")     # the stations the theatre films as the canopy shot


def canopy_faults(shot):
    """Why a shot's canopy cannot be filmed, as lines without the shot's name; none when it has none or is sound."""
    if "canopy" not in shot:
        return []
    if shot["canopy"] is not True:
        return [f"canopy is true or left out, not {json.dumps(shot['canopy'])}"]
    if "run" not in shot:
        return ["canopy is on a shot with no footage; only an Arrival or a Descent is filmed as the canopy"]
    if shot.get("camera") not in CANOPY_CAMERAS:
        return [f"canopy is on a {shot.get('camera')} shot; only an Arrival or a Descent is filmed as the canopy"]
    return []


def canopy_log_fault(slug, log):
    """Why a theatre log does not show the canopy plan for a scene, or None. The director writes
    'take N of <slug> at T s: canopy: ...' for each take planned as the canopy shot (FilmPlans Plan_). Under
    EVOSIM_THEATRE_CANOPY_GROUP it may write instead 'take N of <slug> at T s: descent: ... in place of the canopy
    descent under the densest column of leaves ...' (SafariPlans.GroupDescentAtCanopy), a group descent that takes the
    canopy's place on that one line; that counts too."""
    start = r"take [0-9]+ of " + re.escape(slug) + r" at [^\n]*? s: "
    if re.search(start + r"canopy: ", log):
        return None
    if re.search(start + r"[^\n]*in place of the canopy descent under the densest column of leaves", log):
        return None
    return f"the theatre's log does not show the canopy plan for {slug}"


def safari_dir(folder, k, arm, s=None):
    return theatre_root(s or {}) / "scratch" / "safari" / arm / f"{folder.name}-film{k}"


def cmd_plan(folder, k):
    lines = []
    s = film_settings(folder, lines)
    shots = read_json(folder / "shots.json")
    if shots is None:
        raise Refused("the film has no shots.json; the shot list step writes it")
    if (pass_dir(folder, k) / "plan.json").is_file():
        raise Refused(f"pass {k} is planned already; plan the next pass")
    fault = commit_fault(s)
    if fault:
        raise Refused(fault)
    # subject_by_shot names a world shot's subject; a body's shot already has its subject, so that entry is refused.
    by_id = {x.get("id"): x for x in shots["shots"] if isinstance(x, dict)}
    wrong = [f"subject_by_shot names shot {i}, a {by_id[i].get('camera')} shot; only a shot on a world station "
             f"({', '.join(sorted(WORLD))}) takes a subject, and a body's shot has its body" for i in (s.get("subject_by_shot") or {})
             if i in by_id and by_id[i].get("camera") not in WORLD]
    if wrong:
        raise Refused(*wrong)
    for i in (s.get("subject_by_shot") or {}):
        if i not in by_id:
            lines.append(f"warning: subject_by_shot names shot {i}, which is not in shots.json")
    clips = (read_json(folder / "clips.json") or {}).get("clips", [])
    filmed = {c["shot"]: c["key"] for c in clips if (folder / c["file"]).is_file() and sha256_of(folder / c["file"]) == c["sha256"]}
    colours = colours_of(folder, [x for x in shots["shots"] if isinstance(x, dict)])
    runs = [x for x in shots["shots"] if "run" in x]           # every shot with footage, graphics' included
    todo = [x for x in runs if filmed.get(x["id"]) != shot_key(x, s, colours)]
    if s.get("shots_only"):
        bad = [f"shots_only names {i}, which is not in shots.json" for i in s["shots_only"] if i not in by_id]
        bad += [f"shots_only names {i}, which has no run, so no footage to film" for i in s["shots_only"]
                if i in by_id and "run" not in by_id[i]]
        if bad:
            raise Refused(*bad)
    if not todo:
        return "FILM PLAN: NOTHING TO FILM", lines + [f"every one of the {len(runs)} shots with footage has its clip"]
    filmed_count = len(runs) - len(todo)       # shots whose clip is current; the shots held back are not counted as filmed
    # runs_only films the named runs alone, so one run can be filmed by itself and judged before the rest: round
    # 67's film refilmed the take a driver fault cut short before filming the other runs (2026-11-08).
    if s.get("runs_only"):
        only = set(s["runs_only"])
        waiting = [x for x in todo if x["run"] not in only]
        todo = [x for x in todo if x["run"] in only]
        if not todo:
            raise Refused("runs_only names " + ", ".join(sorted(only)) + ", and no shot of those runs is left to "
                          "film: set it to null, or name a run with shots to film")
        if waiting:
            lines.append(f"{len(waiting)} shots on runs not in runs_only wait for the next passes: "
                         + ", ".join(sorted({x['run'] for x in waiting})))
    # shots_only films the named shots alone, as runs_only films the named runs: a stills round passes some shots of a
    # run that holds many, and only those are filmed. It narrows what runs_only left.
    if s.get("shots_only"):
        named = list(dict.fromkeys(s["shots_only"]))
        left = {x["id"] for x in todo}
        for i in named:
            if i not in left:
                lines.append(f"shots_only names {i}, whose clip is current; it is not filmed")
        waiting = [x for x in todo if x["id"] not in named]
        todo = [x for x in todo if x["id"] in named]
        if not todo:
            raise Refused("shots_only names " + ", ".join(named) + ", and none of them is left to film: set it to "
                          "null, or name a shot with a clip to film")
        if waiting:
            lines.append(f"{len(waiting)} shots not in shots_only wait for the next passes")
    errors, scenes, entries, dirs = [], [], [], {}
    # The director films a run at one size, so a pass does too: an offline pass is the shots set small in
    # size_by_shot, and the 4K pass that follows it is the same shots once they are taken out of it.
    # A record-only pass films nothing, and a window depends on neither the size nor the colour, so it has no size to hold.
    record_only = bool(s.get("record_only"))
    sizes = sorted({size_of(x, s) for x in todo})
    if len(sizes) > 1 and not record_only:
        raise Refused("a pass films at one size, and these shots ask " + ", ".join(sizes) + ": film the shots of "
                      "one size first, by leaving the others' size_by_shot as it was filmed")
    pass_size = s["size"] if record_only else sizes[0]
    # A window is recorded on the farm build that recorded its run, and one pass records on one build. So a film
    # whose runs come from several builds films one build's shots a pass, the build with the most shots first,
    # and the next passes take the rest; collect keeps every pass's clips (round 57's film, 2026-10-10).
    for arm in {x["run"] for x in todo}:
        dirs[arm] = run_dir(runs_root_for(s, arm), arm)
        if dirs[arm] is None:
            raise Refused(f"no run directory for {arm} under {runs_root_for(s, arm)}")
    build = {arm: farm_for(s, arm, rd) or "" for arm, rd in dirs.items()}
    counts = {}
    for x in todo:
        counts[build[x["run"]]] = counts.get(build[x["run"]], 0) + 1
    if len(counts) > 1:
        chosen = sorted(counts, key=lambda b: (-counts[b], b))[0]
        later = [x for x in todo if build[x["run"]] != chosen]
        todo = [x for x in todo if build[x["run"]] == chosen]
        dirs = {arm: rd for arm, rd in dirs.items() if build[arm] == chosen}
        lines.append(f"{len(later)} shots on runs of {len(counts) - 1} other farm build(s) wait for the next passes: "
                     + ", ".join(sorted({x['run'] for x in later})))
    # The theatre reads its colour map once, when its Editor starts, so a pass films one colour: the colour with
    # the most shots first (no colour counts as one), ties by name with none first, and the rest wait.
    by_colour = {}
    for x in todo:
        by_colour[x.get("colour")] = by_colour.get(x.get("colour"), 0) + 1
    pass_colour = None
    if record_only:
        pass        # a window holds no colour: the pass records every shot's, and the passes that film split by colour
    elif len(by_colour) > 1:
        first = sorted(by_colour, key=lambda c: (-by_colour[c], c is not None, c or ""))[0]
        pass_colour = first
        waiting = [x for x in todo if x.get("colour") != first]
        todo = [x for x in todo if x.get("colour") == first]
        dirs = {arm: rd for arm, rd in dirs.items() if arm in {x["run"] for x in todo}}
        lines.append(f"{len(waiting)} shots of other colours ("
                     + ", ".join(sorted({x.get("colour") or "none" for x in waiting}))
                     + ") wait for the next passes: a pass films one colour")
    else:
        pass_colour = next(iter(by_colour))
    # story-windows.py takes one runs root for the whole story, so a pass films the runs under one root: the root
    # with the most shots first, ties by its path, and the rest wait (round 67's film, 2026-11-07).
    by_root = {}
    for x in todo:
        r = str(runs_root_for(s, x["run"]))
        by_root[r] = by_root.get(r, 0) + 1
    pass_root = sorted(by_root, key=lambda r: (-by_root[r], r))[0]
    if len(by_root) > 1:
        waiting = [x for x in todo if str(runs_root_for(s, x["run"])) != pass_root]
        todo = [x for x in todo if str(runs_root_for(s, x["run"])) == pass_root]
        dirs = {arm: rd for arm, rd in dirs.items() if arm in {x["run"] for x in todo}}
        lines.append(f"{len(waiting)} shots on runs under {len(by_root) - 1} other runs root(s) wait for the next "
                     "passes: " + ", ".join(sorted({x['run'] for x in waiting})) + "; a pass films the runs under one root")
    # max_scenes caps the scenes of a pass, in shot-list order, because the theatre leaks memory per scene: round 67's
    # coordinator ruled on 2026-11-08 that the refilm runs in passes of at most 12. The estimate and the wall below
    # are computed from the entries of the shots kept.
    cap = s.get("max_scenes")
    if record_only:
        if cap is not None:
            lines.append(f"max_scenes {cap} is not applied: the theatre's memory is not used, and the pass records every window")
    elif cap is not None and len(todo) > cap:
        waiting = todo[cap:]
        todo = todo[:cap]
        dirs = {arm: rd for arm, rd in dirs.items() if arm in {x["run"] for x in todo}}
        lines.append(f"{len(waiting)} shots wait for the next passes under max_scenes {cap}: "
                     + ", ".join(x["id"] for x in waiting))
    for n, shot in enumerate(todo, 1):
        h = handle_of(shot, s)
        where, arm = f"shot {shot['id']}", shot["run"]
        if arm not in dirs:
            dirs[arm] = run_dir(runs_root_for(s, arm), arm)
            if dirs[arm] is None:
                raise Refused(f"no run directory for {arm} under {runs_root_for(s, arm)}")
        rd = dirs[arm]
        end, a, b = last_second(rd), shot["run_from_s"], shot["run_to_s"]
        after = min(h, end - TAIL_S - b)
        if after < 0:
            errors.append(f"{where}: its stretch ends at {b:g} s, past the run's last second, {end:g}")
            continue
        second = None
        if shot["camera"] == "Birth":
            found = births(rd, shot["bodies"][0], a, b)
            if not found:
                errors.append(f"{where}: body {shot['bodies'][0]} has no child born between {a:g} and {b:g} s")
                continue
            second, start = found[0], found[0] - BIRTH_LEAD_S
            if a < start - EPS:
                errors.append(f"{where}: the director opens a birth's take {BIRTH_LEAD_S:g} s before it, at "
                              f"{start:g} s, after the stretch starts at {a:g} s")
                continue
        else:
            start, why_not = lead_in(rd, shot, h)
            if why_not:
                errors.append(f"{where}: {why_not}")
                continue
        before = a - start
        seconds = b + after - start
        fog = far_fog(shot, s)
        scene = {"n": n, "run": arm, "station": shot["camera"], "second": round(second if second is not None
                 else start, 3), "seconds": round(seconds, 3), "subject": subject(shot, s), "flexible": False,
                 "captions": []}
        if fog is not None:
            scene["fog"] = fog
        focus, side = close_focus(shot, s), side_of(shot)
        if focus is not None:
            scene["focus"] = focus
        if side is not None:
            scene["side"] = side
        if shot.get("canopy"):
            scene["canopy"] = True     # the theatre reads canopy per scene (SafariStory.cs), so no switch on the call
        scenes.append(scene)
        entries.append({"n": n, "shot": shot["id"], "key": shot_key(shot, s, colours), "arm": arm, "camera": shot["camera"],
                        "clip_start_s": round(start, 3), "seconds": round(seconds, 3),
                        "action_in_s": round(before, 3), "action_out_s": round(before + b - a, 3),
                        "handle_before_s": round(before, 3), "handle_after_s": round(after, 3),
                        "birth_s": second if shot["camera"] == "Birth" else None, "fog": fog, "canopy": bool(shot.get("canopy")),
                        "focus": focus, "side": side})
        why = []
        if before < h - EPS:
            why.append(f"{before:.1f} s before" + (" (a birth's take opens 8 s before it)" if shot["camera"] == "Birth"
                                                     else " (its body is born)" if start > a - min(h, a) + EPS
                                                     else " (the run starts)"))
        if after < h - EPS:
            why.append(f"{after:.1f} s after (the run ends at {end:g} s)")
        lines.append(f"{where}: {shot['camera']} on {subject(shot, s)}, run {start:g} to {start + seconds:g} s, "
                     f"handles {before:.1f} and {after:.1f} s" + (f", fog {fog_text(fog)}" if fog is not None else "")
                     + (f", focus {focus['mode']}" if focus is not None else "")
                     + (f", subject {side}" if side is not None else "")
                     + (f"; short: {', '.join(why)}" if why else ""))
    if errors:
        raise Refused(*errors)
    if record_only:
        lines.append(f"record only: the farm windows of {len(entries)} shots on {len(dirs)} runs; nothing is filmed, "
                     "and the next filming pass reuses them")
    else:
        wall, over = wall_lines(s, entries, pass_size)
        if over:
            raise Refused(*over)
        lines.extend(wall)
    farms = {arm: farm_for(s, arm, rd) for arm, rd in dirs.items()}
    named = sorted({f for f in farms.values() if f})
    if len(named) > 1:
        raise Refused("the pass's runs were recorded by different farm programs, which one pass cannot record:",
                      *(f"  {arm}: {f}" for arm, f in sorted(farms.items())))
    farm = named[0] if named else None
    if farm and not Path(farm).is_file():
        raise Refused(f"the farm program that recorded the runs is gone: {farm}. A window recorded on a later build "
                      "can refuse the run's config")
    for arm, rd in sorted(dirs.items()):
        if farms[arm]:
            lines.append(source_check(arm, rd, farms[arm]))
    out = pass_dir(folder, k)
    story = {"title": folder.name, "run": scenes[0]["run"], "scenes": scenes}
    write_json(out / "story.json", story)
    r = subprocess.run([sys.executable, str(REPO / "scripts" / "story-windows.py"), str(out / "story.json"), "--out",
                        str(out / "windows"), "--runs-root", pass_root, "--fps", str(s["fps"])],
                       capture_output=True, text=True, encoding="utf-8", cwd=REPO)
    (out / "windows-plan.txt").write_text(r.stdout + r.stderr, encoding="utf-8")
    windows = read_json(out / "windows" / "windows.json")
    if r.returncode != 0 or windows is None:
        raise Refused("story-windows.py did not plan the windows; see film/pass-%d/windows-plan.txt" % k)
    mains = {w["scene"]: w for w in windows["windows"] if w["part"] == "main"}
    for e in entries:
        w = mains.get(e["n"])
        if w is None:
            errors.append(f"shot {e['shot']}: the window planner planned no window for it")
        elif w["station"] != e["camera"]:
            errors.append(f"shot {e['shot']}: the window planner films it as {w['station']}, not {e['camera']}")
        elif abs(w["start"] - e["clip_start_s"]) > 0.01:
            errors.append(f"shot {e['shot']}: its window opens at {w['start']:g} s, not {e['clip_start_s']:g} s")
        elif e["camera"] == "Birth" and (not w.get("birth") or abs(w["birth"]["at"] - e["birth_s"]) > 0.01):
            errors.append(f"shot {e['shot']}: the window planner films another birth than the one at {e['birth_s']:g} s")
        for note in (w or {}).get("notes", []):
            lines.append(f"shot {e['shot']}: the window planner notes: {note}")
    for sk in windows.get("skipped", []):
        errors.append(f"scene {sk['scene']}: the window planner skipped it: {sk['why']}")
    if errors:
        shutil.rmtree(out)
        raise Refused(*errors)
    reused = reuse_windows(folder, k, windows)
    if reused:
        lines.append(f"windows: {reused} of {len(windows['windows'])} reused from this film's earlier passes, so the farm "
                     "records only the rest")
    write_json(out / "plan.json", {"pass": k, "planned": now(), "shots_sha256": sha256_of(folder / "shots.json"),
                                   "settings": s, "size": pass_size, "farm": farm, "scenes": entries,
                                   "record_only": record_only,
                                   "colour": colour_of({"colour": pass_colour}, colours) if pass_colour else None})
    footage = sum(e["seconds"] for e in entries)
    head = [f"pass {k}: {len(entries)} shots to " + ("record, none filmed" if record_only else "film")
            + f" on {len(dirs)} run(s), {filmed_count} filmed already, {footage:.0f} s of footage with handles",
            (f"farm: {farm}, named for the pass's runs in farm_by_run"
             if any((s.get("farm_by_run") or {}).get(arm) for arm in dirs) else
             f"farm: {farm}, the program that recorded the runs") if farm else
            "farm: the checkout's own build, since the runs' manifests name none"]
    if s.get("theatre_root"):
        head.append(f"theatre root: the render worker and theatre-safari.ps1 are taken from {theatre_root(s)}")
    head.append(f"look dials {fields_look(s)}, seat {seat_of(s) or 'none'}")
    head.append(guard_text(s))
    if s.get("overlap") is False:
        head.append("overlap: off, the pass records every window first and films after, with no farm beside Unity")
    if pass_colour:
        head.append(f"colour: {pass_colour}, {colours[pass_colour]['shows']}")
    stale = None if record_only else stale_worker(s)
    if stale:
        head.append(f"warning: {stale}")
    return "FILM PLAN: READY", head + lines


def fields_look(s):
    return s.get("look_dials") or look_dials()


def guard_text(s):
    mode = s.get("guard", "standard")
    if mode is None:
        return "heat guard: none, the settings' guard is null"
    return (f"heat guard: {mode}, film-guard.ps1 for {s.get('guard_minutes', 600)} minutes beside the job"
            + (" (CPU 90 C once or 85 C twice in a row, and D258's stops)" if mode == "card terms" else
               " (CPU 95 C once or 90 C twice in a row, and D258's stops)" if mode == "film terms" else " (D258's stops)"))


def window_verdict(d):
    """A recorded window's last verdict row (story-windows.ps1's Read-Verdict), or None when it did not finish."""
    identity = d / "identity.jsonl"
    if not (d / "film.poses.bin").is_file() or not identity.is_file():
        return None
    last = None
    for line in identity.read_text(encoding="utf-8", errors="replace").splitlines():
        t = line.strip()
        if t.endswith("}") and '"verdict"' in t:
            last = t
    try:
        return json.loads(last) if last else None
    except ValueError:
        return None


def reuse_windows(folder, k, windows):
    """Links into pass k each window an earlier pass of this film recorded over the same run, seconds and frame
    rate, so the farm records only what is new. A change to the render (the look, the snow, the glass, a camera)
    changes nothing a window holds, and the owner asked on 2026-10-11 that a re-render never record again.
    The files are hard links where the disk allows, copies otherwise; story-windows.ps1 then skips each one, as
    it skips any window already recorded over its plan's span."""
    recorded = []
    for p in sorted((folder / "film").glob("pass-*/windows/*")):
        if p.is_dir() and p.parent.parent.name != f"pass-{k}":
            v = window_verdict(p)
            if v is not None:
                recorded.append((p, v))
    reused = 0
    for w in windows["windows"]:
        target = pass_dir(folder, k) / "windows" / w["out"]
        if target.exists():
            continue
        asked = Path(str(w["run"]).rstrip("/\\")).name
        for p, v in recorded:
            if (abs(float(v.get("from", -1)) - float(w["from"])) <= 1e-6 and abs(float(v.get("to", -1)) - float(w["to"])) <= 1e-6
                    and abs(float(v.get("fps", -1)) - float(w["fps"])) <= 1e-6 and (not v.get("run") or v["run"] == asked)):
                target.mkdir(parents=True)
                for f in p.iterdir():
                    if f.is_file():
                        try:
                            os.link(f, target / f.name)
                        except OSError:
                            shutil.copyfile(f, target / f.name)
                reused += 1
                break
    return reused


def narration_tool():
    spec = importlib.util.spec_from_file_location("narration_tool", HERE / "narration.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def cmd_machine(folder):
    lines = []
    s = film_settings(folder, lines)
    verdict, found = narration_tool().cmd_machine()
    busy = verdict.endswith("BUSY")
    lines += found
    lock = theatre_root(s) / f"unity-w{s['worker']}" / "Temp" / "UnityLockfile"
    if lock.exists():
        busy = True
        lines.append(f"the render worker's lock is held: {lock}")
    stale = stale_worker(s)
    if stale:
        busy = True
        lines.append(stale)
    return ("MACHINE: BUSY" if busy else "MACHINE: FREE"), lines


def alive(pid):
    r = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True)
    return str(pid) in r.stdout


def stop_file(folder):
    """The file film-guard.ps1 writes when it stops a pass for heat: the time and the reason inside."""
    return folder / "film" / "STOP"


def stop_text(folder):
    """The stop file's text, or None while there is no stop file."""
    f = stop_file(folder)
    if not f.is_file():
        return None
    return f.read_text(encoding="utf-8", errors="replace").strip() or "(the stop file is empty)"


def safari_takes_stop_file(s):
    """True when the theatre checkout's theatre-safari.ps1 declares a $StopFile parameter in its param(...) block."""
    script = theatre_root(s) / "scripts" / "theatre-safari.ps1"
    try:
        text = script.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    m = re.search(r"\bparam\s*\(", text, re.IGNORECASE)
    if not m:
        return False
    depth, i = 1, m.end()
    while i < len(text) and depth:
        depth += {"(": 1, ")": -1}.get(text[i], 0)
        i += 1
    return re.search(r"\$StopFile\b", text[m.end():i], re.IGNORECASE) is not None


def path_regex(path):
    """A regex that accepts a path with either slash."""
    return r"[\\/]".join(re.escape(x) for x in re.split(r"[\\/]+", str(path)) if x)


def workers_regex(s):
    """Matches the render worker's folder in a command line: the theatre root's path from the repository when it is
    under it, the repository's own name for the repository, else the root's whole path."""
    root = theatre_root(s)
    try:
        rel = root.relative_to(REPO)
        base = path_regex(rel) if rel.parts else re.escape(REPO.name)
    except ValueError:
        base = path_regex(root)
    return base + r"[\\/]" + re.escape(f"unity-w{s['worker']}")


def guard_args(folder, k, plan, job_pid):
    """The arguments of film-guard.ps1 for pass k, or None when the settings' guard is null."""
    s = plan["settings"]
    mode = s.get("guard", "standard")
    if mode is None:
        return None
    out = pass_dir(folder, k)
    args = ["-NoProfile", "-File", str(HERE / "film-guard.ps1"), "-Done", str(out / "done.json"),
            "-Log", str(out / "guard.log"), "-StopFile", str(stop_file(folder)), "-KillTree", str(job_pid),
            "-Workers", workers_regex(s), "-Minutes", str(s.get("guard_minutes", 600))]
    if plan.get("farm"):
        args += ["-FarmExe", path_regex(Path(plan["farm"]).parent)]
    if mode == "card terms":
        args.append("-CardTerms")
    elif mode == "film terms":
        args.append("-FilmTerms")
    return args


def start_hidden(program, args, stdout, stderr, said):
    """Start-Process, hidden, its output to files; the new process's id. PowerShell's own output goes to a file too."""
    quoted = ", ".join(("'\"" + a + "\"'") if " " in a else f"'{a}'" for a in args)
    command = (f"$p = Start-Process -FilePath '{program}' -ArgumentList @({quoted}) -WorkingDirectory '{REPO}' "
               f"-RedirectStandardOutput '{stdout}' -RedirectStandardError '{stderr}' -WindowStyle Hidden -PassThru; $p.Id")
    with open(said, "w", encoding="utf-8") as f:
        r = subprocess.run(["pwsh", "-NoProfile", "-Command", command], stdin=subprocess.DEVNULL, stdout=f,
                           stderr=subprocess.STDOUT)
    text = said.read_text(encoding="utf-8", errors="replace").strip()
    pid = text.splitlines()[-1].strip() if text else ""
    if r.returncode != 0 or not pid.isdigit():
        raise Refused("PowerShell did not start the process:", text)
    return int(pid)


def cmd_start(folder, k):
    out = pass_dir(folder, k)
    stopped = stop_text(folder)
    if stopped is not None:
        raise Refused(f"a stop file exists, written by the heat guard or by a clean stop (`stop`): "
                      f"{stop_file(folder)} says: {stopped}",
                      "wait until scripts/temp-check.ps1 reads the CPU under 75 °C, tell the machine's coordinator, "
                      "and delete the file")
    if read_json(out / "plan.json") is None:
        raise Refused(f"pass {k} has no plan; run plan first")
    fault = commit_fault(read_json(out / "plan.json")["settings"])
    if fault:
        raise Refused(fault)
    if (out / "started.json").is_file():
        raise Refused(f"pass {k} was started already, at {read_json(out / 'started.json')['started']}")
    if (out / "run.log").is_file():
        # A job writes run.log from its first second, so a pass that has one has a job, even when start never
        # recorded it: a second job would film into the same folder.
        raise Refused(f"pass {k} has a job already: film/pass-{k}/run.log exists")
    verdict, lines = cmd_machine(folder)
    if verdict != "MACHINE: FREE":
        raise Refused("the machine or the render worker is not free:", *lines)
    paths = [sys.executable, str(HERE / "film.py"), str(folder), str(REPO), str(out)]
    if any(" " in p or "'" in p for p in paths):
        raise Refused("a path holds a space or a quote, which the detached launch cannot carry")
    args = ", ".join(f"'{a}'" for a in (str(HERE / "film.py"), "run", str(folder), str(k)))
    command = (f"$p = Start-Process -FilePath '{sys.executable}' -ArgumentList @({args}) -WorkingDirectory '{REPO}' "
               f"-RedirectStandardOutput '{out / 'run.log'}' -RedirectStandardError '{out / 'run.err.log'}' "
               "-WindowStyle Hidden -PassThru; $p.Id")
    # PowerShell's output goes to a file and never to a pipe: the detached job inherits PowerShell's handles, and
    # a pipe it holds would keep this command waiting until the whole pass ended (pass 2 of round 49's film).
    said = out / "start.log"
    with open(said, "w", encoding="utf-8") as f:
        r = subprocess.run(["pwsh", "-NoProfile", "-Command", command], stdin=subprocess.DEVNULL, stdout=f,
                           stderr=subprocess.STDOUT)
    text = said.read_text(encoding="utf-8", errors="replace").strip()
    pid = text.splitlines()[-1].strip() if text else ""
    if r.returncode != 0 or not pid.isdigit():
        raise Refused("PowerShell did not start the job:", text)
    plan = read_json(out / "plan.json")
    gargs = guard_args(folder, k, plan, int(pid))
    gpid = None
    if gargs is not None:
        try:
            gpid = start_hidden("pwsh", gargs, out / "guard.out.log", out / "guard.err.log", out / "guard-start.log")
        except Refused as e:
            subprocess.run(["taskkill", "/PID", pid, "/T", "/F"], capture_output=True)    # no job runs unguarded
            for name in ("run.log", "run.err.log"):
                try:
                    (out / name).unlink()
                except OSError:
                    pass
            raise Refused("the heat guard did not start, so the job (process " + pid + ") was stopped:", *e.lines)
    write_json(out / "started.json", {"pass": k, "pid": int(pid), "guard": gpid, "started": now()})
    return "FILM START: STARTED", [f"pass {k} runs detached as process {pid}; its log is film/pass-{k}/run.log",
                                   f"heat guard: process {gpid}, its log film/pass-{k}/guard.log" if gpid else
                                   "heat guard: none, the settings' guard is null"]


def cores_mask(text):
    """The affinity mask of a list of logical processors such as "16-31" or "0-3,8", or None for none given."""
    if not text:
        return None
    mask = 0
    for part in str(text).split(","):
        low, _, high = part.strip().partition("-")
        for n in range(int(low), int(high or low) + 1):
            mask |= 1 << n
    return mask


def pin(pid, cores):
    """Hold a process to the given logical processors at below-normal priority; its children inherit both. The
    machine's coordinator moved the farm's window recordings to the efficiency cores on 2026-10-10, after the
    farm and the theatre together took the performance cores to 92 °C."""
    mask = cores_mask(cores)
    if mask is None or os.name != "nt":
        return
    import ctypes
    kernel = ctypes.windll.kernel32
    handle = kernel.OpenProcess(0x0200 | 0x0400, False, pid)   # PROCESS_SET_INFORMATION | QUERY_INFORMATION
    if not handle:
        raise Refused(f"could not open process {pid} to pin it to {cores}")
    try:
        if not kernel.SetProcessAffinityMask(handle, ctypes.c_size_t(mask)):
            raise Refused(f"could not pin process {pid} to {cores}")
        kernel.SetPriorityClass(handle, 0x4000)                  # BELOW_NORMAL_PRIORITY_CLASS
    finally:
        kernel.CloseHandle(handle)


def launch(args, sink=None, cores=None):
    """One child of the pass's job, its output to the job's own log or to sink, pinned to cores when given (before
    it has started anything of its own, so its children inherit the pin). Tests stand in for it."""
    proc = subprocess.Popen(args, cwd=REPO, stdout=sink, stderr=subprocess.STDOUT if sink else None)
    pin(proc.pid, cores)
    return proc.wait()


def record_only_pass(plan, done=None):
    """True for a pass planned with record_only: it records the farm windows and films nothing."""
    return bool(plan.get("record_only") or (plan.get("settings") or {}).get("record_only")
                or (done or {}).get("record_only"))


def cmd_run(folder, k):
    """The pass's job. It records each run's windows on the farm, and films each run in the theatre as soon as its
    windows are in, while the next run records: the farm works the processor and the theatre mostly the card, so
    the two overlap. The runs record smallest first, so the theatre starts early. It films one run at a time,
    since a worker takes one Editor. A record-only pass starts no theatre: it records the windows and ends."""
    import queue
    import threading
    out = pass_dir(folder, k)
    plan = read_json(out / "plan.json")
    s = plan["settings"]
    # The theatre's underwater look is a switch read from the environment, off by default
    # (unity/Assets/Theatre/CLAUDE.md), so the job sets it for every theatre it starts.
    os.environ["EVOSIM_THEATRE_LOOK"] = str(s["theatre_look"])
    for name, env in SNOW_ENV.items():
        os.environ.pop(env, None)      # never one left from the shell: the settings say the whole snow
        if (s.get("snow") or {}).get(name) is not None:
            os.environ[env] = str(s["snow"][name])
    for env in SKIN_ENV.values():
        os.environ.pop(env, None)
    os.environ.update(skin_env(s))
    os.environ.pop(TEAMS_ENV, None)    # never a map left from the shell: the pass names its colour or none
    if plan.get("colour"):
        os.environ[TEAMS_ENV] = str(repo_path(plan["colour"]["file"]))
    done = {"pass": k, "started": now(), "look": s["theatre_look"], "stream": bool(s.get("theatre_stream")),
            "snow": s.get("snow"), "colour": plan.get("colour"),
            "overlap": s.get("overlap") is not False, "stopped": None, "record_exit": 0, "recorded": {},
            "render_exit": {}, "safari": {}, "stop_file": None, "record_only": record_only_pass(plan)}
    record_only = done["record_only"]
    if safari_takes_stop_file(s):
        done["stop_file"] = str(stop_file(folder))
    scenes = {}
    for e in plan["scenes"]:
        scenes.setdefault(e["arm"], []).append(e["n"])
    order = sorted(scenes, key=lambda a: (len(scenes[a]), a))
    lock = threading.Lock()

    def say(line):
        with lock:
            print(line, flush=True)

    def note_stop():
        """True while the stop file exists, keeping the first text seen in done.json."""
        text = stop_text(folder)
        if text is not None and done["stopped"] is None:
            done["stopped"] = text
            say(f"{now()} the stop file {stop_file(folder)} exists: {text}")
        return text is not None

    def films(todo):
        while True:
            arm = todo.get()
            if arm is None:
                return
            if note_stop():
                continue       # the heat guard's stop file: nothing more is filmed
            done["safari"][arm] = str(safari_dir(folder, k, arm, s))
            args = ["pwsh", "-NoProfile", "-File", str(theatre_root(s) / "scripts" / "theatre-safari.ps1"), arm,
                    "-Story", str(out / "story.json"), "-FromWindows", str(out / "windows"), "-StoryRun", arm,
                    "-Worker", str(s["worker"]), "-RunsRoot", str(runs_root_for(s, arm)),
                    "-Folder", f"{folder.name}-film{k}", "-DeleteFrames", "-WallMinutes", str(s["wall_minutes"]),
                    "-Fps", str(s["fps"]), "-Size", plan.get("size", s["size"])]
            if done["stop_file"]:
                args += ["-StopFile", done["stop_file"]]   # checked between scenes; a clean stop (film.py stop)
            if s.get("theatre_stream"):
                args.append("-Stream")     # the takes straight to the card's encoder (theatre-safari.ps1 -Stream)
            say(f"{now()} {arm}: filming, its log film/pass-{k}/film-{arm}.log: {' '.join(args)}")
            with open(out / f"film-{arm}.log", "w", encoding="utf-8") as sink:
                done["render_exit"][arm] = launch(args, sink, cores=s.get("theatre_cores"))
            # The theatre's own log, kept beside the pass: the next run's filming writes over it, and collect
            # reads each far take's fog from it.
            log = theatre_root(s) / "scratch" / "logs" / f"theatre-safari-{arm}.log"
            if log.is_file():
                shutil.copyfile(log, out / f"theatre-{arm}.log")
            say(f"{now()} {arm}: filmed, exit {done['render_exit'][arm]}")

    todo = queue.Queue()
    held = []      # overlap false: the runs recorded with exit 0, filmed once every run is recorded
    theatre = None if record_only else threading.Thread(target=films, args=(todo,))
    if theatre:
        theatre.start()
    try:
        for arm in order:
            if note_stop():
                break          # nothing more is recorded
            args = (["pwsh", "-NoProfile", "-File", str(REPO / "scripts" / "story-windows.ps1"), str(out / "windows"),
                     "-Threads", str(s["farm_threads"]), "-Scenes", ",".join(str(n) for n in scenes[arm])]
                    + (["-Exe", plan["farm"]] if plan.get("farm") else []))
            say(f"{now()} {arm}: recording {len(scenes[arm])} window(s): {' '.join(args)}")
            code = launch(args, cores=s.get("farm_cores"))
            done["recorded"][arm] = code
            say(f"{now()} {arm}: recorded, exit {code}")
            if code == 0:
                if record_only:
                    pass
                elif done["overlap"]:
                    todo.put(arm)
                else:
                    held.append(arm)
            elif done["record_exit"] == 0:
                done["record_exit"] = code
    finally:
        if theatre:
            for arm in held:
                todo.put(arm)
            todo.put(None)
            theatre.join()
    done["finished"] = now()
    note_stop()
    write_json(out / "done.json", done)
    ok = done["stopped"] is None and done["record_exit"] == 0 and all(v == 0 for v in done["render_exit"].values())
    return ("FILM RUN: DONE" if ok else "FILM RUN: FAILED"), [json.dumps(done)]


def cmd_wait(folder, k, minutes):
    out = pass_dir(folder, k)
    started = read_json(out / "started.json")
    if started is None:
        raise Refused(f"pass {k} was never started")
    deadline = time.monotonic() + 60 * minutes
    while True:
        done = read_json(out / "done.json")
        if done is not None:
            ok = done["record_exit"] == 0 and all(v == 0 for v in done["render_exit"].values())
            lines = [f"recording exited {done['record_exit']}; "
                     + ("nothing was filmed: a record-only pass, which is not collected" if done.get("record_only") else
                        "filming exited " + (", ".join(f"{a} {c}" for a, c in done["render_exit"].items()) or "not at all"))]
            return ("FILMING: DONE" if ok else "FILMING: FAILED"), lines
        if not alive(started["pid"]):
            return "FILMING: FAILED", [f"process {started['pid']} ended without writing done.json; see film/pass-{k}/run.log"]
        if time.monotonic() > deadline:
            return "FILMING: STILL RUNNING", [f"pass {k} has not ended after {minutes} minutes; wait again"]
        time.sleep(30)


def ffmpeg(args, cwd=None):
    r = subprocess.run(["ffmpeg", "-v", "error", "-y", *args], capture_output=True, text=True, cwd=cwd)
    if r.returncode != 0:
        raise Refused("ffmpeg failed: " + (r.stderr.strip().splitlines() or ["no message"])[-1])


def probe(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-count_packets", "-show_entries",
                        "stream=nb_read_packets,r_frame_rate,width,height:format=duration", "-of", "json", str(path)],
                       capture_output=True, text=True)
    data = json.loads(r.stdout or "{}")
    st, fmt = (data.get("streams") or [{}])[0], data.get("format") or {}
    num, den = (st.get("r_frame_rate") or "0/1").split("/")
    return {"frames": int(st.get("nb_read_packets", 0)), "fps": float(num) / float(den or 1),
            "width": st.get("width"), "height": st.get("height"), "duration": float(fmt.get("duration") or 0)}


def yavg(path, cwd, name, before=""):
    """Each frame's mean luma through a small filter chain, read back from ffmpeg's metadata print."""
    ffmpeg(["-i", str(path), "-vf", f"scale=320:-2,{before}signalstats,metadata=print:key=lavfi.signalstats.YAVG:"
            f"file={name}", "-f", "null", "-"], cwd=cwd)
    text = (cwd / name).read_text(encoding="utf-8")
    (cwd / name).unlink()
    return [float(v) for v in re.findall(r"YAVG=([0-9.]+)", text)]


def look(path, cwd, name, pre="", width=1920):
    """A clip's tone and edges, each frame read at `width` wide (1920 unless the caller says less): the spread of its luma from the 10th to the 90th
    percentile (a flat frame has little), and its mean Sobel edge strength (a soft or fogged frame has little)."""
    ffmpeg(["-i", str(path), "-vf", f"{pre}scale={width}:-2,signalstats,metadata=print:file={name}", "-f", "null", "-"],
           cwd=cwd)
    text = (cwd / name).read_text(encoding="utf-8")
    low = [float(v) for v in re.findall(r"YLOW=([0-9.]+)", text)]
    high = [float(v) for v in re.findall(r"YHIGH=([0-9.]+)", text)]
    ffmpeg(["-i", str(path), "-vf", f"{pre}scale={width}:-2,format=gray,sobel,signalstats,metadata=print:"
            f"key=lavfi.signalstats.YAVG:file={name}", "-f", "null", "-"], cwd=cwd)
    edges = [float(v) for v in re.findall(r"YAVG=([0-9.]+)", (cwd / name).read_text(encoding="utf-8"))]
    (cwd / name).unlink()
    return [h - lo for h, lo in zip(high, low)], edges


def colour(path, pre=""):
    """A clip's mean red, green and blue, 0 to 255: each frame averaged over a 160 by 90 image (a scale to one pixel returns one sample, not a mean), then
    everything averaged.
    The edit's grade stretches a clip's contrast about this point, so its colour stays where it was."""
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-vf", f"{pre}scale=160:90:flags=area,format=rgb24",
                        "-f", "rawvideo", "-"], capture_output=True)
    if r.returncode != 0 or len(r.stdout) < 3:
        raise Refused("ffmpeg could not read the colour of " + str(path))
    px = r.stdout[: len(r.stdout) // 3 * 3]
    n = len(px) // 3
    return [round(sum(px[c::3]) / n, 2) for c in range(3)]


def middle(values):
    return sorted(values)[len(values) // 2] if values else None


def tenth(values):
    return sorted(values)[len(values) // 10] if values else None


def sheet(path, out, frame_numbers):
    terms = "+".join(f"eq(n,{f})" for f in frame_numbers)
    rows = math.ceil(len(frame_numbers) / 4)
    ffmpeg(["-i", str(path), "-vf", f"select='{terms}',scale=480:-2,tile=4x{rows}", "-frames:v", "1",
            "-fps_mode", "vfr", str(out)])


def reference(folder, s, width=1920):
    """The reference film's mean luma and its sheet, made once a film. Its tone and edges are read at 1920 wide, and
    at a smaller width on request, scaled down from its own frames and cached under "at" in reference.json. The dict
    returned for a smaller width holds that width's tone and edges in place of the 1920 ones, and "look_width"."""
    path = repo_path(s["reference"])
    ref = read_json(folder / "film" / "reference.json")
    # The cache holds for the file the settings name now; a different file is measured again.
    if ref is not None and "tone_p10" in ref and same_path(ref.get("file", ""), str(path)):
        return reference_at(folder, path, ref, width)
    if not path.is_file():
        raise Refused(f"the reference film {path} is not there; the settings name the last film the owner accepted")
    (folder / "film").mkdir(parents=True, exist_ok=True)
    info = probe(path)
    luma = yavg(path, folder / "film", "reference-luma.txt", "fps=1,")
    ffmpeg(["-i", str(path), "-vf", f"fps=8/{max(info['duration'], 1):.3f},scale=480:-2,tile=4x2", "-frames:v", "1",
            str(folder / "film" / "reference-sheet.png")])
    tone, edges = look(path, folder / "film", "reference-look.txt", "fps=1,")
    ref = {"file": str(path), "luma": round(sum(luma) / len(luma), 2), "sheet": "film/reference-sheet.png",
           "tone": middle(tone), "tone_p10": tenth(tone), "edges": round(middle(edges), 2),
           "edges_p10": round(tenth(edges), 2)}
    write_json(folder / "film" / "reference.json", ref)
    return reference_at(folder, path, ref, width)


LOOK_KEYS = ("tone", "tone_p10", "edges", "edges_p10")


def reference_at(folder, path, ref, width):
    """The cached reference at a width: itself at 1920 or more, otherwise with the numbers read at that width."""
    if width >= 1920:
        return ref
    at = ref.get("at") or {}
    if str(width) not in at:
        if not path.is_file():
            raise Refused(f"the reference film {path} is not there; the settings name the last film the owner accepted")
        tone, edges = look(path, folder / "film", f"reference-look-{width}.txt", "fps=1,", width=width)
        at[str(width)] = {"tone": middle(tone), "tone_p10": tenth(tone), "edges": round(middle(edges), 2),
                          "edges_p10": round(tenth(edges), 2)}
        ref["at"] = at
        write_json(folder / "film" / "reference.json", ref)
    out = {k: v for k, v in ref.items() if k != "at"}
    out.update(at[str(width)])
    out["look_width"] = width
    return out


def reference_unsuited(ref, w, info):
    """Why a clip w wide cannot have its tone and edges compared with the reference, or None when it can. A reference
    not named by file (a stub) is taken as suited."""
    if not ref.get("file"):
        return None
    size = probe(ref["file"])
    rw, rh = size.get("width"), size.get("height")
    if not rw or not rh or not info.get("width") or not info.get("height"):
        return None
    if rw < w:
        return f"the reference film is {rw} wide, narrower than the clip's {w}"
    if abs((rw / rh) / (info["width"] / info["height"]) - 1) > 0.01:
        return f"the reference film is {rw}x{rh} and the clip {info['width']}x{info['height']}, of another aspect ratio"
    return None


def read_tsv(path):
    rows = [r.split(chr(9)) for r in path.read_text(encoding="utf-8-sig").splitlines() if r.strip()]
    return [dict(zip(rows[0], r)) for r in rows[1:]] if rows else []


def take_frames(log, slug):
    """The frames of each take of a scene, in order, from the theatre's log, or None when the log holds no take
    line for exactly this slug. A scene filmed again in the same log is read from its last take 1 on."""
    found = [(int(n), int(f)) for n, s, f in re.findall(r"take ([0-9]+) of (\S+) ended: ([0-9]+) frames", log)
             if s == slug]
    starts = [i for i, (n, _) in enumerate(found) if n == 1]
    if not starts:
        return None
    found = found[starts[-1]:]
    if [n for n, _ in found] != list(range(1, len(found) + 1)):
        return None
    return [f for _, f in found]


def jumps_within_takes(jumps, takes):
    """The frame-to-frame changes with those that cross a take boundary left out. tblend writes no output for the
    clip's first frame and collect drops the next one, so jumps[j] is the change into frame j + 2, and the change
    into the first frame of a take that begins at frame b is jumps[b - 2]."""
    if not takes or len(takes) < 2:
        return list(jumps)
    cuts, at = set(), 0
    for n in takes[:-1]:
        at += n
        cuts.add(at - 2)
    return [v for j, v in enumerate(jumps) if j not in cuts]


def cmd_collect(folder, k):
    out = pass_dir(folder, k)
    plan, done = read_json(out / "plan.json"), read_json(out / "done.json")
    if plan is None or done is None:
        raise Refused(f"pass {k} has not been planned and run to its end")
    if record_only_pass(plan, done):
        raise Refused(f"pass {k} recorded windows only (record_only) and has nothing to collect: the next filming pass "
                      "reuses its windows")
    s, shots = plan["settings"], read_json(folder / "shots.json")
    colours = colours_of(folder, [x for x in shots["shots"] if isinstance(x, dict)])
    ref = reference(folder, s)
    refs = {}
    clips_dir = folder / "clips"
    clips_dir.mkdir(exist_ok=True)
    tables = {arm: read_tsv(Path(d) / "scenes.tsv") if (Path(d) / "scenes.tsv").is_file() else []
              for arm, d in done["safari"].items()}
    lines, new, errors = [], [], []
    logs = {arm: (out / f"theatre-{arm}.log").read_text(encoding="utf-8", errors="replace")
            for arm in done["safari"] if (out / f"theatre-{arm}.log").is_file()}
    # The look's launcher warns in PowerShell's stream, which the pass keeps in film-<arm>.log.
    launcher = {arm: (out / f"film-{arm}.log").read_text(encoding="utf-8", errors="replace")
                for arm in done["safari"] if (out / f"film-{arm}.log").is_file()}
    for e in plan["scenes"]:
        where = f"shot {e['shot']}"
        row = next((r for r in tables.get(e["arm"], []) if r.get("slug", "").startswith(f"story-{e['n']:02d}-")), None)
        if row is None or row.get("outcome") != "played":
            errors.append(f"{where}: the director did not film it ({row.get('outcome') if row else 'no row in scenes.tsv'})")
            continue
        # The director files a scene it cannot film as asked under another station, with only a warning in its log:
        # round 67's 6.4a, a Birth whose body it could not place in a clade on a resumed run, came back an Arrival.
        if row.get("station") and row["station"] != e["camera"]:
            errors.append(f"{where}: the theatre filmed it as {row['station']}, not the plan's {e['camera']}; its log "
                          f"film/pass-{k}/theatre-{e['arm']}.log says why")
            continue
        source = Path(done["safari"][e["arm"]]) / (row["slug"] + ".mp4")
        if not source.is_file():
            errors.append(f"{where}: {source.name} is not in {source.parent}")
            continue
        # A clip is named by its content, never overwritten: Resolve links a file where it lies, so a
        # re-film written over the old name would silently change every cut already built from it.
        digest = sha256_of(source)
        clip = clips_dir / f"{e['shot']}-{digest[:10]}.mp4"
        if not clip.is_file() or sha256_of(clip) != digest:
            shutil.copyfile(source, clip)
        info = probe(clip)
        want = round(e["seconds"] * s["fps"]) + 1
        if abs(info["frames"] - want) > 3:
            errors.append(f"{where}: the clip holds {info['frames']} frames where {want} were planned")
            continue
        last, fps = info["frames"] - 1, s["fps"]
        a, b, length = e["action_in_s"], e["action_out_s"], last / fps
        times = [0, a, a + (b - a) / 4, a + (b - a) / 2, a + 3 * (b - a) / 4, b, (b + length) / 2, length]
        frames = []
        for t in times:
            f = min(last, max(0, round(t * fps)))
            frames.append(max(f, frames[-1] + 1) if frames else f)
        frames = [min(f, last) for f in frames]
        sheet(clip, clips_dir / f"{e['shot']}-sheet.png", sorted(set(frames)))
        luma = yavg(clip, clips_dir, f"{e['shot']}-luma.txt")
        jumps = yavg(clip, clips_dir, f"{e['shot']}-jump.txt", "tblend=all_mode=difference,")[1:]
        takes = take_frames(logs.get(e["arm"], ""), row["slug"])
        takes = takes if takes and sum(takes) == info["frames"] else None
        within = jumps_within_takes(jumps, takes)
        typical = middle(within) or 0
        ratio = round(max(within) / typical, 1) if typical > 0 and within else None
        w = min(info["width"] or 1920, 1920)
        unsuited = reference_unsuited(ref, w, info)
        if unsuited is None and w not in refs:
            refs[w] = reference(folder, s, w)
        rl = None if unsuited else refs[w]
        tone, edges = look(clip, clips_dir, f"{e['shot']}-look.txt", f"fps={LOOK_RATE},", width=w)
        tone, edges = middle(tone), (round(middle(edges), 2) if edges else None)
        rgb = colour(clip, f"fps={LOOK_RATE},")
        fog = e.get("fog")
        said = f": fog {fog_text(fog)}, the scene's own" if fog is not None else ""
        if fog is not None and not re.search("take [0-9]+ of " + re.escape(row["slug"] + said), logs.get(e["arm"], "")):
            errors.append(f"{where}: the theatre's log does not say its takes were filmed in fog {fog_text(fog)}")
            continue
        focus, side = e.get("focus"), e.get("side")
        log = logs.get(e["arm"], "")
        if s.get("snow") and not all(re.search(pattern, log) for pattern in snow_patterns(s["snow"])):
            errors.append(f"{where}: the theatre's log does not show the snow it was asked for ({snow_text(s['snow'])})")
            continue
        look_fault = look_log_fault(log + "\n" + launcher.get(e["arm"], ""))
        if look_fault:
            errors.append(f"{where}: {look_fault}")
            continue
        colour_fault = colour_log_fault(plan.get("colour"), log)
        if colour_fault:
            errors.append(f"{where}: {colour_fault}")
            continue
        if e.get("canopy"):
            canopy_fault = canopy_log_fault(row["slug"], log)
            if canopy_fault:
                errors.append(f"{where}: {canopy_fault}; its log film/pass-{k}/theatre-{e['arm']}.log")
                continue
        if skin_of(s) and not re.search(skin_pattern(s), log):
            errors.append(f"{where}: the theatre's log does not show the skin it was asked for ({skin_of(s)})")
            continue
        if focus is not None and not re.search("take [0-9]+ of " + re.escape(row["slug"]) + ": focus "
                                               + re.escape(focus["mode"]) + ".*, the scene's own", log):
            errors.append(f"{where}: the theatre's log does not say its takes were focused {focus['mode']}, the scene's own")
            continue
        if side is not None and not re.search("take [0-9]+ of " + re.escape(row["slug"]) + " at .*: portrait of "
                                              ".*subject a third off centre on the " + side, log):
            errors.append(f"{where}: the theatre's log does not put its subject on the {side}")
            continue
        record = {"shot": e["shot"], "key": e["key"], "pass": k, "file": f"clips/{clip.name}",
                  "sha256": sha256_of(clip), "frames": info["frames"], "fps": fps, "width": info["width"],
                  "height": info["height"], "run": e["arm"], "clip_start_s": e["clip_start_s"],
                  "action_in_s": a, "action_out_s": b, "handle_before_s": e["handle_before_s"],
                  "handle_after_s": e["handle_after_s"], "provenance": row.get("provenance", ""),
                  "luma": round(sum(luma) / len(luma), 2) if luma else None, "jump_ratio": ratio, "takes": takes,
                  "tone": tone, "edges": edges, "look_width": w,
                  "reference_look": {k: rl[k] for k in LOOK_KEYS} if rl else None, "rgb": rgb, "fog": fog, "focus": focus, "side": side,
                  "look": plan["settings"].get("theatre_look"), "stream": bool(plan["settings"].get("theatre_stream")),
                  "snow": plan["settings"].get("snow"), "skin": skin_of(plan["settings"]) or None,
                  "colour": (plan.get("colour") or {}).get("name"),
                  "sheet":f"clips/{e['shot']}-sheet.png", "sheet_times_s": [round(f / fps, 2) for f in sorted(set(frames))]}
        new.append(record)
        notes, flags = [], []
        # A run recorded on the farm is filmed in a theatre that cannot replay it exactly, so COUSIN is right there.
        if record["provenance"] not in ("FAITHFUL", "COUSIN"):
            notes.append(f"provenance {record['provenance'] or 'none'}")
            flags.append("provenance")
        if ratio is not None and ratio >= JUMP_FLAG:
            notes.append(f"a frame-to-frame change {ratio} times the clip's median")
            flags.append("jump")
        if takes is None:
            notes.append("the take boundaries were not read (" + ("the theatre's log has no take line for this scene"
                         if not take_frames(logs.get(e["arm"], ""), row["slug"]) else
                         "the takes' frames do not sum to the clip's frames") + "), so the largest change is over the whole clip")
        if record["luma"] is not None and record["luma"] < ref["luma"]:
            flags.append("dark")
            notes.append(f"dark: its luma {record['luma']:g} is under the reference's, {ref['luma']:g}")
        if rl is not None and tone is not None and tone < rl["tone_p10"]:
            flags.append("flat")
            notes.append(f"flat: its tone spread {tone:g} is under the reference's tenth percentile, {rl['tone_p10']:g}, "
                         f"at {w} wide")
        if rl is not None and edges is not None and edges < rl["edges_p10"]:
            flags.append("soft")
            notes.append(f"soft: its edges {edges:g} are under the reference's tenth percentile, {rl['edges_p10']:g}, "
                         f"at {w} wide")
        record["flags"] = flags
        if rl is None:
            notes.append(f"tone and edges not compared: {unsuited}; judge them by eye against the reference sheet")
            versus = f"tone {tone}, edges {edges} at {w} wide, not compared"
        else:
            versus = (f"tone {tone} against the reference's {rl['tone']}, edges {edges} against the reference's "
                      f"{rl['edges']} at {w} wide")
        lines.append(f"{where}: {info['frames']} frames, luma {record['luma']} against the reference's {ref['luma']}, "
                     f"{versus}"
                     + (f", fog {fog_text(fog)}" if fog is not None else "")
                     + f", largest change {ratio} times the median" + (f"; look: {', '.join(notes)}" if notes else ""))
    known = {x["id"] for x in shots["shots"] if "run" in x}
    keys = {x["id"]: shot_key(x, s, colours) for x in shots["shots"] if "run" in x}
    old = (read_json(folder / "clips.json") or {}).get("clips", [])
    fresh = {c["shot"] for c in new}
    kept = [c for c in old if c["shot"] in known and c["shot"] not in fresh]
    clips = sorted(kept + new, key=lambda c: [int(x) if x.isdigit() else x for x in re.split("([0-9]+)", c["shot"])])
    write_json(folder / "clips.json", {"schema": SCHEMA, "shots_sha256": sha256_of(folder / "shots.json"),
                                       "reference": ref, "clips": clips})
    missing = sorted(i for i in known if not any(c["shot"] == i and c["key"] == keys[i] for c in clips))
    head = [f"pass {k}: {len(new)} clips collected; clips.json holds {len(clips)} of the {len(known)} shots with footage"]
    if missing:
        errors.append("shots with footage and no clip of their current shot: " + ", ".join(missing))
    return ("CLIPS: FAIL" if errors else "CLIPS: PASS"), head + lines + [f"ERROR {x}" for x in errors]


# The bar a take is judged against, word for word as the machine's coordinator wrote it (2026-11-07).
BAR = [
    "0. Amazing, judged by eye at full size: light that draws the eye, colour, depth, a subject worth looking at, a composition that holds as a nature-film still. \"As good as the last film\" fails.",
    "1. Light: luminous. As a floor, mean luma no lower than rounds-50-57 v3's, 60 to 70. The surface glows above, never a dark ceiling. Shadow sides lit by the water. Shafts kept.",
    "2. Water: clear teal, not navy-black. Haze only far off.",
    "3. Subject: every take has one. For a portrait or event it fills at least a fifth of the width, at the centre or on a third, never cut by the frame. The event happens in frame.",
    "4. Camera: toward or across the light. Never down into empty dark water or level over an empty floor. A descent ends on life.",
    "5. Test takes first: every shot at 960x540 before any 4K pass. Slides of each take, its sheet and a full-size frame, judged against this bar by eye first, directed and refilmed until it passes. The owner sees only passing takes, with their slides.",
]
SLIDE_CSS = """:root { --bg: #0e1418; --panel: #172128; --ink: #e8eef2; --dim: #9fb0bb; --flag: #ef7b6b; }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--ink); font: 17px/1.5 system-ui, Segoe UI, sans-serif; }
.slide { display: none; min-height: 100vh; padding: 28px 40px 70px; }
.slide.on { display: block; }
h1 { font-size: 30px; margin: 0 0 6px; } h2 { font-size: 24px; margin: 0 0 4px; }
.sub { color: var(--dim); margin: 0 0 16px; }
.row { display: flex; gap: 22px; align-items: flex-start; flex-wrap: wrap; }
.col { flex: 1 1 520px; min-width: 0; }
img { width: 100%; height: auto; border-radius: 4px; display: block; }
.cap { color: var(--dim); font-size: 14px; margin: 4px 0 14px; }
.panel { background: var(--panel); border-radius: 6px; padding: 14px 18px; margin-bottom: 14px; }
.flag { display: inline-block; padding: 2px 10px; border-radius: 4px; font-weight: 600; font-size: 14px; margin-right: 6px; background: var(--flag); color: #10161a; }
table { border-collapse: collapse; width: 100%; } td, th { padding: 6px 10px; border-bottom: 1px solid #2a3740; text-align: left; }
nav { position: fixed; bottom: 0; left: 0; right: 0; display: flex; justify-content: space-between; align-items: center; padding: 10px 40px; background: #0b1013; color: var(--dim); font-size: 14px; }
nav button { background: #22313a; color: var(--ink); border: 0; padding: 6px 14px; border-radius: 4px; cursor: pointer; font-size: 14px; }"""
SLIDE_JS = """var s = document.querySelectorAll('.slide'), i = 0, c = document.getElementById('count');
function show(n) { i = Math.max(0, Math.min(s.length - 1, n)); s.forEach(function (e, j) { e.classList.toggle('on', j === i); });
  c.textContent = (i + 1) + ' / ' + s.length; window.scrollTo(0, 0); }
document.addEventListener('keydown', function (e) {
  if (e.key === 'ArrowRight' || e.key === 'PageDown' || e.key === ' ') show(i + 1);
  else if (e.key === 'ArrowLeft' || e.key === 'PageUp') show(i - 1);
  else if (e.key === 'Home') show(0); else if (e.key === 'End') show(s.length - 1); });
document.getElementById('prev').onclick = function () { show(i - 1); };
document.getElementById('next').onclick = function () { show(i + 1); };
show(0);"""


def esc(x):
    return str(x).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def table_html(rows):
    return "".join(f"<tr><th>{esc(a)}</th><td>{esc(b)}</td></tr>" for a, b in rows)


def tags_html(flags):
    return "".join(f'<span class="flag">{esc(f)}</span>' for f in (flags or [])) or "no flags"


def deck_page(title, first, slides):
    """A deck's page: its first slide, the take slides and the bar last, with the shared style and keys."""
    last = ('<section class="slide">\n<h2>The bar</h2>\n<div class="panel"><ul>'
            + "".join(f"<li>{esc(b)}</li>" for b in BAR) + "</ul></div>\n</section>")
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
            f"<title>{esc(title)}</title>\n<style>\n{SLIDE_CSS}\n</style>\n</head>\n<body>\n"
            + first + "\n" + "\n".join(slides) + "\n" + last
            + '\n<nav><button id="prev">Previous</button><span id="count"></span><button id="next">Next</button></nav>\n'
            f"<script>\n{SLIDE_JS}\n</script>\n</body>\n</html>\n")


def cmd_slides(folder, k):
    """A deck of pass k's collected takes: a first slide, one slide a take in the shot list's order, and the bar last.
    Its images are copies, so the deck survives a later collect."""
    out = pass_dir(folder, k)
    plan = read_json(out / "plan.json")
    if plan is None or read_json(out / "done.json") is None:
        raise Refused(f"pass {k} has not been planned and run to its end")
    if record_only_pass(plan, read_json(out / "done.json")):
        raise Refused(f"pass {k} recorded windows only (record_only) and has no takes to make slides of")
    data, shots = read_json(folder / "clips.json"), read_json(folder / "shots.json")
    mine = {c["shot"]: c for c in (data or {}).get("clips", []) if c.get("pass") == k}
    if not mine:
        raise Refused(f"pass {k} is not collected: clips.json holds no clip of it; collect it first")
    ref = data.get("reference") or {}
    deck, img = out / "slides", out / "slides" / "img"
    order = [x for x in shots["shots"] if isinstance(x, dict) and x.get("id") in mine]
    for x in order:
        c = mine[x["id"]]
        for what in ("file", "sheet"):
            if not (folder / c[what]).is_file():
                raise Refused(f"shot {x['id']}: {c[what]} is not there; collect pass {k} again")
    if deck.exists():
        shutil.rmtree(deck)
    img.mkdir(parents=True)
    slides = []
    for x in order:
        sid, c = x["id"], mine[x["id"]]
        shutil.copyfile(folder / c["sheet"], img / f"{sid}-sheet.png")
        ffmpeg(["-ss", f"{(c['action_in_s'] + c['action_out_s']) / 2:.3f}", "-i", str(folder / c["file"]), "-frames:v", "1",
                str(img / f"{sid}-frame.png")])
        if "reference_look" in c:
            rl, wide = c["reference_look"], f"at {c.get('look_width')} wide"
        else:
            rl, wide = ref, "at 1920 wide"
        if rl:
            tone_row = f"{c['tone']} (reference {rl.get('tone')}, tenth percentile {rl.get('tone_p10')}, {wide})"
            edge_row = f"{c['edges']} (reference {rl.get('edges')}, tenth percentile {rl.get('edges_p10')}, {wide})"
        else:
            tone_row = f"{c['tone']} ({wide}; not compared with the reference, judge by eye)"
            edge_row = f"{c['edges']} ({wide}; not compared with the reference, judge by eye)"
        rows = [("luma", f"{c['luma']} (reference {ref.get('luma')})"), ("tone", tone_row), ("edges", edge_row),
                ("largest change", f"{c['jump_ratio']} times the median"),
                ("provenance", c.get("provenance") or "none"), ("size", f"{c['width']}x{c['height']}")]
        table, tags = table_html(rows), tags_html(c.get("flags"))
        slides.append(
            f'<section class="slide">\n<h2>{esc(sid)} &middot; {esc(x.get("camera", ""))}</h2>\n'
            f'<p class="sub">run {esc(c["run"])}, from {c["clip_start_s"]:g} s of the run; the action lies at '
            f'{c["action_in_s"]:g} to {c["action_out_s"]:g} s of the clip</p>\n'
            f'<div class="panel"><b>The viewer should see:</b> {esc(x.get("see", ""))}</div>\n'
            f'<div class="row"><div class="col"><img src="img/{esc(sid)}-sheet.png" alt="the sheet of {esc(sid)}">'
            f'<p class="cap">The sheet: eight frames, the first, the action in five steps, the handle after, the last.</p></div>\n'
            f'<div class="col"><img src="img/{esc(sid)}-frame.png" alt="a full-size frame of {esc(sid)}">'
            f'<p class="cap">A full-size frame, the action\'s middle, at {c["width"]}x{c["height"]}.</p>\n'
            f'<table>{table}</table><p>{tags}</p></div></div>\n</section>')
    first = (f'<section class="slide">\n<h1>{esc(folder.name)}</h1>\n<p class="sub">Pass {k}, {esc(plan.get("size", ""))}, '
             f'{len(order)} takes, {esc(now()[:10])}</p>\n<div class="panel">Each slide holds a take: what the viewer should '
             "see, the take's sheet, a full-size frame and its numbers. The last slide is the bar. Arrow keys move.</div>\n</section>")
    html = deck_page(f"{folder.name} pass {k}", first, slides)
    (deck / "index.html").write_text(html, encoding="utf-8")
    return "SLIDES: READY", [f"pass {k}: {len(order)} takes, {len(order) + 2} slides", f"deck: {deck / 'index.html'}"]


STILLS_MAX = 5                     # frames a take's stills may hold: a move's 0, 25, 50, 75 and 100 % (the coordinator, 2026-11-09)
STILLS_TOP = {"stills", "head", "provenance", "takes"}


def png_size(path):
    """A PNG's (width, height) from its header, or None when the file is not a PNG."""
    with open(path, "rb") as f:
        head = f.read(24)
    if len(head) < 24 or head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        return None
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def stills_manifest(folder, j, src, s, shots):
    """The stills manifest read and checked against the shot list and the film's size, and the takes it holds in the
    shot list's order and then the variant's letter. Every fault is a line of its own; none stops at the first."""
    m = None
    try:
        m = read_json(src / "manifest.json")
    except (ValueError, OSError):
        m = None
    if m is None:
        raise Refused(f"{src / 'manifest.json'} is missing or cannot be read")
    if not isinstance(m, dict) or not isinstance(m.get("takes"), list):
        raise Refused(f"{src / 'manifest.json'} is not in its form: an object with stills, head, provenance and a list of takes")
    if m.get("stills") != j or isinstance(m.get("stills"), bool):
        raise Refused(f"the manifest is for stills round {m.get('stills')!r}, not {j}")
    errors = []
    if m.get("provenance") not in ("FAITHFUL", "COUSIN"):
        errors.append(f"the manifest's provenance is {m.get('provenance')!r}, not FAITHFUL or COUSIN")
    known = {x["id"]: x for x in shots["shots"] if isinstance(x, dict) and "id" in x}
    order = [x["id"] for x in shots["shots"] if isinstance(x, dict) and "id" in x]
    want = SIZE.fullmatch(str(s["size"]))
    wanted = (int(want.group(1)), int(want.group(2)))
    seen, takes = set(), []
    for n, t in enumerate(m["takes"], 1):
        if not isinstance(t, dict):
            errors.append(f"take {n} is not an object")
            continue
        shot, variant = t.get("shot"), t.get("variant")
        name = f"{shot}:{variant}"
        x = known.get(shot)
        if x is None:
            errors.append(f"take {name}: shot {shot!r} is not in shots.json")
            continue
        if not x.get("run"):
            errors.append(f"take {name}: shot {shot} has no run in shots.json")
            continue
        if t.get("run") != x["run"]:
            errors.append(f"take {name}: its run {t.get('run')!r} is not shot {shot}'s, {x['run']}")
        if not isinstance(variant, str) or not re.fullmatch("[A-Z]", variant):
            errors.append(f"take {name}: the variant is one capital letter")
            continue
        if (shot, variant) in seen:
            errors.append(f"take {name} is given twice")
            continue
        seen.add((shot, variant))
        dials = t.get("dials", {})
        if not isinstance(dials, dict) or not all(isinstance(k, str) and isinstance(v, (str, int, float))
                                                  for k, v in dials.items()):
            errors.append(f"take {name}: dials is an object of dial names and values")
            dials = {}
        frames = t.get("frames")
        if not isinstance(frames, list) or not 1 <= len(frames) <= STILLS_MAX:
            errors.append(f"take {name}: it holds {len(frames) if isinstance(frames, list) else 'no list of'} frames, "
                          f"1 to {STILLS_MAX} are allowed")
            continue
        before, okay = None, True
        for i, fr in enumerate(frames, 1):
            fr = fr if isinstance(fr, dict) else {}
            file, second = fr.get("file"), fr.get("second")
            where = f"take {name}, frame {i}"
            path = src / file if isinstance(file, str) and file and Path(file).name == file else None
            if path is None or not path.is_file():
                errors.append(f"{where}: {file!r} is not a file in {src}")
                okay = False
                continue
            size = png_size(path)
            if size is None:
                errors.append(f"{where}: {file} is not a PNG")
                okay = False
            elif size != wanted:
                errors.append(f"{where}: {file} is {size[0]}x{size[1]}, the film's size is {s['size']}")
                okay = False
            if not number(second):
                errors.append(f"{where}: its second is not a number")
                okay = False
                continue
            if not (x["run_from_s"] <= second <= x["run_to_s"]):
                errors.append(f"{where}: second {second:g} lies outside shot {shot}'s {x['run_from_s']:g} to "
                              f"{x['run_to_s']:g} s of the run")
                okay = False
            if before is not None and second < before:
                errors.append(f"{where}: second {second:g} comes before the frame before it, {before:g}; "
                              "frames go in time order")
                okay = False
            before = second
        if okay:
            takes.append((order.index(shot), variant, x, t, dials))
    if errors:
        raise Refused(*errors)
    return m, sorted(takes, key=lambda r: (r[0], r[1])), wanted


def cmd_stills(folder, j, src):
    """A stills round: the theatre lane's stills of a shot under up to three variants of dials, measured as collect
    measures a clip and made into a deck for the reviewer. Written once to film/stills-<j>/; the pass is not touched."""
    lines = []
    s = film_settings(folder, lines)
    shots = read_json(folder / "shots.json")
    if shots is None:
        raise Refused(f"{folder / 'shots.json'} is not there")
    out = folder / "film" / f"stills-{j}"
    # The manifest is read first, so its faults are named before a round that exists already.
    manifest, takes, (w0, h0) = stills_manifest(folder, j, src, s, shots)
    if out.exists():
        raise Refused(f"film/stills-{j}/ exists already; a stills round is made once, so the next is stills round {j + 1}")
    w = min(w0, 1920)
    ref = reference(folder, s)
    unsuited = reference_unsuited(ref, w, {"width": w0, "height": h0})
    rl = None if unsuited else reference(folder, s, w)
    try:
        (out / "frames").mkdir(parents=True)
        slides = out / "slides"
        (slides / "img").mkdir(parents=True)
        rows = []
        for _, variant, x, t, dials in takes:
            sid = x["id"]
            name = f"{sid}-{variant}"
            frames, lumas, tones, edges = [], [], [], []
            for i, fr in enumerate(t["frames"], 1):
                shutil.copyfile(src / fr["file"], out / "frames" / fr["file"])
                path = out / "frames" / fr["file"]
                lu = yavg(path, out, f"{name}-{i}-luma.txt")
                tn, ed = look(path, out, f"{name}-{i}-look.txt", width=w)
                lumas.append(sum(lu) / len(lu))
                tones.append(tn[0])
                edges.append(ed[0])
                frames.append({"file": f"film/stills-{j}/frames/{fr['file']}", "second": fr["second"],
                               "what": fr.get("what", ""), "luma": round(lumas[-1], 2), "tone": round(tones[-1], 2),
                               "edges": round(edges[-1], 2)})
            luma = round(sum(lumas) / len(lumas), 2)
            tone, edge = round(sum(tones) / len(tones), 2), round(sum(edges) / len(edges), 2)
            flags = []
            if luma < ref["luma"]:
                flags.append("dark")
            if rl is not None and tone < rl["tone_p10"]:
                flags.append("flat")
            if rl is not None and edge < rl["edges_p10"]:
                flags.append("soft")
            paths = [out / "frames" / fr["file"] for fr in t["frames"]]
            if len(paths) == 1:
                shutil.copyfile(paths[0], slides / "img" / f"{name}-sheet.png")
            else:
                scale = ";".join(f"[{n}:v]scale=480:-2[a{n}]" for n in range(len(paths)))
                joined = "".join(f"[a{n}]" for n in range(len(paths)))
                ffmpeg([a for p in paths for a in ("-i", str(p))] + ["-filter_complex",
                        f"{scale};{joined}hstack=inputs={len(paths)}", "-frames:v", "1", str(slides / "img" / f"{name}-sheet.png")])
            shutil.copyfile(paths[len(paths) // 2], slides / "img" / f"{name}-frame.png")
            rows.append({"take": f"{sid}:{variant}", "shot": sid, "variant": variant, "run": t["run"], "dials": dials,
                         "provenance": manifest["provenance"], "size": f"{w0}x{h0}", "frames": frames, "luma": luma,
                         "tone": tone, "edges": edge, "look_width": w,
                         "reference_look": {key: rl[key] for key in LOOK_KEYS} if rl else None, "flags": flags,
                         "camera": x.get("camera", ""), "see": x.get("see", "")})
        data = {"stills": j, "head": manifest.get("head", ""), "provenance": manifest["provenance"], "made": now(),
                "size": f"{w0}x{h0}", "reference": ref if rl is None else {**ref, **{k: rl[k] for k in LOOK_KEYS}, "look_width": w},
                "takes": [{k: v for k, v in r.items() if k not in ("camera", "see")} for r in rows]}
        write_json(out / "stills.json", data)
        deck = []
        for r in rows:
            if rl:
                wide = f"at {w} wide"
                tone_row = f"{r['tone']} (reference {rl.get('tone')}, tenth percentile {rl.get('tone_p10')}, {wide})"
                edge_row = f"{r['edges']} (reference {rl.get('edges')}, tenth percentile {rl.get('edges_p10')}, {wide})"
            else:
                tone_row = f"{r['tone']} (at {w} wide; not compared with the reference, judge by eye)"
                edge_row = f"{r['edges']} (at {w} wide; not compared with the reference, judge by eye)"
            table = table_html([("luma", f"{r['luma']} (reference {ref.get('luma')})"), ("tone", tone_row),
                                ("edges", edge_row), ("stills", str(len(r["frames"]))), ("provenance", r["provenance"]),
                                ("size", r["size"])])
            seconds = ", ".join(f"{fr['second']:g}" for fr in r["frames"])
            what = ", ".join(f"{fr['what']} ({fr['second']:g} s)" for fr in r["frames"] if fr["what"])
            dial = ("; ".join(f"{k} {v}" for k, v in r["dials"].items())) or "the film look's own"
            name = f"{r['shot']}-{r['variant']}"
            deck.append(
                f'<section class="slide">\n<h2>{esc(r["shot"])}:{esc(r["variant"])} &middot; {esc(r["camera"])}</h2>\n'
                f'<p class="sub">run {esc(r["run"])}, stills at {esc(seconds)} s of the run; dials: {esc(dial)}</p>\n'
                f'<div class="panel"><b>The viewer should see:</b> {esc(r["see"])}</div>\n'
                f'<div class="row"><div class="col"><img src="img/{esc(name)}-sheet.png" alt="the sheet of {esc(name)}">'
                f'<p class="cap">The sheet: {len(r["frames"])} stills side by side in time order'
                f'{esc(": " + what) if what else ""}.</p></div>\n'
                f'<div class="col"><img src="img/{esc(name)}-frame.png" alt="the middle still of {esc(name)}">'
                f'<p class="cap">The middle still at full size, {esc(r["size"])}.</p>\n'
                f'<table>{table}</table><p>{tags_html(r["flags"])}</p></div></div>\n</section>')
        shot_ids = {r["shot"] for r in rows}
        first = (f'<section class="slide">\n<h1>{esc(folder.name)}</h1>\n<p class="sub">Stills {j}, {w0}x{h0}, '
                 f'{len(rows)} takes of {len(shot_ids)} shots, head {esc(data["head"])}, {esc(data["provenance"])}, '
                 f'{esc(now()[:10])}</p>\n<div class="panel">Each slide holds a take, one variant of a shot: what the viewer '
                 "should see, its dials, its stills side by side, the middle still at full size and the numbers. Judge each "
                 "take against the bar alone. The last slide is the bar. Arrow keys move.</div>\n</section>")
        (slides / "index.html").write_text(deck_page(f"{folder.name} stills {j}", first, deck), encoding="utf-8")
    except BaseException:
        shutil.rmtree(out, ignore_errors=True)
        raise
    head = [f"stills {j}: {len(rows)} takes of {len(shot_ids)} shots, {len(rows) + 2} slides"]
    notes = [f"{r['take']}: luma {r['luma']}, tone {r['tone']}, edges {r['edges']}"
             + (f", flags {', '.join(r['flags'])}" if r["flags"] else "") for r in rows]
    return "STILLS: READY", head + lines + notes + [f"deck: {slides / 'index.html'}"]


def run_pinned(args, cores):
    """One child at below-normal priority on the given cores (none: unpinned), its output kept; (code, last line)."""
    proc = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")
    pin(proc.pid, cores)
    text = proc.communicate()[0] or ""
    return proc.returncode, (text.strip().splitlines() or [""])[-1]


def clip_seconds(clip):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of",
                        "default=noprint_wrappers=1:nokey=1", str(clip)], capture_output=True, text=True)
    try:
        return float(r.stdout.strip().splitlines()[0])
    except (ValueError, IndexError):
        return 0.0


def film_sheet(clip, s):
    """The theatre checkout's film-sheet.py on the clip, as theatre-safari.ps1 does; its last line, or None."""
    sheet = theatre_root(s) / "scripts" / "film-sheet.py"
    if not sheet.is_file():
        return None
    r = subprocess.run([sys.executable, str(sheet), str(clip)], capture_output=True, text=True, errors="replace")
    return ((r.stdout + r.stderr).strip().splitlines() or [""])[-1]


def take_dirs(scene):
    """A scene's take folders in number order, as (number, path)."""
    found = [(int(p.name[5:]), p) for p in scene.glob("take-*") if p.is_dir() and p.name[5:].isdigit()]
    return sorted(found)


def join_parts(scene, parts, clip, cores):
    """A concat join through parts.txt as theatre-safari.ps1 writes it; the error text, or None."""
    listing = scene / "parts.txt"
    listing.write_text("".join("file '" + str(p).replace(chr(92), "/") + "'\n" for p in parts), encoding="ascii")
    code, last = run_pinned(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i",
                             str(listing), "-c", "copy", "-movflags", "+faststart", str(clip)], cores)
    return None if code == 0 else (last or "ffmpeg failed")


def encode_scene(row, safari, log, s, fps, cores):
    """One played scene: (lines to print, failure text or None, True when it made a clip)."""
    slug, station = row["slug"], row.get("station", "")
    scene, clip = safari / slug, safari / f"{slug}.mp4"
    if not slug.startswith("story-"):
        return [], f"{slug}: not a story scene (a film's slugs start story-)", False
    if clip.is_file():
        return [], None, False
    takes = [(n, d, sorted(d.glob("frame-*.png"))) for n, d in take_dirs(scene)]
    takes = [t for t in takes if t[2]]
    # The frames on disk must be what the theatre says it drew, take for take.
    if log is None:
        return [], f"{slug}: no theatre log to check its frames against", False
    drawn = take_frames(log, slug)
    if drawn is None:
        return [], f"{slug}: the theatre's log holds no take lines for it", False
    diffs = []
    if len(takes) != len(drawn):
        diffs.append(f"{len(takes)} take folders with frames, the log names {len(drawn)}")
    on_disk = {n: len(fr) for n, _, fr in takes}
    for n, f in enumerate(drawn, 1):
        if on_disk.get(n, 0) != f:
            diffs.append(f"take {n}: {on_disk.get(n, 0)} frames on disk, the log says {f}")
    for n in sorted(set(on_disk) - set(range(1, len(drawn) + 1))):
        diffs.append(f"take {n}: {on_disk[n]} frames on disk, the log does not name it")
    if diffs:
        return [], f"{slug}: " + "; ".join(diffs), False
    parts = []
    for n, d, _ in takes:
        part = scene / f"take-{n}.mp4"
        code, last = run_pinned(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-framerate", str(fps),
                                 "-start_number", "0", "-i", str(d / "frame-%06d.png"), "-c:v", "libx264",
                                 "-pix_fmt", "yuv420p", "-crf", "18", "-r", str(fps), "-threads", "4",
                                 "-movflags", "+faststart", str(part)], cores)
        if code != 0 or not part.is_file():
            return [], f"{slug}: take-{n}: ffmpeg failed: {last}", False
        parts.append(part)
    if len(parts) == 1:
        shutil.copyfile(parts[0], clip)
    elif station == "Time":
        first, second = parts[-2], parts[-1]
        faded = clip if len(parts) == 2 else scene / "time-xfade.mp4"
        offset = f"{max(0.0, clip_seconds(first) - 1.0):.3f}".rstrip("0").rstrip(".")
        code, last = run_pinned(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(first), "-i", str(second),
                                 "-filter_complex", f"[0:v][1:v]xfade=transition=fade:duration=1:offset={offset},format=yuv420p[v]",
                                 "-map", "[v]", "-c:v", "libx264", "-crf", "18", "-r", str(fps), "-threads", "4",
                                 "-movflags", "+faststart", str(faded)], cores)
        if code != 0:
            return [], f"{slug}: the crossfade failed: {last}", False
        if len(parts) > 2:
            joined = join_parts(scene, parts[:-2] + [faded], clip, cores)
            if joined:
                return [], f"{slug}: the join after the crossfade failed: {joined}", False
    else:
        joined = join_parts(scene, parts, clip, cores)
        if joined:
            return [], f"{slug}: the join failed: {joined}", False
    lines = [f"  {clip.name:<48} {len(parts)} take(s), {clip_seconds(clip):.1f} s"]
    sheet = film_sheet(clip, s)
    if sheet is not None:
        lines.append(f"    sheet: {sheet}")
    for _, _, frames in takes:      # only once the scene's clip is made, as -DeleteFrames does
        for f in frames:
            f.unlink()
    return lines, None, True


def cmd_encode(folder, k):
    """Encode a pass whose theatre ended at its own wall, before theatre-safari.ps1 reached its encode. The theatre
    flushes its frames and writes scenes.tsv, then quits with code 1, and the script exits before its encode block.
    THIS MUST AGREE WITH the section headed "the encode" of scripts/theatre-safari.ps1 (ffmpeg arguments, the Time
    station's crossfade, the join, the clip's name, the frame delete): change one and change the other. It adds a
    check the script lacks: a scene is encoded only when its frames on disk match the take lines of the theatre's log."""
    out = pass_dir(folder, k)
    plan, done = read_json(out / "plan.json"), read_json(out / "done.json")
    if plan is None or done is None:
        raise Refused(f"pass {k} has not been planned and run to its end")
    if record_only_pass(plan, done):
        raise Refused(f"pass {k} recorded windows only (record_only) and has no frames to encode")
    s = plan["settings"]
    fps, cores = s["fps"], s.get("theatre_cores")
    lines, failed, made = [], [], 0
    for arm, d in done["safari"].items():
        safari = Path(d)
        lines.append(f"{arm}: {safari}")
        sheet = safari / "scenes.tsv"
        if not sheet.is_file():
            lines.append(f"  no scenes.tsv in {safari}; passed over")
            continue
        rows = read_tsv(sheet)
        copy = out / f"theatre-{arm}.log"
        if not copy.is_file():
            copy = theatre_root(s) / "scratch" / "logs" / f"theatre-safari-{arm}.log"
        log = copy.read_text(encoding="utf-8", errors="replace") if copy.is_file() else None
        known = {r.get("slug") for r in rows}
        for sd in sorted(safari.glob("story-*")):
            if sd.is_dir() and sd.name not in known and take_dirs(sd):
                lines.append(f"  {sd.name} : refused, no row in scenes.tsv")
                failed.append(f"{arm} {sd.name}: no row in scenes.tsv")
        for row in rows:
            if row.get("outcome") != "played":
                lines.append(f"  {row.get('slug')} : {row.get('outcome')}")
                continue
            got, fault, did = encode_scene(row, safari, log, s, fps, cores)
            lines.extend(got)
            made += did
            if fault:
                lines.append(f"  refused or failed: {fault}")
                failed.append(f"{arm} {fault}")
    if failed:
        verdict, lines = "ENCODE: FAILED", [f"FAILED {f}" for f in failed] + lines
    else:
        verdict = "ENCODE: DONE" if made else "ENCODE: NOTHING TO ENCODE"
    (out / "encode.log").write_text("\n".join([verdict] + lines) + "\n", encoding="utf-8")
    return verdict, lines


def cmd_stop(folder, k, reason):
    """End a running pass cleanly: write the stop file in the guard's one-line form. A safari given -StopFile
    completes the scene under way, writes scenes.tsv and exits 1; the runs not yet filmed are skipped."""
    out = pass_dir(folder, k)
    started = read_json(out / "started.json")
    if started is None:
        raise Refused(f"pass {k} has not been started")
    if read_json(out / "done.json") is not None:
        raise Refused(f"pass {k} has ended already (film/pass-{k}/done.json exists)")
    reason = " ".join((reason or "").split())
    if not reason:
        raise Refused("a reason is needed: stop <folder> <k> <reason...>")
    existing = stop_text(folder)
    if existing is not None:
        raise Refused(f"the stop file {stop_file(folder)} exists already and says: {existing}")
    plan = read_json(out / "plan.json")
    given = bool(plan) and safari_takes_stop_file(plan["settings"])
    f = stop_file(folder)
    f.parent.mkdir(parents=True, exist_ok=True)
    f.write_text(f"{now()} {reason} (a clean stop: the scene under way completes, then the theatre writes "
                 "scenes.tsv)" + chr(10), encoding="utf-8")
    lines = [f"wrote {f}"]
    if given:
        lines.append("the pass's safari was given the stop file: the scene under way completes, then the theatre "
                     "writes scenes.tsv and exits; the runs not yet filmed are skipped. Then encode, then collect.")
    else:
        lines.append("the pass's safari was not given the stop file (the theatre checkout's theatre-safari.ps1 has no "
                     "-StopFile parameter): the theatre will not see it until its run ends, and only the runs not yet "
                     "filmed are skipped.")
    return "STOP: WRITTEN", lines


COMMANDS = {"plan": (cmd_plan, "FILM PLAN: REFUSED", ["folder", "n"]),
            "machine": (cmd_machine, "MACHINE: BUSY", ["folder"]),
            "start": (cmd_start, "FILM START: REFUSED", ["folder", "n"]),
            "run": (cmd_run, "FILM RUN: FAILED", ["folder", "n"]),
            "wait": (cmd_wait, "FILMING: FAILED", ["folder", "n", "n"]),
            "collect": (cmd_collect, "CLIPS: REFUSED", ["folder", "n"]),
            "slides": (cmd_slides, "SLIDES: REFUSED", ["folder", "n"]),
            "stills": (cmd_stills, "STILLS: REFUSED", ["folder", "n", "path"]),
            "encode": (cmd_encode, "ENCODE: REFUSED", ["folder", "n"]),
            "stop": (cmd_stop, "STOP: REFUSED", ["folder", "n", "text"])}
PASSING = ("WRITTEN", "READY", "NOTHING TO FILM", "NOTHING TO ENCODE", "FREE", "STARTED", "DONE", "PASS")


def main(argv):
    started = time.monotonic()
    spec = COMMANDS.get(argv[1]) if len(argv) > 1 else None
    rest = bool(spec) and spec[2][-1] == "text"      # a last word list, joined; an empty one is the command's to refuse
    if rest:
        argv = argv[:2 + len(spec[2]) - 1] + [" ".join(argv[2 + len(spec[2]) - 1:])]
    if spec is None or len(argv) != 2 + len(spec[2]) or not all(
            kind in ("folder", "text", "path") or (v.isdigit() and int(v) >= 1) for kind, v in zip(spec[2], argv[2:])):
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, refused, kinds = spec
    args = [Path(v).resolve() if kind in ("folder", "path") else v if kind == "text" else int(v)
            for kind, v in zip(kinds, argv[2:])]
    if not args[0].is_dir():
        verdict, lines = refused, [f"cannot read the film's folder {args[0]}"]
    else:
        try:
            verdict, lines = command(*args)
        except Refused as exc:
            verdict, lines = refused, exc.lines
        except (ValueError, TypeError, KeyError, IndexError, AttributeError, OSError) as exc:
            verdict, lines = refused, [f"a file is not in its form or cannot be read: {type(exc).__name__}: {exc}"]
    print(verdict)
    for line in lines:
        print(line)
    print(f"took {time.monotonic() - started:.1f} s")
    return 0 if verdict.endswith(PASSING) else 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
