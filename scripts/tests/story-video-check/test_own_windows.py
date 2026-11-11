"""A theatre pass does not wait for this film's own window recording.

film.py start asks cmd_machine whether the machine is free; a farm run is a busy reason. This film's own record-only
pass replays a window on the farm (--film-window, under the film's own film directory) on the settings' farm_cores,
and can take an hour. A theatre pass whose theatre_cores share no processor with farm_cores does not wait for it.

  python -m pytest scripts/tests/story-video-check/test_own_windows.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_own_windows.py   (where pytest is not installed)
"""

import importlib.util
import types
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

FARM = "a farm run, process 5"


def window(folder, sub="film/pass-24/windows/story-01-main"):
    return (f'"D:\\x\\Evosim.Farm.exe" --film-window D:/x/runs/r1/2026 20717.4 20733.7 '
            f'{str(folder / sub)} --fps 30 --threads 4')


def setup(monkeypatch, tmp_path, found, commands, own=True, theatre="24-27", farm="16-19", unity=None):
    w7, w9 = tmp_path / "unity-w7", tmp_path / "unity-w9"
    fake = types.SimpleNamespace(cmd_machine=lambda: ("MACHINE: BUSY" if found else "MACHINE: FREE", list(found)))
    monkeypatch.setattr(film, "narration_tool", lambda: fake)
    s = {"worker": 9, "theatre_root": "x", "own_workers": [str(w7), str(w9)] if own else [],
         "theatre_cores": theatre, "farm_cores": farm}
    monkeypatch.setattr(film, "film_settings", lambda folder, lines: s)
    monkeypatch.setattr(film, "theatre_root", lambda s: tmp_path)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)
    monkeypatch.setattr(film, "unity_projects", lambda: unity if unity is not None else {})
    monkeypatch.setattr(film, "farm_commands", lambda: commands)
    return w7, w9


def test_the_films_own_window_recording_is_not_waited_for(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)})
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: FREE", lines
    assert len(lines) == 1 and "process 5" in lines[0] and "own window recording on cores 16-19" in lines[0]
    assert "theatre pass on cores 24-27 does not wait for" in lines[0]


def test_overlapping_cores_bar_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)}, theatre="16-19")
    assert film.cmd_machine(tmp_path) == ("MACHINE: BUSY", [FARM])
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)}, theatre="8-17", farm="16-19,24-27")
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: BUSY"


def test_unset_cores_bar_it(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)}, theatre=None)
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: BUSY"
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)}, farm=None)
    assert film.cmd_machine(tmp_path)[0] == "MACHINE: BUSY"


def test_a_window_recording_of_another_folder_bars_it(monkeypatch, tmp_path):
    other = tmp_path / "other-film"
    setup(monkeypatch, tmp_path, [FARM], {5: window(other)})
    assert film.cmd_machine(tmp_path / "film-a") == ("MACHINE: BUSY", [FARM])


def test_a_round_without_film_window_bars_it(monkeypatch, tmp_path):
    cmd = f'"D:\\x\\Evosim.Farm.exe" --run D:/x/runs/r1 --out {tmp_path / "film" / "x"}'
    setup(monkeypatch, tmp_path, [FARM], {5: cmd})
    assert film.cmd_machine(tmp_path) == ("MACHINE: BUSY", [FARM])


def test_without_own_workers_it_is_as_today(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)}, own=False)
    assert film.cmd_machine(tmp_path) == ("MACHINE: BUSY", [FARM])


def test_an_unreadable_farm_list_leaves_it_busy(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], None)
    assert film.cmd_machine(tmp_path) == ("MACHINE: BUSY", [FARM])


def test_another_farm_run_beside_the_window_recording_bars_it(monkeypatch, tmp_path):
    found = [FARM, "a farm run, process 6"]
    setup(monkeypatch, tmp_path, found, {5: window(tmp_path), 6: "Evosim.Farm.exe --run x"})
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and "a farm run, process 6" in lines and any("own window" in x for x in lines)


def test_together_with_the_unity_exemption(monkeypatch, tmp_path):
    found = [FARM, "a batchmode Unity, process 11"]
    w7, w9 = setup(monkeypatch, tmp_path, found, {5: window(tmp_path)})
    monkeypatch.setattr(film, "unity_projects", lambda: {11: film.norm_path(w7)})
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: FREE", lines
    assert len(lines) == 2 and any("own window recording" in x for x in lines) and any("own worker" in x for x in lines)


def test_a_unity_on_an_unlisted_worker_still_bars_it_beside_the_window(monkeypatch, tmp_path):
    found = [FARM, "a batchmode Unity, process 11"]
    setup(monkeypatch, tmp_path, found, {5: window(tmp_path)}, unity={11: film.norm_path(tmp_path / "unity-w3")})
    verdict, lines = film.cmd_machine(tmp_path)
    assert verdict == "MACHINE: BUSY" and "a batchmode Unity, process 11" in lines


def test_a_record_only_pass_is_unchanged(monkeypatch, tmp_path):
    setup(monkeypatch, tmp_path, [FARM], {5: window(tmp_path)})
    assert film.cmd_machine(tmp_path, record_only=True) == ("MACHINE: BUSY", [FARM])
    setup(monkeypatch, tmp_path, ["a batchmode Unity, process 11"], {})
    verdict, lines = film.cmd_machine(tmp_path, record_only=True)
    assert verdict == "MACHINE: FREE" and "record-only pass does not wait for" in lines[0]
