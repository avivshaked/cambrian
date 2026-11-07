"""film.py names a run's farm and checks its source: farm_by_run, the plan's source check and theatre_cores.

A run's `run.json` names the folder of the farm program that recorded it, in a git worktree that can be rebuilt
after the run. On 2026-11-07 round 67's plf-6202 was recorded at 1519f7cd and its worktree then held a later
build with a different Core; film.py refused only a program that was gone, so it would have recorded that run's
windows on the wrong build. `plan` now compares the program's checkout with the run's commit over the
simulation source, and the `farm_by_run` setting names another program for a run. The `theatre_cores` setting
pins the pwsh that starts each theatre, as `farm_cores` pins the window recorder.

  python -m pytest scripts/tests/story-video-check/test_farm_source.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_farm_source.py   (where pytest is not installed)
"""

import importlib.util
import json
import os
import subprocess
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

SHOT = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 100.0,
        "run_to_s": 104.0, "kind": "run"}
EXE = "artifacts/Evosim.Farm/bin/Release/net8.0/Evosim.Farm.exe"


def git(top, *args):
    r = subprocess.run(["git", "-C", str(top), "-c", "user.name=t", "-c", "user.email=t@example.com",
                        "-c", "commit.gpgsign=false", *args], capture_output=True, text=True, encoding="utf-8")
    assert r.returncode == 0, r.stderr
    return r.stdout.strip()


def make_checkout(tmp_path, name="wt"):
    """A tiny git repository with a Core source file and a farm program under artifacts/ (ignored by git);
    returns (top, the first commit, the program's exe)."""
    top = tmp_path / name
    (top / "src" / "Evosim.Core").mkdir(parents=True)
    (top / "src" / "Evosim.Core" / "x.cs").write_text("class X {}\n", encoding="utf-8")
    (top / "notes").mkdir()
    (top / "notes" / "n.txt").write_text("a\n", encoding="utf-8")
    (top / ".gitignore").write_text("artifacts/\n", encoding="utf-8")
    exe = top / EXE
    exe.parent.mkdir(parents=True)
    exe.write_bytes(b"MZ")
    git(top, "init", "-q")
    git(top, "add", "-A")
    git(top, "commit", "-q", "-m", "first")
    return top, git(top, "rev-parse", "HEAD"), exe


def make_rundir(tmp_path, commit, exe=None, name="rundir", **source):
    rd = tmp_path / name
    rd.mkdir(exist_ok=True)
    src = dict(source)
    if commit is not None:
        src["gitCommit"] = commit
    if exe is not None:
        src["programPath"] = str(Path(exe).parent)
    (rd / "run.json").write_text(json.dumps({"source": src}), encoding="utf-8")
    return rd


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


# ---- the source check ---------------------------------------------------------------------------------------

