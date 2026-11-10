"""The dials_by_shot setting: theatre dials that belong to one shot, carried in the pass's own settings.

  python -m pytest scripts/tests/story-video-check/test_dials_by_shot.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_dials_by_shot.py   (where pytest is not installed)

The dial names here are ones scripts/theatre-film-look.json does not set (EVOSIM_THEATRE_ZETA_*), and
EVOSIM_THEATRE_LOOK stands for one it does.
"""

import importlib.util
import json
import os
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

A, B = "EVOSIM_THEATRE_ZETA_A", "EVOSIM_THEATRE_ZETA_B"


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
ONE = shot("2.3a", a=100.0)
TWO = shot("2.4b", a=200.0)
THREE = shot("2.5a", a=300.0)


def test_the_setting_is_validated(tmp_path):
    good = {"2.3a": {A: "2", B: "0.5"}}
    ok = film.film_settings(film_folder(tmp_path, [], dials_by_shot=good), [])
    assert ok["dials_by_shot"] == good
    for bad in ({"2.3a": {"CARD_NEAR": "2"}}, {"2.3a": {"EVOSIM_THEATRE_zeta": "2"}}, {"2.3a": {"EVOSIM_THEATRE_": "2"}},
                {"2.3a": {A: "2 3"}}, {"2.3a": {A: ""}}, {"2.3a": {A: 2}}, {"2.3a": {A: None}}, {"2.3a": "x"},
                ["2.3a"], "2.3a"):
        text = refused(film.film_settings, film_folder(tmp_path, [], dials_by_shot=bad), [])
        assert "dials_by_shot" in text, bad


def test_a_dial_the_look_sets_is_refused_with_the_looks_value(tmp_path):
    look = film.look_values()
    assert "EVOSIM_THEATRE_LOOK" in look and A not in look
    text = refused(film.film_settings, film_folder(tmp_path, [], dials_by_shot={"2.3a": {"EVOSIM_THEATRE_LOOK": "2"}}), [])
    assert "EVOSIM_THEATRE_LOOK is set by the look" in text and f"gives {look['EVOSIM_THEATRE_LOOK']}" in text, text


def test_a_file_without_the_key_behaves_as_today(tmp_path):
    folder = film_folder(tmp_path, [])
    s = json.loads((folder / film.SETTINGS).read_text(encoding="utf-8"))
    s.pop("dials_by_shot", None)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    assert "dials_by_shot" not in film.film_settings(folder, [])
    assert film.shot_key(ONE, dict(BASE)) == film.shot_key(ONE, dict(BASE, dials_by_shot={}))


def test_the_key_holds_the_dials_of_the_named_shot_alone():
    s = dict(BASE, dials_by_shot={"2.3a": {B: "5", A: "2"}})
    assert film.shot_key(ONE, s) != film.shot_key(ONE, dict(BASE))
    assert film.shot_key(TWO, s) == film.shot_key(TWO, dict(BASE))      # no clip already filmed goes stale
    assert film.dials_of(ONE, s) == [[A, "2"], [B, "5"]] and film.dials_of(TWO, s) == []
    assert film.shot_key(ONE, s) == film.shot_key(ONE, dict(BASE, dials_by_shot={"2.3a": {A: "2", B: "5"}}))
    assert film.shot_key(ONE, s) != film.shot_key(ONE, dict(BASE, dials_by_shot={"2.3a": {A: "3", B: "5"}}))


# ---- the plan -----------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shots, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots}:
        d = runs / arm / "r1"
        d.mkdir(parents=True)
        (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
        (d / "lineage.jsonl").write_text("", encoding="utf-8")
    folder = film_folder(tmp_path, shots, runs_root=str(runs), **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"], "birth": None}
              for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def plan_of(folder):
    return json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))


def test_unset_the_plan_is_as_today(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [ONE, TWO])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert plan_of(folder)["dials"] == {} and "shot dials: none" in lines
    assert not any("dial set" in l for l in lines)


