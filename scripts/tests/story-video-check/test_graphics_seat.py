"""graphics.py draws its stills on the film's own seat and look, and holds its renders to the coordinator's terms.

Round 67's film found on 2026-11-07 that the stills' launch took the render worker, the stills entry and the theatre
from the repository root while the film was shot on a worker in another checkout, and set no film look. The same day the
coordinator set terms for the animators' renders beside a filming pass: one at a time under a lock, and none while the
CPU reads 80 C or more. No test starts Blender or Unity: subprocess calls, the clock and the temperature are replaced.

  python -m pytest scripts/tests/story-video-check/test_graphics_seat.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_graphics_seat.py   (where pytest is not installed)
"""

import importlib.util
import json
import re
import subprocess
import sys
import types
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("graphics_tool", SKILL / "graphics.py")
graphics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(graphics)
fspec = importlib.util.spec_from_file_location("film_tool_for_seat", SKILL / "film.py")
film = importlib.util.module_from_spec(fspec)
fspec.loader.exec_module(film)
film.narration_tool = lambda: types.SimpleNamespace(cmd_machine=lambda: ("MACHINE: IDLE", []))

ENTRY = "unity/Assets/Theatre/Editor/TheatreSoloStill.cs"


# ---------------------------------------------------------------------------------------------------- the settings

def settings_problems(tmp_path, **extra):
    folder = tmp_path / "film"
    folder.mkdir(exist_ok=True)
    s = json.loads((SKILL / graphics.SETTINGS).read_text(encoding="utf-8"))
    s.update(extra)
    (folder / graphics.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    try:
        graphics.settings_of(folder, [])
    except graphics.Refused as e:
        return " | ".join(e.lines)
    return ""


def fake_checkout(root, entry=True, look=True, worker_entry=True):
    (root / "scripts").mkdir(parents=True)
    (root / "scripts" / "theatre-safari.ps1").write_text("", encoding="utf-8")
    if look:
        (root / "scripts" / "theatre-film-look.ps1").write_text("", encoding="utf-8")
    for place, has in (("unity", entry), ("unity-w7", worker_entry)):
        d = root / place / "Assets" / "Theatre"
        (d / "Editor").mkdir(parents=True)
        (d / "x.cs").write_text("x = 1", encoding="utf-8")
        if has:
            (d / "Editor" / "TheatreSoloStill.cs").write_text("// still", encoding="utf-8")
    return root


def test_the_skills_own_settings_are_good(tmp_path):
    assert settings_problems(tmp_path) == ""


def test_theatre_root_is_null_or_a_checkout_with_the_safari(tmp_path):
    root = fake_checkout(tmp_path / "other")
    assert settings_problems(tmp_path, theatre_root=str(root)) == ""
    for bad in (str(tmp_path / "nowhere"), "", 7):
        assert "theatre_root" in settings_problems(tmp_path, theatre_root=bad)


def test_theatre_commit_is_null_or_a_name(tmp_path):
    assert settings_problems(tmp_path, theatre_commit="dc0a607d") == ""
    for bad in ("", "  ", "-x", 5):
        assert "theatre_commit" in settings_problems(tmp_path, theatre_commit=bad)


def test_render_lock_is_a_boolean(tmp_path):
    assert settings_problems(tmp_path, render_lock=True) == ""
    assert settings_problems(tmp_path, render_lock=False) == ""
    for bad in (1, "true", None):
        assert "render_lock is" in settings_problems(tmp_path, render_lock=bad)


def test_the_waits_are_whole_minutes(tmp_path):
    assert settings_problems(tmp_path, render_lock_minutes=5, render_heat_minutes=3) == ""
    for key in ("render_lock_minutes", "render_heat_minutes"):
        for bad in (0, -1, 1.5, "20", True, 100000):
            assert key in settings_problems(tmp_path, **{key: bad})


def test_render_heat_is_null_or_a_temperature(tmp_path):
    for good in (None, 80, 79.5):
        assert settings_problems(tmp_path, render_heat_c=good) == ""
    for bad in ("80", 5, 200, True, float("nan")):
        assert "render_heat_c" in settings_problems(tmp_path, render_heat_c=bad)


def test_a_film_whose_settings_predate_the_keys_still_loads(tmp_path):
    folder = tmp_path / "film"
    folder.mkdir()
    s = json.loads((SKILL / graphics.SETTINGS).read_text(encoding="utf-8"))
    for key in ("theatre_root", "theatre_commit", "render_lock", "render_heat_c"):
        s.pop(key, None)
    (folder / graphics.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    assert graphics.settings_of(folder, [])["width"] == 3840


# ---------------------------------------------------------------------------------------------------- the stills

def stills_setup(tmp_path, monkeypatch, runs=("run-a",), **root_kwargs):
    root = fake_checkout(tmp_path / "other", **root_kwargs)
    folder = tmp_path / "film"
    folder.mkdir()
    monkeypatch.setattr(graphics, "film_tool", lambda: film)
    s = {"worker": 7, "unity": "C:/unity/Unity.exe", "still_wall_minutes": 30, "theatre_root": str(root),
         "theatre_commit": None}
    items = [{"name": f"{r}-{i}", "run": r, "genome_file": f"g{i}", "run_dir": f"d{r}", "out": f"o{i}", "size": 64}
             for i, r in enumerate(runs)]
    return root, folder, s, items


def fake_unity(monkeypatch, calls, stdout="look: 3 set"):
    def run(command, **kw):
        calls.append({"command": command, "env": kw["env"]})
        listing = Path(kw["env"]["EVOSIM_STILLS_LIST"])
        (listing.parent / "result.json").write_text(json.dumps({"items": []}), encoding="utf-8")
        return subprocess.CompletedProcess(command, 0, stdout, "")
    monkeypatch.setattr(subprocess, "run", run)


def test_the_worker_and_the_entry_are_looked_for_under_the_theatre_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch)
    calls = []
    fake_unity(monkeypatch, calls)
    assert graphics.launch_stills(s, folder, items, tmp_path / "list.json") is None
    assert len(calls) == 1
    args = calls[0]["command"][-1]
    assert str(root / "unity-w7").replace("'", "''") in args
    assert calls[0]["env"]["EVOSIM_REPO_ROOT"] == str(graphics.REPO)


def test_a_theatre_without_the_entry_is_refused_naming_the_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, entry=False)
    calls = []
    fake_unity(monkeypatch, calls)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and str(root) in why and "TheatreSoloStill.cs" in why
    assert calls == []


