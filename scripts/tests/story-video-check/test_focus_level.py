"""A shot's focus level: the check, the scene, the shot's key and collect's log check.

The theatre's focus rule (EVOSIM_THEATRE_FOCUS_RULE=1) sets each take's depth of field to off, light or full (a shot may ask for off or full only): the scene's
own `focus.level` when the story gives one, otherwise from the hero's width at the take's first frame. A shot may carry
`"focus": "off" | "full"`; the plan writes it into the scene's focus object and the shot's key, and collect
reads the theatre's `focus rule: take N of <slug>: level X` line.

  python -m pytest scripts/tests/story-video-check/test_focus_level.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_focus_level.py   (where pytest is not installed)
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


def shot_of(sid, camera="Card", run="r67-6202", at=100.0, **more):
    return dict({"id": sid, "run": run, "bodies": [], "camera": camera, "run_from_s": at, "run_to_s": at + 4.0,
                 "kind": "run"}, **more)


# ---- focus_faults ------------------------------------------------------------------------------------------

def test_each_of_the_two_levels_is_sound_where_it_is_allowed():
    assert film.focus_faults(shot_of("1.1a", focus="off")) == []
    for camera in ("Portrait", "Birth"):
        assert film.focus_faults(shot_of("1.1a", camera, focus="full")) == []
        assert film.focus_faults(shot_of("1.1a", camera, focus="off")) == []
    assert film.focus_faults(shot_of("1.1a")) == []      # no focus: nothing to check


def test_light_is_a_fault_like_any_unknown_value():
    faults = film.focus_faults(shot_of("1.1a", "Portrait", focus="light"))
    assert faults and "light" in faults[0]


def test_off_is_allowed_on_any_camera_with_footage():
    for camera in ("Arrival", "Descent", "Portrait", "Floor", "Birth", "Colony", "Card"):
        assert film.focus_faults(shot_of("1.1a", camera, focus="off")) == [], camera


def test_full_is_a_fault_on_any_camera_but_a_hero_shot():
    for camera in ("Arrival", "Descent", "Floor", "Colony", "Card"):
        faults = film.focus_faults(shot_of("1.1a", camera, focus="full"))
        assert faults and "hero" in faults[0] and camera in faults[0], camera


def test_a_value_outside_the_two_is_a_fault():
    for bad in ("blur", "light", "", "Off", None, True, 1, ["off"], {"level": "off"}):
        faults = film.focus_faults(shot_of("1.1a", focus=bad))
        assert faults and "focus" in faults[0], bad


def test_a_focus_on_a_shot_without_footage_is_a_fault():
    alone = {"id": "1.1a", "kind": "graphic", "alone": "the end", "focus": "off"}
    faults = film.focus_faults(alone)
    assert faults and "no footage" in faults[0]


# ---- the check ---------------------------------------------------------------------------------------------

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


def test_the_check_accepts_a_shot_with_a_focus_level(tmp_path, monkeypatch):
    assert check(tmp_path / "a", monkeypatch, good_shot()) == ""
    assert check(tmp_path / "b", monkeypatch, good_shot("Card", focus="off")) == ""
    assert check(tmp_path / "c", monkeypatch, good_shot("Portrait", focus="full")) == ""
    assert check(tmp_path / "d", monkeypatch, good_shot("Portrait", focus="off")) == ""


def test_the_check_refuses_a_bad_value_and_a_shot_without_footage(tmp_path, monkeypatch):
    assert "focus" in check(tmp_path / "a", monkeypatch, good_shot(focus="blur"))
    assert "focus" in check(tmp_path / "b", monkeypatch, good_shot(focus=True))
    assert "light" in check(tmp_path / "d", monkeypatch, good_shot("Portrait", focus="light"))
    assert "hero" in check(tmp_path / "e", monkeypatch, good_shot("Descent", focus="full"))
    alone = {"id": "1.1a", "passage": "1.1", "kind": "graphic", "from_s": 0.0, "to_s": 4.0, "see": "End.",
             "alone": "the end", "beats": [{"at_s": 0.0, "shows": "x"}], "stills": [], "text": [],
             "places": "[facts.md:L1]", "focus": "off"}
    assert "no footage" in check(tmp_path / "c", monkeypatch, alone)


# ---- the key -----------------------------------------------------------------------------------------------

def test_the_key_changes_for_the_shot_with_a_focus_only():
    plain, off, full = shot_of("1.2a"), shot_of("1.2a", focus="off"), shot_of("1.2a", focus="full")
    other = shot_of("2.5a", "Arrival")
    assert film.shot_key(plain, dict(BASE)) != film.shot_key(off, dict(BASE))
    assert film.shot_key(off, dict(BASE)) != film.shot_key(full, dict(BASE))
    assert film.shot_key(plain, dict(BASE)) == film.shot_key(dict(plain), dict(BASE))
    assert film.shot_key(other, dict(BASE)) == film.shot_key(shot_of("2.5a", "Arrival"), dict(BASE))
    assert film.shot_key(off, dict(BASE)) == film.shot_key(shot_of("1.2a", focus="off"), dict(BASE))


# ---- the plan ----------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shot_list, close_focus=None):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    if close_focus is not None:
        s["close_focus"] = close_focus
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


def read_story(folder, k):
    return json.loads((folder / "film" / f"pass-{k}" / "story.json").read_text(encoding="utf-8"))


def read_plan(folder, k):
    return json.loads((folder / "film" / f"pass-{k}" / "plan.json").read_text(encoding="utf-8"))


def test_a_card_shot_with_a_focus_has_the_level_alone_in_its_scene(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_of("1.1a", "Card", focus="off")])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert read_story(folder, 1)["scenes"][0]["focus"] == {"level": "off"}
    assert read_plan(folder, 1)["scenes"][0]["focus_level"] == "off"


def test_a_portrait_shot_with_a_close_focus_and_a_level_has_both(tmp_path, monkeypatch):
    close = {"mode": "gaussian", "radius": 1.5}
    folder = plan_film(tmp_path, monkeypatch, [shot_of("1.1a", "Portrait", focus="full", bodies=["5927"])],
                       close_focus=close)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    scene = read_story(folder, 1)["scenes"][0]
    assert scene["focus"] == {"mode": "gaussian", "radius": 1.5, "level": "full"}
    entry = read_plan(folder, 1)["scenes"][0]
    assert entry["focus"] == close and entry["focus_level"] == "full"     # the mode check still reads the mode alone


def test_a_shot_without_a_focus_plans_as_before(tmp_path, monkeypatch):
    close = {"mode": "gaussian", "radius": 1.5}
    folder = plan_film(tmp_path, monkeypatch, [shot_of("1.1a", "Card"), shot_of("1.2a", "Portrait", at=200.0,
                                                                                 bodies=["5927"])], close_focus=close)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plain, portrait = read_story(folder, 1)["scenes"]
    assert "focus" not in plain and portrait["focus"] == close
    entries = read_plan(folder, 1)["scenes"]
    assert not any("focus_level" in e for e in entries)
    assert not any("focus level" in l for l in lines)


# ---- focus_log_fault ---------------------------------------------------------------------------------------

SLUG = "story-01-card"


def rule_line(level, slug=SLUG, n=1):
    return f"[Theatre] focus rule: take {n} of {slug}: level {level} (the scene's own), hero body 5927\n"


MODE_LINE = f"[Theatre] take 1 of {SLUG}: focus gaussian radius 1.5, the scene's own\n"


def test_a_rule_line_with_the_asked_level_passes():
    assert film.focus_log_fault(SLUG, rule_line("off"), None, "off") is None
    assert film.focus_log_fault(SLUG, rule_line("full") + rule_line("full", n=2), "gaussian", "full") is None


def test_a_rule_line_with_another_level_is_a_fault_naming_both():
    fault = film.focus_log_fault(SLUG, rule_line("full"), None, "off")
    assert fault is not None and "full" in fault and "off" in fault
    # one take of two at the wrong level is enough
    assert film.focus_log_fault(SLUG, rule_line("off") + rule_line("light", n=2), None, "off") is not None


def test_a_level_asked_with_no_rule_line_is_a_fault_that_names_the_switch():
    fault = film.focus_log_fault(SLUG, "", None, "off")
    assert fault is not None and "EVOSIM_THEATRE_FOCUS_RULE" in fault
    assert film.focus_log_fault(SLUG, rule_line("off", slug="story-02-other"), None, "off") is not None   # another scene's line
    assert film.focus_log_fault(SLUG, MODE_LINE, "gaussian", "off") is not None     # the mode line is no answer to a level


def test_no_level_and_no_rule_line_falls_back_to_the_mode_check():
    assert film.focus_log_fault(SLUG, MODE_LINE, "gaussian", None) is None
    assert film.focus_log_fault(SLUG, "", "gaussian", None) is not None
    assert film.focus_log_fault(SLUG, "", None, None) is None     # a scene with neither asks nothing


def test_a_rule_line_with_no_level_asked_passes_and_skips_the_mode_check():
    assert film.focus_log_fault(SLUG, rule_line("light"), "gaussian", None) is None
    assert film.focus_log_fault(SLUG, rule_line("light", slug="story-02-other"), "gaussian", None) is not None


# ---- collect -----------------------------------------------------------------------------------------------

def collect_film(tmp_path, monkeypatch, log, level="off", close=None):
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    shot = shot_of("1.2a", "Card", focus=level) if level else shot_of("1.2a", "Card")
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, runs_root="runs")
    entry = {"n": 1, "shot": "1.2a", "key": film.shot_key(shot, settings), "arm": "r67-6202", "camera": "Card",
             "clip_start_s": 1.0, "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
             "handle_after_s": 1.0, "fog": None, "focus": close, "side": None, "canopy": False}
    if level:
        entry["focus_level"] = level
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None, "scenes": [entry]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / "story-01-card.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text("slug\toutcome\tprovenance\tstation\nstory-01-card\tplayed\tFAITHFUL\tCard\n",
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


def test_collect_keeps_a_clip_whose_log_shows_the_level(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, rule_line("off"))
    assert verdict == "CLIPS: PASS", lines


def test_collect_refuses_a_clip_at_another_level(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, rule_line("full"))
    said = " | ".join(lines)
    assert verdict == "CLIPS: FAIL"
    assert "shot 1.2a" in said and "full" in said and "off" in said


def test_collect_refuses_a_clip_with_no_rule_line(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, "")
    said = " | ".join(lines)
    assert verdict == "CLIPS: FAIL"
    assert "EVOSIM_THEATRE_FOCUS_RULE" in said


def test_collect_does_not_fail_on_a_scene_whose_focus_holds_a_level_only(tmp_path, monkeypatch):
    # the entry's focus is the mode dict or None; a mode-less dict must not raise a KeyError
    verdict, lines = collect_film(tmp_path, monkeypatch, rule_line("off"), close={"level": "off"})
    assert verdict == "CLIPS: PASS", lines
