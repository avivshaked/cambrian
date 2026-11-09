"""The subject_by_shot setting: a world shot's subject named in the filming settings, for the theatre's director.

  python -m pytest scripts/tests/story-video-check/test_subject_by_shot.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_subject_by_shot.py   (where pytest is not installed)
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


def shot(sid, run="r67-a", camera="Card", bodies=(), a=100.0):
    return {"id": sid, "run": run, "bodies": list(bodies), "camera": camera, "run_from_s": a, "run_to_s": a + 4.0,
            "kind": "run"}


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


def test_the_setting_is_validated(tmp_path):
    ok = film.film_settings(film_folder(tmp_path, [], subject_by_shot={"2.3a": "plants"}), [])
    assert ok["subject_by_shot"] == {"2.3a": "plants"}
    for bad in ({"2.3a": "animals"}, {"2.3a": 1}, {"2.3a": None}, {"2.3a": ["plants"]}, ["plants"], "plants"):
        text = refused(film.film_settings, film_folder(tmp_path, [], subject_by_shot=bad), [])
        assert "subject_by_shot" in text and "plants" in text, bad


def test_a_file_without_the_key_behaves_as_today(tmp_path):
    folder = film_folder(tmp_path, [])
    s = json.loads((folder / film.SETTINGS).read_text(encoding="utf-8"))
    s.pop("subject_by_shot", None)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    assert "subject_by_shot" not in film.film_settings(folder, [])
    x = shot("2.3a")
    assert film.subject(x, s) == "world" and film.subject(x) == "world"
    assert film.shot_key(x, dict(BASE)) == film.shot_key(x, dict(BASE, subject_by_shot={}))


def test_the_key_changes_for_the_named_shot_alone():
    a, b = shot("2.3a"), shot("2.4a")
    s = dict(BASE, subject_by_shot={"2.3a": "plants"})
    assert film.shot_key(a, s) != film.shot_key(a, dict(BASE))
    assert film.shot_key(b, s) == film.shot_key(b, dict(BASE))


def test_subject_function():
    s = dict(BASE, subject_by_shot={"2.3a": "plants"})
    assert film.subject(shot("2.3a"), s) == "plants"
    assert film.subject(shot("2.4a"), s) == "world"
    assert film.subject(shot("3.1a", camera="Portrait", bodies=["77"]), s) == "body 77"


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
    monkeypatch.setattr(film, "births", lambda rd, body, a, b: [a + film.BIRTH_LEAD_S])
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"] - (film.BIRTH_LEAD_S if sc["station"] == "Birth" else 0)}
              for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


SHOTS = [shot("2.3a"), shot("2.4a", a=200.0), shot("3.1a", camera="Portrait", bodies=["77"], a=300.0)]
BIRTH = shot("3.2a", camera="Birth", bodies=["78"], a=400.0)


def story_of(folder):
    return json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))


def test_the_plan_writes_the_subject(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS, subject_by_shot={"2.3a": "plants"})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert {sc["n"]: sc["subject"] for sc in story_of(folder)["scenes"]} == {1: "plants", 2: "world", 3: "body 77"}
    assert any(x.startswith("shot 2.3a: Card on plants,") for x in lines), lines
    assert any(x.startswith("shot 2.4a: Card on world,") for x in lines)


def test_without_the_entry_every_subject_is_as_before(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert [sc["subject"] for sc in story_of(folder)["scenes"]] == ["world", "world", "body 77"]


def test_a_body_shot_is_refused(tmp_path, monkeypatch):
    for sid in ("3.1a", "3.2a"):
        folder = plan_film(tmp_path / sid, monkeypatch, SHOTS + [BIRTH], subject_by_shot={sid: "plants"})
        text = refused(film.cmd_plan, folder, 1)
        assert "subject_by_shot" in text and sid in text and "world station" in text


def test_an_unknown_id_warns_and_the_plan_is_ready(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, SHOTS[:2], subject_by_shot={"9.9z": "plants"})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    warns = [x for x in lines if "9.9z" in x]
    assert len(warns) == 1 and warns[0].startswith("warning:") and "not in shots.json" in warns[0]
