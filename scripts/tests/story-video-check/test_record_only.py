"""The record_only setting: a pass that records the farm windows and films nothing.

A window is a CPU replay of a run from a checkpoint and depends on nothing about the camera, the look, the colour or
the size, and it was most of round 67's last pass (4.4 hours). On 2026-11-08 the refilm was held for camera fixes, and
the coordinator ruled that the windows of the remaining shots be recorded meanwhile. A record-only pass plans every
shot that needs a clip (after runs_only and the split to one farm build and one runs root), records each run's
windows, starts no theatre, and is not collected; the next filming pass reuses its windows.

  python -m pytest scripts/tests/story-video-check/test_record_only.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_record_only.py   (where pytest is not installed)
"""

import hashlib
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


def shot(sid, run, colour=None, a=100.0):
    x = {"id": sid, "run": run, "bodies": [], "camera": "Floor", "run_from_s": a, "run_to_s": a + 4.0, "kind": "run"}
    if colour:
        x["colour"] = colour
    return x


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def film_folder(tmp_path, shots, **settings):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(settings)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    colours = {}
    for name in sorted({x["colour"] for x in shots if x.get("colour")}):
        file = tmp_path / f"{name}.tsv"
        file.write_text("11248\t0.25\n", encoding="utf-8")
        colours[name] = {"shows": f"what {name} shows", "file": str(file),
                         "sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
                         "runs": sorted({x["run"] for x in shots if x.get("colour") == name})}
    if colours:
        (folder / "colours.json").write_text(json.dumps(colours), encoding="utf-8")
    return folder


# ---- the setting --------------------------------------------------------------------------------------------

def test_the_setting_is_validated(tmp_path):
    for good in (True, False):
        assert film.film_settings(film_folder(tmp_path, [], record_only=good), [])["record_only"] is good
    for bad in ("yes", 1, 0, None, [True]):
        assert "record_only" in refused(film.film_settings, film_folder(tmp_path, [], record_only=bad), [])


def test_not_in_a_shots_key():
    base = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}
    x = shot("1.1a", "a")
    assert film.shot_key(x, dict(base)) == film.shot_key(x, dict(base, record_only=True))


# ---- the plan -----------------------------------------------------------------------------------------------

FARM_X, FARM_Y = "farm-x/Evosim.Farm.exe", "farm-y/Evosim.Farm.exe"

SHOTS = [shot("6.1a", "r-a", "teams"), shot("6.2a", "r-a", "teams", 200.0), shot("6.3a", "r-b", "family"),
         shot("6.4a", "r-b", "family", 200.0), shot("6.5a", "r-a", "teams", 300.0), shot("6.6a", "r-c", "teams")]
# build X holds r-a and r-b (five shots, two colours), build Y holds r-c (one shot)
KEPT_RECORD = ["6.1a", "6.2a", "6.3a", "6.4a", "6.5a"]


def plan_film(tmp_path, monkeypatch, shots=SHOTS, reuse=False, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots}:
        d = runs / arm / "r1"
        d.mkdir(parents=True)
        (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
    farms = {}
    for name, rel in (("x", FARM_X), ("y", FARM_Y)):
        f = tmp_path / rel
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_text("exe", encoding="utf-8")
        farms[name] = str(f)
    folder = film_folder(tmp_path, shots, runs_root=str(runs),
                         farm_by_run={"r-a": farms["x"], "r-b": farms["x"], "r-c": farms["y"]}, **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "source_check", lambda arm, rd, farm: f"{arm}: checked")
    monkeypatch.setattr(film, "stale_worker", lambda s: None)
    if not reuse:
        monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"],
               "run": str(runs / sc["run"] / "r1"), "from": sc["second"], "to": sc["second"] + sc["seconds"],
               "fps": 30.0, "out": f"story-{sc['n']:02d}-{sc['run']}-main"} for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def planned(folder, k=1):
    plan = json.loads((folder / "film" / f"pass-{k}" / "plan.json").read_text(encoding="utf-8"))
    return [e["shot"] for e in plan["scenes"]], plan


def test_record_only_plans_every_shot_of_one_build_whatever_its_colour_and_the_cap(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=True, max_scenes=2)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    kept, plan = planned(folder)
    assert kept == KEPT_RECORD                                        # both colours, past the cap of 2
    assert plan["record_only"] is True and plan["colour"] is None
    assert any("1 shots on runs of 1 other farm build" in x and "r-c" in x for x in lines)   # the build split holds
    assert not any("other colours" in x or "wait for the next passes under max_scenes" in x for x in lines)
    assert any("max_scenes 2 is not applied" in x for x in lines)
    assert not any(x.startswith("theatre r-") for x in lines)         # no theatre estimate
    only = [x for x in lines if x.startswith("record only:")]
    assert only == ["record only: the farm windows of 5 shots on 2 runs; nothing is filmed, and the next filming "
                    "pass reuses them"]
    assert "to record, none filmed" in lines[0]
    story = json.loads((folder / "film" / "pass-1" / "story.json").read_text(encoding="utf-8"))
    assert [sc["n"] for sc in story["scenes"]] == [1, 2, 3, 4, 5]


def test_record_only_applies_runs_only_and_ignores_a_pass_of_two_sizes(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=True, runs_only=["r-b"], size_by_shot={"6.3a": "960x540"})
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert planned(folder)[0] == ["6.3a", "6.4a"]
    assert any(x.startswith("record only: the farm windows of 2 shots on 1 runs") for x in lines)


