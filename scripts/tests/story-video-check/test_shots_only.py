"""The shots_only setting: a pass films the named shots alone, and the rest wait.

A stills round passes some shots of a run that holds many; a pass films those and no others.

  python -m pytest scripts/tests/story-video-check/test_shots_only.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_shots_only.py   (where pytest is not installed)
"""

import importlib.util
import json
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)


def shot(sid, run, a=100.0):
    return {"id": sid, "run": run, "bodies": [], "camera": "Floor", "run_from_s": a, "run_to_s": a + 4.0, "kind": "run"}


def film_folder(tmp_path, shots, **settings):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(settings)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    return folder


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def plan_film(tmp_path, monkeypatch, shots, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots if "run" in x}:
        d = runs / arm / "r1"
        d.mkdir(parents=True)
        (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
    folder = film_folder(tmp_path, shots, runs_root=str(runs), **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
              for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


SHOTS = [shot("1.1a", "r67-a"), shot("1.2a", "r67-b"), shot("6.4a", "r67ev-64a"), shot("9.1a", "r67-b", 300.0),
         {"id": "t.1", "kind": "title"}]


def planned(folder):
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    return sorted(e["shot"] for e in plan["scenes"]), {sc["arm"] for sc in plan["scenes"]}


def test_the_setting_is_validated(tmp_path):
    for good in (None, ["1.1a"], ["a", "b"]):
        ok = film.film_settings(film_folder(tmp_path, [], shots_only=good), [])
        assert ok["shots_only"] == good
    for bad in ([], "1.1a", [""], [5], {"a": 1}, ["1.1a", 5]):
        assert "shots_only" in refused(film.film_settings, film_folder(tmp_path, [], shots_only=bad), [])


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = shot("1.1a", "a")
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, shots_only=["1.1a"]))


def test_null_films_every_shot(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=None)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ["1.1a", "1.2a", "6.4a", "9.1a"]
    assert not any("shots_only" in x for x in lines)


def test_two_named_shots_of_a_run_that_holds_more(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.2a", "9.1a"])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder) == (["1.2a", "9.1a"], {"r67-b"})
    wait = [x for x in lines if "wait for the next passes" in x and "shots_only" in x]
    assert wait == ["2 shots not in shots_only wait for the next passes"]


def test_a_shot_not_in_shots_json_is_refused(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a", "7.7z"])
    said = refused(film.cmd_plan, folder, 1)
    assert "7.7z" in said and "not in shots.json" in said
    assert not (folder / "film" / "pass-1" / "plan.json").exists()


def test_a_shot_with_no_run_is_refused(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a", "t.1"])
    said = refused(film.cmd_plan, folder, 1)
    assert "t.1" in said and "no run" in said


def _with_current_clip(folder, sid):
    x = next(y for y in SHOTS if y["id"] == sid)
    s = film.film_settings(folder, [])
    colours = film.colours_of(folder, [y for y in SHOTS])
    f = folder / "clip.mp4"
    f.write_bytes(b"clip")
    (folder / "clips.json").write_text(json.dumps({"clips": [
        {"shot": sid, "key": film.shot_key(x, s, colours), "file": "clip.mp4", "sha256": film.sha256_of(f)}]}),
        encoding="utf-8")


def test_a_named_shot_whose_clip_is_current_is_said_so_and_the_other_is_planned(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a", "6.4a"])
    _with_current_clip(folder, "1.1a")
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert "shots_only names 1.1a, whose clip is current; it is not filmed" in lines
    assert planned(folder)[0] == ["6.4a"]


def test_every_named_shot_current_is_refused(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a"])
    _with_current_clip(folder, "1.1a")
    said = refused(film.cmd_plan, folder, 1)
    assert "shots_only" in said and "1.1a" in said


def test_with_runs_only_a_shot_must_pass_both(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a", "1.2a", "9.1a"], runs_only=["r67-b"])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ["1.2a", "9.1a"]


def test_the_intersection_empty_is_refused(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, shots_only=["1.1a"], runs_only=["r67-b"])
    assert "shots_only" in refused(film.cmd_plan, folder, 1)
