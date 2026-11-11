"""collect's side check accepts the camera rig's line for a portrait under a graphic.

Since the rig frames every shot the safari director's `take N of <slug> at ...: portrait of ... subject a third off
centre on the left` line is not printed; the rig prints `safari story: story <n>: the subject in the left third`, n
the integer of the slug's `story-NN-` prefix.

  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_rig_side.py
"""

import importlib.util
import json
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, SKILL / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


film = load("film_tool", "film.py")

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}


def rig(n, side="left"):
    return f"[Theatre] safari story: story {n}: the subject in the {side} third, the scene's own\n"


def director(slug, side="left"):
    return (f"[Theatre] take 1 of {slug} at 5 s: portrait of 5927, subject a third off centre on the {side}, "
            "the scene's own\n")


def collect_film(tmp_path, monkeypatch, log, slug):
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    shot = {"id": "1.2a", "run": "r67-6202", "bodies": ["5927"], "camera": "Portrait", "run_from_s": 100.0,
            "run_to_s": 104.0, "kind": "graphic"}
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, runs_root="runs")
    entry = {"n": int(slug.split("-")[1]), "shot": "1.2a", "key": film.shot_key(shot, settings), "arm": "r67-6202", "camera": "Portrait",
             "clip_start_s": 1.0, "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
             "handle_after_s": 1.0, "fog": None, "focus": None, "side": "left", "canopy": False}
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None, "scenes": [entry]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / f"{slug}.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text(f"slug\toutcome\tprovenance\tstation\n{slug}\tplayed\tFAITHFUL\tPortrait\n",
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


S1 = "story-01-r67r1-6202-portrait-nectium-taleplum"
S11 = "story-11-r67r1-6202-portrait-nectium-taleplum"
REFUSAL = "does not put its subject on the left"


def test_the_rig_line_for_its_own_story_collects(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, rig(1), S1)
    assert verdict == "CLIPS: PASS", lines


def test_the_rig_line_for_another_story_is_refused(tmp_path, monkeypatch):
    for k, (log, slug) in enumerate(((rig(11), S1), (rig(1), S11))):
        verdict, lines = collect_film(tmp_path / str(k), monkeypatch, log, slug)
        assert verdict == "CLIPS: FAIL" and REFUSAL in " | ".join(lines), (slug, lines)


def test_the_rig_line_for_the_right_third_is_refused(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, rig(1, "right"), S1)
    assert verdict == "CLIPS: FAIL" and REFUSAL in " | ".join(lines)


def test_the_directors_line_alone_still_collects(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, director(S1), S1)
    assert verdict == "CLIPS: PASS", lines


def test_neither_line_is_refused(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, "[Theatre] nothing\n", S1)
    assert verdict == "CLIPS: FAIL" and REFUSAL in " | ".join(lines)