def test_a_worker_without_the_entry_is_refused_naming_the_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, worker_entry=False)
    calls = []
    fake_unity(monkeypatch, calls)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and "unity-w7" in why and str(root) in why
    assert calls == []


def test_a_missing_worker_is_refused_naming_the_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch)
    s["worker"] = 9
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and "unity-w9" in why and str(root) in why


def test_a_worker_with_another_theatre_is_refused_naming_the_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch)
    (root / "unity-w7" / "Assets" / "Theatre" / "x.cs").write_text("x = 2", encoding="utf-8")
    calls = []
    fake_unity(monkeypatch, calls)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and str(root) in why
    assert calls == []


def test_a_checkout_off_its_commit_is_refused(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch)
    s["theatre_commit"] = "somebranch"
    monkeypatch.setattr(film, "commit_fault", lambda settings: f"the theatre checkout {root} is at abc, not def")
    calls = []
    fake_unity(monkeypatch, calls)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and str(root) in why
    assert calls == []


def test_a_theatre_without_the_film_look_is_refused_naming_the_root(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, look=False)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and str(root) in why and "theatre-film-look.ps1" in why


def test_the_launch_sets_the_film_look_for_the_run_before_unity(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("r67-a-6701",))
    calls, notes = [], []
    fake_unity(monkeypatch, calls, stdout="look: 12 dials set from scripts/theatre-film-look.json\n")
    assert graphics.launch_stills(s, folder, items, tmp_path / "list.json", notes) is None
    command = calls[0]["command"][-1]
    look = str(root / "scripts" / "theatre-film-look.ps1")
    assert f". '{look}'" in command
    assert "Use-FilmLook -Sections wide, close, events -World 'r67-a-6701'" in command
    assert command.index("Use-FilmLook") < command.index("Start-Process")
    assert any(x.startswith("look: look: 12 dials set") for x in notes)
    assert any("run r67-a-6701" in x and "r67-a-6701-0" in x for x in notes)


