"""The child_by_shot setting: the child a Birth shot films, named in the filming settings, for the theatre's director.

  python -m pytest scripts/tests/story-video-check/test_child_by_shot.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_child_by_shot.py   (where pytest is not installed)
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


def shot(sid, run="r67-a", camera="Card", bodies=(), a=100.0, b=None):
    return {"id": sid, "run": run, "bodies": list(bodies), "camera": camera, "run_from_s": a,
            "run_to_s": a + 4.0 if b is None else b, "kind": "run"}


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


BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}

# (parent, child, second): body 78 has children 781 (at 402), 782 (at 407) and 783 (at 500, past the stretch of
# BIRTH); body 79 has the child 791 at 404. A take opens 8 s before the birth and may not open after its stretch
# starts, so a stretch from 400 s holds births up to 408 s.
ROWS = [(78, 781, 402.0), (78, 782, 407.0), (78, 783, 500.0), (79, 791, 404.0)]
BIRTH = shot("3.2a", camera="Birth", bodies=["78"], a=400.0, b=420.0)


def test_the_setting_is_validated(tmp_path):
    ok = film.film_settings(film_folder(tmp_path, [], child_by_shot={"3.2a": "32873"}), [])
    assert ok["child_by_shot"] == {"3.2a": "32873"}
    for bad in ({"3.2a": 32873}, {"3.2a": "body 32873"}, {"3.2a": "32a"}, {"3.2a": ""}, {"3.2a": None},
                {"3.2a": ["32873"]}, ["32873"], "32873"):
        text = refused(film.film_settings, film_folder(tmp_path, [], child_by_shot=bad), [])
        assert "child_by_shot" in text and "digits" in text, bad


def test_a_file_without_the_key_behaves_as_today(tmp_path):
    folder = film_folder(tmp_path, [])
    s = json.loads((folder / film.SETTINGS).read_text(encoding="utf-8"))
    s.pop("child_by_shot", None)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    assert "child_by_shot" not in film.film_settings(folder, [])
    assert film.shot_key(BIRTH, dict(BASE)) == film.shot_key(BIRTH, dict(BASE, child_by_shot={}))


def test_the_key_changes_for_the_named_shot_alone():
    other = shot("3.3a", camera="Birth", bodies=["79"], a=400.0, b=420.0)
    s = dict(BASE, child_by_shot={"3.2a": "782"})
    assert film.shot_key(BIRTH, s) != film.shot_key(BIRTH, dict(BASE))
    assert film.shot_key(other, s) == film.shot_key(other, dict(BASE))
    assert film.shot_key(BIRTH, s) != film.shot_key(BIRTH, dict(BASE, child_by_shot={"3.2a": "781"}))


# ---- the plan -----------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shots, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots}:
        d = runs / arm / "r1"
        d.mkdir(parents=True)
        (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
        (d / "lineage.jsonl").write_text("".join(
            json.dumps({"e": "b", "id": c, "p": p, "t": t, "x": 0}, separators=(",", ":")) + "\n" for p, c, t in ROWS),
            encoding="utf-8")
    folder = film_folder(tmp_path, shots, runs_root=str(runs), **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"],
               "start": sc["second"] - (film.BIRTH_LEAD_S if sc["station"] == "Birth" else 0),
               "birth": {"at": sc["second"]} if sc["station"] == "Birth" else None} for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def story_of(folder):
    return json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))


def plan_of(folder):
    return json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))


def test_unset_the_scene_is_as_today(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [BIRTH])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    sc = story_of(folder)["scenes"][0]
    assert "child" not in sc and sc["second"] == 402.0 and sc["station"] == "Birth"
    assert plan_of(folder)["scenes"][0]["clip_start_s"] == 402.0 - film.BIRTH_LEAD_S


def test_the_second_child_is_filmed_when_named(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [BIRTH], child_by_shot={"3.2a": "782"})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    sc = story_of(folder)["scenes"][0]
    assert sc["child"] == "body 782" and sc["second"] == 407.0
    entry = plan_of(folder)["scenes"][0]
    assert entry["clip_start_s"] == 407.0 - film.BIRTH_LEAD_S and entry["birth_s"] == 407.0


def test_a_body_that_is_not_the_shots_child_is_an_error(tmp_path, monkeypatch):
    for child in ("791", "999"):     # a child of another body, and a body with no birth row at all
        folder = plan_film(tmp_path / child, monkeypatch, [BIRTH], child_by_shot={"3.2a": child})
        text = refused(film.cmd_plan, folder, 1)
        assert "shot 3.2a" in text and f"body {child} is not a child of body 78" in text, text


def test_a_child_born_outside_the_stretch_is_an_error(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [BIRTH], child_by_shot={"3.2a": "783"})
    text = refused(film.cmd_plan, folder, 1)
    assert "shot 3.2a: body 783 is not a child of body 78 born between 400 and 420" in text, text


def test_a_stretch_that_starts_too_soon_is_an_error_for_the_named_child(tmp_path, monkeypatch):
    early = shot("3.2a", camera="Birth", bodies=["78"], a=395.0, b=420.0)    # 407 - 8 = 399 is after 395
    folder = plan_film(tmp_path, monkeypatch, [early], child_by_shot={"3.2a": "782"})
    text = refused(film.cmd_plan, folder, 1)
    assert "opens a birth's take" in text and "at 399 s, after the stretch starts at 395" in text, text


def test_a_portrait_shot_is_refused(tmp_path, monkeypatch):
    portrait = shot("3.1a", camera="Portrait", bodies=["77"], a=300.0)
    folder = plan_film(tmp_path, monkeypatch, [portrait, BIRTH], child_by_shot={"3.1a": "782"})
    text = refused(film.cmd_plan, folder, 1)
    assert "child_by_shot" in text and "3.1a" in text and "Portrait" in text


def test_an_unknown_id_warns_and_the_plan_is_ready(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [BIRTH], child_by_shot={"9.9z": "782"})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    warns = [x for x in lines if "9.9z" in x]
    assert len(warns) == 1 and warns[0].startswith("warning:") and "not in shots.json" in warns[0]
    assert "child" not in story_of(folder)["scenes"][0]
