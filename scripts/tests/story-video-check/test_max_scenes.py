"""The max_scenes setting: a pass films at most that many scenes, in shot-list order, and the rest wait.

Round 67's theatre leaks memory per scene, so on 2026-11-08 the coordinator ruled that the refilm runs in passes of at
most 12 scenes, each its own Unity process. runs_only could hold back whole runs, but nothing capped the count.

  python -m pytest scripts/tests/story-video-check/test_max_scenes.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_max_scenes.py   (where pytest is not installed)
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


def test_the_setting_is_validated(tmp_path):
    for good in (None, 1, 3, 12, 40):
        ok = film.film_settings(film_folder(tmp_path, [], max_scenes=good), [])
        assert ok["max_scenes"] == good
    for bad in (0, -1, 2.5, "12", True, [3]):
        assert "max_scenes" in refused(film.film_settings, film_folder(tmp_path, [], max_scenes=bad), [])


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = shot("1.1a", "a")
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, max_scenes=3))


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


SHOTS = [shot("6.1a", "r67-a"), shot("6.2a", "r67-b"), shot("6.3a", "r67-a", 200.0), shot("6.4a", "r67-c"),
         shot("6.5a", "r67-b", 300.0)]
ORDER = ["6.1a", "6.2a", "6.3a", "6.4a", "6.5a"]


def planned(folder):
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    return [e["shot"] for e in plan["scenes"]], plan


def test_null_films_every_shot(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, max_scenes=None)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ORDER
    assert not any("max_scenes" in x for x in lines)


def test_three_films_the_first_three_and_names_the_rest(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, max_scenes=3)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    shots, plan = planned(folder)
    assert shots == ORDER[:3]
    story = json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))
    assert len(story["scenes"]) == 3
    wait = [x for x in lines if "max_scenes" in x]
    assert len(wait) == 1 and wait[0].startswith("2 shots wait for the next passes under max_scenes 3: 6.4a, 6.5a")
    assert "3 shots to film" in lines[0] and "0 filmed already" in lines[0]
    assert "r67-c" not in {e["arm"] for e in plan["scenes"]}


def test_a_cap_at_or_above_the_count_changes_nothing(tmp_path, monkeypatch):
    for cap in (5, 6, 12):
        folder = plan_film(tmp_path / str(cap), monkeypatch, SHOTS, max_scenes=cap)
        verdict, lines = film.cmd_plan(folder, 1)
        assert verdict == "FILM PLAN: READY", lines
        assert planned(folder)[0] == ORDER
        assert not any("max_scenes" in x for x in lines)


def test_with_runs_only_the_cap_applies_to_the_named_runs(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, runs_only=["r67-a", "r67-b"], max_scenes=2)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ["6.1a", "6.2a"]
    wait = [x for x in lines if "max_scenes" in x]
    assert len(wait) == 1 and wait[0].startswith("2 shots wait") and "6.3a, 6.5a" in wait[0]
    assert "6.4a" not in wait[0]
    assert any("runs_only" in x and x.startswith("1 shots") for x in lines)


def test_the_estimate_comes_from_the_kept_shots(tmp_path, monkeypatch):
    full = plan_film(tmp_path / "a", monkeypatch, SHOTS, max_scenes=None)
    film.cmd_plan(full, 1)
    capped = plan_film(tmp_path / "b", monkeypatch, SHOTS, max_scenes=2)
    verdict, lines = film.cmd_plan(capped, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert "2 shots to film" in lines[0]
    assert len(planned(capped)[1]["scenes"]) == 2
