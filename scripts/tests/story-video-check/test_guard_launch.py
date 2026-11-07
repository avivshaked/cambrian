"""film.py start launches film-guard.ps1 beside the job; theatre_commit gates plan and start; collect refuses a look
that was not the main checkout's calibrated one.

  python -m pytest scripts/tests/story-video-check/test_guard_launch.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_guard_launch.py   (where pytest is not installed)
"""

import importlib.util
import json
import re
import subprocess
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4, "worker": 7, "farm_threads": 1,
        "wall_minutes": 10, "runs_root": "runs", "reference": "ref.mp4"}


def git(top, *args):
    r = subprocess.run(["git", "-C", str(top), "-c", "user.name=t", "-c", "user.email=t@example.com",
                        "-c", "commit.gpgsign=false", *args], capture_output=True, text=True, encoding="utf-8")
    assert r.returncode == 0, r.stderr
    return r.stdout.strip()


def plan_of(tmp_path, root=None, farm=None, **extra):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    s = dict(BASE, theatre_root=str(root) if root else None, **extra)
    plan = {"pass": 1, "settings": s, "size": "1920x1080", "farm": farm, "colour": None, "scenes": []}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    return folder, out, plan


def args_of(tmp_path, **kw):
    folder, out, plan = plan_of(tmp_path, **kw)
    return film.guard_args(folder, 1, plan, 4321), folder, out


def value(args, flag):
    return args[args.index(flag) + 1]


def test_standard_runs_the_guard_without_card_terms(tmp_path):
    args, folder, out = args_of(tmp_path, guard="standard", guard_minutes=600)
    assert args[:3] == ["-NoProfile", "-File", str(SKILL / "film-guard.ps1")]
    assert value(args, "-Done") == str(out / "done.json") and value(args, "-Log") == str(out / "guard.log")
    assert value(args, "-StopFile") == str(film.stop_file(folder)) and value(args, "-KillTree") == "4321"
    assert value(args, "-Minutes") == "600" and "-CardTerms" not in args and "-FarmExe" not in args


def test_an_absent_setting_is_standard(tmp_path):
    args, _, _ = args_of(tmp_path)
    assert args is not None and "-CardTerms" not in args and value(args, "-Minutes") == "600"


def test_card_terms_adds_the_switch_and_minutes_follow_the_setting(tmp_path):
    args, _, _ = args_of(tmp_path, guard="card terms", guard_minutes=90)
    assert "-CardTerms" in args and value(args, "-Minutes") == "90"


def test_null_runs_no_guard(tmp_path):
    args, _, _ = args_of(tmp_path, guard=None)
    assert args is None
    assert "none" in film.guard_text({"guard": None})


def test_the_farm_folder_is_a_regex_with_either_slash(tmp_path):
    args, _, _ = args_of(tmp_path, farm="C:/a b/farm-x/Evosim.Farm.exe")
    rx = value(args, "-FarmExe")
    assert re.search(rx, "C:\\a b\\farm-x\\Evosim.Farm.exe --x") and re.search(rx, "C:/a b/farm-x/Evosim.Farm.exe")
    assert not re.search(rx, "C:/a b/farm-y/Evosim.Farm.exe")


def test_workers_regex_for_a_worktree_root():
    root = film.REPO / "scratch" / "wt-theatre-look"
    rx = film.workers_regex({"worker": 7, "theatre_root": str(root)})
    assert re.search(rx, str(root / "unity-w7" / "x")) and re.search(rx, str(root).replace("\\", "/") + "/unity-w7")
    assert not re.search(rx, str(film.REPO / "unity-w7"))
    assert not re.search(rx, str(root / "unity-w6"))


def test_workers_regex_for_the_repository():
    rx = film.workers_regex({"worker": 6, "theatre_root": None})
    assert re.search(rx, str(film.REPO / "unity-w6" / "Temp")) and not re.search(rx, str(film.REPO / "unity-w7"))


def test_workers_regex_for_a_root_outside_the_repository(tmp_path):
    rx = film.workers_regex({"worker": 7, "theatre_root": str(tmp_path)})
    assert re.search(rx, str(tmp_path / "unity-w7"))


