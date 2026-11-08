"""A shot's colour: colours.json, the check, the shot's key, the one-colour pass and collect's log check.

The theatre turns bodies' hues by a map it reads once, when its Editor starts (EVOSIM_THEATRE_TEAMS), so two
teams or one family can be told apart in a picture. A shot may carry a `colour` named in the film's
colours.json; the check refuses a colour that cannot be filmed, the plan films one colour a pass, the run
sets the variable for it and pops it otherwise, and collect refuses a clip whose theatre log does not show
the map. A film with no colours.json and no colour is filmed and checked exactly as before.

  python -m pytest scripts/tests/story-video-check/test_colour.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_colour.py   (where pytest is not installed)
"""

import hashlib
import importlib.util
import json
import os
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, SKILL / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


film = load("film_tool", "film.py")
shots = load("shots_tool", "shots.py")

SHOT = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 46870.0,
        "run_to_s": 46874.0, "kind": "run"}
BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}


def make_colours(tmp_path, **entries):
    """A film folder with a colours.json naming `teams` and `family`, whose files are written beside it."""
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    data = {}
    for name, runs in (entries or {"teams": ["r67-6202", "r67r1-6202"], "family": ["r67-6202"]}).items():
        file = tmp_path / f"{name}.tsv"
        file.write_text("11248\t0.25\n11250\t-0.25\n", encoding="utf-8")
        data[name] = {"shows": f"what {name} shows", "file": str(file),
                      "sha256": hashlib.sha256(file.read_bytes()).hexdigest(), "runs": runs}
    (folder / "colours.json").write_text(json.dumps(data), encoding="utf-8")
    return folder, data


def coloured(name, **more):
    return dict(SHOT, colour=name, **more)


# ---- the key ---------------------------------------------------------------------------------------------

def test_a_shot_without_a_colour_keeps_its_key(tmp_path):
    folder, _ = make_colours(tmp_path)
    colours, _ = film.read_colours(folder)
    assert film.shot_key(SHOT, dict(BASE)) == film.shot_key(SHOT, dict(BASE), colours)
    assert film.shot_key(SHOT, dict(BASE)) == film.shot_key(SHOT, dict(BASE), None)


def test_a_colour_changes_the_key_and_so_does_its_file(tmp_path):
    folder, data = make_colours(tmp_path)
    colours, _ = film.read_colours(folder)
    plain = film.shot_key(SHOT, dict(BASE))
    teams = film.shot_key(coloured("teams"), dict(BASE), colours)
    family = film.shot_key(coloured("family"), dict(BASE), colours)
    assert len({plain, teams, family}) == 3
    changed = {k: dict(v) for k, v in colours.items()}
    changed["teams"]["sha256"] = "0" * 64
    assert film.shot_key(coloured("teams"), dict(BASE), changed) != teams


def test_a_key_for_a_colour_the_film_lacks_is_refused():
    with pytest.raises(film.Refused):
        film.shot_key(coloured("teams"), dict(BASE), None)


# ---- the refusals ----------------------------------------------------------------------------------------

def test_the_faults_of_a_colour_on_a_shot(tmp_path):
    folder, _ = make_colours(tmp_path)
    colours, problems = film.read_colours(folder)
    assert problems == []
    assert film.colour_faults(SHOT, colours) == []
    assert film.colour_faults(coloured("teams"), colours) == []
    assert "string" in film.colour_faults(coloured(3), colours)[0]
    assert "no colours.json" in film.colour_faults(coloured("teams"), None)[0]
    assert "not a colour" in film.colour_faults(coloured("reds"), colours)[0]
    assert "no footage" in " ".join(film.colour_faults({"id": "9.1a", "kind": "graphic", "colour": "teams",
                                                        "alone": "the end"}, colours))
    assert "not for run r99" in film.colour_faults(coloured("family", run="r99"), colours)[0]
    assert film.colour_faults(coloured("teams", run="r67r1-6202"), colours) == []


def test_colours_json_must_be_sound(tmp_path):
    folder, data = make_colours(tmp_path)
    path = folder / "colours.json"

    def problems_of(text):
        path.write_text(text if isinstance(text, str) else json.dumps(text), encoding="utf-8")
        return film.read_colours(folder)[1]

    assert "not JSON" in problems_of("{teams:")[0]
    assert problems_of([1]) != []
    assert problems_of({"Teams": data["teams"]}) != []
    assert problems_of({"teams": dict(data["teams"], extra=1)}) != []
    assert problems_of({"teams": dict(data["teams"], runs=[])}) != []
    assert problems_of({"teams": dict(data["teams"], sha256="ABC")}) != []
    assert "not there" in problems_of({"teams": dict(data["teams"], file=str(tmp_path / "gone.tsv"))})[0]
    assert "not the file's" in problems_of({"teams": dict(data["teams"], sha256="0" * 64)})[0]
    assert problems_of(data) == []


