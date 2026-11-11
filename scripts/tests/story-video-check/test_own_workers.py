"""The own_workers setting: a film that renders on two workers at once.

Pass A runs on unity-w7 and pass B on unity-w9. Pass B's machine check must not refuse because pass A's Unity (and
its AssetImportWorker child) runs on w7; a farm run, a Unity on its own worker or on a worker not listed, and the
worker's lockfile still bar it.

  python -m pytest scripts/tests/story-video-check/test_own_workers.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_own_workers.py   (where pytest is not installed)
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


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def film_folder(tmp_path, **settings):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(settings)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    return folder


def setup(monkeypatch, tmp_path, own, processes, found, worker=9):
    """processes: {pid: project path or None}; found: the narration tool's busy lines."""
    w7, w9 = tmp_path / "unity-w7", tmp_path / "unity-w9"
    fake = types.SimpleNamespace(cmd_machine=lambda: ("MACHINE: BUSY" if found else "MACHINE: FREE", list(found)))
    monkeypatch.setattr(film, "narration_tool", lambda: fake)
    monkeypatch.setattr(film, "film_settings",
                        lambda folder, lines: {"worker": worker, "theatre_root": "x", "own_workers": own(w7, w9)})
    monkeypatch.setattr(film, "theatre_root", lambda s: tmp_path)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)
    monkeypatch.setattr(film, "unity_projects", lambda: processes(w7, w9))
    return w7, w9


def both(w7, w9):
    return [str(w7), str(w9)]


def on_w7(w7, w9):
    return {11: film.norm_path(w7), 12: film.norm_path(w7)}


def test_unity_on_the_other_own_worker_is_not_waited_for(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, on_w7,
          ["a batchmode Unity, process 11", "a batchmode Unity, process 12"])
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: FREE", lines
    assert len(lines) == 2 and all("on this film's own worker" in x and "does not wait for" in x for x in lines)
    assert "process 11" in lines[0] and "process 12" in lines[1]


def test_forward_slash_quoted_and_case_differences_still_match(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both,
          lambda w7, w9: {11: film.norm_path(str(w7).upper().replace("\\", "/") + "/")},
          ["a batchmode Unity, process 11"])
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: FREE"


def test_a_farm_run_still_bars_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, on_w7,
          ["a farm run, process 5", "a batchmode Unity, process 11", "a batchmode Unity, process 12"])
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY"
    assert "a farm run, process 5" in lines and sum("own worker" in x for x in lines) == 2


def test_a_unity_on_this_passes_own_worker_bars_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, lambda w7, w9: {11: film.norm_path(w9)}, ["a batchmode Unity, process 11"])
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and lines == ["a batchmode Unity, process 11"]


def test_a_unity_on_a_worker_not_listed_bars_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, lambda w7, w9: {11: film.norm_path(tmp_path / "unity-w3")},
          ["a batchmode Unity, process 11"])
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: BUSY"
    setup(monkeypatch, tmp_path, both, lambda w7, w9: {11: None}, ["a batchmode Unity, process 11"])
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: BUSY"


def test_without_own_workers_any_batchmode_unity_bars_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, lambda w7, w9: [], on_w7, ["a batchmode Unity, process 11"])
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and lines == ["a batchmode Unity, process 11"]


def test_an_unreadable_process_list_leaves_it_busy(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, lambda w7, w9: None, ["a batchmode Unity, process 11"])
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and lines == ["a batchmode Unity, process 11"]


def test_the_lockfile_of_this_worker_still_bars_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, on_w7, ["a batchmode Unity, process 11"])
    lock = tmp_path / "unity-w9" / "Temp" / "UnityLockfile"
    lock.parent.mkdir(parents=True)
    lock.write_text("")
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and any("lock is held" in x for x in lines)


def test_a_record_only_pass_is_as_before(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, both, on_w7, ["a batchmode Unity, process 11"])
    verdict, lines = film.cmd_machine(tmp_path, record_only=True)
    assert verdict == "MACHINE: FREE" and lines == ["a batchmode Unity, process 11, which a record-only pass does "
                                                    "not wait for"]
    setup(monkeypatch, tmp_path, both, on_w7, ["a farm run, process 2", "a batchmode Unity, process 11"])
    assert film.cmd_machine(tmp_path, record_only=True)[0] == "MACHINE: BUSY"


def test_the_setting_is_validated(tmp_path):
    w7 = tmp_path / "w7"
    w7.mkdir()
    assert film.film_settings(film_folder(tmp_path), [])  # the default loads
    assert film.film_settings(film_folder(tmp_path, own_workers=[str(w7)]), [])["own_workers"] == [str(w7)]
    for bad in ("scratch/w7", 1, None, [1], [""], {"a": 1}):
        assert "own_workers" in refused(film.film_settings, film_folder(tmp_path, own_workers=bad), [])
    text = refused(film.film_settings, film_folder(tmp_path, own_workers=[str(w7), str(tmp_path / "gone")]), [])
    assert "gone" in text and "not an existing folder" in text


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = {"id": "1.1a", "run": "a", "bodies": [], "camera": "Floor", "run_from_s": 100.0, "run_to_s": 104.0,
         "kind": "run"}
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, own_workers=["a"]))
