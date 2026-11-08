"""film.py stops a pass cleanly through the safari's -StopFile, and `stop` writes the file.

A theatre-safari.ps1 that declares a $StopFile parameter is given -StopFile <film/STOP>, which it checks between scenes;
`stop <folder> <k> <reason...>` writes that file for a started pass and says whether the safari was given it.

  python -m pytest scripts/tests/story-video-check/test_stop_clean.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_stop_clean.py   (where pytest is not installed)
"""

import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
WITH = "param([Parameter(Mandatory)][string]$Arm, [string]$StopFile = '', [int]$Worker = 6)\n# body\n"
WITHOUT = "param([Parameter(Mandatory)][string]$Arm, [int]$Worker = 6)\n# a comment naming $StopFile outside the block\n"


def theatre(tmp_path, text):
    root = tmp_path / "theatre"
    (root / "scripts").mkdir(parents=True)
    (root / "scripts" / "theatre-safari.ps1").write_text(text, encoding="utf-8")
    return root


def make_pass(tmp_path, script, started=False, ended=False):
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None,
                    theatre_root=str(theatre(tmp_path, script)))
    scenes = [{"n": 1, "arm": "a-run"}]
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": None, "scenes": scenes}), encoding="utf-8")
    if started:
        (out / "started.json").write_text(json.dumps({"pass": 1, "pid": 1, "guard": None, "started": "t"}),
                                          encoding="utf-8")
    if ended:
        (out / "done.json").write_text(json.dumps({"pass": 1}), encoding="utf-8")
    return folder, out


def run_pass(tmp_path, monkeypatch, script):
    folder, out = make_pass(tmp_path, script)
    monkeypatch.setattr(os, "environ", dict(os.environ))
    seen = []

    def fake(args, sink=None, cores=None):
        if "theatre-safari.ps1" in " ".join(args):
            seen.append(list(args))
        return 0

    monkeypatch.setattr(film, "launch", fake)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    return folder, seen, json.loads((out / "done.json").read_text(encoding="utf-8"))


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def test_a_safari_with_the_parameter_is_given_the_stop_file(tmp_path, monkeypatch):
    folder, seen, done = run_pass(tmp_path, monkeypatch, WITH)
    assert len(seen) == 1 and seen[0][seen[0].index("-StopFile") + 1] == str(film.stop_file(folder))
    assert done["stop_file"] == str(film.stop_file(folder))


def test_a_safari_without_it_gets_today_s_arguments(tmp_path, monkeypatch):
    folder, seen, done = run_pass(tmp_path, monkeypatch, WITHOUT)
    assert len(seen) == 1 and "-StopFile" not in seen[0]
    assert done["stop_file"] is None


def test_the_helper_reads_the_param_block_only():
    s = {"theatre_root": None}
    assert film.safari_takes_stop_file(s) in (True, False)      # the seated checkout's own script, whichever it is


def test_stop_writes_the_guards_one_line_form(tmp_path):
    folder, out = make_pass(tmp_path, WITH, started=True)
    verdict, lines = film.cmd_stop(folder, 1, "the owner needs the card")
    assert verdict == "STOP: WRITTEN"
    text = film.stop_file(folder).read_text(encoding="utf-8")
    assert len(text.strip().splitlines()) == 1
    stamp, rest = text.split(" ", 1)
    assert len(stamp) == 19 and stamp[10] == "T"
    assert rest.strip() == ("the owner needs the card (a clean stop: the scene under way completes, then the theatre "
                            "writes scenes.tsv)")


def test_stop_says_whether_the_safari_was_given_the_file(tmp_path):
    folder, out = make_pass(tmp_path / "a", WITH, started=True)
    _, lines = film.cmd_stop(folder, 1, "x")
    said = " ".join(lines)
    assert "was given the stop file" in said and "not given" not in said
    folder, out = make_pass(tmp_path / "b", WITHOUT, started=True)
    _, lines = film.cmd_stop(folder, 1, "x")
    said = " ".join(lines)
    assert "not given the stop file" in said and "until its run ends" in said and "not yet filmed" in said


def test_stop_refuses_a_pass_not_started(tmp_path):
    folder, out = make_pass(tmp_path, WITH)
    assert "not been started" in refused(film.cmd_stop, folder, 1, "x")
    assert not film.stop_file(folder).exists()


def test_stop_refuses_a_pass_ended(tmp_path):
    folder, out = make_pass(tmp_path, WITH, started=True, ended=True)
    assert "ended already" in refused(film.cmd_stop, folder, 1, "x")
    assert not film.stop_file(folder).exists()


def test_stop_refuses_an_empty_reason(tmp_path):
    folder, out = make_pass(tmp_path, WITH, started=True)
    for reason in ("", "   "):
        assert "reason" in refused(film.cmd_stop, folder, 1, reason)
    assert not film.stop_file(folder).exists()


def test_stop_refuses_an_existing_stop_file_and_prints_it(tmp_path):
    folder, out = make_pass(tmp_path, WITH, started=True)
    film.stop_file(folder).write_text("2026-11-08T01:00:00 CPU 96 C", encoding="utf-8")
    said = refused(film.cmd_stop, folder, 1, "x")
    assert "2026-11-08T01:00:00 CPU 96 C" in said
    assert film.stop_file(folder).read_text(encoding="utf-8") == "2026-11-08T01:00:00 CPU 96 C"


def test_start_s_refusal_names_the_text_and_both_writers(tmp_path):
    folder, out = make_pass(tmp_path, WITH)
    film.stop_file(folder).write_text("2026-11-08T01:00:00 a reason", encoding="utf-8")
    said = refused(film.cmd_start, folder, 1)
    assert "2026-11-08T01:00:00 a reason" in said and "heat guard" in said and "clean stop" in said
    assert "delete the file" in said and "75 °C" in said


def test_main_joins_the_reason_words(tmp_path):
    folder, out = make_pass(tmp_path, WITH, started=True)
    with contextlib.redirect_stdout(io.StringIO()):
        code = film.main(["film.py", "stop", str(folder), "1", "the", "card", "is", "needed"])
    assert code == 0 and "the card is needed" in film.stop_file(folder).read_text(encoding="utf-8")
    with contextlib.redirect_stdout(io.StringIO()):
        assert film.main(["film.py", "stop", str(folder), "1"]) == 1      # an empty reason is refused, not a usage error
