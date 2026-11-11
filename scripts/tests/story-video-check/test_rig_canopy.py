"""collect does not ask a camera-rig take for the director's canopy line.

Since D347 the rig frames every shot and prints `[Theatre] rig <id> u=...` instead of the director's
`take N of <slug> at T s: canopy: ...`.

  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_rig_canopy.py
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
SLUG = "story-01-r67r1-6202-descent"


def rig(sid):
    return f"[Theatre] rig {sid} u=0.75 t=4510.0 eye=(1,2,3) at=(4,5,6) lens=35 elev=10 pitch=-5\n"


def director():
    return f"[Theatre] take 1 of {SLUG} at 5 s: canopy: under the leaves\n"


def collect_film(tmp_path, monkeypatch, log):
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    shot = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Descent", "run_from_s": 100.0,
            "run_to_s": 104.0, "kind": "run"}
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, runs_root="runs")
    entry = {"n": 1, "shot": "1.2a", "key": film.shot_key(shot, settings), "arm": "r67-6202", "camera": "Descent",
             "clip_start_s": 1.0, "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
             "handle_after_s": 1.0, "fog": None, "focus": None, "side": None, "canopy": True}
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None, "scenes": [entry]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / f"{SLUG}.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text(f"slug\toutcome\tprovenance\tstation\n{SLUG}\tplayed\tFAITHFUL\tDescent\n",
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


REFUSAL = "does not show the canopy plan"


def test_the_rig_line_for_its_id_collects(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, rig("1.2a"))
    assert verdict == "CLIPS: PASS", lines


def test_a_rig_line_for_another_id_is_refused(tmp_path, monkeypatch):
    for k, other in enumerate(("1.2ab", "11.2a", "1.3a")):
        verdict, lines = collect_film(tmp_path / str(k), monkeypatch, rig(other))
        assert verdict == "CLIPS: FAIL" and REFUSAL in " | ".join(lines), (other, lines)


def test_the_directors_line_alone_still_collects(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, director())
    assert verdict == "CLIPS: PASS", lines


def test_neither_line_is_refused(tmp_path, monkeypatch):
    verdict, lines = collect_film(tmp_path, monkeypatch, "[Theatre] nothing\n")
    assert verdict == "CLIPS: FAIL" and REFUSAL in " | ".join(lines)
