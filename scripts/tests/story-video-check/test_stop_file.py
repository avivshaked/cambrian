"""film.py honours the heat guard's stop file, <film folder>/film/STOP.

film-guard.ps1 writes the file, with the time and the reason inside, when it trips, and kills the pass's process
tree. `start` refuses while the file exists, and a running pass records and films nothing more once it appears.

  python -m pytest scripts/tests/story-video-check/test_stop_file.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_stop_file.py   (where pytest is not installed)
"""

import importlib.util
import json
import os
import threading
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
ARMS = ["a-run", "b-run", "c-run"]
REASON = "2026-11-07T17:00:00 CPU 96 C over the limit"


def make_pass(tmp_path, overlap=True):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None,
                    overlap=overlap)
    scenes = [{"n": i + 1, "arm": arm} for i, arm in enumerate(ARMS)]
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": scenes}), encoding="utf-8")
    return folder, out


def run_pass(tmp_path, monkeypatch, overlap=True, trip_after=None, trip_on="record"):
    """cmd_run with launch stubbed; the stop file appears when the `trip_after`-th `trip_on` call has run."""
    folder, out = make_pass(tmp_path, overlap)
    monkeypatch.setattr(os, "environ", dict(os.environ))
    events, count = [], {"record": 0, "film": 0}

    def fake(args, sink=None, cores=None):
        if "story-windows.ps1" in " ".join(args):
            what, arm = "record", ARMS[int(args[args.index("-Scenes") + 1]) - 1]
        else:
            what, arm = "film", args[args.index("-StoryRun") + 1]
        events.append((what, arm))
        count[what] += 1
        if what == trip_on and count[what] == trip_after:
            film.stop_file(folder).write_text(REASON, encoding="utf-8")
        return 0

    monkeypatch.setattr(film, "launch", fake)
    verdict, lines = film.cmd_run(folder, 1)
    return events, verdict, json.loads((out / "done.json").read_text(encoding="utf-8"))


def test_the_stop_file_is_film_stop(tmp_path):
    assert film.stop_file(tmp_path) == tmp_path / "film" / "STOP"


def test_start_is_refused_while_the_stop_file_exists(tmp_path):
    folder, out = make_pass(tmp_path)
    film.stop_file(folder).write_text(REASON, encoding="utf-8")
    with pytest.raises(film.Refused) as e:
        film.cmd_start(folder, 1)
    said = " | ".join(e.value.lines)
    assert REASON in said and "75 °C" in said and "temp-check.ps1" in said and "coordinator" in said
    assert "delete the file" in said
    assert not (out / "started.json").exists()


def test_a_pass_without_the_file_has_stopped_null(tmp_path, monkeypatch):
    events, verdict, done = run_pass(tmp_path, monkeypatch)
    assert verdict == "FILM RUN: DONE" and done["stopped"] is None
    assert len(events) == 6


def test_no_run_is_recorded_after_the_file_appears_between_two_runs(tmp_path, monkeypatch):
    # overlap false, so the theatre films nothing during the recording and the test is about the loop alone
    events, verdict, done = run_pass(tmp_path, monkeypatch, overlap=False, trip_after=1)
    assert [w for w, _ in events if w == "record"] == ["record"]
    assert verdict == "FILM RUN: FAILED" and done["stopped"] == REASON


def test_the_theatre_films_nothing_once_the_file_appears(tmp_path, monkeypatch):
    # overlap false: all three record, the file appears after the last recording, and nothing is filmed
    events, verdict, done = run_pass(tmp_path, monkeypatch, overlap=False, trip_after=3)
    assert events == [("record", a) for a in ARMS]
    assert verdict == "FILM RUN: FAILED" and done["stopped"] == REASON


def test_the_theatre_stops_filming_after_the_first_run_when_the_file_appears_during_it(tmp_path, monkeypatch):
    events, verdict, done = run_pass(tmp_path, monkeypatch, overlap=False, trip_after=1, trip_on="film")
    assert [e for e in events if e[0] == "film"] == [("film", "a-run")]
    assert verdict == "FILM RUN: FAILED" and done["stopped"] == REASON