def run_start(tmp_path, monkeypatch, guard_fails=False, **kw):
    folder, out, plan = plan_of(tmp_path, **kw)
    monkeypatch.setattr(film, "cmd_machine", lambda f: ("MACHINE: FREE", []))
    calls = []

    def fake(cmd, **k):
        command = cmd[-1]
        if cmd[0] == "taskkill":
            calls.append(" ".join(cmd))
            return subprocess.CompletedProcess(cmd, 0)
        calls.append(command)
        if "film-guard.ps1" in command and guard_fails:
            k["stdout"].write("boom")
            k["stdout"].flush()
            return subprocess.CompletedProcess(cmd, 1)
        k["stdout"].write(("222" if "film-guard.ps1" in command else "111") + "\n")
        k["stdout"].flush()
        return subprocess.CompletedProcess(cmd, 0)

    monkeypatch.setattr(film.subprocess, "run", fake)
    return folder, out, calls


def test_start_launches_the_guard_beside_the_job_and_records_its_pid(tmp_path, monkeypatch):
    folder, out, calls = run_start(tmp_path, monkeypatch, guard="card terms")
    verdict, lines = film.cmd_start(folder, 1)
    assert verdict == "FILM START: STARTED" and len(calls) == 2
    assert "film.py" in calls[0] and "film-guard.ps1" in calls[1] and "-CardTerms" in calls[1]
    assert "-WindowStyle Hidden" in calls[1] and "guard.out.log" in calls[1] and "'-KillTree', '111'" in calls[1]
    started = json.loads((out / "started.json").read_text(encoding="utf-8"))
    assert started["pid"] == 111 and started["guard"] == 222


def test_start_with_a_null_guard_launches_the_job_alone(tmp_path, monkeypatch):
    folder, out, calls = run_start(tmp_path, monkeypatch, guard=None)
    film.cmd_start(folder, 1)
    assert len(calls) == 1
    assert json.loads((out / "started.json").read_text(encoding="utf-8"))["guard"] is None


def test_a_guard_that_does_not_start_stops_the_job(tmp_path, monkeypatch):
    folder, out, calls = run_start(tmp_path, monkeypatch, guard_fails=True)
    with pytest.raises(film.Refused):
        film.cmd_start(folder, 1)
    assert any(c.startswith("taskkill") and "111" in c for c in calls) and not (out / "started.json").exists()


def test_the_settings_guard_and_commit_are_validated(tmp_path):
    folder = tmp_path / "f"
    folder.mkdir()

    def said(**extra):
        (folder / film.SETTINGS).write_text(json.dumps(dict(BASE, **extra)), encoding="utf-8")
        try:
            film.film_settings(folder, [])
        except film.Refused as e:
            return " | ".join(e.lines)
        return ""

    assert said(guard="film terms") == "" and said(guard="card terms") == "" and said(guard="standard") == "" and said(guard=None) == ""
    assert "guard is" in said(guard="loud") and "guard_minutes" in said(guard_minutes=0)
    assert "guard_minutes" in said(guard_minutes="600")
    assert said(theatre_commit="main") == "" and "theatre_commit" in said(theatre_commit=5)


def seat(tmp_path):
    root = tmp_path / "wt"
    root.mkdir()
    (root / "a.txt").write_text("1", encoding="utf-8")
    git(root, "init", "-q")
    git(root, "add", "-A")
    git(root, "commit", "-q", "-m", "first")
    git(root, "branch", "seat")
    return root


def test_theatre_commit_null_asks_nothing(tmp_path):
    assert film.commit_fault({"theatre_root": str(tmp_path), "theatre_commit": None}) is None


def test_theatre_commit_matches_at_head(tmp_path):
    root = seat(tmp_path)
    assert film.commit_fault({"theatre_root": str(root), "theatre_commit": "seat"}) is None
    assert film.commit_fault({"theatre_root": str(root), "theatre_commit": git(root, "rev-parse", "HEAD")}) is None


def test_theatre_commit_mismatch_names_both_and_refuses_plan_and_start(tmp_path, monkeypatch):
    root = seat(tmp_path)
    old = git(root, "rev-parse", "HEAD")
    (root / "a.txt").write_text("2", encoding="utf-8")
    git(root, "commit", "-q", "-am", "later")
    new = git(root, "rev-parse", "HEAD")
    said = film.commit_fault({"theatre_root": str(root), "theatre_commit": "seat"})
    assert said and old in said and new in said
    (root / "scripts").mkdir()
    (root / "scripts" / "theatre-safari.ps1").write_text("", encoding="utf-8")
    folder, out, plan = plan_of(tmp_path / "p", root=root, theatre_commit="seat")
    (folder / "shots.json").write_text('{"shots": []}', encoding="utf-8")
    monkeypatch.setattr(film, "cmd_machine", lambda f: ("MACHINE: FREE", []))
    (folder / film.SETTINGS).write_text(json.dumps(dict(BASE, theatre_root=str(root), theatre_commit="seat")),
                                        encoding="utf-8")
    for call in (lambda: film.cmd_start(folder, 1), lambda: film.cmd_plan(folder, 2)):
        with pytest.raises(film.Refused) as e:
            call()
        assert old in " ".join(e.value.lines) and new in " ".join(e.value.lines)