def test_a_film_with_no_colours_json_has_none(tmp_path):
    folder = tmp_path / "film"
    folder.mkdir()
    assert film.read_colours(folder) == (None, [])
    assert film.colours_of(folder, [SHOT]) is None
    with pytest.raises(film.Refused):
        film.colours_of(folder, [coloured("teams")])


def test_the_plan_refuses_on_any_fault(tmp_path):
    folder, _ = make_colours(tmp_path)
    with pytest.raises(film.Refused) as err:
        film.colours_of(folder, [SHOT, coloured("reds", id="1.3a")])
    assert "shot 1.3a" in err.value.lines[0]
    (folder / "colours.json").write_text("nope", encoding="utf-8")
    with pytest.raises(film.Refused):
        film.colours_of(folder, [SHOT])


def minimal_film(tmp_path, shot_list, monkeypatch, colours_json=True):
    """A film folder the check can read: a one-passage script and narration, and the parser stubbed to the script."""
    folder, _ = make_colours(tmp_path) if colours_json else (tmp_path / "film", None)
    folder.mkdir(exist_ok=True)
    (folder / "script.md").write_text("script", encoding="utf-8")
    (folder / "facts.md").write_text("The run r67-6202 and r67r1-6202.\n", encoding="utf-8")
    (folder / "narration.json").write_text(json.dumps({"passages": [{"id": "1.1", "from_s": 0, "to_s": 4}]}),
                                           encoding="utf-8")
    script = {"beats": [{"passages": [{"id": "1.1", "say": ["x"], "picture": {"kind": "run", "run": "r67-6202",
                                                                              "bodies": [], "seconds": None}}]}]}
    monkeypatch.setattr(shots.sc, "parse_script", lambda text: (script, []))
    monkeypatch.setitem(shots._ENDS, "r67-6202", None)
    monkeypatch.setitem(shots._ENDS, "r67r1-6202", None)
    data = {"schema": shots.SCHEMA, "script_sha256": shots.sha256_of(folder / "script.md"),
            "narration_sha256": shots.sha256_of(folder / "narration.json"), "shots": shot_list}
    return folder, data


def good_shot(**more):
    return dict({"id": "1.1a", "passage": "1.1", "kind": "run", "from_s": 0.0, "to_s": 4.0, "see": "Floor.",
                 "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 10.0, "run_to_s": 14.0,
                 "text": [], "places": "[facts.md:L1]"}, **more)


def check(tmp_path, monkeypatch, shot, colours_json=True):
    folder, data = minimal_film(tmp_path, [shot], monkeypatch, colours_json)
    errors, _ = shots.check_shots(data, folder)
    return errors


def test_the_check_passes_a_film_without_colour_as_before(tmp_path, monkeypatch):
    assert check(tmp_path, monkeypatch, good_shot(), colours_json=False) == []
    assert check(tmp_path / "b", monkeypatch, good_shot()) == []


def test_the_check_passes_a_sound_colour(tmp_path, monkeypatch):
    assert check(tmp_path, monkeypatch, good_shot(colour="teams")) == []


def test_the_check_refuses_each_fault(tmp_path, monkeypatch):
    def one(n, shot, colours_json=True):
        (tmp_path / n).mkdir()
        return " | ".join(check(tmp_path / n, monkeypatch, shot, colours_json))

    assert "string" in one("a", good_shot(colour=["teams"]))
    assert "no colours.json" in one("b", good_shot(colour="teams"), colours_json=False)
    assert "not a colour" in one("c", good_shot(colour="reds"))
    assert "not for run r67r1-6202" in one("d", good_shot(colour="family", run="r67r1-6202"))
    alone = {"id": "1.1a", "passage": "1.1", "kind": "graphic", "from_s": 0.0, "to_s": 4.0, "see": "End.",
             "alone": "the end", "beats": [{"at_s": 0.0, "shows": "x"}], "stills": [], "text": [],
             "places": "[facts.md:L1]", "colour": "teams"}
    assert "no footage" in one("e", alone)


def test_the_check_refuses_a_bad_colours_json(tmp_path, monkeypatch):
    folder, data = minimal_film(tmp_path, [good_shot()], monkeypatch)
    (folder / "colours.json").write_text(json.dumps({"teams": {"shows": "x"}}), encoding="utf-8")
    errors, _ = shots.check_shots(data, folder)
    assert any("colours.json" in e for e in errors)


