"""A clip's key names the look's dials and the theatre's seat.

Round 67's film, 2026-11-08: 33 takes were counted as filmed across a change of dials (D329) and of seat (2dc6d92c),
because the key held only the look's version number. The key now holds a digest of the look's `wide` and `events`
sections (the ones the safari reads) as look_dials, and theatre_commit's first 8 characters as seat.

  python -m pytest scripts/tests/story-video-check/test_look_key.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_look_key.py   (where pytest is not installed)
"""

import importlib.util
import json
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

LOOK = {"schema": 1, "about": "x", "wide": {"fog": 0.02, "tone": 1.5}, "close": {"fog": 0.1},
        "events": {"birth": 1, "death": 2}, "trial": {"a": 1}, "previewOnly": {"b": 2}}
SHOTS = [{"id": "1.1a", "run": "a", "bodies": [], "camera": "Floor", "run_from_s": 100.0, "run_to_s": 104.0},
         {"id": "1.2a", "run": "b", "bodies": [3], "camera": "Birth", "run_from_s": 5.0, "run_to_s": 9.0}]
BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4, "theatre_commit": "2dc6d92c"}


def put_look(tmp_path, monkeypatch, look=LOOK, text=None):
    path = tmp_path / "look" / "theatre-film-look.json"
    path.parent.mkdir(exist_ok=True)
    path.write_text(text if text is not None else json.dumps(look), encoding="utf-8")
    monkeypatch.setattr(film, "look_file", lambda: path)
    return path


def keys(**settings):
    s = dict(BASE, **settings)
    s["look_dials"] = film.look_dials()
    return [film.shot_key(x, s) for x in SHOTS]


def changed(**kw):
    return {**LOOK, **kw}


def all_differ(a, b):
    return all(x != y for x, y in zip(a, b))


def test_a_wide_dial_changes_every_key(tmp_path, monkeypatch):
    put_look(tmp_path, monkeypatch)
    before = keys()
    put_look(tmp_path, monkeypatch, changed(wide={"fog": 0.03, "tone": 1.5}))
    assert all_differ(before, keys())


def test_an_events_dial_changes_every_key(tmp_path, monkeypatch):
    put_look(tmp_path, monkeypatch)
    before = keys()
    put_look(tmp_path, monkeypatch, changed(events={"birth": 1, "death": 3}))
    assert all_differ(before, keys())


def test_sections_the_safari_does_not_read_change_no_key(tmp_path, monkeypatch):
    put_look(tmp_path, monkeypatch)
    before = keys()
    put_look(tmp_path, monkeypatch, changed(close={"fog": 0.9}, trial={"a": 9}))
    assert before == keys()


def test_key_order_and_spacing_do_not_matter(tmp_path, monkeypatch):
    put_look(tmp_path, monkeypatch)
    before = keys()
    reordered = {"events": {"death": 2, "birth": 1}, "wide": {"tone": 1.5, "fog": 0.02}, "schema": 1}
    put_look(tmp_path, monkeypatch, text=json.dumps(reordered, indent=4))
    assert before == keys()


def test_the_seat_is_in_the_key_by_its_first_eight_characters(tmp_path, monkeypatch):
    put_look(tmp_path, monkeypatch)
    base = keys()
    assert all_differ(base, keys(theatre_commit="3ec6d92c"))
    assert base == keys(theatre_commit="2DC6D92C") == keys(theatre_commit="2dc6d92cabcdef0123456789")


def test_a_missing_look_file_is_refused_with_its_path(tmp_path, monkeypatch):
    path = tmp_path / "gone" / "theatre-film-look.json"
    monkeypatch.setattr(film, "look_file", lambda: path)
    with pytest.raises(film.Refused) as e:
        film.look_dials()
    assert str(path) in " | ".join(e.value.lines)
    with pytest.raises(film.Refused) as e:
        film.shot_key(SHOTS[0], dict(BASE))
    assert str(path) in " | ".join(e.value.lines)


def test_film_settings_reads_the_look_once_and_the_plan_names_both(tmp_path, monkeypatch):
    path = put_look(tmp_path, monkeypatch)
    folder = tmp_path / "film"
    folder.mkdir()
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s["theatre_commit"] = "2DC6D92Cffff"
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    got = film.film_settings(folder, [])
    assert got["look_dials"] == film.look_dials()
    path.write_text(json.dumps(changed(wide={"fog": 9})), encoding="utf-8")   # later reads of the file do not count
    assert film.shot_key(SHOTS[0], got) == film.shot_key(SHOTS[0], got)
    assert film.fields_look(got) == got["look_dials"]
