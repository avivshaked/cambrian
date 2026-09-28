#!/usr/bin/env python3
"""The filming step's tool, for the create-story-video skill (filming.md).

    python film.py plan <folder> <k>               plan pass k: the director's story and the farm's windows
    python film.py machine <folder>                say whether the machine and the render worker are free
    python film.py start <folder> <k>              start pass k detached: record its windows, then film them
    python film.py run <folder> <k>                the detached job itself; only start runs it
    python film.py wait <folder> <k> <minutes>     wait for pass k to end, for at most the minutes given
    python film.py collect <folder> <k>            copy pass k's clips into clips/, make their sheets, write clips.json

The first line each command prints is its verdict, as filming.md reads it. The last line says how long the
command took. Exit status: 0 on READY, NOTHING TO FILM, FREE, STARTED, DONE or PASS, 1 otherwise, 2 on a
wrong command. It uses the standard library, ffmpeg and ffprobe, and PowerShell to start the detached job.
"""

import hashlib
import importlib.util
import json
import math
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
EPS = 0.0015
WORLD = {"Arrival", "Descent", "Floor", "Card"}
CLOSE = {"Portrait", "Birth"}      # the cameras the theatre focuses on a subject (SafariPlans' focusOn)
# What a close shot's own focus may hold, as the theatre's story reader takes it (SafariStory.ReadFocus).
FOCUS_RANGES = {"aperture": (1, 32), "format": (8, 120), "radius": (0.5, 1.5), "start": (1, 20), "end": (1.1, 40),
                "supersample": (1, 3)}
SLUG = re.compile(r"[A-Za-z0-9._-]+")
SIZE = re.compile(r"([0-9]+)x([0-9]+)")
JUMP_FLAG = 6.0                    # a frame-to-frame change this many times the clip's median is printed
FOG_RANGE = (0.002, 0.08)          # the story look's fog dial's range, which a far shot's own fog keeps to
LOOK_RATE = 3                      # frames a second read for a clip's tone and edges


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
    return s


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


def shot_key(shot, s):
    """What a clip was filmed from: a clip whose key differs from its shot's is filmed again. A far shot's own
    fog is in its key, so setting far_fog films the far shots again and no others."""
    fields = {k: shot[k] for k in ("run", "bodies", "camera", "run_from_s", "run_to_s")}
    fields.update({k: s[k] for k in ("handles_s", "fps", "size")})
    fields["handles_s"] = handle_of(shot, s)
    fields["size"] = size_of(shot, s)
    take = (s.get("takes_by_shot") or {}).get(shot["id"])
    if take is not None:
        fields["take"] = take          # raised to film a shot again when nothing in its key changed, a camera's fix
    if far_fog(shot, s) is not None:
        fields["fog"] = far_fog(shot, s)
    if close_focus(shot, s) is not None:
        fields["focus"] = close_focus(shot, s)
    if side_of(shot) is not None:
        fields["side"] = side_of(shot)
    return hashlib.sha256(json.dumps(fields, sort_keys=True).encode()).hexdigest()[:16]


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


def stale_worker(s):
    """Why the render worker cannot film this pass, or None: missing, or carrying another theatre."""
    worker = REPO / f"unity-w{s['worker']}"
    if not worker.is_dir():
        return f"there is no render worker {worker.name}"
    if tree_hash(REPO / "unity" / "Assets" / "Theatre") != tree_hash(worker / "Assets" / "Theatre"):
        return (f"{worker.name} carries another theatre than unity/Assets/Theatre; refresh it from PowerShell with "
                f"./scripts/new-worker.ps1 -Workers {s['worker']}")
    return None


def subject(shot):
    if shot["camera"] in WORLD or not shot["bodies"]:
        return "world"
    # "parent body N" names no family the window planner can place, and it films such a Birth as an Arrival.
    return "body " + shot["bodies"][0]


def pass_dir(folder, k):
    return folder / "film" / f"pass-{k}"