def test_two_dial_sets_film_the_larger_first_and_the_other_waits(tmp_path, monkeypatch):
    dials = {"2.3a": {A: "2"}, "2.4b": {A: "2"}, "2.5a": {B: "9"}}
    folder = plan_film(tmp_path, monkeypatch, [THREE, ONE, TWO], dials_by_shot=dials)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = plan_of(folder)
    assert sorted(e["shot"] for e in plan["scenes"]) == ["2.3a", "2.4b"] and plan["dials"] == {A: "2"}
    waits = [l for l in lines if "wait for the next passes" in l]
    assert len(waits) == 1 and waits[0].startswith("1 shots of other dial sets") and f"{B}=9" in waits[0]
    assert f"shot dials: {A}=2" in lines


def test_a_tie_goes_to_no_dials(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [ONE, TWO], dials_by_shot={"2.3a": {A: "2"}})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = plan_of(folder)
    assert [e["shot"] for e in plan["scenes"]] == ["2.4b"] and plan["dials"] == {}
    assert "shot dials: none" in lines and any("wait for the next passes" in l and A in l for l in lines)


def test_a_tie_between_two_sets_goes_by_their_text(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [ONE, TWO], dials_by_shot={"2.3a": {B: "1"}, "2.4b": {A: "1"}})
    film.cmd_plan(folder, 1)
    assert [e["shot"] for e in plan_of(folder)["scenes"]] == ["2.4b"]


def test_the_plans_head_shows_the_dials(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [ONE], dials_by_shot={"2.3a": {B: "5", A: "2"}})
    verdict, lines = film.cmd_plan(folder, 1)
    head = [l for l in lines if l.startswith("shot dials:")]
    assert head == [f"shot dials: {A}=2, {B}=5"], lines
    assert lines.index(head[0]) == [i for i, l in enumerate(lines) if l.startswith("look dials")][0] + 1


def test_a_record_only_pass_does_not_split(tmp_path, monkeypatch):
    dials = {"2.3a": {A: "2"}, "2.5a": {B: "9"}}
    folder = plan_film(tmp_path, monkeypatch, [ONE, TWO, THREE], dials_by_shot=dials, record_only=True)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert len(plan_of(folder)["scenes"]) == 3 and plan_of(folder)["dials"] == {}
    assert not any("dial set" in l for l in lines)


def test_an_unknown_id_warns_and_the_plan_is_ready(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [ONE], dials_by_shot={"9.9z": {A: "2"}})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    warns = [x for x in lines if "9.9z" in x]
    assert len(warns) == 1 and warns[0].startswith("warning:") and "not in shots.json" in warns[0]


# ---- the run ------------------------------------------------------------------------------------------------

def run_pass(tmp_path, monkeypatch, dials):
    """cmd_run on a pass planned with these dials, the farm and the theatre stubbed: each launch as (script, env)."""
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None)
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None,
            "scenes": [{"n": 1, "arm": "r67-6202"}]}
    if dials is not None:
        plan["dials"] = dials
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    monkeypatch.setattr(os, "environ", dict(os.environ))
    os.environ.pop(A, None)
    seen = []

    def fake(args, sink=None, cores=None, env=None):
        seen.append((Path(args[3]).name, env))
        return 0

    monkeypatch.setattr(film, "launch", fake)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    return seen, json.loads((out / "done.json").read_text(encoding="utf-8"))


def test_the_theatres_launch_carries_the_dials_and_the_farms_does_not(tmp_path, monkeypatch):
    seen, done = run_pass(tmp_path, monkeypatch, {A: "2", B: "5"})
    by = dict(seen)
    assert by["theatre-safari.ps1"][A] == "2" and by["theatre-safari.ps1"][B] == "5"
    assert by["theatre-safari.ps1"]["EVOSIM_THEATRE_LOOK"] == "4"              # the rest of the environment is kept
    assert by["story-windows.ps1"] is None and A not in os.environ             # nothing leaks to the farm or this job
    assert done["dials"] == {A: "2", B: "5"}


def test_a_pass_without_dials_launches_as_it_always_did(tmp_path, monkeypatch):
    for n, dials in enumerate((None, {})):     # a plan made before the setting has no dials key at all
        seen, done = run_pass(tmp_path / str(n), monkeypatch, dials)
        assert [env for _, env in seen] == [None, None] and done["dials"] == {}