def test_record_only_skips_the_wall_refusal(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=True, wall_minutes=10, theatre_start_s=100000)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    other = plan_film(tmp_path / "o", monkeypatch, record_only=False, wall_minutes=10, theatre_start_s=100000)
    assert "the theatre needs about" in refused(film.cmd_plan, other, 1)


def test_without_record_only_the_plan_is_as_before(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=False, max_scenes=2)
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    kept, plan = planned(folder)
    assert kept == ["6.1a", "6.2a"]                                   # teams, the most shots, then the cap
    assert plan["record_only"] is False and plan["colour"]["name"] == "teams"
    assert any("shots of other colours (family) wait" in x for x in lines)
    assert any("wait for the next passes under max_scenes 2" in x for x in lines)
    assert any(x.startswith("theatre r-a:") for x in lines)
    assert not any("record only" in x for x in lines)
    assert "shots to film" in lines[0]


# ---- the run, wait, collect --------------------------------------------------------------------------------

def run_record_only(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=True)
    assert film.cmd_plan(folder, 1)[0] == "FILM PLAN: READY"
    monkeypatch.setattr(os, "environ", dict(os.environ))
    seen = []

    def fake(args, sink=None, cores=None):
        seen.append(list(args))
        return 0

    monkeypatch.setattr(film, "launch", fake)
    verdict, lines = film.cmd_run(folder, 1)
    return folder, seen, verdict, lines


def test_run_records_each_runs_windows_and_starts_no_theatre(tmp_path, monkeypatch):
    folder, seen, verdict, lines = run_record_only(tmp_path, monkeypatch)
    assert verdict == "FILM RUN: DONE", lines
    joined = [" ".join(a) for a in seen]
    assert len([j for j in joined if "story-windows.ps1" in j]) == 2          # r-a and r-b, one build
    assert not any("theatre-safari.ps1" in j for j in joined)
    done = json.loads((folder / "film" / "pass-1" / "done.json").read_text(encoding="utf-8"))
    assert done["record_only"] is True and done["render_exit"] == {} and done["safari"] == {}
    assert done["record_exit"] == 0 and set(done["recorded"]) == {"r-a", "r-b"}


def test_a_failed_recording_fails_the_run(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, record_only=True)
    film.cmd_plan(folder, 1)
    monkeypatch.setattr(os, "environ", dict(os.environ))
    monkeypatch.setattr(film, "launch", lambda args, sink=None, cores=None: 3)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: FAILED", lines


def test_wait_reads_a_record_only_pass_as_done(tmp_path, monkeypatch):
    folder, _, _, _ = run_record_only(tmp_path, monkeypatch)
    (folder / "film" / "pass-1" / "started.json").write_text(json.dumps({"pass": 1, "pid": 1, "started": "t"}),
                                                              encoding="utf-8")
    verdict, lines = film.cmd_wait(folder, 1, 1)
    assert verdict == "FILMING: DONE", lines
    assert "record-only" in lines[0]


def test_collect_slides_and_encode_refuse_a_record_only_pass(tmp_path, monkeypatch):
    folder, _, _, _ = run_record_only(tmp_path, monkeypatch)
    for fn in (film.cmd_collect, film.cmd_slides, film.cmd_encode):
        text = refused(fn, folder, 1)
        assert "pass 1 recorded windows only" in text, (fn.__name__, text)
    assert not (folder / "clips.json").exists() and not (folder / "clips").exists()


# ---- the windows are reused ----------------------------------------------------------------------------------

def test_a_window_a_record_only_pass_recorded_is_reused_by_the_next_pass(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, reuse=True, record_only=True)
    assert film.cmd_plan(folder, 1)[0] == "FILM PLAN: READY"
    windows = json.loads((folder / "film" / "pass-1" / "windows" / "windows.json").read_text(encoding="utf-8"))
    for w in windows["windows"]:                                      # what story-windows.ps1 leaves of a finished window
        d = folder / "film" / "pass-1" / "windows" / w["out"]
        d.mkdir(parents=True)
        (d / "film.poses.bin").write_bytes(b"poses")
        row = {"verdict": "faithful", "run": Path(w["run"]).name, "from": w["from"], "to": w["to"], "fps": w["fps"]}
        (d / "identity.jsonl").write_text(json.dumps({"frame": 0}) + "\n" + json.dumps(row) + "\n", encoding="utf-8")
    # the film pass after it: record_only set back to false, so it splits by colour and films the 'teams' shots
    s = json.loads((folder / film.SETTINGS).read_text(encoding="utf-8"))
    s["record_only"] = False
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    verdict, lines = film.cmd_plan(folder, 2)
    assert verdict == "FILM PLAN: READY", lines
    kept, plan = planned(folder, 2)
    assert kept == ["6.1a", "6.2a", "6.5a"] and plan["record_only"] is False
    assert any(x.startswith("windows: 3 of 3 reused") for x in lines), lines
    for w in json.loads((folder / "film" / "pass-2" / "windows" / "windows.json").read_text(encoding="utf-8"))["windows"]:
        assert (folder / "film" / "pass-2" / "windows" / w["out"] / "film.poses.bin").read_bytes() == b"poses"
