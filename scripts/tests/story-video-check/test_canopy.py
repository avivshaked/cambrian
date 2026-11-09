"""A shot's canopy: the check, the shot's key, the canopy pass, the run's -Canopy and collect's log check.

Round 67's film, 2026-11-08: the coordinator ruled the canopy for the opening descent alone, and the theatre's
-Canopy switch films every Arrival and Descent of a run's call. A shot may carry `"canopy": true`; the plan films
canopy shots in a pass of their own, and the run adds -Canopy to that pass only.

  python -m pytest scripts/tests/story-video-check/test_canopy.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_canopy.py   (where pytest is not installed)
"""

import importlib.util
import json
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, SKILL / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


film = load("film_tool", "film.py")
shots = load("shots_tool", "shots.py")

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}


def shot_of(sid, camera="Descent", run="r67-6202", at=100.0, **more):
    return dict({"id": sid, "run": run, "bodies": [], "camera": camera, "run_from_s": at, "run_to_s": at + 4.0,
                 "kind": "run"}, **more)


# ---- the check -------------------------------------------------------------------------------------------

def minimal_film(tmp_path, shot_list, monkeypatch):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "script.md").write_text("script", encoding="utf-8")
    (folder / "facts.md").write_text("The run r67-6202.\n", encoding="utf-8")
    (folder / "narration.json").write_text(json.dumps({"passages": [{"id": "1.1", "from_s": 0, "to_s": 4}]}),
                                           encoding="utf-8")
    script = {"beats": [{"passages": [{"id": "1.1", "say": ["x"], "picture": {"kind": "run", "run": "r67-6202",
                                                                              "bodies": [], "seconds": None}}]}]}
    monkeypatch.setattr(shots.sc, "parse_script", lambda text: (script, []))
    monkeypatch.setitem(shots._ENDS, "r67-6202", None)
    data = {"schema": shots.SCHEMA, "script_sha256": shots.sha256_of(folder / "script.md"),
            "narration_sha256": shots.sha256_of(folder / "narration.json"), "shots": shot_list}
    return folder, data


def good_shot(camera="Descent", **more):
    return dict({"id": "1.1a", "passage": "1.1", "kind": "run", "from_s": 0.0, "to_s": 4.0, "see": "Down.",
                 "run": "r67-6202", "bodies": [], "camera": camera, "run_from_s": 10.0, "run_to_s": 14.0,
                 "text": [], "places": "[facts.md:L1]"}, **more)


def check(tmp_path, monkeypatch, shot):
    tmp_path.mkdir(parents=True, exist_ok=True)
    folder, data = minimal_film(tmp_path, [shot], monkeypatch)
    errors, _ = shots.check_shots(data, folder)
    return " | ".join(errors)


def test_the_check_accepts_a_canopy_on_a_descent_and_an_arrival(tmp_path, monkeypatch):
    assert check(tmp_path / "a", monkeypatch, good_shot("Descent")) == ""
    assert check(tmp_path / "b", monkeypatch, good_shot("Descent", canopy=True)) == ""
    assert check(tmp_path / "c", monkeypatch, good_shot("Arrival", canopy=True)) == ""


def test_the_check_refuses_each_fault(tmp_path, monkeypatch):
    assert "Portrait" in check(tmp_path / "a", monkeypatch, good_shot("Portrait", canopy=True))
    assert "canopy" in check(tmp_path / "b", monkeypatch, good_shot("Descent", canopy=False))
    assert "canopy" in check(tmp_path / "c", monkeypatch, good_shot("Descent", canopy="true"))
    assert "canopy" in check(tmp_path / "d", monkeypatch, good_shot("Descent", canopy=1))
    alone = {"id": "1.1a", "passage": "1.1", "kind": "graphic", "from_s": 0.0, "to_s": 4.0, "see": "End.",
             "alone": "the end", "beats": [{"at_s": 0.0, "shows": "x"}], "stills": [], "text": [],
             "places": "[facts.md:L1]", "canopy": True}
    assert "no footage" in check(tmp_path / "e", monkeypatch, alone)