def test_an_unknown_commit_is_refused(tmp_path):
    root = seat(tmp_path)
    assert "theatre_commit nope" in film.commit_fault({"theatre_root": str(root), "theatre_commit": "nope"})


def test_the_plan_header_says_the_guard():
    assert "standard" in film.guard_text({}) and "600 minutes" in film.guard_text({})
    said = film.guard_text({"guard": "card terms", "guard_minutes": 30})
    assert "card terms" in said and "30 minutes" in said


def test_film_terms_pass_their_own_switch(tmp_path):
    args, _, _ = args_of(tmp_path, guard="film terms")
    assert "-FilmTerms" in args and "-CardTerms" not in args


def test_the_plan_header_names_the_film_terms():
    said = film.guard_text({"guard": "film terms", "guard_minutes": 30})
    assert "film terms" in said and "95 C once" in said and "90 C twice" in said


MAIN = ("WARNING: look: the main checkout's theatre-film-look.json was not found (no file at X); "
        "reading this repository's own copy at Y")
SPOT = ("WARNING: look: EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT_GATE takes '0.5', the look's stand-in: world 'w' has no row in "
        "scripts/theatre-world-spots.tsv; a world is calibrated before it is filmed (scripts/theatre-spot-gate.py)")


def test_look_log_fault_reads_both_warnings():
    assert film.look_log_fault("fine") is None
    assert "not the main checkout's" in film.look_log_fault("x\n" + MAIN)
    assert "eyespot gate" in film.look_log_fault(SPOT)
    # the blade stops' table too: a world is calibrated before it is filmed
    assert "blade stops" in film.look_log_fault("takes 'x', the look's stand-in: world 'w' has no row in scripts/theatre-world-stops.tsv; a world")
    assert film.look_log_fault("has no row in scripts/some-other-table.tsv") is None


def collect_with(tmp_path, monkeypatch, theatre_log="", launcher_log=""):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    shot = {"id": "1.2a", "run": "r67-6202", "camera": "Floor", "run_from_s": 2.0, "run_to_s": 4.0, "bodies": [],
            "beat": "x"}
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    settings = dict(BASE, worker=6)
    key = film.shot_key(shot, settings, {})
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None,
            "scenes": [{"n": 1, "shot": "1.2a", "key": key, "arm": "r67-6202", "camera": "Floor", "clip_start_s": 1.0,
                        "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
                        "handle_after_s": 1.0, "fog": None, "focus": None, "side": None}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / "story-01-floor.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text("slug\toutcome\tprovenance\nstory-01-floor\tplayed\tFAITHFUL\n", encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text(theatre_log, encoding="utf-8")
    if launcher_log:
        (out / "film-r67-6202.log").write_text(launcher_log, encoding="utf-8")
    monkeypatch.setattr(film, "probe", lambda p: {"frames": 121, "width": 1920, "height": 1080})
    monkeypatch.setattr(film, "reference", lambda f, s: {"luma": 1, "tone": 1, "tone_p10": 0, "edges": 1, "edges_p10": 0})
    monkeypatch.setattr(film, "sheet", lambda *a, **k: None)
    monkeypatch.setattr(film, "yavg", lambda *a, **k: [1.0, 1.0, 1.0])
    monkeypatch.setattr(film, "look", lambda *a, **k: ([1.0], [1.0]))
    monkeypatch.setattr(film, "colour", lambda *a, **k: [1.0, 1.0, 1.0])
    return film.cmd_collect(folder, 1)


def test_collect_keeps_a_clean_take(tmp_path, monkeypatch):
    verdict, lines = collect_with(tmp_path, monkeypatch)
    assert verdict == "CLIPS: PASS", lines


def test_collect_refuses_a_take_whose_look_was_not_the_main_checkouts(tmp_path, monkeypatch):
    verdict, lines = collect_with(tmp_path, monkeypatch, theatre_log=MAIN)
    assert verdict == "CLIPS: FAIL" and any("not the main checkout's" in l for l in lines)


def test_collect_refuses_a_stand_in_gate_in_the_launchers_log(tmp_path, monkeypatch):
    verdict, lines = collect_with(tmp_path, monkeypatch, launcher_log=SPOT)
    assert verdict == "CLIPS: FAIL" and any("eyespot gate" in l for l in lines)
