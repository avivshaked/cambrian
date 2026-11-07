"""The overlap setting: false records every run's windows before the theatre films any.

A pass records each run's windows on the farm and, by default, films each run in the theatre as soon as its
windows are in, while the farm records the next. On 2026-10-30 a four-thread film window started beside a Unity
step and the CPU went from 78 to 100 degrees C in seven seconds (logbook/specs/machine-and-tools.md). With
`overlap` false the pass records everything first and films after, with nothing of its own overlapping.

  python -m pytest scripts/tests/story-video-check/test_overlap.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_overlap.py   (where pytest is not installed)
"""

import importlib.util
import json
import os
import threading
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

SHOT = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 100.0,
        "run_to_s": 104.0, "kind": "run"}
BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
ARMS = ["a-run", "b-run", "c-run"]      # one scene each, so they record in this order


# ---- the setting -----------------------------------------------------------------------------------------

def settings_of(tmp_path, **more):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.pop("overlap", None)
    s.update(more)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    return folder


def test_the_skills_settings_carry_overlap_true():
    assert json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))["overlap"] is True


def test_overlap_true_false_and_absent_are_accepted(tmp_path):
    for i, more in enumerate(({"overlap": True}, {"overlap": False}, {})):
        folder = settings_of(tmp_path / str(i), **more)
        s = film.film_settings(folder, [])
        assert s.get("overlap", True) is more.get("overlap", True)


def test_a_non_boolean_overlap_is_refused(tmp_path):
    for i, bad in enumerate(["false", 0, 1, "yes", []]):
        folder = settings_of(tmp_path / str(i), overlap=bad)
        with pytest.raises(film.Refused) as e:
            film.film_settings(folder, [])
        assert any("overlap" in l for l in e.value.lines)


# ---- the run ---------------------------------------------------------------------------------------------

def make_pass(tmp_path, overlap):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None)
    if overlap is not None:
        settings["overlap"] = overlap
    scenes = [{"n": i + 1, "arm": arm} for i, arm in enumerate(ARMS)]
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": scenes}), encoding="utf-8")
    return folder, out


def stub_launch(monkeypatch, events, failing=(), on=None):
    """A launch that logs ('record'|'film', arm) in call order; `failing` arms record with exit 3."""
    lock = threading.Lock()

    def fake(args, sink=None, cores=None):
        if "story-windows.ps1" in " ".join(args):
            scene = int(args[args.index("-Scenes") + 1])
            what, arm = "record", ARMS[scene - 1]
        else:
            what, arm = "film", args[args.index("-StoryRun") + 1]
        if on:
            on(what, arm)
        with lock:
            events.append((what, arm))
        return 3 if what == "record" and arm in failing else 0

    monkeypatch.setattr(film, "launch", fake)


def run_pass(tmp_path, monkeypatch, overlap, failing=(), on=None):
    folder, out = make_pass(tmp_path, overlap)
    monkeypatch.setattr(os, "environ", dict(os.environ))
    events = []
    stub_launch(monkeypatch, events, failing, on)
    verdict, lines = film.cmd_run(folder, 1)
    done = json.loads((out / "done.json").read_text(encoding="utf-8"))
    return events, verdict, done


def test_overlap_false_records_everything_before_it_films(tmp_path, monkeypatch):
    events, verdict, done = run_pass(tmp_path, monkeypatch, False)
    assert verdict == "FILM RUN: DONE"
    assert [w for w, _ in events] == ["record"] * 3 + ["film"] * 3
    assert [a for w, a in events if w == "film"] == [a for w, a in events if w == "record"] == ARMS
    assert done["overlap"] is False


def test_overlap_false_does_not_film_a_run_whose_recording_failed(tmp_path, monkeypatch):
    events, verdict, done = run_pass(tmp_path, monkeypatch, False, failing=("b-run",))
    assert verdict == "FILM RUN: FAILED" and done["record_exit"] == 3
    assert [w for w, _ in events] == ["record"] * 3 + ["film"] * 2
    assert [a for w, a in events if w == "film"] == ["a-run", "c-run"]


def overlap_proof(tmp_path, monkeypatch, overlap):
    filmed_first = threading.Event()
    seen = {}

    def on(what, arm):
        if what == "film" and arm == ARMS[0]:
            filmed_first.set()
        if what == "record" and arm == ARMS[-1]:
            # the last run's recording waits for the theatre to film the first: no timing, and a timeout that fails
            seen["waited"] = filmed_first.wait(timeout=30)

    events, verdict, done = run_pass(tmp_path, monkeypatch, overlap, on=on)
    assert seen["waited"] is True, "the theatre did not film the first run while the last one was recording"
    assert verdict == "FILM RUN: DONE" and done["overlap"] is True
    assert events.index(("film", ARMS[0])) < events.index(("record", ARMS[-1]))


def test_overlap_true_films_the_first_run_while_a_later_one_records(tmp_path, monkeypatch):
    overlap_proof(tmp_path, monkeypatch, True)


def test_overlap_absent_reads_as_true(tmp_path, monkeypatch):
    overlap_proof(tmp_path, monkeypatch, None)


# ---- the plan and the key --------------------------------------------------------------------------------

def plan_lines(tmp_path, monkeypatch, **more):
    folder = settings_of(tmp_path, **more)
    shots = [dict(SHOT, id=f"1.{n}a", run_from_s=100.0 * n, run_to_s=100.0 * n + 4) for n in (1, 2)]
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    rd = tmp_path / "rundir"
    rd.mkdir(exist_ok=True)
    monkeypatch.setattr(film, "run_dir", lambda root, arm: rd)
    monkeypatch.setattr(film, "farm_of", lambda d: None)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, shot, h: (shot["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        windows = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
                   for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": windows, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    return lines


def test_the_plan_says_when_overlap_is_off(tmp_path, monkeypatch):
    lines = plan_lines(tmp_path, monkeypatch, overlap=False)
    assert any(l.startswith("overlap: off") and "records every window first" in l for l in lines), lines


def test_the_plan_says_nothing_when_overlap_is_on_or_absent(tmp_path, monkeypatch):
    assert not any("overlap" in l for l in plan_lines(tmp_path / "t", monkeypatch, overlap=True))
    assert not any("overlap" in l for l in plan_lines(tmp_path / "u", monkeypatch))


def test_a_shots_key_does_not_depend_on_overlap():
    on, off = dict(BASE, overlap=True), dict(BASE, overlap=False)
    assert film.shot_key(SHOT, on) == film.shot_key(SHOT, off) == film.shot_key(SHOT, dict(BASE))
