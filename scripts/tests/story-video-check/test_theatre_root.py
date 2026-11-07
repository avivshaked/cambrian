"""film.py takes the render worker and theatre-safari.ps1 from the settings' theatre_root, else the repository's root.

Round 67's film found on 2026-11-07 that its worker lived in the theatre lane's worktree, which film.py could not reach.

  python -m pytest scripts/tests/story-video-check/test_theatre_root.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_theatre_root.py   (where pytest is not installed)
"""

import importlib.util
import json
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4, "worker": 7, "farm_threads": 1,
        "wall_minutes": 10, "runs_root": "runs", "reference": "ref.mp4"}
ARMS = ["a-run"]


def checkout(root, theatre="x = 1"):
    """A fake checkout: the safari script, the theatre, and a worker with the same theatre."""
    (root / "scripts").mkdir(parents=True)
    (root / "scripts" / "theatre-safari.ps1").write_text("", encoding="utf-8")
    for place in ("unity", "unity-w7"):
        d = root / place / "Assets" / "Theatre"
        d.mkdir(parents=True)
        (d / "x.cs").write_text(theatre, encoding="utf-8")
    return root


def settings_in(folder, **extra):
    folder.mkdir(exist_ok=True)
    (folder / film.SETTINGS).write_text(json.dumps(dict(BASE, **extra)), encoding="utf-8")


def problems(tmp_path, **extra):
    folder = tmp_path / "film"
    settings_in(folder, **extra)
    try:
        film.film_settings(folder, [])
    except film.Refused as e:
        return " | ".join(e.lines)
    return ""


def test_the_setting_is_validated(tmp_path):
    root = checkout(tmp_path / "wt")
    assert "theatre_root" not in problems(tmp_path, theatre_root=None)
    assert "theatre_root" not in problems(tmp_path, theatre_root=str(root))
    assert "theatre_root" in problems(tmp_path, theatre_root=str(tmp_path / "missing"))
    bare = tmp_path / "bare"
    bare.mkdir()
    assert "theatre_root" in problems(tmp_path, theatre_root=str(bare))
    assert "theatre_root" in problems(tmp_path, theatre_root=7)


def test_null_is_the_repository_root():
    assert film.theatre_root({}) == film.REPO
    assert film.theatre_root({"theatre_root": None}) == film.REPO


def test_the_root_may_be_absolute(tmp_path):
    assert film.theatre_root({"theatre_root": str(tmp_path)}) == tmp_path


def test_a_missing_worker(tmp_path):
    root = checkout(tmp_path / "wt")
    (root / "unity-w7" / "Assets" / "Theatre" / "x.cs").unlink()
    (root / "unity-w7" / "Assets" / "Theatre").rmdir()
    (root / "unity-w7" / "Assets").rmdir()
    (root / "unity-w7").rmdir()
    said = film.stale_worker({"worker": 7, "theatre_root": str(root)})
    assert said and "no render worker unity-w7" in said


def test_a_matching_worker(tmp_path):
    root = checkout(tmp_path / "wt")
    assert film.stale_worker({"worker": 7, "theatre_root": str(root)}) is None


def test_a_differing_theatre(tmp_path):
    root = checkout(tmp_path / "wt")
    (root / "unity-w7" / "Assets" / "Theatre" / "x.cs").write_text("x = 2", encoding="utf-8")
    said = film.stale_worker({"worker": 7, "theatre_root": str(root)})
    assert said and "another theatre" in said and "its seat is the lane's" in said and "new-worker.ps1" not in said


def test_machine_finds_the_lock_under_the_root(tmp_path, monkeypatch):
    root = checkout(tmp_path / "wt")
    lock = root / "unity-w7" / "Temp" / "UnityLockfile"
    lock.parent.mkdir()
    lock.write_text("", encoding="utf-8")
    folder = tmp_path / "film"
    settings_in(folder, theatre_root=str(root))

    class Narration:
        @staticmethod
        def cmd_machine():
            return "MACHINE: FREE", []

    monkeypatch.setattr(film, "narration_tool", lambda: Narration)
    verdict, lines = film.cmd_machine(folder)
    assert verdict == "MACHINE: BUSY" and any(str(lock) in x for x in lines)


def make_pass(tmp_path, **extra):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    s = dict(BASE, farm_cores=None, theatre_cores=None, overlap=True, **extra)
    scenes = [{"n": 1, "arm": ARMS[0]}]
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": s, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": scenes}), encoding="utf-8")
    return folder, out


def run_pass(tmp_path, monkeypatch, root_setting, log_root):
    folder, out = make_pass(tmp_path, theatre_root=root_setting)
    (log_root / "scratch" / "logs").mkdir(parents=True, exist_ok=True)
    (log_root / "scratch" / "logs" / "theatre-safari-a-run.log").write_text("the theatre's log", encoding="utf-8")
    launched = []

    def fake(args, sink=None, cores=None):
        launched.append(args)
        return 0

    monkeypatch.setattr(film, "launch", fake)
    film.cmd_run(folder, 1)
    return launched, out, json.loads((out / "done.json").read_text(encoding="utf-8")), folder


def test_run_launches_the_roots_script_and_copies_its_log(tmp_path, monkeypatch):
    root = checkout(tmp_path / "wt")
    launched, out, done, folder = run_pass(tmp_path, monkeypatch, str(root), root)
    films = [a for a in launched if "theatre-safari.ps1" in " ".join(a)]
    assert len(films) == 1
    assert films[0][films[0].index("-File") + 1] == str(root / "scripts" / "theatre-safari.ps1")
    assert (out / "theatre-a-run.log").read_text(encoding="utf-8") == "the theatre's log"
    assert done["safari"]["a-run"] == str(root / "scratch" / "safari" / "a-run" / "film-film1")


def test_safari_dir_and_collect_read_the_takes_from_the_root(tmp_path, monkeypatch):
    root = checkout(tmp_path / "wt")
    folder = tmp_path / "film"
    s = {"theatre_root": str(root)}
    assert film.safari_dir(folder, 2, "a-run", s) == root / "scratch" / "safari" / "a-run" / "film-film2"
    # collect reads the takes from done.json's "safari" paths, and the log from beside the pass; cmd_run writes both
    # from the root (test_run_launches_the_roots_script_and_copies_its_log), and collect itself names no root.
    import inspect
    assert "REPO" not in inspect.getsource(film.cmd_collect)


def test_null_keeps_every_path_as_today(tmp_path, monkeypatch):
    folder = tmp_path / "film"
    assert film.safari_dir(folder, 1, "a-run") == film.REPO / "scratch" / "safari" / "a-run" / "film-film1"
    assert film.safari_dir(folder, 1, "a-run", {"theatre_root": None}) == film.safari_dir(folder, 1, "a-run")
    # a plan written before the setting existed has no key
    assert film.theatre_root(dict(BASE)) == film.REPO