def safari_dir(folder, k, arm):
    return REPO / "scratch" / "safari" / arm / f"{folder.name}-film{k}"


def cmd_plan(folder, k):
    lines = []
    s = film_settings(folder, lines)
    shots = read_json(folder / "shots.json")
    if shots is None:
        raise Refused("the film has no shots.json; the shot list step writes it")
    if (pass_dir(folder, k) / "plan.json").is_file():
        raise Refused(f"pass {k} is planned already; plan the next pass")
    runs_root = repo_path(s["runs_root"])
    clips = (read_json(folder / "clips.json") or {}).get("clips", [])
    filmed = {c["shot"]: c["key"] for c in clips if (folder / c["file"]).is_file() and sha256_of(folder / c["file"]) == c["sha256"]}
    runs = [x for x in shots["shots"] if "run" in x]           # every shot with footage, graphics' included
    todo = [x for x in runs if filmed.get(x["id"]) != shot_key(x, s)]
    if not todo:
        return "FILM PLAN: NOTHING TO FILM", lines + [f"every one of the {len(runs)} shots with footage has its clip"]
    errors, scenes, entries, dirs = [], [], [], {}
    # The director films a run at one size, so a pass does too: an offline pass is the shots set small in
    # size_by_shot, and the 4K pass that follows it is the same shots once they are taken out of it.
    sizes = sorted({size_of(x, s) for x in todo})
    if len(sizes) > 1:
        raise Refused("a pass films at one size, and these shots ask " + ", ".join(sizes) + ": film the shots of "
                      "one size first, by leaving the others' size_by_shot as it was filmed")
    pass_size = sizes[0]
    for n, shot in enumerate(todo, 1):
        h = handle_of(shot, s)
        where, arm = f"shot {shot['id']}", shot["run"]
        if arm not in dirs:
            dirs[arm] = run_dir(runs_root, arm)
            if dirs[arm] is None:
                raise Refused(f"no run directory for {arm} under {runs_root}")
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
            start = a - min(h, a)
        before = a - start
        seconds = b + after - start
        fog = far_fog(shot, s)
        scene = {"n": n, "run": arm, "station": shot["camera"], "second": round(second if second is not None
                 else start, 3), "seconds": round(seconds, 3), "subject": subject(shot), "flexible": False,
                 "captions": []}
        if fog is not None:
            scene["fog"] = fog
        focus, side = close_focus(shot, s), side_of(shot)
        if focus is not None:
            scene["focus"] = focus
        if side is not None:
            scene["side"] = side
        scenes.append(scene)
        entries.append({"n": n, "shot": shot["id"], "key": shot_key(shot, s), "arm": arm, "camera": shot["camera"],
                        "clip_start_s": round(start, 3), "seconds": round(seconds, 3),
                        "action_in_s": round(before, 3), "action_out_s": round(before + b - a, 3),
                        "handle_before_s": round(before, 3), "handle_after_s": round(after, 3),
                        "birth_s": second if shot["camera"] == "Birth" else None, "fog": fog,
                        "focus": focus, "side": side})
        why = []
        if before < h - EPS:
            why.append(f"{before:.1f} s before" + (" (a birth's take opens 8 s before it)" if shot["camera"] == "Birth"
                                                     else " (the run starts)"))
        if after < h - EPS:
            why.append(f"{after:.1f} s after (the run ends at {end:g} s)")
        lines.append(f"{where}: {shot['camera']} on {subject(shot)}, run {start:g} to {start + seconds:g} s, "
                     f"handles {before:.1f} and {after:.1f} s" + (f", fog {fog_text(fog)}" if fog is not None else "")
                     + (f", focus {focus['mode']}" if focus is not None else "")
                     + (f", subject {side}" if side is not None else "")
                     + (f"; short: {', '.join(why)}" if why else ""))
    if errors:
        raise Refused(*errors)
    farms = {arm: farm_of(rd) for arm, rd in dirs.items()}
    named = sorted({f for f in farms.values() if f})
    if len(named) > 1:
        raise Refused("the pass's runs were recorded by different farm programs, which one pass cannot record:",
                      *(f"  {arm}: {f}" for arm, f in sorted(farms.items())))
    farm = named[0] if named else None
    if farm and not Path(farm).is_file():
        raise Refused(f"the farm program that recorded the runs is gone: {farm}. A window recorded on a later build "
                      "can refuse the run's config")
    out = pass_dir(folder, k)
    story = {"title": folder.name, "run": scenes[0]["run"], "scenes": scenes}
    write_json(out / "story.json", story)
    r = subprocess.run([sys.executable, str(REPO / "scripts" / "story-windows.py"), str(out / "story.json"), "--out",
                        str(out / "windows"), "--runs-root", str(runs_root), "--fps", str(s["fps"])],
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
    write_json(out / "plan.json", {"pass": k, "planned": now(), "shots_sha256": sha256_of(folder / "shots.json"),
                                   "settings": s, "size": pass_size, "farm": farm, "scenes": entries})
    footage = sum(e["seconds"] for e in entries)
    head = [f"pass {k}: {len(entries)} shots to film on {len(dirs)} run(s), {len(runs) - len(todo)} filmed already, "
            f"{footage:.0f} s of footage with handles",
            f"farm: {farm}, the program that recorded the runs" if farm else
            "farm: the checkout's own build, since the runs' manifests name none"]
    stale = stale_worker(s)
    if stale:
        head.append(f"warning: {stale}")
    return "FILM PLAN: READY", head + lines


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
    lock = REPO / f"unity-w{s['worker']}" / "Temp" / "UnityLockfile"
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


def cmd_start(folder, k):
    out = pass_dir(folder, k)
    if read_json(out / "plan.json") is None:
        raise Refused(f"pass {k} has no plan; run plan first")
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
    write_json(out / "started.json", {"pass": k, "pid": int(pid), "started": now()})
    return "FILM START: STARTED", [f"pass {k} runs detached as process {pid}; its log is film/pass-{k}/run.log"]


def launch(args, sink=None):
    """One child of the pass's job, its output to the job's own log or to sink. Tests stand in for it."""
    return subprocess.run(args, cwd=REPO, stdout=sink, stderr=subprocess.STDOUT if sink else None).returncode


def cmd_run(folder, k):
    """The pass's job. It records each run's windows on the farm, and films each run in the theatre as soon as its
    windows are in, while the next run records: the farm works the processor and the theatre mostly the card, so
    the two overlap. The runs record smallest first, so the theatre starts early. It films one run at a time,
    since a worker takes one Editor."""
    import queue
    import threading
    out = pass_dir(folder, k)
    plan = read_json(out / "plan.json")
    s = plan["settings"]
    done = {"pass": k, "started": now(), "record_exit": 0, "recorded": {}, "render_exit": {}, "safari": {}}
    scenes = {}
    for e in plan["scenes"]:
        scenes.setdefault(e["arm"], []).append(e["n"])
    order = sorted(scenes, key=lambda a: (len(scenes[a]), a))
    lock = threading.Lock()

    def say(line):
        with lock:
            print(line, flush=True)

    def films(todo):
        while True:
            arm = todo.get()
            if arm is None:
                return
            done["safari"][arm] = str(safari_dir(folder, k, arm))
            args = ["pwsh", "-NoProfile", "-File", str(REPO / "scripts" / "theatre-safari.ps1"), arm,
                    "-Story", str(out / "story.json"), "-FromWindows", str(out / "windows"), "-StoryRun", arm,
                    "-Worker", str(s["worker"]), "-RunsRoot", str(repo_path(s["runs_root"])),
                    "-Folder", f"{folder.name}-film{k}", "-DeleteFrames", "-WallMinutes", str(s["wall_minutes"]),
                    "-Fps", str(s["fps"]), "-Size", plan.get("size", s["size"])]
            say(f"{now()} {arm}: filming, its log film/pass-{k}/film-{arm}.log: {' '.join(args)}")
            with open(out / f"film-{arm}.log", "w", encoding="utf-8") as sink:
                done["render_exit"][arm] = launch(args, sink)
            # The theatre's own log, kept beside the pass: the next run's filming writes over it, and collect
            # reads each far take's fog from it.
            log = REPO / "scratch" / "logs" / f"theatre-safari-{arm}.log"
            if log.is_file():
                shutil.copyfile(log, out / f"theatre-{arm}.log")
            say(f"{now()} {arm}: filmed, exit {done['render_exit'][arm]}")

    todo = queue.Queue()
    theatre = threading.Thread(target=films, args=(todo,))
    theatre.start()
    try:
        for arm in order:
            args = (["pwsh", "-NoProfile", "-File", str(REPO / "scripts" / "story-windows.ps1"), str(out / "windows"),
                     "-Threads", str(s["farm_threads"]), "-Scenes", ",".join(str(n) for n in scenes[arm])]
                    + (["-Exe", plan["farm"]] if plan.get("farm") else []))
            say(f"{now()} {arm}: recording {len(scenes[arm])} window(s): {' '.join(args)}")
            code = launch(args)
            done["recorded"][arm] = code
            say(f"{now()} {arm}: recorded, exit {code}")
            if code == 0:
                todo.put(arm)
            elif done["record_exit"] == 0:
                done["record_exit"] = code
    finally:
        todo.put(None)
        theatre.join()
    done["finished"] = now()
    write_json(out / "done.json", done)
    ok = done["record_exit"] == 0 and all(v == 0 for v in done["render_exit"].values())
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
            lines = [f"recording exited {done['record_exit']}; filming exited "
                     + (", ".join(f"{a} {c}" for a, c in done["render_exit"].items()) or "not at all")]
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


def look(path, cwd, name, pre=""):
    """A clip's tone and edges, each frame read at 1920 wide: the spread of its luma from the 10th to the 90th
    percentile (a flat frame has little), and its mean Sobel edge strength (a soft or fogged frame has little)."""
    ffmpeg(["-i", str(path), "-vf", f"{pre}scale=1920:-2,signalstats,metadata=print:file={name}", "-f", "null", "-"],
           cwd=cwd)
    text = (cwd / name).read_text(encoding="utf-8")
    low = [float(v) for v in re.findall(r"YLOW=([0-9.]+)", text)]
    high = [float(v) for v in re.findall(r"YHIGH=([0-9.]+)", text)]
    ffmpeg(["-i", str(path), "-vf", f"{pre}scale=1920:-2,format=gray,sobel,signalstats,metadata=print:"
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


def reference(folder, s):
    """The reference film's mean luma and its sheet, made once a film."""
    ref = read_json(folder / "film" / "reference.json")
    if ref is not None and "tone_p10" in ref:
        return ref
    path = repo_path(s["reference"])
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
    return ref


def read_tsv(path):
    rows = [r.split(chr(9)) for r in path.read_text(encoding="utf-8-sig").splitlines() if r.strip()]
    return [dict(zip(rows[0], r)) for r in rows[1:]] if rows else []


def cmd_collect(folder, k):
    out = pass_dir(folder, k)
    plan, done = read_json(out / "plan.json"), read_json(out / "done.json")
    if plan is None or done is None:
        raise Refused(f"pass {k} has not been planned and run to its end")
    s, shots = plan["settings"], read_json(folder / "shots.json")
    ref = reference(folder, s)
    clips_dir = folder / "clips"
    clips_dir.mkdir(exist_ok=True)
    tables = {arm: read_tsv(Path(d) / "scenes.tsv") if (Path(d) / "scenes.tsv").is_file() else []
              for arm, d in done["safari"].items()}
    lines, new, errors = [], [], []
    logs = {arm: (out / f"theatre-{arm}.log").read_text(encoding="utf-8", errors="replace")
            for arm in done["safari"] if (out / f"theatre-{arm}.log").is_file()}
    for e in plan["scenes"]:
        where = f"shot {e['shot']}"
        row = next((r for r in tables.get(e["arm"], []) if r.get("slug", "").startswith(f"story-{e['n']:02d}-")), None)
        if row is None or row.get("outcome") != "played":
            errors.append(f"{where}: the director did not film it ({row.get('outcome') if row else 'no row in scenes.tsv'})")
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
        typical = middle(jumps) or 0
        ratio = round(max(jumps) / typical, 1) if typical > 0 else None
        tone, edges = look(clip, clips_dir, f"{e['shot']}-look.txt", f"fps={LOOK_RATE},")
        tone, edges = middle(tone), (round(middle(edges), 2) if edges else None)
        rgb = colour(clip, f"fps={LOOK_RATE},")
        fog = e.get("fog")
        said = f": fog {fog_text(fog)}, the scene's own" if fog is not None else ""
        if fog is not None and not re.search("take [0-9]+ of " + re.escape(row["slug"] + said), logs.get(e["arm"], "")):
            errors.append(f"{where}: the theatre's log does not say its takes were filmed in fog {fog_text(fog)}")
            continue
        focus, side = e.get("focus"), e.get("side")
        log = logs.get(e["arm"], "")
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
                  "luma": round(sum(luma) / len(luma), 2) if luma else None, "jump_ratio": ratio,
                  "tone": tone, "edges": edges, "rgb": rgb, "fog": fog, "focus": focus, "side": side,
                  "sheet": f"clips/{e['shot']}-sheet.png", "sheet_times_s": [round(f / fps, 2) for f in sorted(set(frames))]}
        new.append(record)
        notes = []
        if record["provenance"] != "FAITHFUL":
            notes.append(f"provenance {record['provenance'] or 'none'}")
        if ratio is not None and ratio >= JUMP_FLAG:
            notes.append(f"a frame-to-frame change {ratio} times the clip's median")
        if tone is not None and tone < ref["tone_p10"]:
            notes.append(f"flat: its tone spread {tone:g} is under the reference's tenth percentile, {ref['tone_p10']:g}")
        if edges is not None and edges < ref["edges_p10"]:
            notes.append(f"soft: its edges {edges:g} are under the reference's tenth percentile, {ref['edges_p10']:g}")
        lines.append(f"{where}: {info['frames']} frames, luma {record['luma']} against the reference's {ref['luma']}, "
                     f"tone {tone} against {ref['tone']}, edges {edges} against {ref['edges']}"
                     + (f", fog {fog_text(fog)}" if fog is not None else "")
                     + f", largest change {ratio} times the median" + (f"; look: {', '.join(notes)}" if notes else ""))
    known = {x["id"] for x in shots["shots"] if "run" in x}
    keys = {x["id"]: shot_key(x, s) for x in shots["shots"] if "run" in x}
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


COMMANDS = {"plan": (cmd_plan, "FILM PLAN: REFUSED", ["folder", "n"]),
            "machine": (cmd_machine, "MACHINE: BUSY", ["folder"]),
            "start": (cmd_start, "FILM START: REFUSED", ["folder", "n"]),
            "run": (cmd_run, "FILM RUN: FAILED", ["folder", "n"]),
            "wait": (cmd_wait, "FILMING: FAILED", ["folder", "n", "n"]),
            "collect": (cmd_collect, "CLIPS: REFUSED", ["folder", "n"])}
PASSING = ("READY", "NOTHING TO FILM", "FREE", "STARTED", "DONE", "PASS")


def main(argv):
    started = time.monotonic()
    spec = COMMANDS.get(argv[1]) if len(argv) > 1 else None
    if spec is None or len(argv) != 2 + len(spec[2]) or not all(
            kind == "folder" or (v.isdigit() and int(v) >= 1) for kind, v in zip(spec[2], argv[2:])):
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, refused, kinds = spec
    args = [Path(v).resolve() if kind == "folder" else int(v) for kind, v in zip(kinds, argv[2:])]
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
