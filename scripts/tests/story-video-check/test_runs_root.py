"""film.py finds a run under its own root: the settings' runs_root_by_run, else runs_root.

Round 67's film found on 2026-11-07 that its plan was refused for a run under another root, before any split: the
forest test, plf-6202, lies under scratch/plants-work/runs, and the rest of the round under runs.

  python -m pytest scripts/tests/story-video-check/test_runs_root.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_runs_root.py   (where pytest is not installed)
"""

import importlib.util
import json
import os
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
    assert s["runs_root_by_run"] == {}


def test_the_setting_is_validated(tmp_path):
    other = tmp_path / "other"
    other.mkdir()
    for good in ({}, {"plf-6202": str(other)}, {"plf-6202": "scripts"}):
        ok = film.film_settings(film_folder(tmp_path, [], runs_root_by_run=good), [])
        assert ok["runs_root_by_run"] == good
    for bad in ({"plf-6202": str(tmp_path / "missing")}, {"plf-6202": 5}, {"plf-6202": ""}, [str(other)], "x"):
        assert "runs_root_by_run" in refused(film.film_settings, film_folder(tmp_path, [], runs_root_by_run=bad), [])


def test_the_function_reads_the_entry_else_runs_root():
    s = {"runs_root": "runs", "runs_root_by_run": {"b": "scratch/x/runs"}}
    assert film.runs_root_for(s, "a") == film.REPO / "runs"
    assert film.runs_root_for(s, "b") == film.REPO / "scratch" / "x" / "runs"
    assert film.runs_root_for({"runs_root": "runs"}, "b") == film.REPO / "runs"     # a plan made before the setting
    assert film.runs_root_for({"runs_root": "runs", "runs_root_by_run": {"b": str(Path.cwd())}}, "b") == Path.cwd()


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = shot("1.1a", "a")
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, runs_root_by_run={"a": "scripts"}))


# ---- the plan -----------------------------------------------------------------------------------------------

def rundir(root, arm):
    d = root / arm / "r1"
    d.mkdir(parents=True)
    (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
    return d


def plan_film(tmp_path, monkeypatch, shots, windows_args, **settings):
    folder = film_folder(tmp_path, shots, **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        windows_args.append(list(args))
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
              for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def two_roots(tmp_path):
    main, forest = tmp_path / "runs", tmp_path / "forest-runs"
    for arm in ("r67-a", "r67-b"):
        rundir(main, arm)
    rundir(forest, "plf-6202")
    return main, forest


def root_given(seen):
    return seen[0][seen[0].index("--runs-root") + 1]


def test_a_plan_over_two_roots_takes_one_and_names_the_rest(tmp_path, monkeypatch):
    main, forest = two_roots(tmp_path)
    shots = [shot("1.1a", "r67-a"), shot("1.2a", "r67-b"), shot("9.9b", "plf-6202")]
    seen = []
    folder = plan_film(tmp_path, monkeypatch, shots, seen, runs_root=str(main),
                       runs_root_by_run={"plf-6202": str(forest)})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert sorted(e["shot"] for e in plan["scenes"]) == ["1.1a", "1.2a"]
    wait = [x for x in lines if "wait for the next passes" in x and "root" in x]
    assert len(wait) == 1 and "plf-6202" in wait[0] and wait[0].startswith("1 shots")
    # story-windows.py is given the pass's own root
    assert len(seen) == 1 and root_given(seen) == str(main)


def test_the_next_pass_takes_the_other_root(tmp_path, monkeypatch):
    main, forest = two_roots(tmp_path)
    shots = [shot("1.1a", "r67-a"), shot("9.9b", "plf-6202"), shot("9.10a", "plf-6202", 200.0)]
    seen = []
    folder = plan_film(tmp_path, monkeypatch, shots, seen, runs_root=str(main),
                       runs_root_by_run={"plf-6202": str(forest)})
    film.cmd_plan(folder, 1)          # the forest has two shots, the main root one
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert sorted(e["shot"] for e in plan["scenes"]) == ["9.10a", "9.9b"]
    assert root_given(seen) == str(forest)


def test_a_tie_goes_to_the_root_first_by_path(tmp_path, monkeypatch):
    main, forest = two_roots(tmp_path)
    shots = [shot("1.1a", "r67-a"), shot("9.9b", "plf-6202")]
    seen = []
    folder = plan_film(tmp_path, monkeypatch, shots, seen, runs_root=str(main),
                       runs_root_by_run={"plf-6202": str(forest)})
    film.cmd_plan(folder, 1)
    assert root_given(seen) == min(str(main), str(forest))


def test_a_run_under_no_root_is_refused_naming_its_root(tmp_path, monkeypatch):
    main, forest = two_roots(tmp_path)
    folder = plan_film(tmp_path, monkeypatch, [shot("9.9b", "plf-6203")], [], runs_root=str(main),
                       runs_root_by_run={"plf-6203": str(forest)})
    said = refused(film.cmd_plan, folder, 1)
    assert "plf-6203" in said and str(forest) in said


def test_with_the_setting_empty_the_plan_is_as_today(tmp_path, monkeypatch):
    main, forest = two_roots(tmp_path)
    seen = []
    folder = plan_film(tmp_path, monkeypatch, [shot("1.1a", "r67-a"), shot("1.2a", "r67-b")], seen,
                       runs_root=str(main))
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY" and not any("runs root" in x for x in lines)
    assert root_given(seen) == str(main)
    # and a run only under the other root is refused, as before the setting
    folder = plan_film(tmp_path / "b", monkeypatch, [shot("9.9b", "plf-6202")], [], runs_root=str(main))
    assert str(main) in refused(film.cmd_plan, folder, 1)


# ---- the run ------------------------------------------------------------------------------------------------

def run_pass(tmp_path, monkeypatch, arms, **extra):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4, "worker": 6, "farm_threads": 1,
                "wall_minutes": 10, "runs_root": "runs", "farm_cores": None, "theatre_cores": None}
    settings.update(extra)
    scenes = [{"n": i, "arm": a} for i, a in enumerate(arms, 1)]
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": scenes}), encoding="utf-8")
    monkeypatch.setattr(os, "environ", dict(os.environ))
    seen = {}

    def fake(args, sink=None, cores=None):
        if "-RunsRoot" in args:
            seen[args[args.index("-File") + 2]] = args[args.index("-RunsRoot") + 1]
        return 0

    monkeypatch.setattr(film, "launch", fake)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    return seen


def test_run_gives_each_run_its_own_runs_root(tmp_path, monkeypatch):
    seen = run_pass(tmp_path, monkeypatch, ["r67-a", "plf-6202"], runs_root_by_run={"plf-6202": "scripts"})
    assert seen == {"r67-a": str(film.REPO / "runs"), "plf-6202": str(film.REPO / "scripts")}


def test_run_with_the_setting_empty_or_absent_gives_runs_root(tmp_path, monkeypatch):
    for name, extra in (("a", {}), ("b", {"runs_root_by_run": {}})):
        seen = run_pass(tmp_path / name, monkeypatch, ["r67-a", "plf-6202"], **extra)
        assert seen == {"r67-a": str(film.REPO / "runs"), "plf-6202": str(film.REPO / "runs")}