def test_the_sheet_shows_the_colour(tmp_path):
    path = tmp_path / "shots.json"
    path.write_text(json.dumps({"shots": [good_shot(colour="teams"), dict(good_shot(), id="1.1b")]}), encoding="utf-8")
    verdict, lines = shots.cmd_sheet(path)
    assert "teams colour" in lines[0] and "colour" not in lines[1]


# ---- the plan --------------------------------------------------------------------------------------------

def plan_film(tmp_path, monkeypatch, shot_list):
    """cmd_plan on a film of these shots with the farm, the windows and the worker stubbed out."""
    folder, _ = make_colours(tmp_path)
    (folder / "shots.json").write_text(json.dumps({"shots": shot_list}), encoding="utf-8")
    rd = tmp_path / "rundir"
    rd.mkdir(exist_ok=True)
    monkeypatch.setattr(film, "run_dir", lambda root, arm: rd)
    monkeypatch.setattr(film, "farm_of", lambda d: None)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, shot, h: (shot["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        windows = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
                   for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": windows, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def shot_at(n, colour=None, run="r67-6202"):
    s = dict(SHOT, id=f"1.{n}a", run=run, run_from_s=100.0 * n, run_to_s=100.0 * n + 4)
    return dict(s, colour=colour) if colour else s


def test_a_pass_films_the_colour_with_the_most_shots(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_at(1, "family"), shot_at(2, "teams"), shot_at(3, "teams"),
                                               shot_at(4)])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert [e["shot"] for e in plan["scenes"]] == ["1.2a", "1.3a"]
    colours, _ = film.read_colours(folder)
    assert plan["colour"] == {"name": "teams", "file": colours["teams"]["file"], "sha256": colours["teams"]["sha256"]}
    assert any(l.startswith("2 shots of other colours (family, none) wait for the next passes") for l in lines)


def test_a_tie_goes_to_no_colour_first_and_then_by_name(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_at(1, "teams"), shot_at(2), shot_at(3, "family")])
    verdict, lines = film.cmd_plan(folder, 1)
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert [e["shot"] for e in plan["scenes"]] == ["1.2a"] and plan["colour"] is None
    folder = plan_film(tmp_path / "again", monkeypatch, [shot_at(1, "teams"), shot_at(3, "family")])
    film.cmd_plan(folder, 1)
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert plan["colour"]["name"] == "family"


def test_a_film_with_no_colour_plans_as_before(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_at(1), shot_at(2)])
    verdict, lines = film.cmd_plan(folder, 1)
    plan = json.loads((folder / "film" / "pass-1" / "plan.json").read_text(encoding="utf-8"))
    assert plan["colour"] is None and len(plan["scenes"]) == 2
    assert not any("wait for the next passes" in l for l in lines)


def test_the_plan_refuses_a_colour_the_run_is_not_for(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [shot_at(1, "family", run="r67r1-6202")])
    with pytest.raises(film.Refused):
        film.cmd_plan(folder, 1)


# ---- the run ---------------------------------------------------------------------------------------------

def run_pass(tmp_path, monkeypatch, colour, shell_value=None):
    """cmd_run on a pass planned with this colour, the farm and the theatre stubbed; the variable each saw."""
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, farm_threads=1, wall_minutes=10, runs_root="runs", farm_cores=None)
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": settings, "size": "1920x1080", "farm": None,
                                               "colour": colour, "scenes": [{"n": 1, "arm": "r67-6202"}]}),
                                   encoding="utf-8")
    monkeypatch.setattr(os, "environ", dict(os.environ))
    if shell_value is not None:
        os.environ[film.TEAMS_ENV] = shell_value
    seen = []
    monkeypatch.setattr(film, "launch", lambda args, sink=None, cores=None: seen.append(os.environ.get(film.TEAMS_ENV)) or 0)
    verdict, lines = film.cmd_run(folder, 1)
    assert verdict == "FILM RUN: DONE", lines
    done = json.loads((out / "done.json").read_text(encoding="utf-8"))
    return seen, done


def test_a_coloured_pass_sets_the_variable_to_the_absolute_path(tmp_path, monkeypatch):
    colour = {"name": "teams", "file": "scratch/x/teams.tsv", "sha256": "0" * 64}
    seen, done = run_pass(tmp_path, monkeypatch, colour, shell_value="left over")
    assert seen == [str(film.REPO / "scratch/x/teams.tsv")] * 2          # the recording and the filming
    assert Path(seen[0]).is_absolute() and done["colour"] == colour