def test_the_sheet_shows_the_canopy(tmp_path):
    path = tmp_path / "shots.json"
    path.write_text(json.dumps({"shots": [good_shot(canopy=True), dict(good_shot(), id="1.1b")]}), encoding="utf-8")
    verdict, lines = shots.cmd_sheet(path)
    assert "canopy" in lines[0] and "canopy" not in lines[1]


# ---- the key ---------------------------------------------------------------------------------------------

def test_a_canopy_changes_the_key_and_no_other_shots_does():
    plain, canopy = shot_of("1.2a"), shot_of("1.2a", canopy=True)
    other = shot_of("2.5a", "Arrival")
    assert film.shot_key(plain, dict(BASE)) != film.shot_key(canopy, dict(BASE))
    # a shot with no canopy field keeps the key it had before the field existed
    assert film.shot_key(plain, dict(BASE)) == film.shot_key(dict(plain), dict(BASE))
    assert film.shot_key(other, dict(BASE)) == film.shot_key(shot_of("2.5a", "Arrival"), dict(BASE))
    assert film.shot_key(canopy, dict(BASE)) == film.shot_key(shot_of("1.2a", canopy=True), dict(BASE))


# ---- the plan --------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shot_list):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": shot_list}), encoding="utf-8")
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
    return folder


def read_plan(folder, k):
    return json.loads((folder / "film" / f"pass-{k}" / "plan.json").read_text(encoding="utf-8"))


LIST = [shot_of("1.2a", "Descent", at=200.0, canopy=True), shot_of("2.5a", "Arrival", at=300.0),
        shot_of("4.1a", "Descent", at=400.0)]


def test_one_pass_films_all_three_and_only_the_canopy_scene_carries_it(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, LIST)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = read_plan(folder, 1)
    assert [e["shot"] for e in plan["scenes"]] == ["1.2a", "2.5a", "4.1a"]
    assert [e["canopy"] for e in plan["scenes"]] == [True, False, False]
    assert "canopy" not in plan
    assert not any("wait" in l for l in lines)
    story = json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))
    assert [sc.get("canopy") for sc in story["scenes"]] == [True, None, None]
    assert [("canopy" in sc) for sc in story["scenes"]] == [True, False, False]


def test_a_film_with_no_canopy_plans_as_before(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_of("2.5a", "Arrival"), shot_of("4.1a", at=400.0)])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    story = json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))
    assert not any("canopy" in sc for sc in story["scenes"])
    assert not any("canopy" in l for l in lines)


# ---- the run ---------------------------------------------------------------------------------------------

def test_run_never_passes_the_switch_and_the_folder_is_untagged(tmp_path, monkeypatch):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None)
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": [{"n": 1, "arm": "r67-6202", "canopy": True}]}),
                                   encoding="utf-8")
    seen = []
    monkeypatch.setattr(film, "launch", lambda args, sink=None, cores=None: seen.append(list(args)) or 0)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    safari = [a for a in seen if any("theatre-safari.ps1" in str(x) for x in a)]
    assert len(safari) == 1 and "-Canopy" not in safari[0]
    done = json.loads((out / "done.json").read_text(encoding="utf-8"))
    assert Path(done["safari"]["r67-6202"]).parent.name == "r67-6202"


def test_safari_dir_is_unchanged(tmp_path):
    p = film.safari_dir(tmp_path / "round", 2, "r67-6202")
    assert p.parent.name == "r67-6202" and p.name == "round-film2"


# ---- collect ---------------------------------------------------------------------------------------------

CANOPY_LINE = ("[Theatre] take 1 of story-01-descent at 12.5 s: canopy: 41 bodies in the top 10 m; the column ranked 1 "
               "(30 bodies in its nine squares) about (3, 4) with its leaves at -2 m; looking 68 deg up\n")
PLAIN_LINE = "[Theatre] take 1 of story-01-descent at 12.5 s: descent: a dolly down\n"