def test_stills_from_two_runs_make_two_launches_in_order(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b", "run-a"))
    calls, notes = [], []
    fake_unity(monkeypatch, calls)
    assert graphics.launch_stills(s, folder, items, tmp_path / "list.json", notes) is None
    assert len(calls) == 2
    assert "-World 'run-a'" in calls[0]["command"][-1] and "-World 'run-b'" in calls[1]["command"][-1]
    lists = [json.loads(Path(c["env"]["EVOSIM_STILLS_LIST"]).read_text(encoding="utf-8")) for c in calls]
    assert [[x["name"] for x in l["items"]] for l in lists] == [["run-a-0", "run-a-2"], ["run-b-1"]]
    launches = [x for x in notes if x.startswith("launch ")]
    assert len(launches) == 2 and "run run-a" in launches[0] and "run run-b" in launches[1]


def test_a_failed_launch_stops_before_the_next_run(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b"))
    calls = []

    def run(command, **kw):
        calls.append(command)
        return subprocess.CompletedProcess(command, 1, "", "boom")
    monkeypatch.setattr(subprocess, "run", run)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and "run-a" in why and len(calls) == 1


# ------------------------------------------------------------------------------------------------ the render's turn

class Time:
    """A clock that only the pauses move."""

    def __init__(self):
        self.now, self.pauses = 0.0, []

    def clock(self):
        return self.now

    def pause(self, seconds):
        self.pauses.append(seconds)
        self.now += seconds


def render_setup(tmp_path, monkeypatch, **s):
    folder = tmp_path / "film"
    (folder / "graphics" / "work").mkdir(parents=True)
    t = Time()
    monkeypatch.setattr(graphics, "clock", t.clock)
    monkeypatch.setattr(graphics, "pause", t.pause)
    seen = {"ran": 0, "lock_during": None}

    def blender(settings, fold, item, work):
        seen["ran"] += 1
        seen["lock_during"] = (folder / "graphics" / "work" / graphics.RENDER_LOCK).is_file()
        return seen.get("result")
    monkeypatch.setattr(graphics, "blender_run", blender)
    return folder, t, seen, dict(s)


def lock_file(folder):
    return folder / "graphics" / "work" / graphics.RENDER_LOCK


def test_without_the_settings_a_render_takes_no_lock_and_reads_no_heat(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch)
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (_ for _ in ()).throw(AssertionError("read")))
    notes = []
    assert graphics.run_blender(s, folder, {}, folder, notes) is None
    assert seen["ran"] == 1 and seen["lock_during"] is False and notes == []


def test_a_render_holds_the_lock_while_it_runs_and_releases_it(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)
    notes = []
    assert graphics.run_blender(s, folder, {}, folder, notes) is None
    assert seen["lock_during"] is True and not lock_file(folder).exists()
    assert any(x.startswith("lock: took") for x in notes)


def test_the_lock_holds_the_process_id_and_the_time(tmp_path, monkeypatch):
    import os
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)
    held = {}
    monkeypatch.setattr(graphics, "blender_run", lambda *a: held.update(json.loads(lock_file(folder).read_text())))
    graphics.run_blender(s, folder, {}, folder)
    assert held["pid"] == os.getpid() and held["time"]


def test_a_failed_render_releases_the_lock(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)
    seen["result"] = "Blender failed: x"
    assert graphics.run_blender(s, folder, {}, folder) == "Blender failed: x"
    assert not lock_file(folder).exists()


def test_a_render_that_raises_releases_the_lock(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)

    def boom(*a):
        raise RuntimeError("boom")
    monkeypatch.setattr(graphics, "blender_run", boom)
    try:
        graphics.run_blender(s, folder, {}, folder)
    except RuntimeError:
        pass
    else:
        raise AssertionError("did not raise")
    assert not lock_file(folder).exists()