def test_an_uncoloured_pass_pops_the_variable(tmp_path, monkeypatch):
    seen, done = run_pass(tmp_path, monkeypatch, None, shell_value="left over in the shell")
    assert seen == [None, None] and done["colour"] is None


# ---- collect ---------------------------------------------------------------------------------------------

LINE = "[Theatre] teams: {n} bodies turned from {path} (EVOSIM_THEATRE_TEAMS)"


def test_the_log_check_reads_the_teams_line():
    colour = {"name": "teams", "file": "scratch/x/teams.tsv", "sha256": "0" * 64}
    want = str(film.REPO / "scratch/x/teams.tsv")
    assert film.colour_log_fault(colour, "noise\n" + LINE.format(n=40, path=want) + "\n") is None
    # slashes and case are normalised
    assert film.colour_log_fault(colour, LINE.format(n=40, path=want.replace(chr(92), "/").upper())) is None
    assert film.colour_log_fault(colour, LINE.format(n=0, path=want)) is not None
    assert film.colour_log_fault(colour, LINE.format(n=40, path=want + ".other")) is not None
    assert film.colour_log_fault(colour, "no teams line") is not None
    assert film.colour_log_fault(None, "no teams line") is None
    assert film.colour_log_fault(None, LINE.format(n=40, path=want)) is not None


def collect_film(tmp_path, monkeypatch, name, log_of):
    """cmd_collect on a one-shot pass, coloured `name` or not, whose theatre filmed a clip with the log `log_of`
    makes of the colour's file; the tools that read the clip are stubbed."""
    folder, _ = make_colours(tmp_path)
    colours, _ = film.read_colours(folder)
    colour = ({"name": name, "file": colours[name]["file"], "sha256": colours[name]["sha256"]} if name else None)
    log = log_of(colours[name]["file"] if name else "D:/x/teams.tsv")
    shot = dict(SHOT, colour=name) if name else dict(SHOT)
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    settings = dict(BASE, worker=6, runs_root="runs")
    key = film.shot_key(shot, settings, colours)
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": colour,
            "scenes": [{"n": 1, "shot": "1.2a", "key": key, "arm": "r67-6202", "camera": "Floor", "clip_start_s": 1.0,
                        "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
                        "handle_after_s": 1.0, "fog": None, "focus": None, "side": None}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / "story-01-floor.mp4").write_bytes(b"clip")
    (safari / "scenes.tsv").write_text("slug\toutcome\tprovenance\nstory-01-floor\tplayed\tFAITHFUL\n", encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text(log, encoding="utf-8")
    monkeypatch.setattr(film, "probe", lambda p: {"frames": 121, "width": 1920, "height": 1080})
    monkeypatch.setattr(film, "reference", lambda f, s, w=1920: {"luma": 1, "tone": 1, "tone_p10": 0, "edges": 1,
                                                          "edges_p10": 0})
    monkeypatch.setattr(film, "sheet", lambda *a, **k: None)
    monkeypatch.setattr(film, "yavg", lambda *a, **k: [1.0, 1.0, 1.0])
    monkeypatch.setattr(film, "look", lambda *a, **k: ([1.0], [1.0]))
    monkeypatch.setattr(film, "colour", lambda *a, **k: [1.0, 1.0, 1.0])
    return folder, film.cmd_collect(folder, 1)


def test_collect_keeps_a_coloured_clip_whose_log_shows_the_map(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, "teams", lambda f: LINE.format(n=2, path=f))
    assert verdict == "CLIPS: PASS", lines
    clips = json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"]
    assert clips[0]["colour"] == "teams"


def test_collect_refuses_a_coloured_clip_whose_log_lacks_the_map(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, "teams", lambda f: "[Theatre] skin: no teams")
    assert verdict == "CLIPS: FAIL" and any("does not show colour 'teams'" in l for l in lines)


def test_collect_refuses_a_coloured_clip_whose_log_turned_no_bodies(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, "teams", lambda f: LINE.format(n=0, path=f))
    assert verdict == "CLIPS: FAIL"


def test_collect_refuses_an_uncoloured_clip_whose_log_has_a_map(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, None, lambda f: LINE.format(n=5, path=f))
    assert verdict == "CLIPS: FAIL" and any("asked for none" in l for l in lines)


def test_collect_keeps_an_uncoloured_clip_whose_log_has_none(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, None, lambda f: "[Theatre] nothing about teams")
    assert verdict == "CLIPS: PASS", lines
    assert json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"][0]["colour"] is None
