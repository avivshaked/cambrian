"""The runs_only setting: a pass films the named runs alone, and the rest wait.

Round 67's film had a take cut short by a driver fault on 2026-11-08. The ruling was to film that run by itself and
judge it before the other runs were filmed. The plan had no way to leave the other runs out of a pass.

  python -m pytest scripts/tests/story-video-check/test_runs_only.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_runs_only.py   (where pytest is not installed)
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


def test_the_skills_settings_carry_the_key_at_its_default():
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    assert s["runs_only"] is None


def test_the_setting_is_validated(tmp_path):
    for good in (None, ["r67ev-64a-6202"], ["a", "b"]):
        ok = film.film_settings(film_folder(tmp_path, [], runs_only=good), [])
        assert ok["runs_only"] == good
    for bad in ([], "r67ev-64a-6202", [""], [5], {"a": 1}):
        assert "runs_only" in refused(film.film_settings, film_folder(tmp_path, [], runs_only=bad), [])


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = shot("1.1a", "a")
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, runs_only=["a"]))


# ---- the plan -----------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shots, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots}:
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


SHOTS = [shot("1.1a", "r67-a"), shot("1.2a", "r67-b"), shot("6.4a", "r67ev-64a"), shot("9.1a", "r67-b", 300.0)]


def planned(folder):
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    return sorted(e["shot"] for e in plan["scenes"]), {sc["arm"] for sc in plan["scenes"]}


def test_runs_only_films_the_named_run_alone_and_names_the_rest(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, runs_only=["r67ev-64a"])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder) == (["6.4a"], {"r67ev-64a"})
    wait = [x for x in lines if "runs_only" in x]
    assert len(wait) == 1 and wait[0].startswith("3 shots") and "r67-a" in wait[0] and "r67-b" in wait[0]


def test_null_films_every_run(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, runs_only=None)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ["1.1a", "1.2a", "6.4a", "9.1a"]
    assert not any("runs_only" in x for x in lines)


def test_a_list_with_nothing_left_to_film_is_refused(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, runs_only=["r67ev-24b"])
    said = refused(film.cmd_plan, folder, 1)
    assert "runs_only" in said and "r67ev-24b" in said
    assert not (folder / "film" / "pass-1" / "plan.json").exists()