def test_the_canopy_log_check():
    assert film.canopy_log_fault("story-01-descent", CANOPY_LINE) is None
    assert film.canopy_log_fault("story-01-descent", PLAIN_LINE) is not None
    assert film.canopy_log_fault("story-01-descent", "") is not None
    assert film.canopy_log_fault("story-01-arrival", CANOPY_LINE) is not None     # another scene's line


GROUP_LINE = ("[Theatre] safari: take 1 of story-01-descent at 4501 s: descent: group frame, 3 animals round body 5927, "
              "3.3 m wide, in place of the canopy descent under the densest column of leaves at (185.4, -1.8, 139.2), "
              "searched within 25 m; group fill: used (EVOSIM_THEATRE_CANOPY_GROUP took the canopy descent for a group "
              "descent round the canopy's column)\n")
GROUP_PLAIN_LINE = ("[Theatre] safari: take 1 of story-01-descent at 4501 s: descent: group frame, 3 animals round body "
                    "5927, 3.3 m wide; group fill: used\n")
PHRASE_ONLY_LINE = ("[Theatre] safari: note: in place of the canopy descent under the densest column of leaves at "
                    "(185.4, -1.8, 139.2)\n")


def test_the_canopy_log_check_takes_the_group_descent_in_the_canopys_place():
    assert film.canopy_log_fault("story-01-descent", GROUP_LINE) is None
    assert film.canopy_log_fault("story-01-descent", PLAIN_LINE + GROUP_LINE) is None
    assert film.canopy_log_fault("story-01-arrival", GROUP_LINE) is not None      # another scene's line
    assert film.canopy_log_fault("story-01-descent", GROUP_PLAIN_LINE) is not None  # a group the canopy did not ask for
    assert film.canopy_log_fault("story-01-descent", PHRASE_ONLY_LINE) is not None  # no take start
    # the phrase on another line does not count for this slug's line
    assert film.canopy_log_fault("story-01-descent", GROUP_PLAIN_LINE + PHRASE_ONLY_LINE) is not None


def collect_film(tmp_path, monkeypatch, log):
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    shot = shot_of("1.2a", "Descent", canopy=True)
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, runs_root="runs")
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None, "canopy": True,
            "scenes": [{"n": 1, "shot": "1.2a", "key": film.shot_key(shot, settings), "arm": "r67-6202",
                        "camera": "Descent", "clip_start_s": 1.0, "seconds": 4.0, "action_in_s": 1.0,
                        "action_out_s": 3.0, "handle_before_s": 1.0, "handle_after_s": 1.0, "fog": None,
                        "focus": None, "side": None, "canopy": True}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / "story-01-descent.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text("slug\toutcome\tprovenance\tstation\nstory-01-descent\tplayed\tFAITHFUL\tDescent\n",
                                       encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text(log, encoding="utf-8")
    monkeypatch.setattr(film, "probe", lambda p: {"frames": 121, "width": 1920, "height": 1080})
    monkeypatch.setattr(film, "reference", lambda f, s, w=1920: {"luma": 1, "tone": 1, "tone_p10": 0, "edges": 1,
                                                          "edges_p10": 0})
    monkeypatch.setattr(film, "sheet", lambda *a, **k: None)
    monkeypatch.setattr(film, "yavg", lambda *a, **k: [1.0, 1.0, 1.0])
    monkeypatch.setattr(film, "look", lambda *a, **k: ([1.0], [1.0]))
    monkeypatch.setattr(film, "colour", lambda *a, **k: [1.0, 1.0, 1.0])
    return film.cmd_collect(folder, 1)


def test_collect_keeps_a_canopy_clip_whose_log_shows_the_plan(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, CANOPY_LINE)
    assert verdict == "CLIPS: PASS", lines


def test_collect_keeps_a_canopy_clip_whose_log_shows_the_group_descent_in_its_place(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, GROUP_LINE)
    assert verdict == "CLIPS: PASS", lines


def test_collect_refuses_a_canopy_clip_whose_log_lacks_the_plan(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, PLAIN_LINE)
    said = " | ".join(lines)
    assert verdict == "CLIPS: FAIL"
    assert "shot 1.2a" in said and "canopy plan" in said and "theatre-r67-6202.log" in said