def test_a_lock_held_by_a_live_process_makes_a_render_wait_and_then_fail(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True, render_lock_minutes=2)
    holder = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
    try:
        lock_file(folder).write_text(json.dumps({"pid": holder.pid, "time": "2026-11-07T10:00:00"}), encoding="utf-8")
        notes = []
        said = graphics.run_blender(s, folder, {}, folder, notes)
    finally:
        holder.kill()
        holder.wait()
    assert said and "another render held the lock for 2 minutes" in said
    assert seen["ran"] == 0
    assert t.pauses == [15] * 8                       # every 15 s for two minutes
    assert lock_file(folder).exists()                 # the other render's, not removed by the one that gave up
    assert json.loads(lock_file(folder).read_text())["pid"] == holder.pid
    assert any("waiting" in x for x in notes)


def test_a_render_goes_on_when_the_lock_is_released_meanwhile(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)
    holder = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
    lock_file(folder).write_text(json.dumps({"pid": holder.pid, "time": "x"}), encoding="utf-8")
    real = t.pause

    def pause(seconds):
        real(seconds)
        lock_file(folder).unlink(missing_ok=True)      # the other render finishes
    monkeypatch.setattr(graphics, "pause", pause)
    try:
        assert graphics.run_blender(s, folder, {}, folder) is None
    finally:
        holder.kill()
        holder.wait()
    assert seen["ran"] == 1 and not lock_file(folder).exists()


def test_a_lock_whose_process_has_gone_is_taken_over(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True)
    gone = subprocess.Popen([sys.executable, "-c", "pass"])
    gone.wait()
    lock_file(folder).write_text(json.dumps({"pid": gone.pid, "time": "2026-11-07T09:00:00"}), encoding="utf-8")
    notes = []
    assert graphics.run_blender(s, folder, {}, folder, notes) is None
    assert seen["ran"] == 1 and seen["lock_during"] is True and not lock_file(folder).exists()
    assert t.pauses == []
    assert any("no longer runs; taking it over" in x and str(gone.pid) in x for x in notes)


def test_the_lock_is_created_exclusively(tmp_path):
    path = tmp_path / "x.lock"
    path.write_text("{}", encoding="utf-8")
    import os
    try:
        os.open(str(path), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        return
    raise AssertionError("an existing lock was opened again")


# ------------------------------------------------------------------------------------------------ the render's heat

def test_a_render_waits_while_the_cpu_is_at_the_limit_and_goes_on_when_it_drops(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_heat_c=80)
    readings = iter([86, 81, 80, 79])
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (next(readings), None))
    notes = []
    assert graphics.run_blender(s, folder, {}, folder, notes) is None
    assert t.pauses == [30, 30, 30] and seen["ran"] == 1
    heat = [x for x in notes if x.startswith("heat:")]
    assert len(heat) == 4 and "86 C" in heat[0] and "waiting" in heat[0] and "79 C" in heat[3] and "under" in heat[3]


def test_a_render_fails_after_its_heat_limit_with_the_last_reading(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_heat_c=80, render_heat_minutes=2)
    readings = iter([90, 91, 92, 93, 94, 95])
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (next(readings), None))
    said = graphics.run_blender(s, folder, {}, folder)
    assert said and "93 C" not in said and "94 C" in said and "2 minutes" in said
    assert seen["ran"] == 0


def test_an_unreadable_temperature_refuses_the_render(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_heat_c=80)
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (None, "Afterburner is not running"))
    said = graphics.run_blender(s, folder, {}, folder)
    assert said and "could not be read" in said and "Afterburner is not running" in said and "unread" in said
    assert seen["ran"] == 0 and t.pauses == []


def test_the_lock_is_released_when_the_heat_refuses_the_render(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True, render_heat_c=80)
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (None, "no sensor"))
    assert graphics.run_blender(s, folder, {}, folder)
    assert seen["ran"] == 0 and not lock_file(folder).exists()


def test_the_heat_is_read_after_the_lock_is_taken(tmp_path, monkeypatch):
    folder, t, seen, s = render_setup(tmp_path, monkeypatch, render_lock=True, render_heat_c=80)
    during = []
    monkeypatch.setattr(graphics, "read_cpu_temp", lambda: (during.append(lock_file(folder).is_file()), (60, None))[1])
    assert graphics.run_blender(s, folder, {}, folder) is None
    assert during == [True]