def test_source_unchanged_since_the_commit_passes(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    line = film.source_check("r67-6202", rd, str(exe))
    assert commit in line and str(exe) in line and "matches" in line


def test_source_changed_in_a_later_commit_is_refused(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    (top / "src" / "Evosim.Core" / "x.cs").write_text("class X { int a; }\n", encoding="utf-8")
    git(top, "commit", "-q", "-am", "later")
    said = refused(film.source_check, "r67-6202", rd, str(exe))
    assert "r67-6202" in said and str(exe) in said and commit in said and str(top).replace("\\", "/") in said.replace("\\", "/")
    assert "src/Evosim.Core/x.cs" in said


def test_source_changed_but_not_committed_is_refused(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    (top / "src" / "Evosim.Core" / "x.cs").write_text("class X { int edited; }\n", encoding="utf-8")
    assert "src/Evosim.Core/x.cs" in refused(film.source_check, "r67-6202", rd, str(exe))


def test_a_change_outside_the_simulation_source_passes(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    (top / "notes" / "n.txt").write_text("b\n", encoding="utf-8")
    git(top, "commit", "-q", "-am", "notes only")
    (top / "notes" / "n.txt").write_text("c\n", encoding="utf-8")        # and an uncommitted one
    assert "matches" in film.source_check("r67-6202", rd, str(exe))


def test_only_ten_files_are_named(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    for i in range(12):
        (top / "src" / "Evosim.Core" / f"f{i:02d}.cs").write_text("class Y {}\n", encoding="utf-8")
    git(top, "add", "-A")
    git(top, "commit", "-q", "-m", "many")
    with pytest.raises(film.Refused) as e:
        film.source_check("r67-6202", rd, str(exe))
    lines = e.value.lines           # count the listed files only: the commit's hash can hold "f0" or "f1" too
    named = [x for x in lines if x.strip().startswith("src/Evosim.Core/f")]
    assert len(named) == 10 and any("and 2 more" in x for x in lines) and "12 file" in " ".join(lines)


def test_a_commit_the_checkout_does_not_hold_is_refused(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, "1519f7cd" + "0" * 32, exe)
    said = refused(film.source_check, "r67-6202", rd, str(exe))
    assert "does not hold" in said and "1519f7cd" in said


def test_a_program_in_no_git_checkout_is_refused(tmp_path):
    # Where the tests run inside the project, git would find the project's own checkout above tmp_path.
    bare = tmp_path / "bare"
    exe = bare / "prog" / "Evosim.Farm.exe"
    exe.parent.mkdir(parents=True)
    exe.write_bytes(b"MZ")
    rd = make_rundir(tmp_path, "a" * 40, exe)
    before = os.environ.get("GIT_CEILING_DIRECTORIES")
    os.environ["GIT_CEILING_DIRECTORIES"] = str(bare)         # the real environment, which git inherits
    try:
        said = refused(film.source_check, "r67-6202", rd, str(exe))
    finally:
        if before is None:
            del os.environ["GIT_CEILING_DIRECTORIES"]
        else:
            os.environ["GIT_CEILING_DIRECTORIES"] = before
    assert "no git checkout" in said


def test_a_manifest_without_a_commit_gets_a_line_and_no_refusal(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    (top / "src" / "Evosim.Core" / "x.cs").write_text("class X { int edited; }\n", encoding="utf-8")
    for source_commit in (None, "unknown"):
        rd = make_rundir(tmp_path, source_commit, exe, name=f"rd-{source_commit}")
        line = film.source_check("r67-6202", rd, str(exe))
        assert "could not be checked" in line


# ---- the setting --------------------------------------------------------------------------------------------

def film_folder(tmp_path, **settings):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(settings)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    return folder


def test_the_skills_settings_carry_the_new_keys_at_their_defaults():
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    assert s["farm_by_run"] == {} and s["theatre_cores"] is None


def test_the_setting_is_validated(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    ok = film.film_settings(film_folder(tmp_path, farm_by_run={"r67-6202": str(exe)}), [])
    assert ok["farm_by_run"] == {"r67-6202": str(exe)}
    relative = film.film_settings(film_folder(tmp_path, farm_by_run={"r": "scripts/tests/minirun.py"}), [])
    assert relative["farm_by_run"] == {"r": "scripts/tests/minirun.py"}
    for bad in ([str(exe)], {"r": 5}, {"r": ""}, {"r": str(tmp_path / "missing.exe")}, {"r": str(top)}, "x"):
        said = refused(film.film_settings, film_folder(tmp_path, farm_by_run=bad), [])
        assert "farm_by_run" in said


def test_theatre_cores_has_farm_cores_form(tmp_path):
    for good in (None, "16-19", "0-3,8"):
        film.film_settings(film_folder(tmp_path, theatre_cores=good), [])
    for bad in ("all", "16-", "a-b"):
        assert "theatre_cores" in refused(film.film_settings, film_folder(tmp_path, theatre_cores=bad), [])


def test_neither_setting_is_in_a_shots_key(tmp_path):
    top, commit, exe = make_checkout(tmp_path)
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    assert film.shot_key(SHOT, dict(base)) == film.shot_key(SHOT, dict(base, farm_by_run={"r67-6202": str(exe)},
                                                                       theatre_cores="16-19", farm_cores="16-31"))


# ---- the plan -----------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, rd, **settings):
    """cmd_plan on a one-shot film, the windows and the worker stubbed out, farm_of left as it is."""
    folder = film_folder(tmp_path, **settings)
    (folder / "shots.json").write_text(json.dumps({"shots": [SHOT]}), encoding="utf-8")
    monkeypatch.setattr(film, "run_dir", lambda root, arm: rd)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, shot, h: (shot["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    real_run = subprocess.run

    def fake_windows(args, **kw):
        if args[0] == "git":            # film.subprocess is the module itself, so the source check's git comes here
            return real_run(args, **kw)
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        windows = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
                   for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": windows, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def test_the_override_is_used_in_place_of_the_manifests_program(tmp_path, monkeypatch):
    old, old_commit, old_exe = make_checkout(tmp_path, "old")            # the manifest's, rebuilt since
    (old / "src" / "Evosim.Core" / "x.cs").write_text("class X { int later; }\n", encoding="utf-8")
    git(old, "commit", "-q", "-am", "later")
    good = tmp_path / "good"                                             # a program at the run's source
    git(tmp_path, "clone", "-q", str(old), str(good))
    git(good, "checkout", "-q", old_commit)
    good_exe = good / EXE
    good_exe.parent.mkdir(parents=True)
    good_exe.write_bytes(b"MZ")
    good_commit = old_commit
    rd = make_rundir(tmp_path, old_commit, old_exe)
    # without the override the manifest's program is refused ...
    folder = plan_film(tmp_path / "a", monkeypatch, rd)
    said = refused(film.cmd_plan, folder, 1)
    assert str(old_exe) in said or str(old_exe).replace("\\", "/") in said.replace("\\", "/")
    # ... and with it the pass records on the override, which is the one program, checked against the commit
    folder = plan_film(tmp_path / "b", monkeypatch, rd, farm_by_run={"r67-6202": str(good_exe)})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert Path(plan["farm"]) == Path(good_exe)
    assert any("matches" in ln and good_commit in ln for ln in lines), lines


def test_the_plan_refuses_a_rebuilt_program_before_writing_anything(tmp_path, monkeypatch):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    (top / "src" / "Evosim.Farm").mkdir()
    (top / "src" / "Evosim.Farm" / "p.cs").write_text("class P {}\n", encoding="utf-8")
    git(top, "add", "-A")
    git(top, "commit", "-q", "-m", "later")
    folder = plan_film(tmp_path / "f", monkeypatch, rd)
    assert "src/Evosim.Farm/p.cs" in refused(film.cmd_plan, folder, 1)
    assert not (folder / "film" / "pass-1" / "story.json").exists()


def test_the_plan_names_the_program_and_commit_when_the_source_matches(tmp_path, monkeypatch):
    top, commit, exe = make_checkout(tmp_path)
    rd = make_rundir(tmp_path, commit, exe)
    verdict, lines = film.cmd_plan(plan_film(tmp_path / "f", monkeypatch, rd), 1)
    assert verdict == "FILM PLAN: READY"
    assert any(str(exe) in ln and commit in ln and "matches" in ln for ln in lines), lines


def test_a_film_without_a_program_plans_as_before(tmp_path, monkeypatch):
    rd = make_rundir(tmp_path, "a" * 40)
    verdict, lines = film.cmd_plan(plan_film(tmp_path / "f", monkeypatch, rd), 1)
    assert verdict == "FILM PLAN: READY" and not any("matches" in ln for ln in lines)


# ---- the theatre's cores ------------------------------------------------------------------------------------

def run_pass(tmp_path, monkeypatch, **cores):
    """cmd_run on a planned pass, the farm and the theatre stubbed; each launch's script and cores."""
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4, "worker": 6, "farm_threads": 1,
                "wall_minutes": 10, "runs_root": "runs", "farm_cores": None, "theatre_cores": None}
    settings.update(cores)
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": [{"n": 1, "arm": "r67-6202"}]}),
                                   encoding="utf-8")
    monkeypatch.setattr(os, "environ", dict(os.environ))
    seen = []
    monkeypatch.setattr(film, "launch", lambda args, sink=None, cores=None: seen.append((Path(args[3]).name, cores)) or 0)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    return dict(seen)


def test_the_theatre_is_launched_pinned_to_its_cores(tmp_path, monkeypatch):
    seen = run_pass(tmp_path, monkeypatch, theatre_cores="16-19", farm_cores="20-31")
    assert seen == {"story-windows.ps1": "20-31", "theatre-safari.ps1": "16-19"}


def test_a_null_setting_launches_the_theatre_unpinned(tmp_path, monkeypatch):
    seen = run_pass(tmp_path, monkeypatch)
    assert seen == {"story-windows.ps1": None, "theatre-safari.ps1": None}


def test_a_plan_made_before_the_setting_launches_the_theatre_unpinned(tmp_path, monkeypatch):
    out = tmp_path / "film" / "film" / "pass-1"
    seen = run_pass(tmp_path, monkeypatch)
    plan = json.loads((out / "plan.json").read_text(encoding="utf-8"))
    del plan["settings"]["theatre_cores"]
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    seen.clear()
    monkeypatch.setattr(film, "launch", lambda args, sink=None, cores=None: seen.update({Path(args[3]).name: cores}) or 0)
    film.cmd_run(tmp_path / "film", 1)
    assert seen["theatre-safari.ps1"] is None