def test_the_heat_reader_is_the_guards_own_cpu_temp():
    guard = (SKILL / "film-guard.ps1").read_text(encoding="utf-8-sig")
    assert "function CpuTemp" in guard and "'CpuTemp'" in graphics.HEAT_READER


def test_render_passes_its_printout_to_the_graphics_lines(tmp_path, monkeypatch):
    """cmd_render hands run_blender its lines, so the readings reach the animator's printout."""
    got = {}

    def run_blender(s, folder, item, work, notes=None):
        got["notes"] = notes
        return "stop here"
    monkeypatch.setattr(graphics, "run_blender", run_blender)
    folder = tmp_path / "film"
    (folder / "graphics" / "scenes").mkdir(parents=True)
    (folder / "graphics" / "scenes" / "1.1a.py").write_text("DRAWS = [('x', 'axis')]\ndef graphic(clock):\n    pass\n",
                                                           encoding="utf-8")
    plan = {"graphics": [{"id": "1.1a", "scene": "graphics/scenes/1.1a.py", "stills": []}], "stills": []}
    monkeypatch.setattr(graphics, "settings_of", lambda f, lines: {"size": "1x1", "draft_size": "1x1"})
    monkeypatch.setattr(graphics, "load_plan", lambda f: plan)
    monkeypatch.setattr(graphics, "records_of", lambda f: {"stills": {}, "graphics": {}, "words": {}})
    monkeypatch.setattr(graphics, "blender_ready", lambda s: ("5.1.1", None))
    monkeypatch.setattr(graphics, "check_draws", lambda *a: None)
    verdict, lines = graphics.cmd_render(folder, "1.1a")
    assert verdict == "RENDER: FAIL" and got["notes"] is not None


# ------------------------------------------------------------------------------------------------ one log a launch

def log_of(command):
    return re.search(r"-logFile'?,? *'?([^',]+\.log)", command[-1]).group(1)


def test_two_runs_write_two_different_logs_each_naming_its_run(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b"))
    calls, notes = [], []
    fake_unity(monkeypatch, calls)
    assert graphics.launch_stills(s, folder, items, tmp_path / "list.json", notes) is None
    logs = [log_of(c["command"]) for c in calls]
    assert len(set(logs)) == 2
    assert "run-a" in logs[0] and "run-b" in logs[1]
    assert logs[0].endswith(f"stills-{folder.name}-run-a.log")


def test_the_notes_name_each_launchs_log(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b"))
    calls, notes = [], []
    fake_unity(monkeypatch, calls)
    graphics.launch_stills(s, folder, items, tmp_path / "list.json", notes)
    named = [x for x in notes if x.startswith("log: ")]
    assert len(named) == 2
    assert "run-a" in named[0] and "run-b" in named[1]
    assert notes.index(named[0]) == notes.index([x for x in notes if x.startswith("launch 1")][0]) + 1


def test_a_failing_launch_names_its_own_log(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b"))
    n = []

    def run(command, **kw):
        n.append(command)
        if len(n) == 1:
            listing = Path(kw["env"]["EVOSIM_STILLS_LIST"])
            (listing.parent / "result.json").write_text(json.dumps({"items": []}), encoding="utf-8")
            return subprocess.CompletedProcess(command, 0, "", "")
        return subprocess.CompletedProcess(command, 1, "", "boom")
    monkeypatch.setattr(subprocess, "run", run)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and "run-b.log" in why and "run-a.log" not in why


def test_a_timed_out_launch_names_its_own_log(tmp_path, monkeypatch):
    root, folder, s, items = stills_setup(tmp_path, monkeypatch, runs=("run-a", "run-b"))

    def run(command, **kw):
        raise subprocess.TimeoutExpired(command, 1)
    monkeypatch.setattr(subprocess, "run", run)
    why = graphics.launch_stills(s, folder, items, tmp_path / "list.json")
    assert why and "run-a.log" in why
